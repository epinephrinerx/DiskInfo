# Handoff: R-004 phase 3, fix round 2 (host tests that did not compile)

Repo: `D:\Dev\Apps\SystemMonitor4.0`, branch `agent/agy-sysmon-ext-smart`, uncommitted work in the tree. Writing Owner: agy.
Same hard rules as `phase3-fixround.md` (only read/search/edit/create tools; never run shell, tests, builds or git; tests never
touch real disks, `DiskInfoCore.dll`, `%ProgramData%`, `AppConfig.Save()` or `%APPDATA%\SystemMonitor`; no NuGet packages).

## What went wrong
In fix round 1 the host tests were written into a NEW file `SysMonitor.Tests/ExtensionHostTests.cs` that invented APIs
(`ApprovalRecord`, `Core(..)` constructors, a `FirstPartyAllowlist` built from a `HashSet`, `ExtensionManifest.Validate`
called wrongly, manifest named `manifest.json`) and did not compile. The integrator moved it out of the tree; its text is
at `C:\Users\golf\AppData\Local\Temp\claude\D--Dev-Apps-DiskInfo\84cce59e-9a09-44b8-b308-8b2c38046b2b\scratchpad\stray-ExtensionHostTests.cs.txt`
(use it only as a list of intended cases). The production code of round 1 and all other test files compile and pass.

## Task
Write the following host tests properly. Put them in the EXISTING `SysMonitor.Tests/Extensions/ExtensionHostTests.cs`
(and, for the view-order test, `SysMonitor.Tests/Extensions/WidgetViewModelExtensionsTests.cs`), at the end of the class, using
ONLY helpers and APIs that exist: read the source before using any type. Copy the patterns the existing tests in these files
already use: `CreateExtensionWithManifest(id, out manifestSha256, configure)` (manifest file is `extension.json`),
`new ApprovalStore(path, _ => true, _ => { }, _ => { }, _ => { })`, `new FirstPartyAllowlist(new[] { hash })`,
`ExtensionLoaderOptions { ExtensionsRoot = _tempDir, ... }`, `new ExtensionHost(config, options)`, `await host.StartAsync()`,
`DummyViewsExtension` / `DummyDemandAndTempExtension` AppDomain-data hooks (read their sources for the exact hook names,
and `Reset()` them first). Build `Snapshot`/`WidgetViewModel` the way `WidgetViewModelExtensionsTests` does. Every test
name must say what it proves; no test may pass vacuously (assert the positive outcome that proves the code ran).

Cases (all required):
1. Manifest `place_after`: valid values `cpu ram disk gpu network` accepted; `"foo"`, empty string, upper-case `"Disk"` give
   `ManifestInvalid`; absent is valid (`ExtensionManifest.TryParse` / `Validate(manifest, out error)` - read the signatures).
2. Overview and full-view order: a first-party-like extension with `place_after = "disk"` plus GPU and network core data:
   sections/tabs order is Disk, extension, GPU, Network; an extension without the field goes last. Use the real
   `ExtensionHost` + `WidgetViewModel` (or the existing way `WidgetViewModelExtensionsTests` feeds extension views).
3. `GetDiskTemperatures`: (a) a provider whose returned dictionary has a throwing enumerator does not stop a healthy second
   provider (two packages in the temp root; both need distinct ids, entry types may be the same dummy type if the hooks
   allow, otherwise add a tiny second dummy type in the dummy project); (b) a provider that yields invalid pairs
   endlessly returns within the 256 cap (bounded wait of 5 s); (c) duplicate keys across two providers: the ordinal-first
   id wins; (d) no extension loaded returns an empty dictionary.
4. Demand: (a) after `Reload(id)` the NEW instance gets `SetDemand` once with the current demand; (b) a non-kept
   `IDemandAware` extension switched off then on gets a `SetDemand` on the new instance; (c) a `SetDemand` callback that
   blocks while another thread flips the demand and completes: the second call returns without waiting for the first,
   and after the first callback is released the instance ends on the final value; (d) `Reload` completing while a
   `SetDemand` callback is paused never delivers a `SetDemand` after `Shutdown` on the old instance. Use AppDomain-data
   gates for the blocking; add the needed hooks to `DummyViewsExtension`/`DummyDemandAndTempExtension` if missing.
5. End-to-end: a package built from the real `SysMonitor.Ext.Smart` assembly (`typeof(SysMonitor.Ext.Smart.SmartExtension).Assembly.Location`)
   with no native file, `place_after = "disk"`, `provides = ["disk-temperature"]`, listed in the first-party allowlist,
   non-elevated host: status `Loaded`, `CollectViews("en")` returns the view whose page `Message`, overview `Note` and the tab detail
   contain the missing-core sentence ("DiskInfoCore.dll"), and the WidgetViewModel renders it. If a test with this exact
   behaviour already exists in `Extensions/ExtensionHostTests.cs`, strengthen it instead of duplicating.

## Definition of done
All cases implemented with real APIs; no new file with test classes named `ExtensionHostTests`; summary of files touched.
Do not claim tests passed (you cannot run them).
