# Handoff: R-016 phase 3, .NET binding `DiskInfo.Kit` + tests

From: Coding Owner (Claude Code, Opus 5.5 High) → Writing Owner (agy, Gemini-3.8 Flash Medium; fallback Codex GPT-6.1-Lun High)
Reviewers: Claude Opus 5.5 High · Codex GPT-6.1-Sol High · Integrator: Claude Fable 5.1 High
Requirement: R-016 (🔨) in `Requirements.md` · Date: 2026-10-31
Repo: `D:\Dev\Apps\DiskInfo`, branch `main` (from `c414087`; this repo works on `main` only)

## Objective
A thin managed library, `DiskInfo.Kit`, that wraps the DiskInfoKit C API 1.0 (`DiskInfoKit/include/diskinfokit.h`, contract `docs/DISKINFOKIT_API.md`) for the Avalonia app (R-017), plus an MSTest project. It also owns the SMART attribute **name table** (the C API has no names; key = `smart_key` + `id`).

## Hard rules
- Only read/search/edit/create tools. Never run shell, builds, tests, `dotnet` or git. The Coding Owner builds and tests and sends you the errors.
- Do not modify anything under `CrystalDiskInfo/`, `DiskInfoCore/`, `DiskInfoCoreLib/`, `DiskInfoKit/` or `docs/CORE_ABI.md`. The C API is frozen; the binding follows it.
- Read `DiskInfoKit/include/diskinfokit.h` and `docs/DISKINFOKIT_API.md` fully before writing. Every offset, constant and rule must come from there. Do not invent fields.
- Packages: only `Microsoft.NET.Test.Sdk` 17.14.1, `MSTest.TestAdapter` 3.1.1, `MSTest.TestFramework` 3.1.1 in the test project (already in the local NuGet cache). The library itself has **no** package references.
- Target `net10.0` (not `-windows`: the same binding must load `libDiskInfoKit.so`/`.dylib` for R-018). `Nullable` enable, `ImplicitUsings` enable, `AllowUnsafeBlocks` true in the library.
- Tests must not need administrator rights, must not write outside their temp/output directory, and must pass whether or not the process is elevated.

## Work

### 1. `DiskInfo.Kit/DiskInfo.Kit.csproj` (class library, namespace `DiskInfo.Kit`)
- `.gitignore` in each new project folder: `bin/` and `obj/`.
- `InternalsVisibleTo` `DiskInfo.Kit.Tests` (via `<InternalsVisibleTo Include="DiskInfo.Kit.Tests" />`).

