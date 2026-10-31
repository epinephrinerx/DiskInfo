# Handoff: R-016 phase 2, DiskInfoKit.dll + C header + API doc

From: Coding Owner (Claude Code, Opus 5.5 High) → Writing Owner (agy, Gemini-3.8 Flash Medium; fallback Codex GPT-6.1-Lun High)
Reviewers: Claude Opus 5.5 High · Codex GPT-6.1-Sol High · Integrator: Claude Fable 5.1 High
Requirement: R-016 (🔨) in `Requirements.md` · Date: 2026-10-31
Repo: `D:\Dev\Apps\DiskInfo`, branch `main` (from `d4dc73c`; this repo works on `main` only)

## Objective
Add a second DLL, `DiskInfoKit.dll`, built on the shared core `DiskInfoCoreLib` (phase 1). It exposes what the CrystalDiskInfo main window shows for each disk plus the SMART attribute table, through a **new, portable C API** (version 1.0) that a Linux/macOS build (R-018) can implement from the same header. `DiskInfoCore.dll` and its ABI v1 are not touched.

## Hard rules
- Only read/search/edit/create tools. Never run shell, builds, tests or git. The Coding Owner builds and tests and sends you the errors.
- Do not modify `CrystalDiskInfo/`, `DiskInfoCore/` (any file) or `docs/CORE_ABI.md`.
- The public header must be **plain C99**: `<stdint.h>`/`<stddef.h>` only, no Win32 types (`DWORD`, `BOOL`, `WCHAR`, `HANDLE`), no MFC, no C++. All text is UTF-8.
- Values and health come from `CAtaSmart` (Crystal's logic). Do not invent new interpretation. Where a value needs Crystal GUI logic that is not in `CAtaSmart` (see "Attribute status" below), port that logic **literally** and cite the source file/lines in a comment.
- Verify every member you use by reading `CrystalDiskInfo/AtaSmart.h` / `.cpp`. Do not invent members.
- No new packages, no disk writes, read-only IOCTLs only (`CAtaSmart` already guarantees this), no elevation.

## Work

### 1. Shared core additions (`DiskInfoCoreLib/engine.h/.cpp`)
Logic that the future TUI (R-019, links the lib directly) also needs goes here, not in the DLL:
- `int dicore::AttributeStatus(const CAtaSmart& ata, int disk, int attr)` → `0` unknown · `1` good · `2` caution · `3` bad. A literal port of the icon decision in `CrystalDiskInfo/DiskInfoDlgUpdate.cpp` `CDiskInfoDlg::UpdateListCtrl` (from ~line 283): NVMe branch, the ATA branch with `IsSmartCorrect && IsThresholdCorrect && !IsThresholdBug`, the 0x05/0xC5/0xC6 raw-vs-`Threshold05/C5/C6` caution rule, the `CurrentValue < ThresholdValue` bad rule, the `m_bGreenMode` offset is **not** part of the result (GUI colour only), and every other branch that function has. Map `ICON_GOOD/CAUTION/BAD/UNKNOWN` to 1/2/3/0. Read the whole function; if a branch depends on GUI-only state (other than green mode), stop and report it instead of guessing.
- Keep `engine.h` self-contained (phase 1 rule) and add no `CWinApp`.

### 2. New project `DiskInfoKit/`
- `DiskInfoKit.vcxproj`: DynamicLibrary, `UseOfMfc` Static, Unicode, `PlatformToolset` v142 (overridden on the command line). Configurations `Debug|Win32`, `Release|Win32`, `Debug|x64`, `Release|x64`. Copy compiler/linker settings, `ProgramDataBaseFileName` pattern and the static-MFC/CRT notes from `DiskInfoCore/DiskInfoCore.vcxproj`. `OutDir` `$(MSBuildProjectDirectory)\Build\$(Configuration)$(Platform)\`, `IntDir` `...\Build\obj\$(Configuration)$(Platform)\`. `ProjectReference` to `..\DiskInfoCoreLib\DiskInfoCoreLib.vcxproj` with `LinkLibraryDependencies` true. `IncludePath` adds `..\DiskInfoCoreLib`, `..\CrystalDiskInfo`, `..\CrystalDiskInfo\Priscilla` (as DiskInfoCore does) and `include`. New unique `ProjectGuid`.
- `.gitignore` with `Build/`.
- `DiskInfoKit.def`: exports exactly the functions below.
- `include/diskinfokit.h`: the public header (below).
- `kit_api.cpp`: its own `CWinApp` object (as `core_api.cpp` has), the handle, the snapshot copy from `ata.vars`, the exports. Same COM/`CAtaSmart` ordering as `di_scan`: `dicore::ComScope com;` → usable check → `CAtaSmart ata;` → `dicore::Scan(ata, useWmi)` → copy everything (including attribute status) → swap into the handle → destructors. Same `catch (CException* e) { e->Delete(); }` / `catch (...)` → `DIK_E_IO`. Same "empty and not elevated → `DIK_E_ACCESS`" rule. Reading after `dik_scan` returns works from the snapshot only (no `CAtaSmart` kept alive, no COM needed).

### 3. Public API (`include/diskinfokit.h`), version 1.0
Prefix `dik_` / `DIK_`. Keep the rules of `docs/CORE_ABI.md`: `struct_size` first in every out-struct (caller sets it; the DLL rejects `< ` its v1 size with `DIK_E_STRUCT_SIZE` and writes only its own size), fixed UTF-8 buffers always NUL-terminated and cut on a character boundary (`dicore::CopyUtf8`), sentinel instead of 0 for "no value", no exceptions across the boundary, one thread per handle, scans serialised process-wide (same comment as `core_api.h`).

```c
#define DIK_API_MAJOR 1
#define DIK_API_MINOR 0
#define DIK_NA   ((int32_t)INT32_MIN)
#define DIK_NA64 ((int64_t)INT64_MIN)
/* errors: same values as DI_* */
#define DIK_OK 0
#define DIK_E_ARG (-1)
#define DIK_E_ACCESS (-2)
#define DIK_E_NOT_FOUND (-3)
#define DIK_E_IO (-4)
#define DIK_E_UNSUPPORTED (-5)
#define DIK_E_STRUCT_SIZE (-6)
#define DIK_OPEN_NO_WMI 0x00000001u

/* interface: same numbering as DI_IF_*; media, health: same as DI_MEDIA_*, DI_HEALTH_* */
/* features bitmask */
#define DIK_FEAT_SMART      0x0001u  /* IsSmartSupported */
#define DIK_FEAT_APM        0x0002u
#define DIK_FEAT_AAM        0x0004u
#define DIK_FEAT_NCQ        0x0008u
#define DIK_FEAT_TRIM       0x0010u
#define DIK_FEAT_DEVSLEEP   0x0020u
#define DIK_FEAT_STREAMING  0x0040u
#define DIK_FEAT_GPL        0x0080u
#define DIK_FEAT_VWC        0x0100u  /* IsVolatileWriteCachePresent */
#define DIK_FEAT_LBA48      0x0200u
#define DIK_FEAT_NVCACHE    0x0400u
#define DIK_FEAT_APM_ON     0x0800u  /* IsApmEnabled */
#define DIK_FEAT_AAM_ON     0x1000u
/* unit of host_reads/host_writes/nand_writes: CAtaSmart HOST_READS_WRITES_UNIT / NAND_WRITES_UNIT numbering */
#define DIK_UNIT_UNKNOWN 0
#define DIK_UNIT_512B 1
#define DIK_UNIT_1MB 2
#define DIK_UNIT_16MB 3
#define DIK_UNIT_32MB 4
#define DIK_UNIT_GB 5

typedef struct dik_handle dik_handle;

typedef struct {
  uint32_t struct_size;
  int32_t  physical_drive_id;   /* N of \\.\PhysicalDriveN on Windows; backend-defined index elsewhere */
  char     model[96];
  char     serial[48];
  char     firmware[24];
  char     interface_name[32];  /* CAtaSmart::Interface, e.g. "Serial ATA", "NVM Express" */
  char     transfer_current[32];/* CurrentTransferMode */
  char     transfer_max[32];    /* MaxTransferMode */
  char     standard[64];        /* MajorVersion (+ " | " + MinorVersion if not empty), as the GUI shows it */
  char     drive_letters[32];   /* DriveMap; empty off Windows */
  char     smart_key[32];       /* SmartKeyName, e.g. "Smart", "SmartNVMe": selects the attribute-name table */
  int32_t  interface_kind;      /* DI_IF_* numbering */
  int32_t  media;               /* DI_MEDIA_* numbering */
  int32_t  health;              /* DiskStatus, DI_HEALTH_* numbering */
  int32_t  temperature_c;       /* DIK_NA if none */
  int32_t  life_percent;        /* DIK_NA if none */
  int32_t  rotation_rpm;        /* NominalMediaRotationRate if > 1; 1 means SSD → DIK_NA; 0 → DIK_NA */
  int64_t  power_on_hours;      /* DIK_NA64 if none */
  int64_t  power_on_count;      /* DIK_NA64 if not reported */
  int64_t  capacity_bytes;      /* from the same field the GUI capacity uses; DIK_NA64 if unknown */
  int64_t  host_reads;          /* raw counter in host_rw_unit; DIK_NA64 if not reported */
  int64_t  host_writes;
  int64_t  nand_writes;         /* in nand_unit; DIK_NA64 if not reported */
  int32_t  host_rw_unit;        /* DIK_UNIT_* */
  int32_t  nand_unit;           /* DIK_UNIT_* */
  uint32_t features;            /* DIK_FEAT_* */
  uint32_t access;              /* same as DI_ACCESS_* */
  uint32_t source;              /* CommandType value */
  int32_t  attribute_count;     /* rows dik_attribute can return */
} dik_disk_t;

typedef struct {
  uint32_t struct_size;
  uint8_t  id;
  uint8_t  raw_len;             /* 6, 7 or 8 bytes valid in raw[] (IsRawValues7/8; NVMe as Crystal stores it) */
  uint8_t  reserved[2];
  int32_t  current;             /* DIK_NA when not meaningful (NVMe) */
  int32_t  worst;               /* DIK_NA when not meaningful */
  int32_t  threshold;           /* DIK_NA when not available */
  uint8_t  raw[8];              /* little-endian bytes as Crystal stores them */
  uint64_t raw_value;           /* raw[0..raw_len-1] as a little-endian integer */
  int32_t  status;              /* dicore::AttributeStatus: 0 unknown 1 good 2 caution 3 bad */
} dik_attribute_t;

uint32_t dik_api_version(void);                           /* major << 16 | minor */
int  dik_open(dik_handle** out, uint32_t flags);
void dik_close(dik_handle* h);
int  dik_scan(dik_handle* h);                             /* slow; see threading comment */
int  dik_disk_count(const dik_handle* h);
int  dik_disk(const dik_handle* h, int disk, dik_disk_t* out);
int  dik_attribute(const dik_handle* h, int disk, int row, dik_attribute_t* out);
```
Rules for the copy:
- Attribute rows: the same rows the GUI lists, in the same order (skip `Id == 0x00` and `Id == 0xFF` as `UpdateListCtrl` does). `attribute_count` is the number of rows after skipping. `current/worst/threshold` for ATA come from `Attribute[j]` / `Threshold[j]` exactly as the GUI columns do; check what the GUI shows for NVMe and for disks without correct thresholds and mirror it with `DIK_NA`.
- `temperature_c`, `life_percent`, `power_on_hours`: same sentinel mapping as `core_api.cpp` (`Temperature`, `Life`, `Hours` helpers). Factor nothing out of `DiskInfoCore`; duplicate the three small helpers in `kit_api.cpp`.
- `power_on_count`, host/NAND counters: find how `CAtaSmart` marks "not reported" (e.g. `-1` or 0 with unit unknown) and how the GUI decides to hide them; mirror that.
- `capacity_bytes`: find the field and unit the GUI capacity string is built from (`DiskInfoDlgUpdate.cpp`) and convert to bytes; say which in the summary.
- `standard`: mirror how the GUI builds `m_AtaAtapi` (~line 1555).
- Names of attributes are **not** in this API: Crystal reads them from its language files (`[<SmartKeyName>] <id hex>=name`), which are not in this repo. `smart_key` + `id` is the lookup key; the binding/app (phase 3, R-017) owns the name table.

### 4. API doc `docs/DISKINFOKIT_API.md`
Short and exact: purpose, the rules above, every function (arguments, return codes, threading), every struct field with its source member in `CAtaSmart`, the sentinel/unit conventions, name lookup via `smart_key`+`id`, versioning rule (append-only struct fields, minor bump on additions). Thai or English is fine; match `docs/CORE_ABI.md` style.

## Acceptance criteria (this phase)
- [ ] `MSBuild DiskInfoKit\DiskInfoKit.vcxproj` builds `Release|x64`, `Debug|x64`, `Release|Win32`, 0 errors; DLL at `DiskInfoKit\Build\<cfg><plat>\DiskInfoKit.dll`.
- [ ] `dumpbin /exports` lists exactly the 7 `dik_*` functions.
- [ ] `include/diskinfokit.h` compiles as C (`cl /TC /c` of a file that includes only it) with no Windows headers.
- [ ] Non-admin probe: `api=0x10000 open=0 scan=-2 count=0`, short struct → `-6`, bad index → `-3`, NULL out → `-1`.
- [ ] Admin probe (user clicks UAC): same 3 disks as `DiskInfoCore.dll` in the same session with matching model/serial/firmware/health/temperature (±2 °C)/life/hours; attribute rows and per-row status match what CrystalDiskInfo's GUI shows for the same disks (spot-check by the user if they run CrystalDiskInfo).
- [ ] `DiskInfoCore.dll` rebuild: exports, size and non-admin probe unchanged from phase 1; `CrystalDiskInfo/`, `DiskInfoCore/` unchanged.

## Out of scope
.NET binding and tests (phase 3), attribute name table, Linux/macOS backends, any change to `DiskInfoCore.dll`.

## Summary to return
Files created/changed; the final header; where each `dik_disk_t` field comes from (member + any GUI logic copied, with file:line); the ported `AttributeStatus` branches with their source lines; anything you could not verify by reading source.

## Fix round 1 (2026-10-31, after Reviewer1 Opus 5.5 + Reviewer2 Codex GPT-6.1-Sol)
Build (Release/Debug x64, Release Win32), 7 exports, C compile of the header, non-admin probe and the unchanged `DiskInfoCore.dll` all passed. Both reviewers confirmed the `AttributeStatus` branch port and the row/`j` mapping. Fix these, nothing else (the API is unreleased, so break it freely; keep `DIK_API_MINOR 0`):
1. **Counters are already GB, not raw units (bug, both reviewers).** `CAtaSmart` converts `HostReads/HostWrites/NandWrites` to GB (2^30 bytes) before storing (`AtaSmart.cpp:4063-4064` NVMe, `4557-4590` ATA 0xF1, `4493-4495` NAND) and the GUI prints them as `"%d GB"`. Rename the fields to `host_reads_gb`, `host_writes_gb`, `nand_writes_gb` (same sentinel rule), delete `host_rw_unit`, `nand_unit` and all `DIK_UNIT_*`. Fix the header comments and `docs/DISKINFOKIT_API.md`. (The handoff wording above was wrong; this round supersedes it.)
2. **Capacity is GUI-rounded decimal MB, not bytes (risk, Reviewer1).** `TotalDiskSize` = sectors × size / 10^6 − 49 on ATA/WMI (`AtaSmart.cpp:3194, 3204, 1831`) and without −49 on NVMe (`3956, 3968`). Replace `capacity_bytes` with two fields: `capacity_mb` (`TotalDiskSize` as is, the number the GUI prints; `DIK_NA64` if 0) and `capacity_bytes` = `NumberOfSectors * LogicalSectorSize` **only if** reading the source shows both are set on the ATA and NVMe paths (else `DIK_NA64` for the paths where they are not, and say which in the summary). Document both precisely.
3. **Calling convention (risk, Reviewer1).** Add `#if defined(_WIN32) #define DIK_CALL __cdecl #else #define DIK_CALL #endif` and use it on all 7 prototypes and definitions; state "cdecl" in the header comment and the doc (a 32-bit P/Invoke must set `CallingConvention.Cdecl`).
4. **Explicit layout (risk, both).** No implicit padding in either struct on any ABI: in `dik_attribute_t` add explicit `uint8_t reserved2[4]` before `raw_value` and an explicit tail pad after `status` so sizeof is 48 everywhere (or reorder to the same effect). Recompute `dik_disk_t` after fix 1/2 and pad explicitly to a multiple of 8. In `kit_api.cpp` add `static_assert`s on `sizeof` and `offsetof` of every field of both structs, and list the offsets in the doc.
5. **Header comments (both).** Replace "see threading comment" with the real rules (adapt `core_api.h:82-86`: slow, never on a UI thread, initialises COM on the calling thread on Windows, one thread per handle, serialise scans across the whole process, returns `DIK_E_ACCESS` when not elevated and no disk was found) and the `struct_size` rule (caller sets it to its sizeof, must be ≥ the v1 size). Replace references to `DI_IF_*`/`DI_MEDIA_*`/`DI_HEALTH_*`/`DI_ACCESS_*` with defined `DIK_*` constants (define them in this header, same values).
6. **Literal port (nit, both).** In `dicore::AttributeStatus` remove the 60/50 `AlarmTemperature` fallback and the added `AttributeCount > 1/> 3` guards so the expressions are exactly the GUI's (`Attribute[]` is a fixed zero-initialised array of `MAX_ATTRIBUTE`; `Scan` already sets `AlarmTemperature`). Add one line to the `engine.h` comment: callers must run `dicore::Scan` first (it sets `AlarmTemperature` and the caution thresholds). In `kit_api.cpp` the NVMe value-layout check must be the GUI's `DiskVendorId == SSD_VENDOR_NVME` only (`DiskInfoDlgUpdate.cpp:919`), drop `|| a.IsNVMe`.
Return: the final header, the final offsets table, and the source lines you used for fix 2.
