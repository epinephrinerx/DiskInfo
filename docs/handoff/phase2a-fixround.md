# Handoff: R-004 phase 2a, fix round (Opus 5.5 review findings)

Repo: `D:\Dev\Apps\SystemMonitor4.0`, branch `agent/agy-sysmon-ext-host` (phase 2a work is uncommitted in the
working tree; do not commit, do not switch branch, do not touch git at all).
Writing Owner: agy. Coding Owner / integrator: Claude Code. Read `docs/handoff/phase2a-extension-views.md` in
`D:\Dev\Apps\DiskInfo` first for the design; this file only lists what must change.

## Hard rules
- Use only read / search / edit / create tools. Never run a shell command, never run tests, never run git.
- Never call `AppConfig.Save()` from any production class other than the existing callers in `MainWindow`, and
  never from a test. `AppConfig.Save` writes the user real profile file. Tests must not read or write
  `%APPDATA%\SystemMonitor\config.wpf.json` except the one read-only guard test described below.
- Do not add NuGet packages. Keep the diff scoped to the files named here (plus new test files).
- Comments in the surrounding style: short, explain why, no banner comments.

## Fixes (all are required)

### A. `SysMonitor.Wpf/Extensions/ExtensionHost.cs` concurrency and robustness
1. **Lifetime gate per extension (use-after-Shutdown).** Wrap each loaded extension in a small internal class
   (new file `ExtensionSlot.cs`) holding the `LoadedExtension`, a user count and a retired flag.
   `TryAcquire()` increments the count only if not retired and returns false otherwise; `Release()` decrements and
   signals a `ManualResetEventSlim` when the count reaches 0 after retirement; `RetireAndDispose(TimeSpan wait)`
   marks retired, waits up to `wait` (use 5 seconds) for the count to reach 0, then calls `LoadedExtension.Dispose()`
   (log with `SafeLog` if the wait timed out, and still dispose). Every call into extension code from the host
   (`GetView`, `GetDisplayName`) must happen between `TryAcquire` and `Release` (try/finally). `SetEnabled(false)` and
   `Dispose()` must retire the slot instead of disposing directly.
2. **Do not hold `_lock` while running extension or loader code.** `_lock` only guards the dictionaries and swaps
   (short sections). Hashing, loading (`ExtensionLoader.LoadAll`/`LoadFolder`), `Initialize`, `Dispose` and
   `GetDisplayName` run outside `_lock`. Serialise all lifecycle operations (`StartAsync` scan, `SetEnabled`,
   `Dispose`) with a separate `SemaphoreSlim(1,1)` so toggles cannot run out of order. `CollectViews` must never wait
   for that semaphore (the sampler must not stall while an extension loads); it only takes `_lock` briefly to copy
   the current slot list.
3. **Host-owned enabled state.** `_config.Extensions` is a plain dictionary edited by the UI thread and read by
   background threads. The host keeps its own `Dictionary<string,bool> _enabled` (OrdinalIgnoreCase, guarded by `_lock`),
   filled from the config in the constructor (skip null settings). `SetEnabled` updates `_enabled` under `_lock`, then
   also writes the config entry (`_config.Extensions[id] = new ExtensionSetting { Enabled = enabled }`, on the caller
   thread, still without saving). `IsEnabledInConfig` reads `_enabled`.
4. **Sanitising must never throw.** In `CollectViews` wrap the whole per-extension body (names, `GetView`, sanitising)
   in one try/catch so a hostile list (throwing indexer, wrong `Count`, endless enumerable) only drops that extension
   for that tick and logs once. In `SanitizeView` read each collection `Count` once, iterate by index only up to
   `Max*` accepted items, and for `Tabs` (and any other enumerable) inspect at most `MaxDetailTabs * 4` elements in
   total, counting skipped null/blank/duplicate ones, so an endless or all-blank sequence terminates. Change the loop
   over `view.Tabs` accordingly (index loop if it is an `IReadOnlyList`, otherwise a bounded `foreach`).
5. **Log de-duplication.** Key = extension id + exception type name only (not the message), and cap the set at 256
   entries (stop adding when full; keep logging nothing new rather than growing). Also log once per extension when
   sanitising fails.
6. **Names follow the UI language.** `CollectViews(language)` already gets the display name; remember the latest
   sanitised display name per extension id and the language it was computed for. When a name changes, rebuild and swap
   `_entries` (under `_lock`, cheap). `ExtensionEntry.Name` must therefore update after a language change without
   reloading. `BuildEntriesList` must not call extension code while holding `_lock`: compute provider names before
   taking the lock (through the slot gate) or use the cached names.
7. `Dispose()`: set `_disposed`, wait for the lifecycle semaphore with a bounded wait (5 seconds), retire all slots
   (each waits up to 5 seconds but run them one after another only if the total is acceptable; simplest is to retire
   them in sequence, it is acceptable), clear dictionaries. Must be idempotent and must not deadlock when a toggle is
   running (bounded wait, then continue).

