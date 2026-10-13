# Architecture

เอกสารนี้อธิบายโครงสร้างและขอบเขตของ `DiskInfo` ให้อัปเดตเมื่อมีการเปลี่ยน service boundary, data flow, storage, public API หรือ dependency direction

สถานะ (2026-10-13): บันทึกเฉพาะสิ่งที่มีอยู่และยืนยันแล้ว **ทิศทาง "แยก Core ออก แล้วเพิ่มความสามารถเป็น plug-in/extension/DLL ทั้ง SystemMonitor และ DiskInfo" (R-004) ยังเป็น 🕐 รอยืนยัน — ยังไม่อยู่ในเอกสารนี้** เมื่อรับรองแล้วจะเขียนเป็น ADR ใน `docs/adr/` แล้วค่อยปรับหัวข้อ 4–5 และ 8

## 1. System overview

DiskInfo คือ fork ของ CrystalDiskInfo (GUI แบบ MFC บน Windows) ที่เก็บซอร์สต้นฉบับไว้ใน `CrystalDiskInfo/` โดยไม่แก้ และเพิ่ม `DiskInfoCore/` ซึ่งเป็น native DLL ที่ห่อ `CAtaSmart` ของ Crystal ด้วย C ABI เพื่อให้แอปอื่น (ตอนนี้คือ SystemMonitor แบบ WPF ผ่าน P/Invoke) อ่านสุขภาพ อุณหภูมิ อายุ และชั่วโมงใช้งานของดิสก์ได้โดยไม่ต้องมี GUI ของ Crystal ผู้ใช้หลักคือเจ้าของโครงการเอง ส่วน core อ่าน SMART ต้องรันแบบผู้ดูแลระบบ (ถ้าไม่ใช่ จะคืน `DI_E_ACCESS` และไม่พบดิสก์เลย)

### Goals

- ให้ SystemMonitor แสดงอุณหภูมิและ SMART ของดิสก์จริงผ่านตัวอ่านเดียวกับ Crystal (R-002, R-003)
- มี C ABI ที่นิ่ง (`docs/CORE_ABI.md`) ให้ผู้ใช้หลายโครงการใช้ร่วมกัน
- (ชะลอ) core แบบ portable C++17 + CMake + CLI บน Ubuntu 21.04+/Debian 11+ แบบ compile+link เท่านั้น (R-001, ผู้ใช้สั่งชะลอ 2026-10-13)

### Non-goals

- ไม่แก้ GUI MFC เดิมของ CrystalDiskInfo และไม่เปลี่ยนพฤติกรรมของ `DiskInfo.sln`
- ไม่ทำฟีเจอร์ Crystal ที่ SystemMonitor ไม่มีในข้อกำหนด เช่น แจ้งเตือนอีเมล กราฟประวัติ SMART AAM/APM skin
- ไม่อ่านดิสก์จริงบน Linux และไม่ทำ .deb (R-001)

## 2. Context diagram

```text
[SystemMonitor.exe (WPF, repo SystemMonitor4.0)]          [DiskInfo64.exe (MFC เดิม)]
      |  P/Invoke: di_open / di_scan / di_disk_info              |
      v  (C ABI v1, docs/CORE_ABI.md)                            |
[DiskInfoCore.dll]  --- compile จากซอร์สเดียวกัน ---> [CrystalDiskInfo/AtaSmart.cpp (CAtaSmart)]
      |                                                          |
      v   IOCTL อ่านอย่างเดียว + WMI (root\cimv2, root\WMI)      v
[Windows storage stack / ตัวควบคุม NVMe-SATA-USB bridge] --> [ดิสก์จริง]
```

- trust boundary: การอ่าน SMART ต้องใช้สิทธิ์ผู้ดูแลระบบ ฝั่ง SystemMonitor ไม่ยกระดับสิทธิ์เอง ผู้ใช้กดปุ่มเอง
- ไม่มี network boundary: ไม่มีการเรียกเครือข่าย (SystemMonitor มีตัวตรวจอัปเดตแยกต่างหาก ไม่เกี่ยวกับ DLL นี้)

## 3. Repository map

```text
/
├── CrystalDiskInfo/     # ซอร์ส Crystal ต้นฉบับ (baseline cbe5ac2) ห้ามแก้
├── DiskInfoCore/        # DiskInfoCore.dll: core_api.h/.cpp, .def, .vcxproj (Build/ ถูก git ignore)
├── docs/                # AI_WORKFLOW, ARCHITECTURE, TESTING, CORE_ABI, handoff/
├── Requirements.md      # ข้อตกลง R-001..R-004 (หลักของ R-002 อยู่ที่นี่)
├── CLAUDE.md / AGENTS.md
```

