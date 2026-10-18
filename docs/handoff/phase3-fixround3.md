# Handoff: R-004 phase 3, fix round 3 (Opus verification findings)

Repo: `D:\Dev\Apps\SystemMonitor4.0`, branch `agent/agy-sysmon-ext-smart`, uncommitted work in the tree (553 tests pass).
Same hard rules as `phase3-fixround.md`: only read/search/edit/create tools, never run shell/tests/builds/git, tests never
touch real disks, `DiskInfoCore.dll`, `%ProgramData%`, `AppConfig.Save()` or `%APPDATA%\SystemMonitor`, no NuGet packages,
verify every API by reading its source before using it (round 1 invented APIs once; do not repeat that).
Already fixed by the integrator, do not redo: `gate.Dirty` is now set before `Monitor.TryEnter` and is `volatile`;
the full-view RAM tab key match is `"ram"`.

## Fixes

### 1. `Reload` of a package whose last result was an early failure (ExtensionHost ~454-505, ExtensionLoader ~642-741)
Early-failure results (HashMismatch, read failure, EntryNotFound, DuplicateId, native `LoadFailed`, and any result built after
`isFirstParty` is known) are created without `isFirstParty`. Make every `ExtensionLoadResult` built in `LoadSingleFolder`
after `isFirstParty` is computed carry it. In `ReconcileExtension` (refresh branch) the "abort" decision must not discard a
good result: after `LoadFolder` returns `Loaded`, compare against the state recomputed from the NEW result and the current
`_enabled`, not against the stale previous result. Test: first-party `provides: ["disk-temperature"]` package, switched off in
config, one file with a wrong hash -> `StartAsync` gives `HashMismatch`; fix the file; `Reload(id)` -> status `Loaded`,
`GetDiskTemperatures()` answers (use the demand/temperature dummy).

### 2. `place_after` when the target module is not shown (WidgetViewModel.cs ~522-544 and WidgetViewModel.Extensions.cs ~150-185)
HEAD put SMART between RAM and GPU even when Disk was hidden. Canonical order: `cpu, ram, disk, gpu, net`. If the target
module has no section/tabs, insert after the last present module that comes earlier in that order; if none is present,
insert at the start. Apply the same rule to the overview and to the full-view tabs (keep "several extensions on one target
in ordinal id order" and "no field = last"). Tests (overview and tabs): `ShowDisk = false` with GPU + network shown -> order
`cpu, ram, ext, gpu, net`; only GPU shown -> `ext, gpu, ...`; `place_after = "ram"` in the full view goes right after the RAM tab.

### 3. First `SetDemand` before the first view/metric read (ExtensionHost ~164/172, ~512, ~189/526)
Today the slot is published to `_slots` before the first `PushDemand` has run, so `CollectViews`/`GetDiskTemperatures` can
read a demand-aware instance that never got its demand. Add a volatile `DemandApplied` flag on `ExtensionSlot`: `true`
at creation when the instance is not `IDemandAware`; for an `IDemandAware` instance it becomes true only when
`PushDemand` has delivered a demand to that slot (or the delivery failed with an exception, so a hostile extension cannot
hide itself). `CollectViews` and `GetDiskTemperatures` skip slots whose flag is false (they appear on the next tick).
Test: block the first `SetDemand` of a new instance with an AppDomain-data gate; during the block `CollectViews` returns no
view for it and `GetView` was not called; after release the view appears.

### 4. Manual text (I18n.cs `manual_10_t`, th and en)
Restore the old sentences and only change what moved. English: "Turn it on with the SMART switch in the extensions list of the
sidebar. It needs the app to run as administrator and DiskInfoCore.dll in the SMART extension folder. It reads about every 2
minutes, which can wake a sleeping hard disk, so it is off by default. The bar shows life left; a disk with no such gauge
shows a full bar in the colour of its health." Thai: keep the HEAD wording (`git show HEAD:SysMonitor.Wpf/I18n.cs`, key
`manual_10_t`), replacing only "เปิดได้ที่ แสดง SMART ของดิสก์" with "เปิดได้ที่สวิตช์ SMART ในรายการส่วนเสริมที่แถบข้าง" and
"ข้างโปรแกรม" with "ในโฟลเดอร์ส่วนเสริม SMART". Heading keys `manual_10_h`: "Disk SMART" / "SMART ของดิสก์" as at HEAD. The sidebar name
is "SMART", not "S.M.A.R.T.": check that `manual_10_*` and any other user text use "SMART".

### 5. Scan interval (SmartExtension.SetDemand ~113-116)
Only `Views` off -> on resets the schedule and rescans at once. `None -> DiskTemperature` (and other changes that do not turn
`Views` on) must keep the 2-minute `next` time, as HEAD did when `_smartNeeded` flipped; it may still wake the loop so a first
ever scan is not delayed (first scan when none has run yet is allowed immediately).

## Tests to add or strengthen (L = lost coverage, W = weak)
- L: restore HEAD's check that every `DllImport` in `SmartCore` has `DefaultDllImportSearchPaths` and that it is exactly
  `System32` (reflection over the nested/static methods of `SysMonitor.Ext.Smart.SmartCore`).
- L: `Every_word_of_the_module_exists_in_both_languages` style test must cover `manual_10_h`/`manual_10_t`.
- W: `SmartExtension_views_off_to_on_discards_old_state_and_rescans_immediately` must assert the discard: right after
  `SetDemand(Views)` and before the fake scanner is released, `GetView` shows the Waiting sentence and
  `GetDiskTemperatures()` is empty; then the rescan result appears.
- W: `ExtensionHost_Demand_ReloadCompletingWhileSetDemandPaused...`: replace `Thread.Sleep(50)` by a condition (the
  reload has started and is waiting: expose it through a hook or poll `Entries`/a flag with a 5 s bound) and assert the NEW
  instance received its demand once the callback is released.
- W: `SmartExtension_demand_none_does_zero_scans`: prove it positively (drive the loop with the fake clock/wake seam and
  assert zero scans after at least one full loop pass observed through a hook), no fixed sleep.
- W: `SmartExtension_shutdown_with_scan_outliving_bound...`: wait for "scan started" with a 5 s event, not a 1 s poll.
- W: `MockContext.Logs` / `FallbackMockContext.Logs` are read by the test thread while the scan thread writes: use a
  `ConcurrentQueue<string>` or a lock.
- New: a test for fix 5 (a demand change `None -> DiskTemperature` right after a scan does not trigger a second scan before
  2 minutes of fake clock; `Views` off -> on does).

## Definition of done
Everything above implemented with real APIs; existing tests unchanged in intent; short summary of files touched and what was
not done. Do not claim tests passed (you cannot run them).
