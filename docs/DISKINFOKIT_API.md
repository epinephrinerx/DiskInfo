# DiskInfoKit C API (v1.0)

สัญญา C API ของ `DiskInfoKit.dll` สำหรับอ่านข้อมูลดิสก์และตาราง S.M.A.R.T. attribute ตามที่แสดงบนหน้าต่างหลักของ CrystalDiskInfo ออกแบบด้วย C99 ล้วน เพื่อให้รองรับทั้ง Windows และแพลตฟอร์มอื่นในอนาคต (Linux/macOS ตาม R-018)

---

## กฎหลัก (Core Rules)

1. **Plain C99 header (`diskinfokit.h`)**:
   - ใช้เฉพาะ `<stdint.h>` และ `<stddef.h>`
   - ไม่มีชนิดข้อมูลเฉพาะของ Win32 (`DWORD`, `BOOL`, `WCHAR`, `HANDLE`), ไม่มี MFC, ไม่มี C++ types
   - ข้อความทั้งหมดเป็นสตริง UTF-8 ใน fixed buffer ที่ลงท้ายด้วย `\0` เสมอ และตัดที่รอยต่ออักขระ UTF-8 (`dicore::CopyUtf8`)
2. **Calling Convention (cdecl)**:
   - ทุกฟังก์ชัน API ประกาศด้วย `DIK_CALL` (`__cdecl` บน Windows, ว่างบนแพลตฟอร์มอื่น)
   - การเรียกแบบ P/Invoke ใน .NET / C# (โดยเฉพาะบน 32-bit x86) ต้องกำหนด `CallingConvention = CallingConvention.Cdecl`
3. **Struct Sizing, Explicit Layout & ABI Compatibility**:
   - ทุก out-struct (`dik_disk_t`, `dik_attribute_t`) มี `uint32_t struct_size` เป็นฟิลด์แรก
   - ผู้เรียกต้องกำหนด `struct_size = sizeof(...)` ก่อนส่งเข้ามา
   - DLL ปฏิเสธ struct ที่มีขนาดเล็กกว่า v1 ด้วย `DIK_E_STRUCT_SIZE` (`-6`)
   - DLL เขียนข้อมูลเท่ากับขนาดของ v1 และเขียนขนาดที่เติมจริงกลับลงใน `struct_size` (รองรับ caller ที่คอมไพล์ด้วย struct รุ่นใหม่กว่า)
   - **Explicit Layout**: โครงสร้างข้อมูลทุกตัวมีการจัดวางและใส่ padding ไบต์อย่างชัดเจน ไม่มี implicit padding บน ABI ใด ๆ (ทั้ง 32-bit และ 64-bit):
     - `sizeof(dik_disk_t) == 496` (พหุคูณของ 8)
     - `sizeof(dik_attribute_t) == 48` (พหุคูณของ 8, มี `reserved2[4]` ก่อน `raw_value` และ tail pad `reserved3[4]` หลัง `status`)
   - กฎการเปลี่ยนรุ่น: เพิ่มฟิลด์ต่อท้ายเท่านั้น (append-only), bump minor version เมื่อมีฟิลด์ใหม่
4. **Sentinels**:
   - ใช้ sentinel แทน 0 สำหรับค่าที่อุปกรณ์ไม่รายงาน:
     - 32-bit: `DIK_NA` (`INT32_MIN` = `-2147483648`)
     - 64-bit: `DIK_NA64` (`INT64_MIN` = `-9223372036854775808LL`)
5. **Snapshot Semantics & Lifetime**:
   - `dik_scan` ทำการ scan และคัดลอกข้อมูลทั้งหมด (รวม attribute status) เก็บไว้ใน snapshot ของ handle
   - เมื่อ `dik_scan` คืนค่า อ็อบเจกต์ `CAtaSmart` และ COM บน thread จะถูกทำลายทันที
   - ฟังก์ชันอ่านค่าหลังจากนั้น (`dik_disk_count`, `dik_disk`, `dik_attribute`) ทำงานจาก snapshot ในหน่วยความจำเท่านั้น ไม่แตะต้องฮาร์ดแวร์และไม่ต้องใช้ COM
