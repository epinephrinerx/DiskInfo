# Core ABI (draft · เฟส 0)

สัญญาระหว่าง native DLL ของ DiskInfo กับผู้ใช้ภายนอก (ตอนนี้คือ SystemMonitor ผ่าน P/Invoke)
ใช้รองรับ R-002, R-003 และ R-001 — โค้ดภายในย้ายได้ แต่ ABI ต้องไม่เปลี่ยน

สถานะ: **ร่างเพื่อรีวิว** ค่าที่ยังไม่ยืนยันจากโค้ดจริงมีเครื่องหมาย `TBV` (to be verified) ในเฟส 1

## กฎ

- C ABI ล้วน (`extern "C"`) ไม่ปล่อย exception ข้ามขอบเขต; คืน error code แทน
- ข้อความเป็น UTF-8 ใน buffer ขนาดคงที่ ไม่มี `CString`/`std::string` ใน ABI
- ทุก struct ขึ้นต้นด้วย `uint32_t struct_size` ผู้เรียกเติมก่อนเรียก เพื่อเพิ่มฟิลด์ท้าย struct ภายหลังได้โดยไม่แตก
- ค่าที่ไม่มี (เช่น ไม่มี sensor) คืน sentinel `DI_NA` (`INT32_MIN`) ไม่คืน 0
- handle หนึ่งตัวใช้ได้ทีละ thread; การ scan ช้า (อาจหลายวินาที) ผู้เรียกต้องเรียกจาก thread พื้นหลัง ห้ามเรียกจาก UI thread
- ไม่มี global state; ไม่เขียนดิสก์; ทุก IOCTL เป็นแบบอ่านอย่างเดียว

## ฟังก์ชัน

```c
uint32_t di_abi_version(void);                 // major<<16 | minor

int  di_open (di_handle** out, uint32_t flags); // DI_OPEN_* ด้านล่าง
void di_close(di_handle*);

int  di_scan(di_handle*);                       // อ่านดิสก์ทั้งหมดใหม่ (ช้า)
int  di_disk_count(const di_handle*);

int  di_disk_info (const di_handle*, int idx, di_disk_info_t* out);
int  di_attr_count(const di_handle*, int idx);
int  di_attr_info (const di_handle*, int idx, int attr, di_attr_t* out);
```

## struct

```c
typedef struct {
  uint32_t struct_size;
  int32_t  physical_drive_id;   // ตรงกับ \.\PhysicalDriveN ที่ SystemMonitor ใช้ (join key)
  char     model[64], serial[32], firmware[16];   // UTF-8
  int32_t  interface_kind;      // DI_IF_ATA / NVME / USB / ...
  int32_t  media;               // DI_MEDIA_HDD / SSD / UNKNOWN
  int32_t  temperature_c;       // DI_NA ถ้าไม่มี
  int32_t  health;              // 0 unknown · 1 good · 2 caution · 3 bad  (ตาม DISK_STATUS)
  int32_t  life_percent;        // DI_NA ถ้าไม่มี
  int64_t  power_on_hours;      // DI_NA ถ้าไม่มี
  uint32_t access;              // DI_ACCESS_FULL / PARTIAL / DENIED   (ข้อมูลครบเพราะสิทธิ์หรือไม่)
  uint32_t source;              // ทางที่ได้ค่า: ATA SMART / NVMe / SAT / USB bridge  (บันทึกใน diag)
} di_disk_info_t;

typedef struct {
  uint32_t struct_size;
  uint8_t  id;  uint8_t current, worst, threshold;
  uint64_t raw;
  char     name[48];
} di_attr_t;
```

## Error code

`DI_OK=0` · `DI_E_ARG` · `DI_E_ACCESS` (ต้องใช้ admin; ผลบางส่วนอาจใช้ได้) · `DI_E_NOT_FOUND` · `DI_E_IO` · `DI_E_UNSUPPORTED` · `DI_E_STRUCT_SIZE`

## flag ของ `di_open`

- `DI_OPEN_NO_WMI` — ข้าม WMI/COM (ลดเวลา scan)  `TBV`
- `DI_OPEN_BASIC` — อ่านเฉพาะที่ไม่ต้องใช้ admin  `TBV`

## ที่ต้องยืนยันในเฟส 1 (ห้ามเดา)

