# Handoff: R-004 phase 3, fix round 4 (Codex verification findings)

Repo: `D:\Dev\Apps\SystemMonitor4.0`, branch `agent/agy-sysmon-ext-smart`, uncommitted work in the tree (560 tests pass).
Same hard rules as `phase3-fixround.md`: only read/search/edit/create tools, never run shell/tests/builds/git; tests never
touch real disks, `DiskInfoCore.dll`, `%ProgramData%`, `AppConfig.Save()` or `%APPDATA%\SystemMonitor`; no NuGet packages.
**Verify every API and member name by reading its source before using it** (twice before, tests used invented members
such as `UpdateFull` or a 6-argument `ExtensionViewData`; the real ones are `PushHistory` and
`ExtensionViewData(Id, DisplayName, View, PlaceAfter)`).

## Fixes

### 1. Demand gate belongs to the slot, not the id (ExtensionHost ~740-850)
A callback that blocks on a retired instance keeps the id's gate locked, so the replacement never gets its demand and
`DemandApplied` stays false forever. Make the gate a field of the slot (`ExtensionSlot.DemandGate`, created with the slot);
`PushDemand(id)` takes the gate of the CURRENT slot for the id. Remove the per-id `_demandGates` dictionary. The
dirty-before-TryEnter rule, the recompute loop and `DemandApplied` stay as they are. Test: load, block a `SetDemand`
callback indefinitely (gate released only at the end of the test), `Reload` (the 5 s retirement wait elapses, so keep the
test bounded: the retire budget can be shortened through an internal option/seam if one exists, otherwise accept the 5 s),
then the NEW instance receives its demand and `CollectViews` returns its view.

### 2. `Reload` must publish a failure (ExtensionHost `ReconcileExtension`, refresh branch ~490-520)
For `refresh == true` (called only by `Reload`, which has already removed the slot) the abort check
`curDesired != desired` must not apply: always store `_lastResults[id] = result`, create the slot only if `result` is `Loaded`
(and then also require that it is still wanted: `enabled || kept` computed from the NEW result; if it is not wanted, retire
the new slot and store a `Disabled` result), and publish. Only `_disposed` aborts. Tests: a disabled first-party provider
whose DLL is deleted -> `Reload` -> entry `FileMissing` (or the loader's status), no slot, `GetDiskTemperatures` empty; a
provider whose manifest changes so it is no longer first-party (hash not in allowlist) -> `Reload` -> `NotApproved`.

### 3. Generation check for scans (SmartExtension)
A scan that started before a `Views` off->on discard must not publish its result over the discard. Keep an `int _generation`
that `SetDemand` increments (under `_lock`) when it discards; the loop reads it before calling the scanner and publishes only
if it is unchanged afterwards (otherwise it drops the result and loops to scan again because `_rescanRequested` is set).
Test with barriers: block a `DiskTemperature` scan, turn Views on (state is Waiting, temps empty), release the old scan: the
state is STILL Waiting/empty while the new scan is blocked; release it: the new result appears.

### 4. One placement algorithm for overview and tabs (WidgetViewModel.cs ~522-544, WidgetViewModel.Extensions.cs ~150-185)
The two code paths disagree when targets collapse onto one fallback anchor (Disk hidden, extension `a` -> `disk`,
extension `b` -> `ram`: overview `ram, b, a, gpu`, tabs `ram, a, b, gpu`). Put the rule in ONE helper used by both: each extension
gets an anchor = its target module if that module is present, else the last present module that precedes the target in the
canonical order `cpu, ram, disk, gpu, net` (or "start" if none). Extensions are inserted after their anchor, ordered first by
the canonical position of their TARGET, then by ordinal id. Test both views with that example: expected `ram, b, a, gpu`
in the overview AND `ram, b, a, gpu` in the tabs (b targets ram, which comes before disk).

### 5. Tests
- `SmartFallbackTests` interval test (~348): replace the 50 ms sleep by observing that the worker processed the
  `None -> DiskTemperature` change (a hook/counter on the loop pass, polled with a 5 s bound) before asserting that no
  second scan happened and then that `Views` off->on does cause one.
- `SmartModuleTests` shutdown-pin test (~696): the fake scan must outlive the 4 s join deterministically (block on an event that
  the test releases only after `Shutdown` has returned), not a 6 s timer.
- `ExtensionHostTests` GetDiskTemperatures sanitisation (~1286): supply 70 valid readings and assert the exact retained
  count (64) and representative values; add a case with invalid values mixed in.

## Definition of done
Everything above implemented with real APIs; existing tests unchanged in intent; short summary of files touched and what was
not done. Do not claim tests passed (you cannot run them).