### 2. Native layer (internal)
- `Native/NativeDisk.cs`, `Native/NativeAttribute.cs`: `[StructLayout(LayoutKind.Explicit, Size = 496)]` / `Size = 48` unsafe structs, one `[FieldOffset]` per field exactly as the offset tables in `docs/DISKINFOKIT_API.md`, `fixed byte` for char/byte arrays and reserved fields. Blittable only (no `MarshalAs`, no managed arrays).
- `Native/KitNative.cs`: source-generated `[LibraryImport("DiskInfoKit")]` for all 7 functions, each with `[UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]` and `[DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.System32)]` (load only from the binding's own folder or System32, never CWD/PATH). `dik_disk`/`dik_attribute` take `NativeDisk*`/`NativeAttribute*` or `ref`.
- `Native/KitSafeHandle.cs`: `SafeHandleZeroOrMinusOneIsInvalid`, `ReleaseHandle` → `dik_close`. `dik_open` returns it through an `out` parameter.
- Constants mirroring the header (`DIK_NA`, `DIK_NA64`, API major/minor, error codes) in one internal static class.

### 3. Conversion (internal, pure, no P/Invoke) — `Native/NativeConvert.cs`
- `static string Utf8(ReadOnlySpan<byte> buffer)`: bytes up to the first NUL (whole buffer if none), decoded with `Encoding.UTF8` (invalid sequences → U+FFFD, never throw).
- `static DiskSnapshot ToDisk(in NativeDisk d, IReadOnlyList<SmartAttribute> attributes)` and `static SmartAttribute ToAttribute(in NativeAttribute a, string smartKey)`.
- `DIK_NA` / `DIK_NA64` → `null`. Every other value passes through unchanged (no unit conversion, no clamping beyond what is stated here).
- Enum fields: a value that is not a defined member maps to `Unknown` (or for `AccessLevel`, keep the raw cast). `features` → `[Flags] DiskFeatures` cast as is.
- `raw_len` outside 1..8 → clamp into 0..8 for slicing `raw` (never read past `raw[8]`); `Raw` exposes exactly that many bytes. `RawValue` copies `raw_value` unchanged.

### 4. Public API
```csharp
public enum KitError { Ok = 0, Argument = -1, Access = -2, NotFound = -3, Io = -4, Unsupported = -5, StructSize = -6 }
public sealed class DiskInfoKitException : Exception { public KitError Error { get; } public int NativeCode { get; } }
public enum InterfaceKind { Unknown, Pata, Sata, Usb, Ieee1394, Scsi, Nvme, Other }   // DIK_IF_* values
public enum MediaType { Unknown, Hdd, Ssd }                                            // DIK_MEDIA_*
public enum HealthStatus { Unknown, Good, Caution, Bad }                               // DIK_HEALTH_*
public enum AttributeStatus { Unknown, Good, Caution, Bad }                            // DIK_ATTR_STATUS_*
public enum AccessLevel : uint { Full = 0, Partial = 1 }                               // DIK_ACCESS_*
[Flags] public enum DiskFeatures : uint { None = 0, Smart = 0x1, Apm = 0x2, Aam = 0x4, Ncq = 0x8, Trim = 0x10, DevSleep = 0x20, Streaming = 0x40, Gpl = 0x80, VolatileWriteCache = 0x100, Lba48 = 0x200, NvCache = 0x400, ApmEnabled = 0x800, AamEnabled = 0x1000 }
[Flags] public enum KitOpenFlags : uint { None = 0, NoWmi = 0x1 }

public sealed record SmartAttribute(byte Id, string? Name, bool IsGenericName, int? Current, int? Worst, int? Threshold,
    IReadOnlyList<byte> Raw, ulong RawValue, AttributeStatus Status);

public sealed record DiskSnapshot(int PhysicalDriveId, string Model, string Serial, string Firmware, string InterfaceName,
    string TransferCurrent, string TransferMax, string Standard, string DriveLetters, string SmartKey,
    InterfaceKind Interface, MediaType Media, HealthStatus Health, int? TemperatureC, int? LifePercent, int? RotationRpm,
    long? PowerOnHours, long? PowerOnCount, long? CapacityMb, long? CapacityBytes,
    long? HostReadsGb, long? HostWritesGb, long? NandWritesGb, DiskFeatures Features, AccessLevel Access, uint Source,
    IReadOnlyList<SmartAttribute> Attributes);

public sealed class DiskInfoKitSession : IDisposable
{
    public static Version ApiVersion { get; }                 // from dik_api_version: major.minor
    public static bool TryGetApiVersion(out Version? version);// false when the native library or export is missing (DllNotFoundException / EntryPointNotFoundException), never throws for that
    public static DiskInfoKitSession Open(KitOpenFlags flags = KitOpenFlags.None); // throws DiskInfoKitException(Unsupported) if major != 1, or the dik_open error
    public IReadOnlyList<DiskSnapshot> Scan();                // blocking (seconds); dik_scan, then reads every disk and every attribute row into immutable snapshots
    public Task<IReadOnlyList<DiskSnapshot>> ScanAsync();     // runs Scan on a dedicated thread (TaskCreationOptions.LongRunning), never on the caller's thread
    public void Dispose();                                    // idempotent; Scan after Dispose → ObjectDisposedException
}
```
- Any non-zero native return → `DiskInfoKitException` with the matching `KitError` (unknown codes keep `NativeCode`, `Error` = `Io`) and an English message naming the function. `Access` message says administrator rights are needed.
- Threading (doc + header say the caller must serialise): one **process-wide** static lock held for the whole of `Scan` (scan + reading all rows), and `Dispose` takes the same per-session state safely so it cannot close the handle while a scan on it is running.
- `struct_size` is set to `sizeof` before every `dik_disk`/`dik_attribute` call; structs are zero-initialised first.
- XML doc comments on every public member, including: Windows host exe needs the Windows 10 manifest (else NVMe disks are missing; .NET apphost already has it), units (`*Gb` = GB of 2^30 bytes, `CapacityMb` decimal MB as the GUI prints, `CapacityBytes` null on NVMe), and that `Name` is English.

### 5. Attribute names — `AttributeNames.cs`
`public static AttributeName? Lookup(string? smartKey, byte id)` returning `public readonly record struct AttributeName(string Text, bool IsGeneric)`; `SmartAttribute.Name`/`IsGenericName` are filled from it.
- `"SmartNVMe"` table, IDs as `CrystalDiskInfo/NVMeInterpreter.cpp` assigns them (read it): 01 Critical Warning · 02 Composite Temperature · 03 Available Spare · 04 Available Spare Threshold · 05 Percentage Used · 06 Data Units Read · 07 Data Units Written · 08 Host Read Commands · 09 Host Write Commands · 0A Controller Busy Time · 0B Power Cycles · 0C Power On Hours · 0D Unsafe Shutdowns · 0E Media and Data Integrity Errors · 0F Number of Error Information Log Entries · 10 Warning Composite Temperature Time · 11 Critical Composite Temperature Time · 12–19 Temperature Sensor 1–8 · 1A Thermal Management Temperature 1 Transition Count · 1B Thermal Management Temperature 2 Transition Count · 1C Total Time For Thermal Management Temperature 1 · 1D Total Time For Thermal Management Temperature 2.
- `"Smart"` (generic ATA/HDD) table: 01 Read Error Rate · 02 Throughput Performance · 03 Spin-Up Time · 04 Start/Stop Count · 05 Reallocated Sectors Count · 06 Read Channel Margin · 07 Seek Error Rate · 08 Seek Time Performance · 09 Power-On Hours · 0A Spin Retry Count · 0B Recalibration Retries · 0C Power Cycle Count · 0D Soft Read Error Rate · 16 Current Helium Level · B7 SATA Downshift Error Count · B8 End-to-End Error · BB Reported Uncorrectable Errors · BC Command Timeout · BD High Fly Writes · BE Airflow Temperature · BF G-Sense Error Rate · C0 Power-off Retract Count · C1 Load/Unload Cycle Count · C2 Temperature · C3 Hardware ECC Recovered · C4 Reallocation Event Count · C5 Current Pending Sector Count · C6 Uncorrectable Sector Count · C7 UltraDMA CRC Error Count · C8 Write Error Rate · CA Data Address Mark Errors · CB Run Out Cancel · CC Soft ECC Correction · CD Thermal Asperity Rate · CE Flying Height · CF Spin High Current · D0 Spin Buzz · D1 Offline Seek Performance · DC Disk Shift · DD G-Sense Error Rate · DE Loaded Hours · DF Load/Unload Retry Count · E0 Load Friction · E1 Load/Unload Cycle Count · E2 Load 'In'-time · E3 Torque Amplification Count · E4 Power-Off Retract Cycle · E6 GMR Head Amplitude · F0 Head Flying Hours · F1 Total LBAs Written · F2 Total LBAs Read · FA Read Error Retry Rate · FE Free Fall Protection.
- `"Smart"` and `"SmartNVMe"` hits → `IsGeneric = false`. Any **other** key (the ~48 vendor keys such as `SmartSsd`, `SmartSandForce`; Crystal's language files that hold their names are not in this repo) → look up the `"Smart"` table and return it with `IsGeneric = true`, because vendor tables may name an ID differently. NVMe IDs never fall back. Unknown ID, `null` or empty key → `null` (the app shows "Vendor Specific", as Crystal does).
- Key comparison is ordinal (case-sensitive), the names are written by us from the ATA/NVMe specs (do not copy Crystal language files), English only. Comment at the top states this and that vendor tables are future work.

### 6. `DiskInfo.Kit.Tests/DiskInfo.Kit.Tests.csproj` (MSTest, `net10.0`)
- `ProjectReference` to the library. Copy the native DLL for the integration tests: `<None Include="..\DiskInfoKit\Build\Releasex64\DiskInfoKit.dll" Link="DiskInfoKit.dll" CopyToOutputDirectory="PreserveNewest" Visible="false" Condition="Exists('..\DiskInfoKit\Build\Releasex64\DiskInfoKit.dll')" />`.
- **LayoutTests**: `sizeof`/`Unsafe.SizeOf` = 496 / 48; `Marshal.OffsetOf` of **every** field equals the doc table (28 disk fields, 12 attribute fields).
- **HeaderContractTests**: find `DiskInfoKit/include/diskinfokit.h` by walking up from `AppContext.BaseDirectory`; parse its `#define DIK_*` integer constants and assert every one matches the managed enum/constant (`KitError`, `InterfaceKind`, `MediaType`, `HealthStatus`, `AttributeStatus`, `AccessLevel`, `DiskFeatures`, `KitOpenFlags`, API major/minor). Fail (not skip) if the header is not found.
- **ConvertTests** (synthetic structs built in the test, no DLL): every sentinel → null; ASCII string; buffer completely full with no NUL; Thai UTF-8 text; invalid UTF-8 → no throw; undefined enum value → `Unknown`; features flags; `raw_len` 6/7/8/0/200; NA current/worst/threshold → null; status mapping.
- **AttributeNamesTests**: Smart 0x05, SmartNVMe 0x02 / 0x12 / 0x1D, vendor key fallback with `IsGeneric`, NVMe unknown ID → null, unknown key + unknown ID → null, null/empty key → null, case-sensitive key.
- **ErrorTests**: each native code → `KitError` and exception; unknown code.
- **NativeTests** (real DLL; `Assert.Inconclusive` with a clear message when `DiskInfoKit.dll` is not next to the test assembly): `ApiVersion` = 1.0, `TryGetApiVersion` true; open/dispose; double dispose; `Scan` after dispose → `ObjectDisposedException`; when **not** elevated (`Environment.IsPrivilegedProcess == false`) `Scan` and `ScanAsync` throw `DiskInfoKitException` with `Access`; when elevated every disk has `Attributes.Count` rows, a non-empty `Model` and `SmartKey`; two sessions scanning in parallel finish without deadlock (bounded wait, e.g. 120 s).

### 7. Docs
Add a short section "## .NET binding (`DiskInfo.Kit`)" at the end of `docs/DISKINFOKIT_API.md`: where it is, the type map (sentinel → null, enums, names table and `IsGeneric`), threading/`ScanAsync`, native DLL lookup (binding folder or System32), manifest note. Do not edit other docs; the Coding Owner updates ARCHITECTURE/TESTING/Requirements.

## Deliverable
List every file you created or changed and anything you could not decide (stop and report rather than guess).

## Fix round 1 (after Reviewer1 Opus 5.5 + Reviewer2 Codex GPT-6.1-Sol)
Same Hard rules. Edit only `DiskInfo.Kit/`, `DiskInfo.Kit.Tests/` and the ".NET binding" section of `docs/DISKINFOKIT_API.md` (the Coding Owner already fixed rule 8 above it; do not touch it).
1. **Immutable snapshots**: `SmartAttribute.Raw` and `DiskSnapshot.Attributes` must not be castable back to a mutable array/list (`Array.AsReadOnly` / `ReadOnlyCollection<T>` over a private copy). Add a test that `Raw` is not a `byte[]` and `Attributes` is not a `List<>`/array.
2. **Tautology** `NativeTests.cs:89`: in the elevated branch compare each managed disk's `Attributes.Count` with the native `attribute_count` read through the internal `KitNative.dik_disk` on the same session (add an internal accessor if needed). Do **not** require a non-empty `SmartKey` or any attributes: disks without readable SMART (USB sticks, RAID volumes) legitimately have an empty key and 0 rows. Keep only "Model non-empty". Also exercise `ScanAsync` in the elevated branch.
3. **Deadlock test** `NativeTests.cs:126`: on timeout it must fail without blocking: do not dispose sessions whose scan has not finished (no `using` for them; dispose only after both tasks completed).
4. **NVMe no-fallback test**: use `Lookup("SmartNVMe", 0xC5)` → `null` (0xC5 exists in the `Smart` table).
5. **Header contract**: also assert the internal `KitConstants` values the code switches on (`DIK_E_*`, `DIK_IF_*`, `DIK_MEDIA_*`, `DIK_HEALTH_*`, `DIK_ATTR_STATUS_*`, `DIK_ACCESS_*`, `DIK_FEAT_*`, `DIK_OPEN_NO_WMI`, `DIK_NA*`, API major/minor) against the parsed header, in addition to the public enums.
6. **Conversion coverage**: ErrorTests/status tests must use literal header numbers (-1..-6, 0..3), not `KitConstants`. Add tests mapping every **defined** `interface_kind` (0..7), `media` (0..2) and `health` (0..3) value, and a `ToDisk` test that fills every text field to its full length with distinct content (model 96, serial 48, firmware 24, interface_name 32, transfer_current 32, transfer_max 32, standard 64, drive_letters 32, smart_key 32) and checks each string, so a wrong span length or offset fails.
7. **Architecture mismatch**: `TryGetApiVersion` also returns false on `BadImageFormatException`. `NativeTests` is Inconclusive (not failed) when `RuntimeInformation.ProcessArchitecture != Architecture.X64` because only the x64 DLL is copied.
8. **Messages/docs**: the `Open` version-mismatch message names `dik_api_version`. XML doc of `Dispose` (and the doc section) states that it blocks until a scan running on the same session finishes. Doc section corrections: unknown values map to `Unknown` only for `InterfaceKind`/`MediaType`/`HealthStatus`/`AttributeStatus`; `AccessLevel` and `DiskFeatures` keep the raw value; unknown native error codes → `KitError.Io` with `NativeCode` kept. Name lookup: `null` only for a null/empty key or an ID absent from the table used; any other non-empty key falls back to `Smart` with `IsGeneric = true`; `SmartNVMe` never falls back. Native lookup: on Windows the binding's folder (and the app's deps.json native folders) then System32; on Linux/macOS the platform loader's normal search (including `LD_LIBRARY_PATH`) still applies after the binding folder — hardening is R-018 work. Add one line: the lock is per AssemblyLoadContext (load the binding once per process).

## Fix round 2 (after round-2 reviews)
Same Hard rules. Edit only `DiskInfo.Kit/` and `DiskInfo.Kit.Tests/` (docs are done).
1. **Elevated count check is circular** (`NativeTests.cs` elevated branch): keep the `attribute_count` comparison but add independent probes on the same session after `Scan`: for each disk `i`, internal `dik_attribute(handle, i, attribute_count, ...)` returns `-3` (NotFound), and `dik_disk(handle, disks.Count, ...)` returns `-3`, proving no row or disk was skipped.
2. **Async scan comparison**: do not compare disk count/models by index between the `Scan` and `ScanAsync` results (topology can change between scans). Validate the `ScanAsync` result against its own native snapshot with the same checks as item 1.
3. **A native hang must not hang the run**: every test that calls `Scan`/`ScanAsync` gets a bounded wait (MSTest `[Timeout(...)]`, e.g. 180000 ms, or an explicit bounded wait), so a stuck scan holding the static lock fails the remaining native tests instead of blocking forever.
4. **`smart_key` over-read**: `ToDisk` uses `NativeConvert.GetSmartKey` (single place with the 32-byte length). In the full-length text test set `interface_kind` to a value whose bytes are non-zero printable ASCII (e.g. `0x41414141`) so an over-long span on `smart_key` would change the result, and assert `GetSmartKey` returns the same 32 characters. (`interface_kind` then maps to `Unknown`; assert that too.)
5. **`ToDisk` always copies** the incoming attributes into a new private array wrapped read-only (no reuse of an incoming `ReadOnlyCollection`); add a test that clearing the caller's list after `ToDisk` does not change `Attributes.Count`.
