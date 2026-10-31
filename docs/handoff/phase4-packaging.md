# Handoff: R-004 phase 4, packaging, installer and sample extension

Repo: `D:\Dev\Apps\SystemMonitor4.0`, branch `agent/agy-sysmon-ext-packaging` (from `77280d7`, phase 3 committed, tree clean).
Writing Owner: agy. Coding Owner / integrator: Claude Code (builds and runs every test; you cannot).

## Hard rules
- Only read/search/edit/create tools. Never run shell, tests, builds or git. You cannot delete files: if a file must go, say so in the summary.
- Tests never touch the real `%APPDATA%\SystemMonitor`, `%ProgramData%`, the real install folder (`%LOCALAPPDATA%\Programs\SystemMonitor`), the registry, real disks or `DiskInfoCore.dll`. `Installer.Install()/Uninstall()` (registry, shortcuts, real paths) are NEVER called by tests; test only the new pure-file helpers with a temp directory.
- No NuGet packages in the product projects or the pack tool. Test project keeps its existing packages.
- Verify every API/member name by reading its source before using it. Earlier rounds invented members (`UpdateFull`, a 6-arg `ExtensionViewData`); the integrator compiles everything and will reject invented APIs. Copy existing patterns (`ExtensionTestPackageBuilder`, `ExtensionHostTests`, `InstallerSafetyTests`).
- Keep the diff in scope: no contract change, no host UI change except what is listed, no Linux, no DiskInfoCore.dll source change.

## Background (read these first)
- Package layout the loader accepts: `<ExtensionsRoot>\<id>\extension.json` plus exactly the files listed in `files[]` (`ExtensionLoader.cs` ~475-530: any unlisted file anywhere in the subtree, including `.pdb` and `.deps.json`, makes the load fail). `ExtensionsRoot` is `<exe dir>\extensions` (`App.xaml.cs` ~70). `SysMonitor.Extensibility.dll` is shared from the host and must NOT be in a package. Native files are `"native": true`; the entry assembly cannot be native.
- Manifest class: `SysMonitor.Wpf/Extensions/ExtensionManifest.cs` (snake_case JSON, `ExtensionManifest.JsonOptions`, `TryParse`/`Validate`).
- First-party trust: `FirstPartyAllowlist.FromAssembly(assembly)` reads the embedded resource named `first-party-extensions.json` (a JSON array of manifest SHA-256 hex strings of `extension.json` bytes). It does not exist yet: nothing produces it, so today no extension is first-party in the real app.
- Installer: `SysMonitor.Setup/Installer.cs` (per-user, explicit owned-file list, never recursive delete, `IsSafeTarget`/`PassesThroughLink` guards). `DiskInfoCore.dll` is still embedded and written next to the exe (`CoreName`, `WriteCore`, TODO(phase 4) comments there and in `build-wpf.cmd`).

## Work

### 1. Pack tool: new project `SysMonitor.PackTool` (net8.0, console, no packages)
- References `SysMonitor.Extensibility` (for `ContractInfo`) and compiles `..\SysMonitor.Wpf\Extensions\ExtensionManifest.cs` as a linked file (`<Compile Include=... Link=...>`; it only needs `System.Text.Json`). Read the file to check it has no dependency on other host types; if it has, report instead of inventing.
- Logic in a public static class (e.g. `ExtensionPacker`) so tests call it directly; `Program.Main` only parses arguments and maps to exit codes (0 ok, 1 invalid input/IO failure, 2 usage).
- Command line: `SysMonitor.PackTool.exe --template <extension.template.json> --out <dir> --version <x.y.z> [--file <name>=<path>]... [--allowlist <file>]`.
- Template (`extension.template.json`, checked in next to each extension project, JSON, same snake_case): `id, name, version` (literal `"$VERSION"` is replaced by `--version`), `contract, min_host, entry{assembly,type}, provides[], place_after`, and `files[]` of `{ "path", "native"?, "optional"? }`. `optional` is tool-only and never written to the manifest.
- Behaviour: `--file name=path` supplies the source for a file named in the template. A non-optional template file without a source, or an existing source for a name not in the template, or a source path that does not exist for a non-optional file -> exit 1 with a message naming the file. An optional file without a source is left out of the manifest and not copied. Copy only the supplied files into `--out` (create it; if it already contains anything, exit 1 so stale files can never survive). Compute SHA-256 (lower-case hex) of each copied file, build `ExtensionManifest` (files in template order), serialize with `ExtensionManifest.JsonOptions` indented, UTF-8 without BOM, `\n` line ends, write `extension.json`; then re-read it with `ExtensionManifest.TryParse` and fail if it does not validate. Reject a template whose `contract` has a different major than `ContractInfo.Current` or a larger minor. Print one line: `packed <id> <manifestSha256> <fileCount> files`.
- `--allowlist <file>`: read the JSON array (missing file = empty array; malformed = exit 1), add the manifest SHA-256 if not present, write the array back sorted ordinal, indented, UTF-8 no BOM. Idempotent.
- Public API returns a result record (manifest sha, written file names) so tests need not parse stdout.