ฝั่งผู้ใช้ DLL อยู่นอก repo นี้: `D:\Dev\Apps\SystemMonitor4.0` (`Native/SmartCore.cs` และ `Sensors/SmartBoard.cs`)

## 4. Components and ownership

| Component | Responsibility | Public interface | Owner |
|---|---|---|---|
| `CrystalDiskInfo/` | GUI และตัวอ่าน SMART เดิม (`CAtaSmart`) | ไม่มี (ใช้ภายใน) | Apichart Chantanis |
| `DiskInfoCore/` | ห่อ `CAtaSmart` เป็น C ABI: ตั้งค่า threshold/flag เหมือน GUI, แปลงค่า "ไม่มี" เป็น sentinel, serialize การสแกน | 6 export: `di_abi_version`, `di_open`, `di_close`, `di_scan`, `di_disk_count`, `di_disk_info` | Apichart Chantanis |
| `SystemMonitor4.0` (repo อื่น) | ตัวใช้ DLL: P/Invoke, thread สแกนแยก, แสดงผล | `Native/SmartCore.cs` | Apichart Chantanis |

## 5. Dependency rules

- `CrystalDiskInfo/` ไม่ถูกแก้ และไม่รู้จัก `DiskInfoCore/`; `DiskInfoCore.vcxproj` อ้างซอร์สเดิมผ่าน `..\CrystalDiskInfo\...`
- ผู้ใช้ DLL คุยผ่าน C ABI เท่านั้น ห้ามอ้าง header C++ ของ Crystal; header ของ ABI (`core_api.h`) เป็น C ล้วน
- ABI ไม่เปลี่ยนแบบแตกหัก: struct ขึ้นต้น `struct_size`, เพิ่มฟิลด์ท้าย struct ได้ (ผู้เรียกที่ส่งขนาด ≥ v1 ใช้ได้), เลข major เปลี่ยนเมื่อแตกหัก
- DLL ไม่เขียนดิสก์ ทุก IOCTL อ่านอย่างเดียว และไม่ลบไฟล์ข้าง host (แทน `DebugPrint` เดิมด้วย no-op)
- การสแกนทั้ง process ต้องทีละครั้ง และต้องเรียกจาก thread พื้นหลัง (ใช้เวลาหลายวินาที และเรียก COM)
- ห้ามมี circular dependency ระหว่าง repo: SystemMonitor พึ่ง DiskInfo ไม่ใช่ทางกลับ

## 6. Runtime data flow

### สแกน SMART จาก SystemMonitor

1. thread `smart-scan` (STA) เรียก `di_open` → `di_scan`
2. `di_scan` เปิดแฟล็ก USB/RAID/CSMI เหมือน GUI, ตั้ง caution threshold 1/1/1/10 แล้ว `CAtaSmart::Init` และ `CheckDiskStatus` ทุกดิสก์
3. SystemMonitor อ่านทีละดิสก์ด้วย `di_disk_info` แล้วแปล sentinel (`DI_NA`, `DI_NA64`) เป็น "ไม่มีค่า"
4. ผลทั้งชุดและสถานะถูกเขียนเป็นค่าเดียว แล้ว UI อ่านจาก snapshot

Failure behavior:

- ไม่ใช่ admin → `di_scan` คืน `DI_E_ACCESS` (-2) และไม่พบดิสก์ → SystemMonitor แสดง "ต้องรันแบบผู้ดูแลระบบ"
- ไม่มี DLL หรือ ABI major ไม่ตรง → โมดูลบอกว่า "ไม่พบ DiskInfoCore.dll" แอปทำงานต่อ
- สแกนล้ม → ทิ้งผลเก่าทั้งหมด ไม่แสดงดิสก์ค้าง
- ดิสก์ที่อ่านข้อมูลไม่ครบ → `access = PARTIAL` และตัวเลขที่ไม่มีเป็น sentinel

## 7. Data model and storage

ไม่มี database หรือไฟล์ที่ DLL เก็บเอง โครงสร้างที่ข้าม ABI คือ `di_disk_info_t` (160 ไบต์ v1; `power_on_hours` ที่ offset 144) ดูฟิลด์และ sentinel ใน `docs/CORE_ABI.md` ค่าตั้งของ SystemMonitor (`config.wpf.json`) เป็นของ repo นั้น