6. **Threading & Concurrency**:
   - Handle 1 ตัวเรียกได้จากทีละ 1 thread (not thread-safe per handle)
   - `dik_scan` ใช้เวลานาน (ระดับวินาที) ห้ามเรียกจาก UI thread
   - เริ่มต้น COM บน calling thread ของตนเอง (บน Windows)
   - ไม่แยกขาดระหว่าง handle: เนื่องจาก `CAtaSmart::Init` มีการเรียก `CoInitializeSecurity` ระดับ process, อาจเริ่ม service WMI และใช้สถานะระดับไฟล์ ให้ serialize การ scan ทั่วทั้ง process
7. **สิทธิ์ (Elevation)**:
   - การอ่าน S.M.A.R.T. ต้องใช้สิทธิ์ Administrator บน Windows
   - หากรันโดยไม่มีสิทธิ์และไม่พบดิสก์ `dik_scan` คืนค่า `DIK_E_ACCESS` (`-2`)
8. **Read-only**:
   - การส่งคำสั่ง IOCTL ทั้งหมดเป็นการอ่านข้อมูลเท่านั้น ไม่มีการเขียนข้อมูลลงดิสก์

---

## เงื่อนไขของโปรแกรมที่โหลด DLL (Windows)

`.exe` ที่โหลด DiskInfoKit ต้องประกาศว่ารองรับ Windows 10 ใน application manifest:

```xml
<compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
  <application>
    <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/>
  </application>
</compatibility>
```

`CAtaSmart` เปิดการอ่าน NVMe (`m_bNVMeStorageQuery`) เมื่อ `IsWindowsVersionOrGreaterFx(10, 0)` เป็นจริง (`CrystalDiskInfo/AtaSmart.cpp:44`) ซึ่งใช้ `VerifyVersionInfoW` — Windows ตอบตามเวอร์ชันที่ manifest ของ `.exe` ประกาศ ถ้าไม่มี manifest จะได้ 6.2 (Windows 8) และ**ดิสก์ NVMe จะหายไปจากผลโดยไม่มี error** (ตรวจจริง 2026-10-31: probe ไม่มี manifest เห็น 2 ดิสก์ มี manifest เห็น 3) — app .NET (apphost) และ PowerShell มี manifest นี้อยู่แล้ว; `.exe` native ของเราเอง (เช่น TUI ของ R-019) ต้องฝังเอง

## รหัสข้อผิดพลาดและค่าคงที่ (Constants)

### Calling Convention Macro
```c
#if defined(_WIN32)
#define DIK_CALL __cdecl
#else
#define DIK_CALL
#endif
```

### รหัสข้อผิดพลาด (Error Codes)
```c
#define DIK_OK               0
#define DIK_E_ARG          (-1)  /* invalid argument / NULL pointer */
#define DIK_E_ACCESS       (-2)  /* administrator rights required */
#define DIK_E_NOT_FOUND    (-3)  /* disk or attribute index out of range */
#define DIK_E_IO           (-4)  /* COM or hardware I/O error */
#define DIK_E_UNSUPPORTED  (-5)  /* feature or interface not supported */
#define DIK_E_STRUCT_SIZE  (-6)  /* struct_size smaller than supported version */
```

### Flag สำหรับ `dik_open`
```c
#define DIK_OPEN_NO_WMI 0x00000001u  /* ข้ามการค้นหาดิสก์ผ่าน WMI/COM */
```

### Interface Kind (`interface_kind`)
ตรงตามลำดับ `CAtaSmart::INTERFACE_TYPE` (0..6):
```c
#define DIK_IF_UNKNOWN  0
#define DIK_IF_PATA     1
#define DIK_IF_SATA     2
#define DIK_IF_USB      3
#define DIK_IF_IEEE1394 4
#define DIK_IF_SCSI     5
#define DIK_IF_NVME     6
#define DIK_IF_OTHER    7
```

### Media (`media`)
```c
#define DIK_MEDIA_UNKNOWN 0
#define DIK_MEDIA_HDD     1
#define DIK_MEDIA_SSD     2
```

