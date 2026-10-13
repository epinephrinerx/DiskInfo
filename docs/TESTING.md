# Testing

เอกสารนี้เป็นแหล่งข้อมูลหลักสำหรับวิธีตรวจสอบ `DiskInfo` แทนคำสั่งตัวอย่างทั้งหมดด้วยคำสั่งจริงก่อนใช้งาน

## 1. Quick start

ยืนยันแล้วบนเครื่องพัฒนา (2026-10-11):

```sh
# SystemMonitor (D:\Dev\Apps\SystemMonitor4.0) — ผ่าน 312/312
dotnet test SysMonitor.Tests
build-wpf.cmd            # test + publish + installer
```

DiskInfo (C++, build อ้างอิง Windows) — ยืนยันแล้วเมื่อ 2026-10-11 หลังติดตั้ง component `Microsoft.VisualStudio.Component.VC.ATL` และ `...VC.ATLMFC` บน VS Build Tools 2026 โดย override toolset เป็น v145 ที่บรรทัดคำสั่ง ไม่แก้ `.vcxproj` (ไฟล์กำหนด v142):

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe" `
  CrystalDiskInfo\DiskInfo.vcxproj /p:Configuration=Release /p:Platform=x64 `
  /p:PlatformToolset=v145 /p:WindowsTargetPlatformVersion=10.0.26100.0 `
  "/p:OutDir=<dir>\bin\" "/p:IntDir=<dir>\obj\" /m /nologo /v:m
```

ผล: `Build succeeded` 0 error 1 warning ได้ `DiskInfo64.exe` (2,883,072 bytes) ใช้ `OutDir`/`IntDir` นอก repo แต่ `.vcxproj` เดิมตั้งไฟล์ PDB ไว้ที่ `Build\<config><platform>\<toolset>.pdb` ใต้โฟลเดอร์ซอร์ส จึงมี `CrystalDiskInfo\Build\Releasex64\145.pdb` (~3 MB) โผล่ใน working tree ต้องลบก่อน commit (พบและลบแล้วครั้งหนึ่ง) ยังไม่ได้ build ผ่าน `DiskInfo.sln` ทั้งชุดและยังไม่มี test อัตโนมัติของ DiskInfo

**DiskInfoCore.dll** (เฟส 1, โปรเจกต์ใหม่ `DiskInfoCore\` อยู่ที่ root ของ repo แยกจาก `CrystalDiskInfo\` ซึ่งไม่ถูกแก้) build ด้วยคำสั่งเดียวกันแต่ชี้ไปที่ `DiskInfoCore.vcxproj` (output ไป `DiskInfoCore\Build\` ซึ่ง git ignore แล้ว):

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe" `
  DiskInfoCore\DiskInfoCore.vcxproj /p:Configuration=Release /p:Platform=x64 `
  /p:PlatformToolset=v145 /p:WindowsTargetPlatformVersion=10.0.26100.0 /m /nologo /v:m
```

