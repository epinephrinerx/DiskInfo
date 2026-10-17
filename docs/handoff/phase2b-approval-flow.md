# Handoff: R-004 phase 2b, extension approval flow (one-time UAC helper)

Repo: `D:\Dev\Apps\SystemMonitor4.0`, branch `agent/agy-sysmon-ext-approval` (from `ecc08ba`, phase 2a committed, clean tree).
Writing Owner: agy. Coding Owner / integrator: Claude Code. Background: `docs/handoff/phase1-extension-loader.md`
(trust model), `phase2a-extension-views.md` (host), Requirements.md R-004 (security decision, layer 1 and 2).

## Goal
A third-party extension (not in the embedded first-party allowlist) shows `NotApproved` in the sidebar of a normal
(non-elevated) host. The user can press **Approve...** next to it. That starts a ONE-TIME copy of the app through UAC
(`runas`, the user confirms the UAC prompt and a second in-app confirm dialog themselves). The elevated copy writes the
approval to `%ProgramData%\SystemMonitor\approved-extensions.json` through the existing `ApprovalStore.Approve`, then
exits. The normal host then reloads that extension. Nothing ever elevates automatically.

## Hard rules
- Use only read / search / edit / create tools. Never run a shell command, tests or git.
- No NuGet packages. Short why-comments in the surrounding style, no banners. Keep the diff scoped.
- **Tests must never write to the real `%ProgramData%`, never call `AppConfig.Save()`, never start a real UAC prompt or
  a real process.** All filesystem work in tests uses temp folders; `ApprovalStore` is constructed with a temp path and
  no-op `secureDirectory`/`secureFile` and an `aclChecker` returning true (see how `ExtensionLoaderTests` does it).
- Do not weaken any trust rule: hash before load, elevated host loads first-party only, the approval store ACL checks.
- The elevated helper must NEVER load, initialise or run any extension code (no `AssemblyLoadContext`, no native load).

## Design

### 1. `ExtensionLoader.Inspect` (new, in `ExtensionLoader.cs`, no behaviour change to `LoadAll`/`LoadFolder`)
`public static ExtensionInspection Inspect(string folder, ExtensionLoaderOptions options)` where
`ExtensionInspection(ExtensionStatus Status, string Detail, ExtensionManifest? Manifest, string? ManifestSha256, bool IsFirstParty)`
(new record, new file `ExtensionInspection.cs`; `Status == Loaded` means "structurally valid, every declared file
exists and its SHA-256 matches"; nothing is loaded). It applies the same root check as `LoadFolder` (folder must be
exactly `<ExtensionsRoot>\<folderName>`), steps 1 to 7 of `LoadSingleFolder` (reparse points, manifest parse, id equals
folder, contract, min_host, safe paths, no unlisted files, files exist) and then reads and hashes every declared file
like step 9 does, WITHOUT loading anything. It does not check trust, approvals, `IsEnabled` or duplicates. Implement it
by giving `LoadSingleFolder` an internal `inspectOnly` mode that stops before step 8 and does the hash verification only
(share the hashing code, do not copy-paste it), or by extracting steps 1 to 7 into a helper both use. All existing loader
tests must stay green unchanged.

### 2. `ExtensionApproval` (new file `SysMonitor.Wpf/Extensions/ExtensionApproval.cs`)
```
public static class ExtensionApproval
{
    public const string Switch = "--approve-extension";
    public const int ExitApproved = 0, ExitBadArguments = 1, ExitDeclined = 2, ExitInspectFailed = 3,
                     ExitWriteFailed = 4, ExitNotElevated = 5;
    public static bool TryParse(string[] args, out string id, out string manifestSha256);
    public static ProcessStartInfo CreateStartInfo(string exePath, string id, string manifestSha256);
    public static int Run(string[] args, ApprovalEnvironment env);
}
public sealed record ApprovalPrompt(string Id, string Name, string Version, string ManifestShaPrefix,
                                    IReadOnlyList<string> Files, bool HasNativeFiles);
public sealed class ApprovalEnvironment   // seams; production builds the real one
{
    public required Func<bool> IsElevated { get; init; }
    public required string ExtensionsRoot { get; init; }
    public required ApprovalStore Store { get; init; }
    public required FirstPartyAllowlist FirstParty { get; init; }
    public required Func<ApprovalPrompt, bool> Confirm { get; init; }
    public required Action<string> Log { get; init; }
    public required Version HostVersion { get; init; }
}
```
- `TryParse`: `args` must be exactly `[... , "--approve-extension", id, sha]` with the switch found by exact match and
  exactly two following arguments; `id` must match `^[a-z0-9][a-z0-9._-]{0,63}$` (so no leading `-`, `.` or `_`) and not be `.` or `..`; `sha` must be 64 hex
  characters (accept upper case, return lower case). Anything else returns false.