1. ~~`CAtaSmart::Init` ทำอะไรที่ต้อง COM/WMI และต้อง `CoInitialize` เองหรือไม่~~ **ตอบจากโค้ดแล้ว (2026-10-12, อ่านโค้ดอย่างเดียว ยังไม่ได้รันจริง):** `Init` **ไม่เรียก** `CoInitialize` เอง (`AtaSmart.cpp:836-837` ถูกคอมเมนต์ไว้; GUI เดิมเรียก `CoInitializeEx(COINIT_APARTMENTTHREADED)` ที่ `DiskInfo.cpp:317`) แต่เรียก `CoInitializeSecurity` (`:839` ทิ้งผลลัพธ์ และเป็นค่าระดับ process) และ `CoCreateInstance(CLSID_WbemLocator)` (`:843`, `:10613`) ต่อด้วย `ConnectServer` ไป `root\cimv2` และ `root\WMI` ดังนั้นข้อกำหนดสำหรับ DLL: `di_scan` ต้องเรียก `CoInitializeEx` บน thread ที่เรียกเอง (รองรับ `S_OK`/`S_FALSE`, ถ้าได้ `RPC_E_CHANGED_MODE` ให้ใช้ต่อโดยไม่ `CoUninitialize`) และ `CoUninitialize` เฉพาะเมื่อ init สำเร็จเอง; ผลของ `CoInitializeSecurity` ที่ล้ม (`RPC_E_TOO_LATE`) เมื่อโฮสต์ .NET/WPF ตั้งค่าไปแล้ว ต้องทดสอบจริง — ยังไม่ทราบว่ามีผลกับ WMI query หรือไม่
1b. **ยืนยันด้วยการรันจริง (2026-10-12, `DiskInfoCore.dll` Release x64 เรียกจาก pwsh ผ่าน P/Invoke):** `di_scan` ทำงานได้ทั้ง thread STA และ MTA (ได้ผลเท่ากัน) ทางที่ได้ `RPC_E_CHANGED_MODE` ยังไม่ได้ทดสอบ (thread ใหม่ของ .NET ยังไม่มี COM) และยังไม่ได้ทดสอบเรียกจากโฮสต์ WPF จริง
2. ~~โหมดไม่ใช้ admin ได้ดิสก์ไหนบ้าง~~ **วัดแล้ว: ไม่ได้เลย** — สิทธิ์ปกติ `CAtaSmart::Init` เจอ **0 ดิสก์** (2 ms) แม้ NVMe และ SATA ที่ storage query อ่านอุณหภูมิได้ในสิทธิ์ปกติ ดังนั้น core นี้ต้องใช้ admin ทั้งหมด `di_scan` จึงคืน `DI_E_ACCESS` เมื่อไม่ใช่ admin และไม่เจอดิสก์ (สมมติฐานเดิม "NVMe ได้แบบ non-admin" ผิดสำหรับ CAtaSmart) ผลต่อแผน: ทางอุณหภูมิในสิทธิ์ปกติต้องใช้ storage query (R-019 เฟส 2a) ส่วน DLL นี้ใช้เฉพาะโหมด admin
3. **วัดแล้ว:** scan 3.1–4.3 วินาที (admin, 2–3 ดิสก์) ต้องเรียกจาก thread พื้นหลังตามสัญญา **USB HDD:** รอบแรก (flag USB/RAID ทั้งหมดเป็นค่าเริ่มต้น FALSE) ไม่เจอ Seagate เลย — Reviewer1 ชี้ว่า GUI เปิด flag เหล่านี้เป็นค่าเริ่มต้น (`DiskInfoDlgInit.cpp:167-216`, `DiskInfoDlg.cpp:238`) ผมตรวจยืนยันกับซอร์สแล้วเพิ่มใน `di_scan` หลังแก้ **เจอ** ST4000DM004 (ใน Seagate One Touch Hub, USB/SAT, `source=4`) อุณหภูมิ 50 °C, `health=1`, 3,612 ชม., `life=DI_NA` ตัวเลข drive id = 3 ตรงกับ `Win32_DiskDrive` (มี USB flash SanDisk เสียบเป็น index 2 ซึ่ง CAtaSmart ไม่แสดง ตามค่าเริ่มต้น `FlagUsbMemory` ปิด)
4. **วัดแล้ว:** ลิงก์ static MFC ได้ (build ผ่าน 0 error) ขนาด `DiskInfoCore.dll` Release x64 = ~598 KB; export ครบ 6 ชื่อไม่มี decoration (`dumpbin /exports`)
5. **ตอบจากโค้ด + วัด:** `CAtaSmart` ใช้ `Temperature = -1000`, `Life = -1`, `DetectedPowerOnHours = -1` เป็น "ไม่มีค่า" (`AtaSmart.cpp:2736,2741,2745`) → map เป็น `DI_NA`/`DI_NA64`; `DiskStatus` ต้องเรียก `CheckDiskStatus(i)` เอง (Init ไม่ตั้งให้ เหมือน GUI ที่ `DiskInfoDlgInit.cpp:446`) ไม่เช่นนั้น health = 0 เสมอ — วัดแล้ว health = 1 (good) ทั้งสองดิสก์หลังเพิ่ม; HDD ที่ไม่มี life ได้ `DI_NA` ถูกต้อง
5b. **ผลรีวิว (2026-10-12):** Reviewer2 (Codex) ไม่พบ defect; Reviewer1 (Opus) พบและผมยืนยันกับซอร์สแล้วแก้ทั้งหมด — F1 threshold caution (05/C5/C6/FF) ต้องตั้ง 1/1/1/10 เหมือน GUI ก่อน `CheckDiskStatus` ไม่เช่นนั้น HDD ที่มี reallocated sector รายงาน Good · F2 flag USB/RAID/CSMI ต้องเปิดตามค่าเริ่มต้นของ GUI · F3 `DebugPrint` เดิม (`UtilityFx.cpp:48-63`) **ลบไฟล์ `<host exe>.log`** ในการเรียกครั้งแรก — ฝั่ง DLL ใช้ `DebugPrint` แบบ no-op และ compile `UtilityFx.cpp` ด้วยการเปลี่ยนชื่อ (ไม่แก้ไฟล์เดิม) ทดสอบแล้วไฟล์ sentinel ข้าง host ไม่ถูกลบหลัง scan แบบ admin · F4 `access = FULL` ต้องมีข้อมูลกลับมาจริง (NVMe ตั้ง `IsSmartCorrect` ก่อนอ่าน) — **ยังไม่ได้ทดสอบกรณี NVMe อ่านล้มเหลว** · F5 `struct_size` รับ `>=` v1 เพื่อเพิ่มฟิลด์ท้าย struct ได้ · F6 ไม่มีการแยกกันระหว่าง handle (CoInitializeSecurity ระดับ process, ไฟล์ global) → ให้ serialize การ scan ทั้ง process
6b. **ผลเพิ่ม (2026-10-12) หลังเสียบ NVMe ภายนอก (Micron 3400 512 GB ใน enclosure Realtek RTL9210B, Windows เห็นเป็น PhysicalDrive2 / USB):** `di_scan` (admin) เจอ 4 ดิสก์ — NVMe ภายนอกอ่านได้ครบ: 34 °C, life 96%, health Good, 947 ชม., `source=21` (ทางอ่านผ่าน bridge) ขณะที่ storage query (`StorageDeviceTemperatureProperty`) ล้มด้วย error 87 ทั้งสิทธิ์ปกติและ admin และ WMI reliability counter คืน **0 °C (ค่าปลอม)** → ห้ามใช้ WMI เป็นทางสำรองโดยไม่กรอง 0; ส่วนนี้ปิดข้อกังวล F4 บางส่วน (ได้ตัวอย่าง NVMe ผ่าน bridge ที่อ่านสำเร็จ แต่กรณีอ่านล้มเหลวยังไม่ได้ทดสอบ) **อุณหภูมิ SMART เทียบ storage query พร้อมกัน:** TOSHIBA 45/45 · WD NVMe 51/51 · Seagate (SMART) 51 / WMI 51 — ตรงกัน ส่วนต่างเดิมในข้อ 6 มาจากเวลาที่วัด
6. **ข้อสังเกตที่ยังตีความไม่ได้ (ปิดแล้ว ดูข้อ 6b):** อุณหภูมิ SMART ของ TOSHIBA = 43–44 °C ขณะที่ storage query ก่อนหน้านี้ได้ 34–37 °C (วัดคนละเวลา) ต้องเทียบพร้อมกันกับ CrystalDiskInfo ก่อนสรุปว่าตัวไหนใกล้ความจริง