### Health Status (`health`)
ตรงตามลำดับ `CAtaSmart::DISK_STATUS`:
```c
#define DIK_HEALTH_UNKNOWN 0
#define DIK_HEALTH_GOOD    1
#define DIK_HEALTH_CAUTION 2
#define DIK_HEALTH_BAD     3
```

### Access Level (`access`)
```c
#define DIK_ACCESS_FULL    0u  /* อ่าน S.M.A.R.T. ได้สำเร็จและ checksum ถูกต้อง */
#define DIK_ACCESS_PARTIAL 1u  /* พบดิสก์ แต่ไม่มีข้อมูล S.M.A.R.T. หรือ checksum ผิดพลาด */
```

### Features Bitmask (`features`)
```c
#define DIK_FEAT_SMART      0x0001u  /* IsSmartSupported */
#define DIK_FEAT_APM        0x0002u  /* IsApmSupported */
#define DIK_FEAT_AAM        0x0004u  /* IsAamSupported */
#define DIK_FEAT_NCQ        0x0008u  /* IsNcqSupported */
#define DIK_FEAT_TRIM       0x0010u  /* IsTrimSupported */
#define DIK_FEAT_DEVSLEEP   0x0020u  /* IsDeviceSleepSupported */
#define DIK_FEAT_STREAMING  0x0040u  /* IsStreamingSupported */
#define DIK_FEAT_GPL        0x0080u  /* IsGplSupported */
#define DIK_FEAT_VWC        0x0100u  /* IsVolatileWriteCachePresent */
#define DIK_FEAT_LBA48      0x0200u  /* IsLba48Supported */
#define DIK_FEAT_NVCACHE    0x0400u  /* IsNvCacheSupported */
#define DIK_FEAT_APM_ON     0x0800u  /* IsApmEnabled */
#define DIK_FEAT_AAM_ON     0x1000u  /* IsAamEnabled */
```

### Attribute Health Status (`status`)
ผลลัพธ์จาก `dicore::AttributeStatus`:
```c
#define DIK_ATTR_STATUS_UNKNOWN 0
#define DIK_ATTR_STATUS_GOOD    1
#define DIK_ATTR_STATUS_CAUTION 2
#define DIK_ATTR_STATUS_BAD     3
```

---

## ฟังก์ชัน (Functions)

```c
uint32_t DIK_CALL dik_api_version(void);
```
- คืนค่า `(major << 16) | minor` สำหรับ v1.0 คือ `0x00010000`

```c
int DIK_CALL dik_open(dik_handle** out, uint32_t flags);
```
- สร้าง handle ใหม่สำหรับเรียกใช้งาน
- `flags`: บิตแมสก์ของ `DIK_OPEN_*` (เช่น `DIK_OPEN_NO_WMI`)
- คืนค่า `DIK_OK` (0) เมื่อสำเร็จ, `DIK_E_ARG` ถ้า `out == NULL`, หรือ `DIK_E_IO` ถ้าจัดสรรหน่วยความจำไม่สำเร็จ

```c
void DIK_CALL dik_close(dik_handle* h);
```
- คืนทรัพยากรและหน่วยความจำทั้งหมดของ handle
- ยอมรับ `NULL` ได้อย่างปลอดภัย

```c
int DIK_CALL dik_scan(dik_handle* h);
```
- สแกนดิสก์ทั้งหมดในระบบและอัปเดต snapshot ภายใน handle
- ทำงานช้า (หลายวินาที) ห้ามเรียกจาก UI thread
- เริ่มต้น COM บน calling thread ของตนเอง (บน Windows)
- ผู้เรียกต้อง serialize การ scan ทั่วทั้ง process เอง (DLL ไม่มี lock)
- คืนค่า:
  - `DIK_OK` (0) สำเร็จ
  - `DIK_E_ARG` ถ้า `h == NULL`
  - `DIK_E_ACCESS` (`-2`) ถ้าไม่มีสิทธิ์ผู้ดูแลระบบและไม่พบดิสก์
  - `DIK_E_IO` (`-4`) ถ้า COM ล้มเหลวหรือเกิดข้อยกเว้นระหว่าง scan