- `CreateStartInfo`: same shape as `AdminRestart.Start` (UseShellExecute true, Verb `runas`, WorkingDirectory the exe
  folder) with `Arguments = $"{Switch} {id} {sha}"` built only from validated values (throw `ArgumentException` if invalid).
- `Run` order matters: (1) `TryParse` else `ExitBadArguments`; (2) not elevated -> `ExitNotElevated` (never prompt, never
  write); (3) `ExtensionLoader.Inspect(Path.Combine(env.ExtensionsRoot, id), options)` where options carry only
  `ExtensionsRoot`, `HostVersion`, `FirstParty`, `Log` (build a minimal `ExtensionLoaderOptions`); `Status != Loaded` ->
  log + `ExitInspectFailed`; (4) the recomputed `ManifestSha256` must equal the argument (ordinal ignore case) else
  `ExitInspectFailed` (the package changed between the click and the elevated run); (5) if `IsFirstParty` return
  `ExitApproved` without writing anything; (6) build `ApprovalPrompt` (hash prefix = first 12 hex chars, file list =
  manifest file paths, `HasNativeFiles` = any native) and call `env.Confirm`; false -> `ExitDeclined` and no write;
  (7) `env.Store.Approve(id, manifest.Name, sha)`; failure -> log + `ExitWriteFailed`; success -> `ExitApproved`.
  Wrap everything in try/catch that logs and returns `ExitInspectFailed` (never throw out of `Run`).
- Production wiring: `public static int RunProduction(string[] args)` in the same class builds the environment: `Win32.IsElevated`,
  `Path.Combine(AppContext.BaseDirectory, "extensions")`, `new ApprovalStore(ApprovalStore.DefaultPath, log: Diag.Write)`,
  `FirstPartyAllowlist.FromAssembly(typeof(App).Assembly, Diag.Write)`, host version from the assembly, `Log = Diag.Write`
  and `Confirm` = a `System.Windows.MessageBox` Yes/No (default button No, icon Warning, `MessageBoxOptions` none) whose
  text comes from the i18n table in the user language (read `AppConfig.Load().Lang`, read only). The text lists name, id,
  version, the first 12 characters of the manifest hash, the file names, a warning that the extension will run with the
  user's rights inside the app and that only extensions from a source the user trusts should be approved, and an extra
  line when `HasNativeFiles` (it loads native code).

### 3. `App.xaml.cs`
Right after the existing `MemoryTools.ElevatedSwitch` block (before the single-instance guard and before `Diag.Start`
is not required; just before the guard) add:
`if (e.Args.Contains(ExtensionApproval.Switch)) { Shutdown(ExtensionApproval.RunProduction(e.Args)); return; }`
The helper must not start the sampler, the host or a window and must not take the single-instance guard.

### 4. `ExtensionHost.Reload(string id)` (public `Task`)
Serialised through `_lifecycle` like `SetEnabled`. Under the lock remove the slot and the last result for `id`
(retire the slot OUTSIDE `_lock`), then run the same reconcile used by `SetEnabled` (load via `LoadFolder` when desired
enabled, otherwise record the loader result, which for an approved but not enabled extension is `Disabled`). It must call
`LoadFolder` even when not enabled so the entry status is refreshed (trust is checked before `Disabled`). Rebuild
`_entries`. A `Reload` of an unknown id is a no-op. Extract the shared reconcile code rather than duplicating it.

