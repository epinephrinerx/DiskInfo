# Handoff: R-004 phase 3, fix round 1 (Opus 5.5 and Codex GPT-6.1 findings)

Repo: `D:\Dev\Apps\SystemMonitor4.0`, branch `agent/agy-sysmon-ext-smart`, uncommitted phase 3 work in the tree
(509 tests pass, 1 skipped; the four SMART stub files in the host were already deleted by the integrator, do not recreate them).
Spec: `phase3-smart-extension.md`. Writing Owner: agy. Integrator: Claude Code.

## Hard rules (same as before)
- Only read / search / edit / create tools. Never run a shell command, tests, builds or git.
- Tests never scan real disks, never call `DiskInfoCore.dll`/`SmartCore` natives, never touch real `%ProgramData%`,
  `AppConfig.Save()` or `%APPDATA%\SystemMonitor`. No NuGet packages. Short why-comments, no banners.
- Do not weaken any trust rule (hash before load, elevated host first-party only, approval store ACL checks).

## Fixes (all required)

### 1. Demand plumbing in `ExtensionHost` (H1, M1, Codex 2/3/4)
- `_lastPushedDemand` must describe the **instance** that is loaded, not the id. Whenever a slot is created for an id
  (initial load, Reload, re-enable, abort path) the stored value for that id is cleared, so the first push to the new
  instance always happens, after `Initialize` and before its first `GetView`. Clear it when a slot is retired too.
- Every call into an extension from `SetDemand` goes through `slot.TryAcquire()` / `Release()` (skip when `TryAcquire`
  fails), exactly like `CollectViews`.
- Ordering: concurrent pushes for one id must never leave the instance on an older value than the host believes it sent.
  Use a per-id gate with this rule: the thread that gets the gate (`Monitor.TryEnter`, never blocking) loops
  `compute desired -> if different from applied: call -> record applied` until a pass finds nothing to do, then leaves
  the gate and re-checks once more (a `dirty` flag set by a thread that could not enter) so a late update is never lost.
  A thread that cannot enter just sets `dirty` and returns, so a stuck extension cannot block the sampler tick.
  `desired` is always recomputed from current state (`_enabled`, the volatile temperature flag), never from an
  argument captured earlier; this also fixes the kept-loaded `SetEnabled` path that pushed its captured `enabled`
  (around ExtensionHost.cs:296): it must call the same routine and not pass `enabled`.
- Tests: (a) an `IDemandAware` extension that is reloaded (`Reload(id)`) gets `SetDemand` once on the new instance with
  the current demand; (b) switched off and on again (non-kept) likewise; (c) a callback that blocks (AppDomain-data
  gate) while a second thread flips the demand and finishes: after the first callback is released the instance has the
  final value, and the sampler-side call returned without waiting; (d) `Reload` completing while `SetDemand` is paused
  never delivers `SetDemand` after `Shutdown` on the old instance.

### 2. `GetDiskTemperatures` isolation and cost (M2, L6, Codex 5)
- The enumeration of the returned dictionary is inside the per-extension try/catch. Count every inspected pair (valid or
  not) against a hard cap of 256 and stop. Replace the List/OrderBy/ToList per tick by an array of slots kept sorted by
  id when slots change (or a cached snapshot rebuilt on change); per tick allocate only the result dictionary. Keep
  first-id-wins for duplicate keys.
- Tests: provider whose enumerator throws (healthy provider still collected); provider yielding invalid pairs forever
  (returns within the cap); duplicate keys across two providers (ordinal id order wins); none loaded.

### 3. `SmartExtension` (H2, M4, Codex 1/6/7/9)
- **One published state**: a single immutable record `ScanState(SmartInfo Info, IReadOnlyDictionary<int,int> Temps)` replaced
  with one `Volatile.Write`; `GetView` and `GetDiskTemperatures` both read that one object. Remove the separate
  `_currentInfo` / `_temperatures` fields. The failure and exception paths publish an empty-temps state in the same write.
- **Demand transitions**: keep the previous demand. When `Views` goes from off to on (including the first
  `SetDemand` with `Views`) discard the state (`Waiting` + no temps) and scan at once. Any demand change signals an
  `AutoResetEvent`/`ManualResetEventSlim` that the loop waits on (with the 3 s timeout) so a change is noticed
  immediately, not up to 3 s later. Demand `None` means no scan.