## 8. Interfaces

### Public API

- Contract location: `docs/CORE_ABI.md` และ `DiskInfoCore/core_api.h`
- Versioning policy: `di_abi_version()` คืน `major<<16 | minor` (ตอนนี้ 1.0) ผู้ใช้ตรวจเฉพาะ major
- Compatibility policy: เพิ่มฟิลด์ท้าย struct ได้ใน minor; แตกหัก = เพิ่ม major

### Events and jobs

ไม่มี

### External dependencies

| Dependency | Purpose | Timeout/retry | Failure impact |
|---|---|---|---|
| Windows storage IOCTL | อ่าน SMART/NVMe log | ไม่มี timeout เอง (สแกนวัดได้ 3.1–4.3 วินาที กับ 2–4 ดิสก์) | ดิสก์นั้นไม่มีค่า |
| WMI (`root\cimv2`, `root\WMI`) | ค้นหาดิสก์ | ไม่มี retry | ไม่พบดิสก์บางตัว |

## 9. Cross-cutting concerns

- Authentication/Authorization: ต้องเป็นผู้ดูแลระบบ จุดบังคับคือ Windows (IOCTL ถูกปฏิเสธ) ไม่มี authz ของโครงการเอง
- Configuration: DLL ไม่มีไฟล์ตั้งค่า มีแค่แฟล็ก `di_open` (`DI_OPEN_NO_WMI`)
- Secrets: ไม่มี
- Logging: DLL ไม่เขียน log ไม่สร้างหรือลบไฟล์ข้าง host
- Localization: ข้อความเป็น UTF-8 ใน buffer ตายตัว DLL ไม่มีสตริงสำหรับผู้ใช้ การแปลอยู่ฝั่งผู้ใช้ DLL

## 10. Quality attributes

| Attribute | Target | How verified |
|---|---|---|
| Latency | ยังไม่กำหนดเป้าหมาย (วัดได้ 3.1–4.3 วินาทีต่อการสแกนกับ 2–4 ดิสก์) | วัดด้วย P/Invoke จริง 2026-10-12 |
| Data integrity | ไม่เขียนดิสก์ ไม่แสดงค่าประมาณ | ตรวจโค้ด + review (Codex/Opus) |
| Availability, Throughput, Security/privacy | ยังไม่กำหนด | — |

## 11. Architecture decisions

| ADR | Decision | Status |
|---|---|---|
| (ยังไม่มีไฟล์ใน `docs/adr/`) | ตัดสินใจมาแล้วแต่บันทึกไว้ใน `CORE_ABI.md` และ `Requirements.md`: ห่อ `CAtaSmart` ด้วย C ABI แทนการเขียนตัวอ่านใหม่ · ใช้ native DLL กับ SystemMonitor (ผู้ใช้ยอมรับ 2026-10-09) | Accepted |
| (ถัดไป) | plug-in/extension architecture ของ SystemMonitor และ DiskInfo (R-004) | Proposed — รอผู้ใช้ยืนยัน |

## 12. Known constraints and technical debt

- `CAtaSmart` ผูกกับ MFC/ATL (`AtaSmart.cpp` 13,570 บรรทัด ใช้ `CString` 127 จุดและ `DeviceIoControl` 65 จุด) DLL จึงลิงก์ MFC แบบ static (582,656 B) และการ port ไป Linux (R-001) ต้องแยกส่วนก่อน
- `DiskInfoCore.vcxproj` ตั้ง toolset v142 ต้อง override เป็น v145 ที่บรรทัดคำสั่ง
- ถ้า registry `KitsRoot10` ชี้ผิด build ล้มที่ `new.h` ดูวิธีเลี่ยงใน `docs/TESTING.md`
- `DiskInfo.vcxproj` เดิมเขียน PDB ใต้ซอร์ส (`CrystalDiskInfo\Build\`) ต้องลบก่อน commit
- ยังไม่ได้ build ผ่าน `DiskInfo.sln` ทั้งชุด และยังไม่มี test อัตโนมัติของ DLL เอง (ทดสอบผ่าน test ของ SystemMonitor และสคริปต์ P/Invoke)
- ยังไม่ได้ทดสอบ: กรณี NVMe อ่านล้มเหลว (ต้องไม่รายงาน `access = FULL`, ข้อ F4 ใน `CORE_ABI.md`) และทางที่ `CoInitializeEx` คืน `RPC_E_CHANGED_MODE`
