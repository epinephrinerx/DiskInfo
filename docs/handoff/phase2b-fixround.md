# Handoff: R-004 phase 2b, fix round 1 (Opus 5.5 and Codex GPT-6.1 findings)

Repo: `D:\Dev\Apps\SystemMonitor4.0`, branch `agent/agy-sysmon-ext-approval`, uncommitted phase 2b work in the tree
(517 tests pass, 1 skipped). Spec: `phase2b-approval-flow.md`. Writing Owner: agy. Integrator: Claude Code.

## Hard rules (same as before)
- Only read / search / edit / create tools. Never run a shell command, tests or git.
- Tests never write to the real `%ProgramData%`, never call `AppConfig.Save()`, never start a real process or UAC,
  and never read or write `%APPDATA%\SystemMonitor`. No NuGet packages. Short why-comments, no banners.
- Do not weaken any trust rule (hash before load, elevated host first-party only, store ACL checks).

## Fixes (all required)

### 1. `ExtensionHost.ReconcileExtension` and `Reload` (regression of the phase 2a behaviour)
The 2b refactor made every reconcile retire the slot and call `LoadFolder`. Restore the 2a semantics and keep `Reload`:
- `ReconcileExtension(id, bool refresh)`.
  - `refresh == false` (used by `SetEnabled`): read `desired` from `_enabled` under `_lock`.
    - desired and a slot exists -> return, do nothing (no Shutdown, no Initialize).
    - not desired -> under `_lock`: remove the slot from `_slots`, drop its cached name, and replace the last result by a
      `Disabled` result (same id/folder/manifest/hash, detail "Extension is disabled in settings") ONLY if the previous
      result was `Loaded`, `Disabled` or missing; if it was any other status (NotApproved, NotTrustedWhenElevated,
      HashMismatch, EntryNotFound, LoadFailed, DuplicateId, ...) keep the previous result untouched. Publish `_entries`
      (still under `_lock`), leave the lock, THEN `RetireAndDispose` the removed slot. No `LoadFolder` call on this path,
      so the sidebar shows `Disabled` immediately even while a view call is in flight.
    - desired and no slot -> load as today (LoadFolder, slot, display name, abort check, publish).
  - `refresh == true` (used only by `Reload`): the slot was already removed by `Reload`; always call `LoadFolder` (also
    when not desired, so the trust status is refreshed; the loader checks trust before `Disabled`) and publish.
- `Reload`: under `_lock` remove ONLY the slot (keep `_lastResults[id]` and the entry so the row never vanishes if the
  reload aborts); retire it outside `_lock`; then `ReconcileExtension(id, refresh: true)`.
- Tests: (a) enabled and loaded, `SetEnabled(id,true)` again -> `Shutdown` not called and `Initialize` ran once (use the
  AppDomain-data hooks of `DummyViewsExtension`; add an initialize counter hook if none exists); (b) the existing
  `ExtensionHost_LifetimeGate_ShutdownWaitsForInFlightViewsCall` stays green and `Entries` shows `Disabled` promptly
  (well under the 5 s wait) while `GetView` is still blocked; (c) enabled package with a corrupted file ->
  status `HashMismatch`; `SetEnabled(false)` keeps `HashMismatch`; (d) NotApproved + not enabled -> approve in the temp
  store -> `Reload` -> `Disabled` (spec test 11 as written), and NotApproved + enabled -> approve -> `Reload` -> `Loaded`.

### 2. `ApprovalStore.Approve` cross-process safety (lost updates)
Two helpers may approve different ids at the same time; the read-merge-write must be exclusive. Inside `Approve`, after the
directory checks and before reading the existing file, take an exclusive lock on a lock file in the store directory:
`<store file name>.lock` opened with `FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None`, retried every 50 ms for up
to 10 s (fail with `ApprovalResult.Failed("Approval store is busy")` on timeout); hold it until after the final `File.Move`;
dispose and best-effort delete it at the end. The lock file is created in the already ACL-checked directory (call
`_secureFile` on it when newly created, same as the temp file; with the no-op seams in tests nothing else is needed).
`IsApproved` must ignore the lock file. Test: 8 parallel `Approve` calls on 8 different ids against one temp store
(separate `ApprovalStore` instances, no-op seams) -> all 8 ids are approved afterwards; a held lock file makes `Approve`
return failure after the timeout (add an internal/optional constructor parameter or property for the timeout so the test
takes about 1 s, default 10 s).

