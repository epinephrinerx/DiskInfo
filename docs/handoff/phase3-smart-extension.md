# Handoff: R-004 phase 3, SMART becomes the first extension (`SysMonitor.Ext.Smart`)

Repo: `D:\Dev\Apps\SystemMonitor4.0`, branch `agent/agy-sysmon-ext-smart` (from `97fcdc4`, phase 2b committed, clean tree).
Writing Owner: agy. Coding Owner / integrator: Claude Code. Background: `phase1-extension-loader.md` (trust model),
`phase2a-extension-views.md` (host, views), `phase2b-approval-flow.md`, Requirements.md R-004 and R-002 (SMART module).

## Goal
Everything that is "SMART" leaves the host and lives in a new first-party extension project `SysMonitor.Ext.Smart`,
loaded through the extension loader like any third-party extension. After this phase the host (`SysMonitor.Wpf`) has no
reference to `DiskInfoCore.dll`, no `SmartCore`, no SMART view code and no `smart_*` strings. What the user sees (widget
page, overall section, full-view tabs, disk temperature fallback) must look the same as today.

Out of scope here (phase 4): the real packaging (manifest/hash generation, embedding the first-party allowlist in the
exe, installer, `build-wpf.cmd`), and the sample third-party extension. In this phase packages are built inside tests
with `ExtensionTestPackageBuilder`. Until phase 4 the real app will therefore not load the SMART extension; that is expected.

## Hard rules
- Use only read / search / edit / create tools. Never run a shell command, tests or git.
- No NuGet packages in any non-test project. Short why-comments in the surrounding style, no banners. Keep the diff scoped.
- **Tests must never scan real disks, call `DiskInfoCore.dll`, write to the real `%ProgramData%`, call
  `AppConfig.Save()` or read/write `%APPDATA%\SystemMonitor`.** The extension takes its scanner through an internal seam
  (see 3) so tests use a fake. A test host that is non-elevated never starts a scan thread.
- Do not weaken any trust rule (hash before load, elevated host loads first-party only, approval store ACL checks).
- Do not touch the Linux/WSL side or `DiskInfoCore.dll` itself. Do not touch `D:\Dev\Apps\AIMonitor2.0`.

## Design

### 1. Contract 1.1 (additive, `SysMonitor.Extensibility`)
`ContractInfo.Version` becomes `1.1`. The loader already accepts a manifest whose contract has the same major and a
minor not above the host's, so 1.0 extensions keep loading and the dummy test extensions stay at `"1.0"`.
New public types (XML docs in the style of the existing files; every method must be instant and thread-safe):
```
[Flags] public enum ExtensionDemand { None = 0, Views = 1, DiskTemperature = 2 }
public interface IDemandAware { void SetDemand(ExtensionDemand demand); }
public interface IDiskTemperatureSource
{
    // Latest cached temperatures in degrees C, keyed by physical drive number (\\.\PhysicalDriveN). No scanning here.
    IReadOnlyDictionary<int, int> GetDiskTemperatures();
}
```
`Views` means "the user wants this extension's views on screen". `DiskTemperature` means "some disk has no temperature
from the storage stack, so the host would use yours". An extension should read hardware only while demand is non-zero
(a SMART command can wake a sleeping drive).

### 2. Manifest `provides` and the "service" rule (loader + host)
- `ExtensionManifest` gets an optional `provides` list (`"provides": ["disk-temperature"]`). Validation: at most 8
  entries, each `^[a-z0-9][a-z0-9-]{0,31}\z`, no duplicates, no null; otherwise `ManifestInvalid`. Unknown capability
  names are accepted and ignored (forward compatible). Update the files-hash/`Inspect` code paths only as far as needed.
- Rule: a **first-party** extension (manifest hash in the embedded allowlist) that provides `disk-temperature` is a host
  service. It stays loaded even when the user switches it off in the sidebar; the switch then only controls whether its
  **views** are shown. A third-party extension is never kept loaded against the user's switch, whatever it declares.
