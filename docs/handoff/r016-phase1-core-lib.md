# Handoff: R-016 phase 1, shared core static library

From: Coding Owner (Claude Code, Opus 5.5 High) → Writing Owner (agy, Gemini-3.8 Flash Medium; fallback Codex GPT-6.1-Lun High)
Reviewers: Claude Opus 5.5 High · Codex GPT-6.1-Sol High · Integrator: Claude Fable 5.1 High
Requirement: R-016 (🔨) in `Requirements.md` · Date: 2026-10-31
Repo: `D:\Dev\Apps\DiskInfo`, branch `main` (from `4820fd0`; started on `agent/agy-diskinfokit`, moved to `main` before commit per the main-only rule, tree clean except this file and `Requirements.md`)

## Objective
Move the code that drives `CAtaSmart` out of `DiskInfoCore/core_api.cpp` into a new **static library** (the "shared core") so a second DLL (`DiskInfoKit.dll`, phase 2) can reuse it. `DiskInfoCore.dll` must link the shared core and behave **exactly** as today: same 6 exports, same structs, same return codes, same output path. This phase is a pure refactor.

## Hard rules
- Only read/search/edit/create tools. Never run shell, builds, tests or git. The Coding Owner builds and tests everything and sends you the errors.
- Do not modify anything under `CrystalDiskInfo/`. Do not modify `DiskInfoCore/core_api.h` or `DiskInfoCore/DiskInfoCore.def` (byte-for-byte unchanged).
- Do not change `DiskInfoCore.dll`'s `OutDir` (`DiskInfoCore\Build\$(Configuration)$(Platform)\`): SystemMonitor's build script picks the DLL up from `..\DiskInfo\DiskInfoCore\Build\Releasex64\`.
- No new packages, no new runtime behaviour, no disk writes, no elevation.
- Verify every name you use by reading its source (`CrystalDiskInfo/AtaSmart.h`, `core_api.cpp`). Do not invent members.

## Work

### 1. New project `DiskInfoCoreLib/` (repo root, next to `DiskInfoCore/`)
- `DiskInfoCoreLib.vcxproj`: `ConfigurationType` StaticLibrary, `UseOfMfc` Static, `CharacterSet` Unicode, `PlatformToolset` v142 (the Coding Owner overrides it on the command line, as for `DiskInfoCore`).
  Configurations: `Debug|Win32`, `Release|Win32`, `Debug|x64`, `Release|x64` (x86 is needed later for WinPE 32-bit, R-019).
  `OutDir` `$(MSBuildProjectDirectory)\Build\$(Configuration)$(Platform)\`, `IntDir` `...\Build\obj\$(Configuration)$(Platform)\`.
  Same `IncludePath`, `PreprocessorDefinitions` (minus `_USRDLL`; keep `WIN32;_WINDOWS;_AFX_NO_MFC_CONTROLS_IN_DIALOGS`), warning level, `RuntimeLibrary` (MultiThreaded / MultiThreadedDebug), optimisation settings and `ProgramDataBaseFileName` pattern as `DiskInfoCore.vcxproj`. Use a new unique `ProjectGuid`.
- Move these compile items **from** `DiskInfoCore.vcxproj` **to** the lib, unchanged, including the `DebugPrint=CdiOriginalDebugPrint` define on `UtilityFx.cpp` and its comment: `AtaSmart.cpp`, `NVMeInterpreter.cpp`, `SlotSpeedGetter.cpp`, `Priscilla\UtilityFx.cpp`, `Priscilla\OsInfoFx.cpp`.
- `DiskInfoCoreLib/.gitignore` containing `Build/` (same as `DiskInfoCore/.gitignore`).

### 2. `DiskInfoCoreLib/engine.h` + `engine.cpp` (internal C++ interface, not an exported ABI)
Move from `core_api.cpp`, keeping the existing comments (they record why each line exists):
- the no-op `void DebugPrint(CString) {}` and its comment,
- `CopyUtf8`, `IsElevated`, `ComScope`,
- the body of `di_scan` that configures and runs `CAtaSmart`: the bridge/RAID flags, `CsmiType`, the `Init(...)` call with the same arguments, and the per-disk caution thresholds (1/1/1/10) + `CheckDiskStatus`.

Suggested shape (namespace `dicore`; adjust only if the source forces it, and say why):
```cpp
namespace dicore {
  // UTF-8 into a fixed buffer, always NUL-terminated, cut on a character boundary.
  void CopyUtf8(const CString& src, char* dst, size_t cap);
  bool IsElevated();
  // Runs one full scan into `ata` with the GUI's defaults and computes DiskStatus for every disk.
  // Initialises COM on the calling thread for the duration of the call.
  // Returns false only when COM cannot be used on this thread (caller maps it to an I/O error).
  // CException* and other exceptions propagate; callers catch them at their ABI boundary.
  bool Scan(CAtaSmart& ata, bool useWmi);
}
```
Keep the COM scope inside `Scan` so COM stays initialised exactly as long as today (the `ComScope` lived for the whole `di_scan`, including the loop that reads `ata.vars` — make sure the caller's reading of `ata.vars` either happens inside the same COM lifetime or is shown not to need COM; reading `vars` is plain memory, but state your conclusion in the summary).
The lib must **not** define a `CWinApp` object (the DLLs and the future TUI exe each own theirs).

### 3. `DiskInfoCore/` becomes a thin ABI layer
- `core_api.cpp` keeps: `di_handle`, `CDiskInfoCoreApp theApp`, the sentinel helpers (`Temperature`, `Life`, `Hours`), `Fill`, and all six `extern "C"` functions.
- `di_scan` = `CAtaSmart ata;` → `dicore::Scan(ata, !(h->flags & DI_OPEN_NO_WMI))` (false → `DI_E_IO`) → fill `next` from `ata.vars` → `h->disks.swap(next)` → the same `empty && !IsElevated()` → `DI_E_ACCESS` rule → `DI_OK`. Same `catch (CException*)` / `catch (...)` → `DI_E_IO`.
- `DiskInfoCore.vcxproj`: remove the moved compile items, add a `ProjectReference` to `..\DiskInfoCoreLib\DiskInfoCoreLib.vcxproj` (so building the DLL builds the lib for the same configuration/platform) and make sure the lib is linked. Add the lib's folder to `IncludePath` for `engine.h`. Nothing else changes (`.def`, `OutDir`, `TargetName`, link settings).

## Acceptance criteria (this phase)
- [ ] `MSBuild DiskInfoCore\DiskInfoCore.vcxproj /p:Configuration=Release /p:Platform=x64 ...` builds the lib and the DLL, 0 errors, DLL still at `DiskInfoCore\Build\Releasex64\DiskInfoCore.dll`.
- [ ] `MSBuild DiskInfoCoreLib\DiskInfoCoreLib.vcxproj` builds for `Release|Win32` and `Release|x64`.
- [ ] `dumpbin /exports` lists the same 6 names; `core_api.h` and `.def` unchanged (git diff empty).
- [ ] Non-admin probe gives the baseline: `abi=0x10000 open=0 scan=-2 count=0 short_struct=-6 bad_idx=-3`.
- [ ] Admin probe (user clicks UAC): same disks, health, temperature (±2 °C), life, hours, access, source as the baseline DLL run in the same session.
- [ ] SystemMonitor: `dotnet test SysMonitor.Tests` passes and its SMART extension still loads the rebuilt DLL.
- [ ] `CrystalDiskInfo/` untouched (git diff empty).

## Baseline recorded by the Coding Owner (2026-10-31, before any change)
- Build command (the old UCRT workaround in `docs/TESTING.md` is now wrong on this machine: the SDK moved to `D:\Program Files\Windows Kits\10` and the registry points there; build without the workaround):
  `MSBuild.exe DiskInfoCore\DiskInfoCore.vcxproj /p:Configuration=Release /p:Platform=x64 /p:PlatformToolset=v145 /p:WindowsTargetPlatformVersion=10.0.26100.0 /m` → succeeded, `DiskInfoCore.dll` 582,656 bytes, exports `di_abi_version di_close di_disk_count di_disk_info di_open di_scan`.
- Non-admin probe (`scratchpad/probe-v1.ps1`, P/Invoke on the copied baseline DLL): `abi=0x10000 open=0 scan=-2 (3 ms) count=0 short_struct=-6 bad_idx=-3`.

## Open questions / known risks
- Static MFC in a static lib linked into a static-MFC DLL: `_AFX_NO_MFC_CONTROLS_IN_DIALOGS` and the CRT choice must match across both projects or two MFC libs get linked (see the comment in `DiskInfoCore.vcxproj`). Report any LNK2005/LNK4098 instead of working around it with `/FORCE` or `/NODEFAULTLIB`.
- `DebugPrint` override: the no-op must still win over the renamed original once it lives in a lib. If the linker picks a different definition, say so.
- Out of scope: `DiskInfoKit` (phase 2), docs (`ARCHITECTURE.md`, `TESTING.md` — the Coding Owner updates them during integration).

## Summary to return
List every file created/changed, the final `engine.h` interface, your conclusion on the COM lifetime question, and anything you could not verify by reading source.

## Fix round 1 (2026-10-31, after Reviewer1 Opus 5.5 + Reviewer2 Codex GPT-6.1-Sol)
Build, exports, size and the non-admin probe were identical to the baseline. Fix these, nothing else:
1. **`engine.h` must make `CAtaSmart`'s layout unambiguous.** `AtaSmart.h:1983-1989` adds members only under `JMICRON_USB_RAID_SUPPORT`, set in `CompileOptions.h`; a consumer that includes `AtaSmart.h` without `CompileOptions.h` first gets a 20-byte-smaller object and the lib writes past it. Replace the forward declaration with `#include "CompileOptions.h"` followed by `#include "AtaSmart.h"` inside `engine.h` (keep the comment that explains why the order matters, moved from `core_api.cpp`/`engine.cpp`). Callers then include only `engine.h` (plus `core_api.h` in the DLL).
2. **Restore the original COM/`CAtaSmart` ordering exactly.** Before: `ComScope com;` → `if (!com.Usable()) return DI_E_IO;` → `CAtaSmart ata;` → configure/Init/thresholds → fill → `~CAtaSmart` (which `FreeLibrary`s JMicron DLLs loaded during Init) → `CoUninitialize`. Now `CAtaSmart` is built before COM and destroyed after `CoUninitialize`. Make `ComScope` a public class in `namespace dicore` (same body), declared by the caller before `CAtaSmart`; `dicore::Scan(CAtaSmart&, bool useWmi)` no longer touches COM and returns `void`. Update `engine.h` comments: Scan requires a usable `dicore::ComScope` alive on the calling thread for the lifetime of the `CAtaSmart` object.
3. `engine.cpp:15` comment: the `DebugPrint` rename now lives in `DiskInfoCoreLib.vcxproj`, not `DiskInfoCore.vcxproj`.
4. `engine.cpp:125` comment: "so the DLL must too" → "so the core must too".
5. `core_api.cpp`: drop `<objbase.h>` and `<string>` if nothing there uses them any more (check before removing; `<vector>` stays).
Return the same summary as before, plus the final `di_scan` body.

## Fix round 2 (2026-10-31, after round-2 reviews)
All 5 round-1 fixes verified by both reviewers. Two header-hygiene fixes in `DiskInfoCoreLib/engine.h` only:
1. Add `#include <afxtempl.h>` after `<afxwin.h>` and before `AtaSmart.h`: `AtaSmart.h:1942-1954` declares `CArray` members, and `<afxwin.h>` only gives the definition indirectly (through `afxanimationcontroller.h`, skipped if `__UIAnimation_h__` is defined). `engine.h` must compile when it is the only include.
2. Make `dicore::ComScope` non-copyable: `ComScope(const ComScope&) = delete; ComScope& operator=(const ComScope&) = delete;` and make `uninit`/`hr` private (only the constructor, destructor and `Usable()` use them). A copy would call `CoUninitialize` twice.
