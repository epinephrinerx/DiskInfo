# Handoff: R-004 phase 2a, fix round 2 (Codex GPT-6.1 review findings)

Repo: `D:\Dev\Apps\SystemMonitor4.0`, branch `agent/agy-sysmon-ext-host`, uncommitted work in the tree (state after
round 1 in `phase2a-fixround.md`; 488 tests pass, 1 skipped). Writing Owner: agy. Integrator: Claude Code.

## Hard rules (same as round 1)
- Use only read / search / edit / create tools. Never run a shell command, tests or git.
- Never call `AppConfig.Save()` from production code outside `MainWindow` or from any test. Tests must not touch
  `%APPDATA%\SystemMonitor\config.wpf.json` (the existing read-only guard test is fine).
- No new NuGet packages. Keep the diff scoped to the files named here. Short why-comments, no banners.
- Do not weaken any trust rule from `phase1-extension-loader.md` (hash before load, approvals, elevated rules).

## Fixes (every one is required)

1. **`ExtensionHost.cs` - never retire/dispose under `_lock`.** In `ScanAndLoadAll` (the `_disposed` branch, around the
   first `RetireAndDispose`) and in `SetEnabled` (the `_disposed` branch of the enable path) collect the slots to retire
   inside the lock, leave the lock, then call `RetireAndDispose`. `Shutdown`, logging and unloading must never run while
   `_lock` is held.
2. **`ExtensionHost.SetEnabled` - order independence.** Workers are scheduled with `Task.Run`, so they may start in a
   different order than they were requested. After a worker has acquired `_lifecycle`, it must ignore its own `enabled`
   parameter and reconcile the extension to the current desired state read from `_enabled` (under `_lock`): desired
   enabled and not loaded -> load; desired disabled and loaded -> retire. A stale worker then simply finds nothing to do.
   Test: with an extension loaded call `SetEnabled(id,false)` then `SetEnabled(id,true)` many times in a row (for
   example 20 pairs, awaiting `Task.WhenAll` at the end ending with `true`) and assert the final state is loaded,
   `Entries` shows `Loaded` and `Enabled`, and `CollectViews` returns the view. Repeat with a final `false`.
3. **`ExtensionHost.SetEnabled(false)` keeps a rejection reason.** When disabling, replace the previous result with the
   `Disabled` status only if the previous status was `Loaded` or `Disabled` (or there was none). If it was a failure
   status (NotApproved, NotTrustedWhenElevated, HashMismatch, ...) keep the previous `ExtensionLoadResult` unchanged.
   Tests: enabled+NotApproved extension, toggle off -> `Entries` still `NotApproved`; and `CanToggle(NotApproved,false)` stays false.
4. **`ExtensionLoader.LoadFolder` - stay inside the root.** Reject (return an `UnsafePath` result with a clear detail,
   no hashing and no loading) a folder whose full path is not exactly `Path.Combine(ExtensionsRoot, <folder name>)`
   i.e. whose parent directory (full path, case-insensitive, trailing separators trimmed) differs from the full path of
   `options.ExtensionsRoot`. Test: a valid package placed in a sibling directory outside the root is refused with
   `UnsafePath`; the same package inside the root loads.
5. **`ExtensionHost.SanitizeView` / display name - bound the CPU before scanning.** Truncate to `MaxTextLength` first,
   then test `IsNullOrWhiteSpace`. Applies to the tab key (`SanitizeTab`: sanitise the key text first, then check blank)
   and to the display name in `CollectViews`. Test: 128 tabs whose key is a 5 MB whitespace string produce no tabs and
   finish quickly (assert completes within 5 s using a task wait, and no tab survives).
6. **`ExtensionHost.CollectViews` - cache the display name independently of the view.** Call `GetDisplayName`, sanitise
   it and update `_cachedDisplayNames` (and rebuild `_entries` when it changed) before calling `GetView`; a throwing
   `GetView` must not stop the name following the language. Keep the single try/catch semantic: an exception from
   `GetDisplayName` itself still skips the extension for that tick. Test: ViewFunc throws, DisplayNameFunc depends on
   language; `CollectViews("th")` then `CollectViews("en")` -> `Entries[0].Name` becomes the English name.
7. **`WidgetViewModel.cs` (around the Full-view tab selection, ~line 1263) - two selection bugs.**
   a. When no tab remains (`Tabs.Count == 0`) clear `SelectedTab` (set to null / empty as the property allows) so the
      Full panel stops showing a removed extension tab.
   b. Remembered extension tab that arrives late. Track whether the current selection is only a fallback (the
      remembered `_config.FullTab` was not available when the selection was made, or nothing was remembered) versus a
      choice the user made through the selection API. When the tab whose key equals `_config.FullTab` appears later and the
      current selection is a fallback, switch to it. A selection the user made explicitly must never be replaced.
   Tests (new, in `WidgetViewModelExtensionsTests.cs`): (a) show only an extension tab, then an empty extension
   snapshot -> `Tabs` empty and `SelectedTab` cleared; (b) remembered `ext:my_ext:tab1`, first snapshot core only (CPU
   selected as fallback), second snapshot adds the extension -> selection moves to `ext:my_ext:tab1`; (c) same but the
   user explicitly selected CPU before the extension arrives -> selection stays on CPU.
8. **`WidgetViewModel.cs` (~line 525, section reuse) - identity beyond the title.** When deciding whether existing
   `Sections` can be reused, compare a stable identity (core module key or `ext:<id>`) as well as the title; a mismatch
   rebuilds the section and resets `Note`. If sections do not currently carry a key, add an `Id` string to the section
   view-model and fill it where sections are created. Test: extension overview whose display name equals the localized
   disk heading is replaced by the core disk section -> `Note` is empty and rows are the core rows.
9. **Tests that could pass vacuously (fix them).**
   a. `ExtensionHost_LifetimeGate_ShutdownWaitsForInFlightViewsCall`: after calling `SetEnabled(id,false)` do not assert
      immediately. Wait (bounded, 5 s) until `host.Entries` no longer reports the extension as loaded/enabled or until a
      hook flag signals that retirement started (`Entries` shows `Disabled` after the lock section; use that), then assert
      `ShutdownCalled` is still false while `GetView` is still blocked; release; await; assert true.
   b. `WidgetViewModel_..._PageSetChanges...` test (~line 210): stop calling `RebuildViews(snap2)` explicitly; feed the
      second snapshot through `UpdateWidget` (as the app does) and assert the pages were rebuilt.
10. **Extra coverage requested:** (a) `CollectViews` with one hostile provider (throwing indexer) and one healthy
    provider returns the healthy view (you may use two dummy extension folders or the existing dummy twice if the
    harness allows; if the harness cannot load two extensions, skip with a comment saying why); (b) null elements in the
    tab / fact / page / row lists are skipped without exception.

## Definition of done
All of the above implemented; no change to behaviour not named here; summary of files touched and anything not done.
Do not claim tests passed (you cannot run them).