### 2. Extension templates
- `SysMonitor.Ext.Smart\extension.template.json`: id `smart`, name `SMART`, version `$VERSION`, contract `1.1`, min_host `4.2.0`, entry assembly `SysMonitor.Ext.Smart.dll` type `SysMonitor.Ext.Smart.SmartExtension`, provides `["disk-temperature"]`, place_after `disk`, files: `SysMonitor.Ext.Smart.dll` (managed), `DiskInfoCore.dll` (`native: true`, `optional: true`). Check the entry type/namespace against `SmartExtension.cs`.
- Tell the integrator if the csproj needs `<None Remove>`/copy settings so the template is not copied into build output.

### 3. Sample third-party extension: new project `SysMonitor.Ext.Sample` (net8.0, refs `SysMonitor.Extensibility` only)
- Purpose: lets the user exercise the real approval flow (UAC) and the install/remove-without-rebuilding-the-host criterion. It is NOT embedded in the installer and NOT in the allowlist.
- Minimal `IExtension` + `IViewProvider`: one widget page / overview section / detail tab (read `ExtensionView` and `SmartViewBuilder`/`DummyViewsExtension` for the record shapes) showing a short static sentence and the host UI language it was asked for, with its own Thai/English strings via `StringTable` (follow `SmartStrings` style, th/en key parity). Display name `Sample` / `ตัวอย่าง`. No native code, no file or network access, no timers, no threads. `place_after` omitted (goes last).
- `extension.template.json`: id `sample`, name `Sample extension`, same contract/min_host rules, entry type, files: the dll only.

### 4. Allowlist embedding in the host
- `SysMonitor.Wpf.csproj`: embed `first-party-extensions.json` as `EmbeddedResource` with `LogicalName="first-party-extensions.json"` and `Condition="Exists('first-party-extensions.json')"`. The file is generated by `build-wpf.cmd` into `SysMonitor.Wpf\first-party-extensions.json`; add it to `.gitignore`. Also ignore the new build staging folder (below).
- Nothing else in the host changes (`ExtensionApproval` already uses `FromAssembly`).