### B. `SysMonitor.Wpf/AppConfig.cs`
- After JSON deserialisation (in `LoadFrom`, so both `Load` paths get it) normalise `Extensions`: a null dictionary
  becomes empty, null values are removed, and the dictionary is rebuilt with `StringComparer.OrdinalIgnoreCase`.
  Put this in a small `private void Normalize()` method called on the freshly loaded object.

### C. `SysMonitor.Wpf/MainWindow.xaml.cs` sidebar
- The checkbox must stay clickable when `entry.Enabled` is true, whatever the status (so an enabled extension that
  fails to load can be switched off). Only disable it when `!entry.Enabled` and the status is not `Loaded` or
  `Disabled` (for example NotApproved, ManifestInvalid): there the user cannot meaningfully enable it.
- Label stays `name (status)`; add a tooltip on the checkbox with `Version` (if not empty) and `entry.Detail`.
- Include the entry `Name` in the change signature (`id:name:status:enabled`) so a language change redraws.

### D. Tests (all new tests use temp folders and never the real config)
Add to the existing test files in `SysMonitor.Tests/Extensions/`:
1. `ExtensionHostTests`: **real config untouched.** Read the bytes (or `LastWriteTimeUtc` plus length) of
   `AppConfig` real path (`Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
   "SystemMonitor", "config.wpf.json")`, treat a missing file as a valid state) before and after a
   `SetEnabled` true/false cycle; assert equal.
2. A view provider that throws from its list members (use the dummy extension `ViewFunc` hook returning an
   `ExtensionView` whose `WidgetPages` is a custom `IReadOnlyList<WidgetPage>` with a throwing indexer and a huge
   `Count`): `CollectViews` must return without throwing, other extensions still return, and the log is written
   once even after 5 calls.
3. Tabs: an endless `IEnumerable<DetailTab>`/list of tabs with blank keys terminates (use a timeout guard of 5 s
   with `Task.Run(...).Wait(5000)` and assert completed), duplicate keys and blank keys are dropped, a tab with
   more than `MaxFactsPerTab` facts is capped, text longer than `MaxTextLength` is cut in `Subtitle`, `Detail`,
   `Hardware`, `Fact.Name`, `Fact.Value`, `OverviewRow.ValueText`.
4. Boundaries: temperature -50 and 150 accepted, -51 and 151 become null; percent -5 -> 0 and 105 -> 100.
5. **Lifetime gate:** `GetView` of the dummy blocks on a `ManualResetEventSlim` carried in AppDomain data; start
   `CollectViews` on a thread, then call `SetEnabled(id,false)` (do not await yet); assert `ShutdownCalled` is still
   false while blocked, release the event, await both, assert `ShutdownCalled` is true and the views call returned
   its data. (Reset the dummy hooks in `finally`.)
6. **Sampler not stalled by loading:** while `SetEnabled(id,true)` is running a load that blocks (use the loader
   `NativeLoadingForTesting` style hook only if one exists; otherwise block `Initialize` through a dummy hook), a
   `CollectViews` call returns within 1 second.
7. Enabled-but-NotApproved extension: sidebar rule helper. Extract the checkbox rule into a tiny public static
   method `ExtensionStatusText.CanToggle(ExtensionStatus status, bool enabled)` (in `ExtensionStatusText.cs`) used
   by `MainWindow`, and unit-test it: Loaded/Disabled -> true; NotApproved with enabled=true -> true, with
   enabled=false -> false.
8. Language change: after `CollectViews("th")` then `CollectViews("en")` with a dummy `DisplayNameFunc` depending on
   the language, `host.Entries[0].Name` changes to the English name.
9. `AppConfig`: loading JSON with `"extensions": null`, with `{"x": null}` and an old config without the key all
   give a usable non-null dictionary; a round trip through the serializer keeps `{"Ext.One":{"enabled":true}}` and
   lookups are case-insensitive after loading. Use `AppConfig.LoadFrom(tempFile)` (it takes a path); never `Save()`.
10. Fix the vacuous assertion in `WidgetViewModelExtensionsTests.cs` (`Title.StartsWith("ext:")`): assert the exact
    expected widget title/page content from the dummy view instead. Add: widgets rebuild when the page set changes with
    the same count; the extension pages disappear from the view model after the extension is disabled; a page with a
    `Message` has no percent bar value; severity maps to the expected colour brush for Good/Warning/Critical (use the
    same helper the view model uses).
11. Dispose the host in a `finally`/`using` in every host test.

## Definition of done for this round
- Everything above implemented, no behaviour of the already green tests changed except where stated.
- Output a short summary: files touched, anything not done and why. Do not claim tests passed (you cannot run them).