- **Shutdown**: join bound 4 s (not 10; the host retires with a 5 s budget on the UI thread at exit). If the join times
  out, the scan is still inside `DiskInfoCore.dll`: pin the library so the host's `FreeLibrary` cannot unmap code that is
  running, by loading it once more with `NativeLibrary.TryLoad(Path.Combine(context.ExtensionDirectory, "DiskInfoCore.dll"), out _)`
  and never freeing that handle (log one line through `context.Log`). Never throws, idempotent. The scan thread must not
  be restarted after Shutdown.
- **Native search path (M3)**: put `[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]` on every `DllImport` in
  `SmartCore` (not ApplicationDirectory, not the default order), so a library that is not in the verified package can
  never be found next to the exe or on PATH. A missing file must still end as `Unavailable`.
- Fix the flaky elevated test: wait for the first scan with a bounded poll of up to 5 s on a condition, rely on the wake
  event, no fixed sleeps.

### 4. Parity with the old module (Codex 10/11, M6/M7)
- Temperature text in the extension is `"51°C"` (no space), the same as `Palette.TempText(t, false)`, in the tab detail
  and in the temperature fact. Add an assertion.
- **Placement.** Old order: the SMART section/tabs sat right after Disk and before GPU/Network. Add an optional manifest field
  `place_after` (string, one of `cpu`, `ram`, `disk`, `gpu`, `network`; anything else is `ManifestInvalid`; absent =
  today's behaviour, appended at the end). `ExtensionViewData` carries it. In `WidgetViewModel`, extension overview sections
  and full-view tabs with `place_after` are inserted directly after that core module's section/tabs (several extensions for
  the same module keep ordinal id order); widget rotation order is unchanged. The SMART package declares
  `"place_after": "disk"`; tests that build the SMART package set it. Tests: manifest validation; overview and full-view
  order with SMART + GPU + network equals the old order; an extension without the field still goes last.
- Restore the manual section: put `manual_10_h`/`manual_10_t` back in `I18n.cs` (th and en), reworded for the new place:
  turned on with the SMART switch in the sidebar's extension list, needs administrator and `DiskInfoCore.dll` in the SMART
  extension folder; keep the rest of the sentences.

### 5. Migration strictness (Codex 12)
`FullTab` mapping only for the exact forms `smart` and `smart<digits>` (regex `^smart(\d+)?$`); `smart0custom`, `smartx`
stay untouched. Test.

### 6. Tests (L1, L2, L3, Codex tests)
- `SmartFallbackTests.A_missing_library_is_reported_once...` must not call `SmartCore.Scan()`: rewrite it with the fake
  scanner (repeated `Unavailable` results log the status change once through the context log).
- The end-to-end test is renamed to what it checks: package built from the real assembly without a native file, non-elevated
  host: status `Unavailable` (old rule: unavailable library wins over not elevated, keep `SmartBoard.Build` as is), page,
  overview note and tab say the missing-core sentence; the widget view model renders it. Also an elevated-flag-free
  variant with a fake scanner is not needed here.
- `SmartViewBuilder` tests (exact `ExtensionView` content): no disks per state incl. `Ok` with none (`smart_none`); SSD with
  all values (exact Title/Verdict/Line/Percent/Severity/Temperature, overview Detail/ValueText, tab facts in order,
  `Hardware`, `Subtitle`, `Detail`); HDD without life (`Percent == 100`, text verdict); disk without temperature;
  `Full == false` fact; Caution and Bad severities; long power-on hours (`123,456 h (5,144 days)` style, both languages);
  Thai output for page, overview and tab; two disks keep drive order.
- `SmartExtension` tests with the fake scanner/clock: demand `None` -> zero scans; `DiskTemperature` only -> scans and
  temps, view still available; failed pass clears both; throwing scanner; `Views` off->on discards the old state and
  rescans; `Shutdown` stops the thread, is idempotent, no scan after it; Shutdown with a scan that outlives the bound
  returns and logs the pin line (a fake `Func` can stand in for `NativeLibrary.TryLoad` through an internal seam; do not
  load a real native library in tests).

## Definition of done
Everything above implemented; existing tests unchanged in intent and green; short summary of files touched and what was
not done. Do not claim tests passed (you cannot run them).
