# Handoff — เฟส 1: DLL ห่อ CAtaSmart ตาม C ABI

จาก: Coding Owner (Claude Code) → ถึง: Writing Owner (agy, Gemini-3.8 Flash Medium)
Reviewer1: Claude Opus 5.5 High · Reviewer2: Codex GPT-6.1-Lun High · Integrator: Claude Opus 5.5 High
Requirement: R-003 (🔨) ผ่านการต่อ R-019 ใน `D:\Dev\Apps\SystemMonitor4.0\Requirements.md` · เตรียมฐานให้ R-002 และ R-001
วันที่: 2026-10-11 · Branch ที่แนะนำ: `agent/agy-diskinfo-dll`

> **อัปเดต 2026-10-11:** ตรวจพบว่าสาเหตุอุณหภูมิ n/a คือ property ID ผิดใน SystemMonitor (ดู `phase2a-sysmon-temp-id.md`) ลำดับที่เสนอใหม่: 2a ก่อน แล้วเฟส 1 นี้ยังจำเป็นสำหรับ USB HDD (R-003 ส่วนที่เหลือ) และ R-002/R-001 — รอผู้ใช้รับรองลำดับ

> **สถานะ 2026-10-12 — ข้อยกเว้นของ workflow:** agy ติดโควตา (429, รีเซ็ตอีก ~42 ชม., ลองทั้ง Gemini 3.8 Flash Medium และ 3.1 Pro High ได้ผลเดียวกัน) ผู้ใช้อนุมัติให้ Coding Owner เขียนเฟสนี้เอง (ทางเลือก 3) ไฟล์ใหม่ใน `CrystalDiskInfo/DiskInfoCore/` เขียนโดย Claude Code ไม่ใช่ agy; Reviewer2 (Codex) ตรวจแล้วไม่พบ defect, Reviewer1 (Opus) กำลังตรวจ ผล build/ทดสอบอยู่ใน `docs/TESTING.md` และ `docs/CORE_ABI.md` ข้อ 1b–6

## Objective
สร้าง Windows DLL (`DiskInfoCore.dll`, x64) ที่ห่อ `CAtaSmart` เดิม และเปิดฟังก์ชันตามสัญญา `docs/CORE_ABI.md` ให้ C# เรียกผ่าน P/Invoke ได้ ในเฟสนี้ทำเฉพาะ `di_abi_version`, `di_open`, `di_close`, `di_scan`, `di_disk_count`, `di_disk_info` (ยังไม่ต้องทำ `di_attr_*`)

