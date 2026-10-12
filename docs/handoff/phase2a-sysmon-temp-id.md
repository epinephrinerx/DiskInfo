# Handoff — เฟส 2a: แก้ property ID อุณหภูมิ disk ใน SystemMonitor

จาก: Coding Owner (Claude Code) → ถึง: Writing Owner (agy) · Reviewer1: Claude Opus 5.5 High · Reviewer2: Codex GPT-6.1-Lun High
Requirement: R-003 (DiskInfo) = R-019 (SystemMonitor) · วันที่ 2026-10-11 · ต้องให้ผู้ใช้รับรองแผนใหม่ก่อนเริ่ม (ดูหมายเหตุท้ายไฟล์)
Repo ที่แก้: `D:\Dev\Apps\SystemMonitor4.0` (ใช้ workflow/git rules ของ repo นั้น)

## Objective
ให้ `Win32.DriveTemperature` คืนอุณหภูมิจริงของ SATA HDD และ NVMe ในสิทธิ์ปกติ โดยแก้ค่า `StorageDeviceTemperatureProperty` จาก `22` เป็น `52`

## หลักฐาน (Coding Owner รันแล้ว 2026-10-11)
- `winioctl.h` (SDK 10.0.26100.0) บรรทัด 1168–1172: `StorageDeviceIoCapabilityProperty = 48`, 49 AdapterProtocolSpecific, 50 DeviceProtocolSpecific, 51 AdapterTemperature, **52 DeviceTemperature**; ไม่มี ID 22
- โพรบ P/Invoke เรียก `IOCTL_STORAGE_QUERY_PROPERTY` (0x2D1400), query 12 byte, buffer 256:
  - ID 22 → `ERROR_INVALID_FUNCTION (1)` ทุกดิสก์ ทั้งปกติและ admin
  - ID 52 สิทธิ์ปกติ → TOSHIBA MQ04ABF100 (SATA) 37 °C · WDS250G3X0C (NVMe) 42 °C · Seagate USB → `ERROR_IO_DEVICE (1117)` (admin ก็ 1117)
  - `bytes=40`, อุณหภูมิที่ offset 26 (int16) ตรงกับ `Get-StorageReliabilityCounter` (admin): 37 / 45 / 41 °C

## Acceptance criteria
- [ ] `Win32.cs:36` เป็น 52 พร้อมคอมเมนต์อ้างค่าใน `winioctl.h`
- [ ] test ของ parser มีครบอยู่แล้วใน `SysMonitor.Tests/StorageDescriptorTests.cs` (อย่าเขียนซ้ำ) ช่องว่างจริงคือไม่มี test ล็อกค่า ID: เปลี่ยน `StorageDeviceTemperatureProperty` เป็น `internal const` แล้วเพิ่ม test หนึ่งตัวใน `StorageDescriptorTests.cs` ยืนยันว่าเท่ากับ 52 (คอมเมนต์อ้าง `winioctl.h`)
- [ ] `dotnet test SysMonitor.Tests` ผ่านทั้งหมด (ฐานเดิม 312/312) และ `build-wpf.cmd` สำเร็จ
- [ ] รันแอปแบบไม่ใช่ admin: SATA HDD และ NVMe ขึ้นอุณหภูมิใน Widget/Overall/Full; USB ยังเป็น n/a (ปกติ — เฟส 2b)
- [ ] อัปเดตคอมเมนต์ `DriveTemperature` ที่บอกว่า "USB-bridged simply do not expose one" ให้ตรงผลจริง (error 1117 บนเครื่องพัฒนา)

## Scope
`SysMonitor.Wpf/Native/Win32.cs` · `SysMonitor.Tests/` (ไฟล์ test ใหม่สำหรับ `StorageDescriptors`) · ห้ามแก้ที่อื่น

## Constraints / Non-goals
- ไม่เพิ่ม package ไม่เพิ่มการยกระดับสิทธิ์ ไม่แตะ DiskInfo/DLL
- ไม่ทำทางสำรองของ USB, ตัวเลือก admin, หรือโมดูล SMART (เฟส 2b / R-002)
- ห้ามแสดงค่าประมาณสำหรับดิสก์ที่อ่านไม่ได้

## Commit / diff
ยังไม่มี ผู้ใช้เป็นผู้สั่ง commit

## Open questions / risks
- ผู้ใช้ยังไม่ได้รับรองแผนใหม่ (2a ก่อน DLL) — เกณฑ์ตรวจรับของ R-003 ยังเขียนตามแผนเดิม
- เทียบกับ CrystalDiskInfo ±2 °C ต้องให้ผู้ใช้ตรวจเอง
- เครื่องอื่นที่ firmware ไม่รองรับ descriptor นี้ยังได้ n/a — เป็นเหตุผลที่ยังต้องมีเฟส 2b