### 5. Sidebar (`MainWindow.xaml.cs`, `ExtensionStatusText.cs`, `I18n.cs`)
- `ExtensionStatusText.CanApprove(ExtensionStatus status, bool hostElevated) => status == NotApproved && !hostElevated`.
- In `BuildSidebar`, below an extension row whose `CanApprove` is true, add a small button "Approve..." (th: `อนุมัติ...`)
  styled like the other sidebar buttons. Click: `ExtensionApproval.CreateStartInfo(Environment.ProcessPath, id, sha)`
  where `sha` is the entry hash: add `ManifestSha256` (string, may be empty) to `ExtensionHost.ExtensionEntry`, filled from
  the load result; no button when it is empty. Start the process in a try/catch: `Win32Exception` with
  `AdminRestart.IsDeclined(error)` -> do nothing; other errors -> log via `Diag`. Wait for exit off the UI thread
  (`Process.WaitForExitAsync`), then if `ExitCode == ExtensionApproval.ExitApproved` call
  `_extensions.Reload(id)` and `_sampler.Nudge()`, on the UI thread rebuild is automatic through the signature. Guard
  against double clicks while a helper for that id is running (a `HashSet<string>` of ids in flight, UI thread only).
- When the host itself is elevated and an entry is `NotApproved` or `NotTrustedWhenElevated` show the existing status text
  and add a tooltip line (new i18n key `ext_elevated_hint`: en "Open SystemMonitor normally (not as administrator) to approve or use this extension.", th equivalent).
- i18n th/en keys (add to the existing tables, same style, parity test exists): `ext_approve_button`, `ext_elevated_hint`,
  `ext_approve_title`, `ext_approve_body` (format string with placeholders for name, id, version, hash prefix, files),
  `ext_approve_native_warning`. Thai text must be correct Thai with full diacritics.

## Tests (new file `SysMonitor.Tests/Extensions/ExtensionApprovalTests.cs`, plus small additions)
Use the package builder helpers already used by `ExtensionLoaderTests` (look at them; reuse or extract a shared helper,
do not copy 100 lines). Every test uses temp folders and an `ApprovalStore` with a temp path and no-op securers.
1. `TryParse` accepts a valid id/hash (and lowercases the hash); rejects: no switch, missing args, three args, id with
   `..`, `/`, `\`, space, upper case, empty, 65 chars, hash of 63 or 65 chars, non-hex hash.
2. `CreateStartInfo`: verb `runas`, `UseShellExecute`, arguments exactly `--approve-extension <id> <sha>`; invalid values throw.
3. `Run` not elevated -> `ExitNotElevated`; `Confirm` not called; store file not created.
4. `Run` elevated, valid unapproved package, `Confirm` returns true -> `ExitApproved`; `store.IsApproved(id, sha)` true;
   the `ApprovalPrompt` passed to `Confirm` has the right id/name/version/12-char prefix/file list/native flag.
5. `Confirm` false -> `ExitDeclined`; store not written (`IsApproved` false, file absent).
6. Manifest changed after the hash was computed (argument is the old hash) -> `ExitInspectFailed`; `Confirm` not called; no write.
7. A declared file with a wrong hash, an unlisted file in the folder, a folder missing, an id/folder mismatch -> `ExitInspectFailed`.
8. First-party package (put its manifest hash in the `FirstPartyAllowlist` the test builds; check how existing tests do it)
   -> `ExitApproved`, nothing written, `Confirm` not called.
9. **No extension code runs:** set `ExtensionLoader.AlcCreatedForTesting` and `NativeLoadingForTesting` to counters
   (reset in `finally`); a full approved `Run` leaves both at zero; the dummy extension `Initialized` flag stays false.
10. Store write failure -> `ExitWriteFailed` (use a store whose directory check fails: for example an `aclChecker`
    returning false for the directory).
11. `ExtensionHost.Reload`: unapproved package, host started (status `NotApproved`) -> approve in the temp store ->
    `Reload(id)` -> with the extension enabled in the config object the entry becomes `Loaded` and `CollectViews` returns
    its view; with it not enabled the entry becomes `Disabled`. `Reload` of an unknown id does nothing. Never `Save()`.
12. `ExtensionStatusText.CanApprove` truth table; `ExtensionEntry.ManifestSha256` is filled for a `NotApproved` entry.
13. Inspect tests in `ExtensionLoaderTests`: valid package -> `Loaded` with manifest/hash and no ALC created; wrong file
    hash -> `HashMismatch`; folder outside the root -> `UnsafePath`; not-approved package still inspects as `Loaded`.

## Definition of done
All implemented; every existing test unchanged and green; short summary of files touched and anything not done. You
cannot run tests; do not claim they passed.