```c
int DIK_CALL dik_disk_count(const dik_handle* h);
```
- คืนจำนวนดิสก์ที่พบ (`>= 0`) หรือ `DIK_E_ARG` ถ้า `h == NULL`

```c
int DIK_CALL dik_disk(const dik_handle* h, int disk, dik_disk_t* out);
```
- คัดลอกข้อมูลดิสก์ลำดับที่ `disk` (0-based) ลงใน `out`
- คืนค่า:
  - `DIK_OK` (0)
  - `DIK_E_ARG` ถ้า `h == NULL` หรือ `out == NULL`
  - `DIK_E_STRUCT_SIZE` (`-6`) ถ้า `out->struct_size < sizeof(dik_disk_t)`
  - `DIK_E_NOT_FOUND` (`-3`) ถ้า index `disk` อยู่นอกช่วง `[0, count-1]`

```c
int DIK_CALL dik_attribute(const dik_handle* h, int disk, int row, dik_attribute_t* out);
```
- คัดลอกข้อมูล S.M.A.R.T. attribute แถวที่ `row` (0-based, อ้างอิงตามแถวที่กรองแล้ว) ของดิสก์ `disk`
- คืนค่า:
  - `DIK_OK` (0)
  - `DIK_E_ARG` ถ้า `h == NULL` หรือ `out == NULL`
  - `DIK_E_STRUCT_SIZE` (`-6`) ถ้า `out->struct_size < sizeof(dik_attribute_t)`
  - `DIK_E_NOT_FOUND` (`-3`) ถ้า index `disk` หรือ `row` อยู่นอกช่วง

---

## โครงสร้างข้อมูลและการจับคู่ฟิลด์ (Struct Mapping)

### `dik_disk_t` (ขนาด 496 ไบต์)