### 3. Extension id must not look like a switch (Codex 5)
Ids may not start with `-` or `.` or `_`: change the id rule to `^[a-z0-9][a-z0-9._-]{0,63}$` in BOTH
`ExtensionManifest` (id validation) and `ExtensionApproval` (`IdRegex`). Check every place that duplicates the id rule or
documents it (loader folder-name check, tests that use ids, docs strings). In `TryParse` keep the strict
"switch at index `args.Length - 3`" check. Tests: manifest with id `--elevated-op`, `--approve-extension`, `-x`, `.hidden`,
`_x` -> `ManifestInvalid`; `TryParse` rejects the same ids; `a`, `a-b`, `0.x_y` still accepted. Fix any existing test
that used a now-invalid id.

### 4. Approval dialog cannot be spoofed by manifest text (Opus 4)
In `ExtensionApproval.Run`, before building the `ApprovalPrompt` (and before calling `Store.Approve`), sanitise display
text: remove every control character, line/paragraph separator and Unicode format/bidi-control character
(`char.IsControl`, `UnicodeCategory.Format`, `LineSeparator`, `ParagraphSeparator`), collapse runs of whitespace to one
space, trim, then truncate: name 60 chars, version 32, each file path 80 (add an ellipsis char when cut). The file list
shows at most 10 entries followed by one extra entry `(+N more)` (en) when longer. Use the sanitised name for
`Store.Approve(id, name, sha)` too. In the i18n body, put identity first: id and manifest hash prefix come BEFORE the
name and version lines (both are validated, safe values). Tests: a name containing `\n`, `\r`, bidi override `\u202E`
and 5000 chars -> the prompt name has none of them and is at most 61 chars; 25 files -> prompt lists 10 + the "more"
entry; file names compared by name, not only count (spec test 4).

### 5. Helper must not touch the configuration (Codex 6)
`RunProduction`'s `Confirm` must not call `AppConfig.Load()` (it migrates and may write). Pick the language from
`System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "th" ? "th" : "en"`. Also give the
confirmation `MessageBox` an owner so it is on top and visible: create a hidden helper `Window` (`WindowStyle.None`,
`ShowInTaskbar=false`, `Width=Height=0`, `Topmost=true`, `Opacity=0`), `Show()` it, pass it as owner, `Close()` it in `finally`.

### 6. Sidebar (`MainWindow.xaml.cs`)
- Include `ManifestSha256` and the in-flight state in the rebuild signature (`id:name:status:enabled:sha:inflight`).
- Compute each Approve button `IsEnabled` from `_approvalsInFlight.Contains(id)` when the sidebar is built (so a rebuilt
  button is right), and when a helper finishes remove the id and force a sidebar rebuild (reset the stored signature so the
  next tick or an immediate `BuildSidebar()` call redraws). Remove the code that re-enables the old detached button.
- At click time take the hash from the CURRENT `_extensions.Entries` item with that id (not a value captured when the
  sidebar was built); if the entry is gone, not `NotApproved`, or has no hash, do nothing.

### 7. Wording and tests
- Thai `ext_approve_native_warning`/`ext_approve_body` warning sentences must read as warnings. Replace the line
  "ไม่อนุมัติส่วนเสริมที่คุณไม่มั่นใจในที่มา" by "อย่าอนุมัติส่วนเสริมที่คุณไม่มั่นใจในที่มา" (or an equivalent clear imperative).
- i18n tests: assert the five 2b keys (`ext_approve_button`, `ext_elevated_hint`, `ext_approve_title`, `ext_approve_body`,
  `ext_approve_native_warning`) exist and are non-empty in both languages and that `ext_approve_body` contains the format
  placeholders `{0}` to `{4}` in both (use the existing parity test file `StringTableAndI18nTests.cs` or the I18n tests).
- Replace the vacuous "Initialized stays false" assertion in `ExtensionApprovalTests.cs` (around line 349): the
  extension runs in its own ALC, so a plain static of the test assembly is not observable. Use the package built from
  `DummyViewsExtension` and its AppDomain-data `Initialized` flag (reset the hooks in `finally`) so the assertion can
  actually fail if the helper ever initialised the extension.
- `ExtensionLoaderTests` keeps a private package builder that duplicates the shared `ExtensionTestPackageBuilder`.
  If the signatures allow, make the old private helper delegate to the shared builder (or remove it); keep all existing
  loader tests unchanged in behaviour. If it does not fit, leave it and say so in the summary.

## Definition of done
Everything above implemented; existing tests unchanged and green; short summary of files touched and what was not done.
Do not claim tests passed (you cannot run them).
