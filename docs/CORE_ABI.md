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

1. `CAtaSmart::Init` ทำอะไรที่ต้อง COM/WMI และต้อง `CoInitialize` เองหรือไม่ — กระทบ thread ที่ SystemMonitor ใช้เรียก
2. โหมดไม่ใช้ admin ได้ดิสก์ไหนบ้าง (สมมติฐาน: NVMe ผ่าน storage query ได้ ส่วน SATA HDD ต้อง admin)
3. เวลา scan จริงบนเครื่อง 3 ดิสก์ (SATA HDD · USB HDD · NVMe)
4. DLL ต้องลิงก์ MFC แบบ static ได้หรือไม่ และขนาดไฟล์ที่ได้
5. จัดการ `DI_NA` ให้ตรงกับ `DiskStatus`/`Temperature` ที่ `CAtaSmart` คืน (ค่าเริ่มต้นเป็น 0)