## Acceptance criteria (เฟสนี้)
- [ ] build ด้วยคำสั่งใน `docs/TESTING.md` (ปรับ target เป็น DLL project ใหม่) ผ่าน 0 error
- [ ] export เป็น `extern "C"` ครบ 6 ฟังก์ชัน ตรวจด้วย `dumpbin /exports`
- [ ] ไม่มี exception หลุดข้าม boundary; `struct_size` ผิด → `DI_E_STRUCT_SIZE`
- [ ] โปรแกรมทดสอบเล็ก ๆ (C++ console หรือ C# สคริปต์) เรียก `di_scan` แล้วพิมพ์ model, interface_kind, temperature_c ของแต่ละดิสก์บนเครื่องพัฒนา โดยรันแบบ **ไม่ใช่ admin**
- [ ] บันทึกผล 5 ข้อ "ต้องยืนยัน" ท้าย `docs/CORE_ABI.md` เป็นตัวเลข/ข้อเท็จจริงจากการรันจริง (COM/WMI threading, ดิสก์ที่อ่านได้แบบ non-admin, เวลา scan, static MFC ใน DLL ได้หรือไม่ + ขนาด, DI_NA vs ค่า default 0)
- [ ] `DiskInfo.sln` เดิม build ไม่พัง (ไม่แก้พฤติกรรม GUI)

## Scope (อนุญาตให้แก้/เพิ่ม)
- เพิ่มโปรเจกต์ใหม่ `CrystalDiskInfo/DiskInfoCore/` (vcxproj + `core_api.cpp` + `core_api.h`) และเพิ่มเข้า `DiskInfo.sln` เฉพาะถ้า build ไม่กระทบ config เดิม
- `docs/CORE_ABI.md` (เติมผลที่ยืนยัน) และ `docs/TESTING.md` (คำสั่ง build/test ใหม่)
- ห้ามแก้ `AtaSmart.cpp/.h` เว้นแต่จำเป็นจริงและบอกเหตุผล

## Constraints / Non-goals
- อ่านอย่างเดียว (read-only IOCTL) ห้ามเขียน/ส่งคำสั่งที่เปลี่ยนสถานะดิสก์
- ไม่ elevate เอง ไม่เพิ่ม service/helper ไม่เพิ่มแพ็กเกจภายนอก
- ไม่ทำ portable C++17/CMake/Linux ในเฟสนี้ (เป็น R-001 เฟสหลัง)
- ไม่แก้ SystemMonitor ในเฟสนี้
- DLL เรียกครั้งเดียวต่อ handle ต่อเธรด; `di_scan` ช้า ห้ามเรียกจาก UI thread (เอกสารใน header)

## Commit / diff ที่ต้องตรวจ
ยังไม่มี (ยังไม่ได้ commit อะไรทั้งสอง repo) ผู้ใช้เป็นผู้สั่ง commit เมื่อถึงเวลา

## Commands ที่ Coding Owner รันแล้วในเฟส 0
- `dotnet test SysMonitor.Tests` ที่ SystemMonitor4.0 → 312/312 ผ่าน
- ติดตั้ง MFC/ATL (exit 0) แล้ว `MSBuild DiskInfo.vcxproj /p:Configuration=Release /p:Platform=x64 /p:PlatformToolset=v145 /p:WindowsTargetPlatformVersion=10.0.26100.0` → Build succeeded, 0 error, ได้ `DiskInfo64.exe`

## ข้อกำหนดเพิ่มจากการอ่านโค้ด (2026-10-12)
- `di_scan` ต้อง `CoInitializeEx` บน thread ที่เรียก (Init ไม่ทำเอง) ดูรายละเอียดข้อ 1 ใน `docs/CORE_ABI.md`
- `CAtaSmart::Init(BOOL useWmi, BOOL advancedDiskSearch, PBOOL flagChangeDisk, BOOL workaroundHD204UI, BOOL workaroundAdataSsd, BOOL flagHideNoSmartDisk, BOOL flagSortDriveLetter, BOOL flagHideRAIDVolume)` ผลอยู่ใน `CArray<ATA_SMART_INFO> vars` (`AtaSmart.h:1768,1942`)
- agy รัน headless สั่ง shell ไม่ได้ → agy แก้ไฟล์อย่างเดียว Coding Owner เป็นคน build/ทดสอบแล้วส่ง error กลับ

## Open questions / known risks
- `CAtaSmart` อาจเริ่ม COM/WMI ภายใน — ต้องเช็คว่าเรียกจากเธรดที่ C# สร้างได้ปลอดภัย (ข้อ 1 ใน CORE_ABI "ต้องยืนยัน")
- static MFC ใน DLL อาจใหญ่หรือมีปัญหา CRT/MFC-in-DLL; ถ้าไม่ได้ ให้รายงานก่อนเปลี่ยนเป็น shared MFC
- สมมติฐานที่ยังไม่ยืนยัน: IOCTL อุณหภูมิของ SystemMonitor ไม่คืนค่าสำหรับ SATA HDD/USB bridge — ผลจาก DLL นี้คือหลักฐานแรก ห้ามสรุปเกินกว่าที่รันได้
- ไม่มีผู้ตรวจรับฮาร์ดแวร์จริงนอกเครื่องผู้ใช้: ผลเทียบ CrystalDiskInfo ±2 °C ต้องให้ผู้ใช้ยืนยัน