| Offset | ฟิลด์ | ชนิด | ที่มาใน `CAtaSmart::ATA_SMART_INFO` / โค้ด GUI | รายละเอียด |
|---|---|---|---|---|
| 0 | `struct_size` | `uint32_t` | — | ขนาด struct ผู้เรียกส่ง `sizeof(dik_disk_t)` (496) |
| 4 | `physical_drive_id` | `int32_t` | `PhysicalDriveId` | หมายเลข N ของ `\\.\PhysicalDriveN` |
| 8 | `model` | `char[96]` | `Model` | รุ่นดิสก์ (UTF-8) |
| 104 | `serial` | `char[48]` | `SerialNumber` | หมายเลขประจำเครื่อง (UTF-8) |
| 152 | `firmware` | `char[24]` | `FirmwareRev` | เวอร์ชันเฟิร์มแวร์ (UTF-8) |
| 176 | `interface_name` | `char[32]` | `Interface` | ชื่ออินเทอร์เฟซ เช่น `"Serial ATA"`, `"NVM Express"` |
| 208 | `transfer_current` | `char[32]` | `CurrentTransferMode` | โหมดรับส่งปัจจุบัน เช่น `"SATA/600"`, `"PCIe 4.0 x4"` |
| 240 | `transfer_max` | `char[32]` | `MaxTransferMode` | โหมดรับส่งสูงสุดที่รองรับ เช่น `"SATA/600"`, `"PCIe 4.0 x4"` |
| 272 | `standard` | `char[64]` | `MajorVersion` + `MinorVersion` | ตาม GUI (`DiskInfoDlgUpdate.cpp:1557-1564`): `MajorVersion` (` + " \| " + MinorVersion` ถ้าไม่ว่าง) |
| 336 | `drive_letters` | `char[32]` | `DriveMap` | ตัวอักษรไดรฟ์ เช่น `"C: D:"` |
| 368 | `smart_key` | `char[32]` | `SmartKeyName` | คีย์สำหรับเลือกตารางชื่อ attribute เช่น `"Smart"`, `"SmartNVMe"` |
| 400 | `interface_kind` | `int32_t` | `InterfaceType` | แมป 0..6 เป็น `DIK_IF_*`, นอกเหนือจากนั้นเป็น `DIK_IF_OTHER` |
| 404 | `media` | `int32_t` | `IsSsd` | `IsSsd ? DIK_MEDIA_SSD : DIK_MEDIA_HDD` |
| 408 | `health` | `int32_t` | `DiskStatus` | ระดับสุขภาพ (`DIK_HEALTH_*`: 0 unknown, 1 good, 2 caution, 3 bad) |
| 412 | `temperature_c` | `int32_t` | `Temperature` | อุณหภูมิเป็นองศาเซลเซียส (`-1000` แมปเป็น `DIK_NA`) |
| 416 | `life_percent` | `int32_t` | `Life` | เปอร์เซ็นต์อายุที่เหลือ (`< 0` แมปเป็น `DIK_NA`) |
| 420 | `rotation_rpm` | `int32_t` | `NominalMediaRotationRate` | RPM ถ้า `> 1`; หาก `= 1` (SSD) หรือ `0` แมปเป็น `DIK_NA` (`DiskInfoDlgUpdate.cpp:1494-1506`) |
| 424 | `power_on_hours` | `int64_t` | `DetectedPowerOnHours` | ชั่วโมงเปิดเครื่อง (`< 0` แมปเป็น `DIK_NA64`) |
| 432 | `power_on_count` | `int64_t` | `PowerOnCount` | จำนวนครั้งเปิดเครื่อง (`<= 0` แมปเป็น `DIK_NA64`, `DiskInfoDlgUpdate.cpp:1261`) |
| 440 | `capacity_mb` | `int64_t` | `TotalDiskSize` | ความจุ decimal MB ตามที่ GUI แสดง (`TotalDiskSize`); คืน `DIK_NA64` ถ้า `<= 0` (`DiskInfoDlgUpdate.cpp:1533-1544`) |
| 448 | `capacity_bytes` | `int64_t` | `NumberOfSectors * LogicalSectorSize` | ความจุจริงเป็นไบต์บน ATA path (`AtaSmart.cpp:3191, 3195, 3205`); บน NVMe path คืน `DIK_NA64` เนื่องจาก `NumberOfSectors` ไม่ได้ถูกกำหนด (เป็น 0) |
| 456 | `host_reads_gb` | `int64_t` | `HostReads` | จำนวนอ่านสะสมเป็น GB แบบ 2^30 ไบต์ (`CAtaSmart` แปลงเป็น GB ก่อนเก็บ; คืน `DIK_NA64` ถ้าอุปกรณ์ไม่รายงาน) |
| 464 | `host_writes_gb` | `int64_t` | `HostWrites` | จำนวนเขียนสะสมเป็น GB แบบ 2^30 ไบต์ (`CAtaSmart` แปลงเป็น GB ก่อนเก็บ; คืน `DIK_NA64` ถ้าอุปกรณ์ไม่รายงาน) |
| 472 | `nand_writes_gb` | `int64_t` | `NandWrites` | จำนวนเขียน NAND สะสมเป็น GB แบบ 2^30 ไบต์ (`CAtaSmart` แปลงเป็น GB ก่อนเก็บ; คืน `DIK_NA64` ถ้าอุปกรณ์ไม่รายงาน) |
| 480 | `features` | `uint32_t` | `Is*Supported`, `Is*Enabled`, `Is*Present` | บิตแมสก์ของฟีเจอร์ (`DIK_FEAT_*`) |
| 484 | `access` | `uint32_t` | `IsSmartCorrect && !IsCheckSumError && gotData` | `DIK_ACCESS_FULL` หรือ `DIK_ACCESS_PARTIAL` |
| 488 | `source` | `uint32_t` | `CommandType` | ชนิดคำสั่ง IOCTL/driver ที่ใช้เชื่อมต่อ |
| 492 | `attribute_count`| `int32_t` | — | จำนวนแถว attribute หลังกรอง ID 0x00 และ 0xFF ออกแล้ว |

---

### `dik_attribute_t` (ขนาด 48 ไบต์)