### 5. `build-wpf.cmd` (keep the existing style: comments, `|| exit /b 1`)
New order, renumber the steps:
1. Tests (unchanged).
2. Extensions: `dotnet build SysMonitor.Ext.Smart -c Release --nologo -v quiet`; `dotnet build SysMonitor.Ext.Sample ...`; `dotnet build SysMonitor.PackTool ...`; delete `build-ext` (the staging folder, in the repo root and ignored by git) and `SysMonitor.Wpf\first-party-extensions.json`; run the tool for `smart` into `build-ext\smart` with `--allowlist SysMonitor.Wpf\first-party-extensions.json` and `--file SysMonitor.Ext.Smart.dll=<built dll>` plus `--file DiskInfoCore.dll=%DISKINFOCORE%` only when that file exists (keep the existing DISKINFOCORE default and messages); run it for `sample` into `build-ext\sample` WITHOUT `--allowlist`. If the allowlist file does not exist after packing, fail. Version for `--version` is read the same way `VERSION` is read today (move that block earlier, keep one definition).
3. Publish `SystemMonitor.exe` (now embeds the allowlist; must come after step 2).
4. Installer: clear `SysMonitor.Setup\payload\extensions`, copy `build-ext\smart\*` to `payload\extensions\smart\`, remove the old root-level `payload\DiskInfoCore.dll` handling; publish the setup.
5. Collect: as today, plus copy `build-ext\sample\` to `dist-wpf\sample-extension\sample\` (replace its contents) and print how to try it (copy the `sample` folder into `<install dir>\extensions\`, start the app, open the Extensions list, use the approval button).

### 6. Installer (`SysMonitor.Setup`)
- `SysMonitor.Setup.csproj`: remove the `DiskInfoCore.dll` embed; embed `payload\extensions\**\*` with logical names `ext:<id>/<file>` (convert `\` to `/`; MSBuild `%(RecursiveDir)` gives backslashes, normalise in code or pick a form that avoids it), conditioned so a payload-less build still compiles.
- `Installer`: new public static, explicitly parameterised, side-effect-limited helpers so they are testable with a temp directory and a fake payload (suggested: a small `PayloadFile(string Id, string Name, Func<Stream> Open)` record):
  - `ReadExtensionPayload()` -> the files from the assembly's `ext:` resources. Validate every name: id matches `^[a-z0-9][a-z0-9._-]{0,63}\z`, file name has no path separators, no `..`, not empty, not `extension.json`-lookalike tricks (case-insensitive duplicates rejected); anything else throws `InvalidOperationException`.
  - `WriteExtensions(string installDir, IReadOnlyList<PayloadFile> files)`: for each id: refuse when `installDir\extensions` or `installDir\extensions\<id>` is or passes through a link (reuse `RequireOwnDirectory`/`PassesThroughLink`), create the folder, delete files directly inside `extensions\<id>` that are not part of the new package (non-recursive, `IsSafeTarget` guarded, subfolders untouched), write the package files. Write `extension.json` last so a half-written package is never complete. A locked/in-use file must not abort the other ids; report which files failed (return a list) and let `Install` log nothing fatal for DiskInfoCore.dll in use (same tolerance as today's `WriteCore`).
  - `ExtensionOwnedPaths(IEnumerable<PayloadFile>)` for uninstall. `OwnedPaths(bool includeSettings)` must include the extension files and the legacy root `DiskInfoCore.dll` (so an upgrade from a phase-3-or-older install cleans it, on install and on uninstall). `Install` removes the legacy root `DiskInfoCore.dll` after the extensions are written.
  - `Uninstall` removes only owned extension files, then `RemoveIfEmpty` for each `extensions\<id>`, then `extensions`, then `InstallDir`. A third-party folder (`extensions\sample`) and the user's approval file must be left alone and keep their parent folders.
  - `RegisterUninstall` EstimatedSize includes the extension files. Remove the stale TODO(phase 4) comments and the now-wrong `CoreName`/`CorePath` doc text; keep `CoreName` only as the legacy name if still needed.
- Do not call any of this from tests except through the temp-dir helpers.

### 7. Tests (`SysMonitor.Tests`, add `ProjectReference` to PackTool and Sample; put new tests in new files, do not rename existing classes)
Pack tool:
- Packing a small fake dll (copy of the dummy extension dll) + fake native file produces a folder that the REAL `ExtensionLoader` loads as `Loaded` when the emitted manifest hash is in a `FirstPartyAllowlist`, and as `NotApproved` (non-elevated, no approval) when it is not; changing one byte of a file afterwards gives `HashMismatch`.
- Optional file absent -> not in manifest, not copied; non-optional absent -> exit/exception naming the file; unknown `--file` name rejected; non-empty `--out` rejected; `$VERSION` replaced; invalid template (bad id, bad contract major, contract minor above current) rejected; the manifest written parses with `ExtensionManifest.TryParse`; `optional` never appears in the output.
- Allowlist: created when missing, merged, sorted, idempotent, malformed file rejected; the written array is accepted by `FirstPartyAllowlist` (use the public constructor on the parsed list).
- Template parity: the checked-in `smart` and `sample` templates pass the packer's validation using the real built dlls (`typeof(SmartExtension).Assembly.Location`, `typeof(...Sample...)`), `contract` equals `ContractInfo.Current` major.minor, `entry.type` resolves via reflection to a type implementing `IExtension`, and `smart` declares `provides: ["disk-temperature"]` and `place_after: "disk"` (the placement/kept-loaded behaviour depends on them).
- Sample: packed with the real sample dll, loaded through `ExtensionHost` with an approval in an `ApprovalStore` over a temp file (no-op ACL/secure delegates as in `ExtensionHostTests`) -> `Loaded`, `CollectViews("th")` and `("en")` both return a view; without approval -> `NotApproved`.
Installer (temp dir, fake `PayloadFile`s):
- Writes `extensions\smart\...`, `extension.json` written last (assert via a payload ordering/fault-injection or by file times only if deterministic; otherwise assert the order the helper opens streams).
- Stale unlisted file in `extensions\smart` removed, subfolder kept, sibling `extensions\sample` untouched.
- Hostile payload names (`..\x`, `a/b`, absolute path, uppercase id, empty, duplicate case-insensitive) rejected before anything is written.
- Junction at `extensions` or `extensions\smart` -> refused (use `TestJunctionHelper` as `InstallerJunctionTests` does; skip safely if junctions cannot be created, as there).
- Uninstall helper removes owned extension files and empty folders only; third-party folder and its parents survive; legacy root `DiskInfoCore.dll` is in `OwnedPaths` and `IsSafeTarget` accepts it.
- Update the existing assertions that expect `DiskInfoCore.dll` directly in the install dir (`SmartFallbackTests` ~253, `InstallerSafetyTests`, `InstallerJunctionTests` as needed) to the new rules; keep their intent.
- The embedded resource reader: payload-less test assembly returns an empty list (no payload present in the test run is acceptable; do not depend on `payload\` existing).

## Not in this phase
Docs (`ARCHITECTURE`, ADR, `EXTENSION_CONTRACT.md`) are phase 5. No change to `ExtensionLoader`, `ExtensionHost`, the contract, or DiskInfoCore.dll. No signing, no zip packaging of the sample (a folder is enough). No auto-update.

## Definition of done
Everything above implemented with real APIs, files listed, what was not done and any assumption stated. Do not claim tests passed (you cannot run them).