**ข้อควรระวัง (พบ 2026-10-13):** ถ้า registry `HKLM\SOFTWARE\Microsoft\Windows Kits\Installed Roots\KitsRoot10` ถูกตัวติดตั้งอื่น (เช่น Windows ADK/Debuggers) เปลี่ยนไปชี้ที่ที่ไม่มี SDK (เครื่องนี้ชี้ `D:\Program Files\Windows Kits\10\`) compile จะล้มด้วย `C1083: Cannot open include file: 'new.h'` เพราะ path ของ UCRT ผิด วิธีเลี่ยงโดยไม่แก้ registry คือเพิ่ม (ต้อง escape `;` เป็น `%3B`):

```powershell
$k='C:\Program Files (x86)\Windows Kits\10'; $v='10.0.26100.0'
... "/p:UniversalCRT_IncludePath=$k\Include\$v\ucrt%3B" "/p:UniversalCRT_LibraryPath_x64=$k\lib\$v\ucrt\x64%3B"
```

ผล (2026-10-12): Build succeeded 0 error 0 warning, `DiskInfoCore.dll` ~598 KB, export 6 ชื่อ ทดสอบเรียกจริงด้วยสคริปต์ P/Invoke (ไม่อยู่ใน repo — ยังไม่มี test อัตโนมัติ): สิทธิ์ปกติ → `di_scan` คืน `DI_E_ACCESS` (-2) และ 0 ดิสก์; admin → 3 ดิสก์ (TOSHIBA SATA, WDS NVMe, Seagate USB ผ่าน SAT) health = Good ทั้งหมด ใช้เวลา 3.1–4.3 วินาที และไฟล์ `.log` ข้าง host ไม่ถูกลบ ดูรายละเอียดและข้อสังเกตที่ `docs/CORE_ABI.md`

ข้อกำหนดเบื้องต้น:

- Runtime: .NET SDK 10.0.401 (เป้าหมาย `net8.0-windows`), MSVC ผ่าน VS Build Tools 2026 (toolset v145, มี MFC/ATL แล้ว)
- Package manager: N/A
- Services ที่ต้องใช้: NONE
- ตัวแปร environment: ดู `.env.example`; ห้ามใช้ production credentials

## 2. Test levels

| Level | Purpose | Location | Command |
|---|---|---|---|
| Unit | logic ฝั่งผู้ใช้ DLL (แปลง sentinel, state, หน้าจอ) | `D:DevAppsSystemMonitor4.0SysMonitor.Tests` (MSTest, ไม่ใช่ใน repo นี้) | `dotnet test SysMonitor.Tests` |
| Integration | DLL จริง: โหลด, สแกน, struct layout | ยังไม่มี test อัตโนมัติใน repo นี้; ตรวจ struct (160 B, `power_on_hours` ที่ 144) ใน `SmartFallbackTests` ของ SystemMonitor และเรียก P/Invoke ด้วยสคริปต์ครั้งเดียว | ดูหัวข้อ 8 |
| Contract/API | ABI ตาม `docs/CORE_ABI.md` | `SysMonitor.Tests/SmartModuleTests.cs` ตรวจ search path ของ P/Invoke และการแปลงฟิลด์ native | `dotnet test SysMonitor.Tests` |
| End-to-end | แอปจริงแบบ admin เห็นข้อมูลดิสก์ | ยังไม่มี test อัตโนมัติ; ตรวจด้วยมือและภาพหน้าจอ (หัวข้อ 8) | ดูหัวข้อ 8 |
| Smoke | ตรวจหลัง build | `build-wpf.cmd` (รัน test ก่อน แล้ว publish และสร้าง installer ที่รวม DLL) | `build-wpf.cmd` ใน `SystemMonitor4.0` |

## 3. เลือกชุดตรวจสอบตามการเปลี่ยนแปลง

| Change | Required checks |
|---|---|
| Docs/comments only | formatting/link check ถ้ามี |
| Local logic | targeted unit tests + lint/type-check ที่เกี่ยวข้อง |
| Public API | unit + integration/contract + compatibility checks |
| Database/schema | migration test + integration + rollback/forward-fix validation |
| Authentication/security | negative tests + authorization boundary tests + relevant integration tests |
| UI behavior | component tests + critical E2E + visual/accessibility check ตามความเสี่ยง |
| Dependency/config/build | build + smoke + affected test suites |
| Shared/core code | affected tests แล้วตามด้วย full suite |

## 4. Test design rules

- Test observable behavior ไม่ผูกกับ implementation detail โดยไม่จำเป็น
- ทุก bug fix ควรมี regression test ที่ fail ก่อน fix และ pass หลัง fix
- ครอบคลุม happy path, boundary, invalid input และ failure path ที่สำคัญ
- Tests ต้อง deterministic และทำซ้ำได้
- ห้ามพึ่งลำดับการรัน เวลาจริง network จริง หรือ shared mutable state หากหลีกเลี่ยงได้
- ใช้ fake clock, seeded random และ controlled fixtures เมื่อต้องทดสอบเวลา/ความสุ่ม
- Mock เฉพาะ system boundary; อย่า mock logic ที่กำลังทดสอบ
- Test name อธิบายเงื่อนไขและผลลัพธ์ที่คาดหวัง

## 5. Test data and isolation

- ใช้ synthetic data; ห้ามใช้ข้อมูลลูกค้าจริง
- แต่ละ test ต้องสร้างและล้างข้อมูลของตนเอง
- Local test database: N/A (ไม่มี database)
- Fixture factories: ฝั่ง SystemMonitor สร้าง `Snapshot`/`SmartDisk` สังเคราะห์ใน `SmartModuleTests.cs`; ฝั่ง DiskInfo ยังไม่มี
- Reset command: N/A (test ไม่เขียนข้อมูลถาวร; test ที่รัน `Sampler` จริงอ่านเครื่องแบบอ่านอย่างเดียว)
- Tests ที่ destructive ต้องทำงานเฉพาะใน disposable environment และมี guard ป้องกัน production

## 6. Coverage policy

- Coverage เป็นสัญญาณ ไม่ใช่เป้าหมายเพียงอย่างเดียว
- Minimum project threshold: ไม่กำหนด (โครงการไม่วัด coverage)
- โค้ดใหม่/เปลี่ยนต้องครอบคลุม decision paths ที่มีความเสี่ยง
- ห้ามเพิ่ม meaningless assertions เพื่อให้ตัวเลขผ่าน
- รายงานส่วนที่ไม่ทดสอบพร้อมเหตุผลและ risk mitigation

## 7. CI quality gates

Pull request ต้องผ่าน:

1. Format/lint
2. Type-check หรือ static analysis
3. Unit tests
4. Required integration/contract tests
5. Build/package validation
6. Security/license checks ตามที่โครงการกำหนด

CI configuration: ไม่มี (ทั้งสอง repo ไม่มี `.github`; gate คือ `build-wpf.cmd` ที่เจ้าของรันเอง)

Flaky test ห้าม rerun จนเขียวแล้วละเลย ให้เก็บหลักฐาน เปิด issue และ quarantine เฉพาะเมื่อมีเจ้าของกับวันติดตามชัดเจน

## 8. Manual verification

ใช้ manual testing เมื่อ automation ไม่คุ้มค่าหรือครอบคลุมไม่ได้ และบันทึก:

- Environment/build ที่ทดสอบ
- Preconditions และ test data
- Steps
- Expected/actual result
- Screenshot/log ที่ไม่เปิดเผยข้อมูลสำคัญเมื่อจำเป็น

Critical smoke checklist:

- เปิด SystemMonitor แบบผู้ดูแลระบบ (ปุ่ม "รันใหม่แบบผู้ดูแลระบบ" ในหน้าตั้งค่า) → ติ๊ก "แสดง SMART ของดิสก์" → เห็นแท็บ/section/หน้า Widget ของทุกดิสก์พร้อมข้อมูลจริง
- เปิดแบบปกติ (ไม่ใช่ admin) → โมดูลบอกว่าต้องใช้ผู้ดูแลระบบ และอุณหภูมิ SATA/NVMe ในเครื่องยังขึ้นจาก storage query
- วิธีตรวจแบบ admin ที่ใช้แล้ว (2026-10-13): สคริปต์ PowerShell ยกระดับครั้งเดียว (ผู้ใช้กด UAC) สำรองและคืน `config.wpf.json` เอง เปิดแอปด้วย `--allow-multiple --full`/`--expanded` (ไม่ชนสำเนาที่ผู้ใช้เปิดอยู่) แล้วจับภาพหน้าต่างด้วย `CopyFromScreen` (โปรเซสที่ยกระดับรับ input จากเชลล์ปกติไม่ได้ จึงควบคุมด้วย argument และ config เท่านั้น) สคริปต์ไม่อยู่ใน repo
- ต้องวาง `DiskInfoCore.dll` ข้าง `SystemMonitor.exe` (ตอน build ด้วย `build-wpf.cmd` ตัว setup รวมไว้ให้; รันจาก `binDebug` ต้องคัดลอกเอง)
- Error handling และ recovery ที่สำคัญ

## 9. Failure handling

เมื่อ test fail:

1. เก็บ command, error และ environment ที่เกี่ยวข้อง
2. ยืนยันว่า reproduce ได้
3. แยกว่าเกิดจาก change ปัจจุบัน, pre-existing failure หรือ environment
4. แก้เฉพาะ failure ที่อยู่ใน scope; อย่าซ่อนด้วย skip หรือ assertion ที่อ่อนลง
5. รัน targeted test ซ้ำ แล้วรัน broader suite ตามความเสี่ยง

หากไม่สามารถรัน test ได้ ให้รายงานเหตุผล สิ่งที่ตรวจแทน และความเสี่ยงที่เหลือ

## 10. Performance and security testing

- Performance command/scenario: ยังไม่มี (วัดเวลาสแกนด้วยมือ 3.1–4.3 วินาที)
- Baseline และ regression threshold: ยังไม่กำหนด
- Security scanning: ยังไม่มี (DLL อ่านอย่างเดียว; การยกระดับสิทธิ์ทำโดยผู้ใช้กดเองเท่านั้น)
- Dependency audit: N/A (SystemMonitor ห้ามใช้ package ของบุคคลที่สาม; DLL ลิงก์ MFC แบบ static ไม่มี package)
- ห้ามยิง load test หรือ security test ไป production โดยไม่ได้รับอนุญาต

## 11. Release verification

ก่อน release:

- CI gates ผ่านจาก commit ที่จะ release
- migration และ compatibility ได้รับการตรวจ
- artifacts สามารถ build แบบทำซ้ำได้
- smoke tests ผ่านใน staging หรือ environment ที่กำหนด
- rollback/forward-fix plan พร้อม
- monitoring และ alert ที่เกี่ยวข้องพร้อมใช้งาน