| Offset | ฟิลด์ | ชนิด | ที่มา / ตรรกะ | รายละเอียด |
|---|---|---|---|---|
| 0 | `struct_size` | `uint32_t` | — | ขนาด struct ผู้เรียกส่ง `sizeof(dik_attribute_t)` (48) |
| 4 | `id` | `uint8_t` | `Attribute[j].Id` | รหัส Attribute ID (ข้าม 0x00 และ 0xFF เสมอ) |
| 5 | `raw_len` | `uint8_t` | ตามชนิดดิสก์ | จำนวนไบต์ที่ถูกต้องใน `raw[]`: 6 (ATA ทั่วไป), 7 (SandForce / NVMe), 8 (JMicron IsRawValues8 / Indilinx) |
| 6 | `reserved` | `uint8_t[2]` | — | ไบต์สำรองสำหรับ alignment (ค่า 0) |
| 8 | `current` | `int32_t` | `Attribute[j].CurrentValue` | ค่า Current Value (คืน `DIK_NA` สำหรับ NVMe และ Indilinx) |
| 12 | `worst` | `int32_t` | `Attribute[j].WorstValue` | ค่า Worst Value (คืน `DIK_NA` สำหรับ NVMe, Indilinx, JMicron IsRawValues8) |
| 16 | `threshold` | `int32_t` | `Threshold[j].ThresholdValue` | ค่า Threshold (`DIK_NA` ถ้า `!IsThresholdCorrect` หรือ NVMe/Indilinx/JMicron8) |
| 20 | `raw` | `uint8_t[8]` | `RawValue`, `Reserved`, `WorstValue`, `CurrentValue` | ไบต์แบบ little-endian ตามที่ Crystal จัดเก็บและแสดงผลใน `UpdateListCtrl` |
| 28 | `reserved2` | `uint8_t[4]` | — | ไบต์สำรอง padding ชัดเจนก่อน `raw_value` (ค่า 0) เพื่อป้องกัน implicit padding |
| 32 | `raw_value` | `uint64_t` | little-endian integer | รวมค่า `raw[0..raw_len-1]` เป็นจำนวนเต็ม 64 บิต |
| 40 | `status` | `int32_t` | `dicore::AttributeStatus` | สถานะสุขภาพของ attribute (`DIK_ATTR_STATUS_*`): 0 unknown, 1 good, 2 caution, 3 bad |
| 44 | `reserved3` | `uint8_t[4]` | — | ไบต์สำรอง tail padding ชัดเจน (ค่า 0) ให้ struct ครบ 48 ไบต์บนทุกสถาปัตยกรรม |

#### การจัดเรียงไบต์ใน `raw[]` และ `raw_value`
- **ATA มาตรฐาน (raw_len = 6)**:
  `raw[0..5] = RawValue[0..5]`, `raw[6..7] = 0`
- **SandForce (raw_len = 7)**:
  `raw[0..5] = RawValue[0..5]`, `raw[6] = Reserved`, `raw[7] = 0`
- **JMicron RawValues8 (raw_len = 8)**:
  `raw[0] = WorstValue`, `raw[1..6] = RawValue[0..5]`, `raw[7] = Reserved`
- **Indilinx (raw_len = 8)**:
  `raw[0] = CurrentValue`, `raw[1] = WorstValue`, `raw[2..7] = RawValue[0..5]`
- **NVMe (raw_len = 7)**:
  `raw[0..5] = RawValue[0..5]`, `raw[6] = Reserved`, `raw[7] = 0`

---

## การค้นหาชื่อ Attribute (Attribute Name Lookup)

ชื่อ Attribute ไม่ได้ถูกส่งผ่าน C API นี้ เนื่องจาก CrystalDiskInfo อ่านชื่อภาษาจากไฟล์การแปลภาษาภายนอกตามหมวดหมู่:
- คีย์สำหรับค้นหาคือ: `[smart_key]` + `[id ในรูปเลขฐานสิบหก 2 หลัก เช่น "05", "C5"]`
- ตัวอย่าง: ไดรฟ์รายงาน `smart_key = "SmartNVMe"` และ attribute `id = 0x01` ให้ค้นหา section `[SmartNVMe]` key `01` ในตารางภาษาของแอปพลิเคชัน