- Loader: add `ExtensionLoaderOptions.KeepLoaded` (`Func<ExtensionManifest, bool, bool>?`, args = manifest, isFirstParty).
  Where the loader returns `Disabled` for `IsEnabled(id) == false`, it first asks `KeepLoaded`; if that returns true the
  extension loads normally. Trust checks still run before this decision, exactly as today. The host passes
  `(m, firstParty) => firstParty && m.Provides.Contains("disk-temperature")`.
- Host (`ExtensionHost`): `Entry.Enabled` keeps meaning "views shown" (the user's switch). For a kept-loaded extension
  `SetEnabled` only flips `_enabled`, republishes the entry (status stays `Loaded`), re-sends the demand (3 below) and
  does NOT retire or reload anything; `CollectViews` skips an extension whose `Enabled` is false (also today's rule, keep
  it). For every other extension nothing changes. `ReconcileExtension` must compute `desired` as
  `enabled || keptLoaded(lastResult)`, where `keptLoaded` uses the manifest and first-party flag stored in the last
  result. Keep the lifetime-gate and ordering guarantees of 2a/2b (no retire/dispose under `_lock`, `_lifecycle` serialises).
- `CanToggle` for such an extension is true while it is `Loaded`.

### 3. Host services for the SMART extension (`ExtensionHost`)
- `public IReadOnlyDictionary<int, int> GetDiskTemperatures()`: for each loaded slot whose instance is
  `IDiskTemperatureSource`, `TryAcquire`/`Release`, per-extension try/catch, keep only keys `0..255` and values `1..149`
  (the existing `DiskTemperature.Plausible`), at most 64 entries per extension, extensions visited in ordinal id order,
  first value for a key wins. Returns an empty dictionary when none. Must be cheap (called every sampler tick); allocate
  only a small dictionary.
- `public void SetDemand(bool diskTemperatureNeeded)`: remembers the flag (volatile) and pushes
  `ExtensionDemand` to every loaded `IDemandAware` extension: `Views` when its entry is enabled, `DiskTemperature` when the
  flag is set. Push only on change per extension (remember the last demand sent per id). Per-extension try/catch plus the
  same log-dedupe as elsewhere. It is also applied (a) right after an extension finishes `Initialize` and before its
  first view is read, and (b) when `SetEnabled` changes the switch. It runs on the caller's thread and never inside `_lock`.
- Sampler (`Sensors/Sampler.cs`): the temperature merge takes the SMART value from `_extensions?.GetDiskTemperatures()`
  (empty when there is no host), and every tick calls `_extensions?.SetDemand(wantSmart)` with the flag it already
  computes (any disk without a plausible storage-stack temperature). Delete `SmartResult`, `SmartLoop`, `_smartLoop`,
  `RequestSmart`, `BuildSmart`, `SmartEvery` and their start-up code. `Elevated` stays (the settings panel uses it).
- `DiskTemperature.Merge(int? storage, int? external)`: drop the `elevated`/`smartAvailable` parameters (the extension
  returns nothing when it cannot read). Keep `Plausible`. `TempSource.Smart` stays as the name for "from an extension";
  update the summaries. Adapt `SmartFallbackTests` (rename the file if it helps) so every rule it covered is still covered.

### 4. The extension project `SysMonitor.Ext.Smart`
New folder `SysMonitor.Ext.Smart`, `net8.0`, nullable and implicit usings on, project reference to
`SysMonitor.Extensibility` only, `AssemblyName` = `SysMonitor.Ext.Smart`, `InternalsVisibleTo("SysMonitor.Tests")`.
It is added to the test project as a `ProjectReference`. Do not add it to the Wpf project.
Move (git mv semantics, keep history readable) and adapt:
- `Native/SmartCore.cs` to `SmartCore` (internal). Keep the C ABI, the structs and the `DllImport("DiskInfoCore.dll")`
  as they are. The extension loader resolves that name from the preloaded native handle of the package, so the
  `DefaultDllImportSearchPaths` attribute may stay. Put the scan behind an internal seam
  `interface ISmartScanner { bool IsAvailable { get; } (SmartScanStatus Status, IReadOnlyList<SmartReading> Disks) Scan(); }`
  with the real implementation delegating to the static code; a test fake implements it.
- `Model/SmartInfo.cs` (`SmartState` loses `Off`; `SmartDisk`, `DiskHealth`, `SmartInfo`) and `Sensors/SmartBoard.cs`
  (`Build` loses the `show` parameter; `elevated` stays and still yields `NeedAdmin` when false).
- Moved texts: the `smart_*` keys, plus the two shared keys it needs (`temp_label`, `type`), copied word for word in
  th and en from `I18n.cs` into a `StringTable` in the extension (resource-free: a static dictionary). Only change:
  `smart_missing_core` becomes "DiskInfoCore.dll was not found in the SMART extension folder, so SMART cannot be read"
  (Thai equivalent). Remove `show_smart`, `smart`, `smart_*` from the host `I18n.cs` and keep `disk_smart`,
  `disk_smart_hint`, `restart_admin`, `admin_active`, `admin_missing_core` there (the settings panel still uses them).
  A parity test (`StringTable.MissingKeys`) must pass for the new table.
- `SmartExtension : IExtension, IViewProvider, IDemandAware, IDiskTemperatureSource` (public sealed, parameterless
  constructor for the loader; an internal constructor takes `ISmartScanner`, a clock and `elevated` for tests).
  - `Initialize(context)`: remember language/elevation; when `context.HostIsElevated` start a background STA thread
    named `smart-scan` (what the DiskInfo GUI uses for COM). Not elevated: no thread, state `NeedAdmin`.
  - Scan loop = the old `SmartLoop`: every 3 s wake-up; scan when demand is non-zero and the next time has come; next
    scan in 2 minutes; a pass that does not succeed replaces the readings by an empty set in ONE write (a reader never
    pairs a new status with old disks); log status changes through `context.Log` (not `Diag`). When demand changes from
    no-`Views` to `Views`, discard the old result and scan at once (this replaces `RequestSmart`: whatever was read while
    the views were off may be hours old). `Shutdown()` signals stop, joins the thread (bounded 10 s) and never throws; a
    scan that is still inside native code after the bound is left to finish on its background thread.
  - `SetDemand`: store (volatile) and wake the loop. `GetDiskTemperatures()`: from the last good result, drive number to
    plausible `TempC`, empty otherwise. Both instant.
  - `GetDisplayName(language)`: "SMART". `GetView(language)`: builds from the cached `SmartInfo` only, never scans.
- `SmartViewBuilder` (internal, pure): `SmartInfo` + language + `StringTable` to `ExtensionView`, reproducing what
  `WidgetViewModel.Smart.cs` draws today:
  - Widget pages: one `WidgetPage` per disk (`Title` = "SMART Disk N" as today's head, `Verdict` = health text, `Line` =
    life/hours line, `TemperatureC` = disk temp, `Percent` = life left or 100, `Severity` from health); with no disks one
    page whose `Message` is the status sentence.
  - Overview: `OverviewSection` with one `OverviewRow` per disk (`Title` "Disk N", `Detail` = model, interface+media and the
    life/hours line joined with the same separator as today, `ValueText` = health text, `Percent` and `Severity` as above,
    `TemperatureC`); with no disks `Note` = status sentence and no rows.
  - Tabs: one `DetailTab` per disk with key `disk<N>`, title `SMART <N>`, subtitle = health text, `Hardware` = model, the
    same facts in the same order as `AddSmartTabs`; with no disks one tab keyed `status` with the status sentence as
    detail and as its single fact. Health to `Severity`: Good, Caution, Bad as is; Unknown to `Neutral`.
  - Read `WidgetViewModel.UpdateExtensionPage` and the overview/tab code for extensions in the host and compare with the old
    `UpdateSmartPage`/`FillSmartSection`/`AddSmartTabs`. List in your summary every visual difference you find (for
    example the colour used for `Neutral`, the bar for a hard disk without wear gauge, the compact meter). Fix a
    difference in the **extension view data** when you can; only change the generic extension renderer when the change
    cannot alter how other extensions look (say so). Do not silently accept a difference.

### 5. Host clean-up (the "no SMART in the core" part)
Delete: `Native/SmartCore.cs`, `Sensors/SmartBoard.cs`, `Model/SmartInfo.cs`, `ViewModels/WidgetViewModel.Smart.cs`,
`Snapshot.Smart`, every `smart` kind/case/branch in `WidgetViewModel.cs` (view list, `SmartViewsStale`, `UpdateSmartPage`,
overview "smart" section and its heading, `AddSmartTabs`, the "smart" label, the `smart*` remembered-tab special case),
the `show_smart` checkbox in `MainWindow.xaml.cs`, and `SmartCore` use in `MainWindow.SidePanels.cs`.
- `AppConfig.ShowSmart` is removed. Migration: keep a private/legacy nullable `show_smart` property that is read from
  old files and never written (`JsonIgnore(Condition = WhenWritingNull)`); `Normalize()` copies a non-null value into
  `Extensions["smart"]` (`Enabled = old value`) when that key is absent, then clears it. Old default (off) therefore stays
  off. Likewise `FullTab` values `smart` map to `ext:smart:status` and `smart<N>` to `ext:smart:disk<N>`.
- `MainWindow.SidePanels.cs` `AddDiskSmart`: show the "restart as administrator" button when the host is not elevated
  and `_extensions.Entries` contains id `smart` with status `Loaded`; otherwise show `admin_missing_core` as today.
- Everything else in the host that mentioned SMART must compile without it. Search for `Smart`, `SMART`, `smart`.
- Installer/build files: only fix what stops compiling; the packaging is phase 4. Leave a one-line `TODO(phase 4)`
  comment where `DiskInfoCore.dll` is still copied next to the exe.

### 6. Tests
- Port `SmartModuleTests` and `SmartFallbackTests`: pure rules (board states, reading conversion, interface/media names,
  plausibility, merge) as unit tests of the extension's internals; view tests become `SmartViewBuilder` tests asserting the
  exact `ExtensionView` content (text, order, severity, percent, temperature, null handling) for: no disks per state
  (Waiting, NeedAdmin, Unavailable, Failed, Ok with none), one SSD with all values, one HDD without life, a disk with no
  temperature, a partial reading (`Full == false`), long power-on hours, th and en.
- `SmartExtension` tests with a fake `ISmartScanner` and fake clock/wake seam: not elevated (no thread, NeedAdmin);
  elevated, demand `None` (no scan), `Views` (scan, view shows disks), `DiskTemperature` only (scan, temperatures
  returned, views still show disks only if the builder is asked - state is shared), failed pass clears readings,
  `Views` off-to-on discards the old result, `Shutdown` stops the thread and is idempotent, a scanner that throws.
- Host tests: kept-loaded rule (first-party provider switched off stays `Loaded`, `Enabled == false`, no views collected,
  `GetDiskTemperatures` still answers; a third-party provider with the same manifest is `Disabled` when switched off;
  a first-party non-provider is `Disabled` when switched off); `provides` validation; `KeepLoaded` runs after trust
  (untrusted elevated-host provider is still `NotTrustedWhenElevated`); `GetDiskTemperatures` sanitising (range, count,
  hostile provider that throws, duplicate keys across two providers, none loaded); `SetDemand` pushes on change only,
  on enable/disable and after initialise (use AppDomain-data hooks in a new tiny test extension implementing the three
  interfaces, in the dummy-extension project, or extend `DummyViewsExtension`; contract `"1.1"` for it).
- One end-to-end test: build a package from the real `SysMonitor.Ext.Smart` assembly (no native file; the extension must
  answer `Unavailable` when `DiskInfoCore.dll` is absent), first-party allowlist containing its manifest hash, non-elevated
  host: it loads, `CollectViews` returns the `NeedAdmin` page/section/tab, and the `WidgetViewModel` shows it.
- Config tests: legacy `show_smart: true/false` migrates to `Extensions["smart"]`; the file written back has no
  `show_smart`; `FullTab` mapping; unknown values untouched. Tests that load/save config use temp paths only.
- Existing tests unrelated to SMART stay green unchanged. Update `StringTableAndI18nTests` for the removed keys.

## Definition of done
Everything above implemented; the solution compiles with no SMART symbol left in `SysMonitor.Wpf` except the legacy
config migration and the settings-panel strings; short summary of files touched, every visual difference found in 4, and
anything not done. Do not claim tests passed (you cannot run them).
