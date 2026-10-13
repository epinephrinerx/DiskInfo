# Handoff — เฟส 3: โมดูล SMART ใน SystemMonitor (R-002)

จาก: Coding Owner (Claude Code) → ถึง: Reviewer1 (Claude Opus 5.5 High) · Reviewer2 (Codex GPT-6.1)
Requirement: R-002 (DiskInfo; หลักอยู่ที่ไฟล์นี้ที่เดียว) · วันที่ 2026-10-13
Repo ที่แก้: `D:\Dev\Apps\SystemMonitor4.0` branch `agent/claude-sysmon-smart-module` (แตกต่อจาก `agent/claude-sysmon-smart-fallback`)

> **ข้อยกเว้นของ workflow:** agy (Writing Owner) ติดโควตา 429 อีก ~22 ชม. นับจาก 2026-10-13 (ตรวจจริงตอนเริ่มเฟส) ผู้ใช้สั่ง "ทำต่อเลยครับ" หลังผมแจ้งว่าต้องเขียนเองเป็นข้อยกเว้น (เหมือนเฟส 1 และ 2b) จึงให้ Coding Owner เขียนเฟสนี้ แล้วให้ Reviewer ทั้งสองตรวจ

## Objective
SystemMonitor มีโมดูล SMART ที่แสดงข้อมูลสุขภาพดิสก์จริงจาก `DiskInfoCore.dll` ในรูปแบบเดียวกับโมดูลอื่น: แท็บใน Full view (หนึ่งแท็บต่อดิสก์), section ใน Overall view, หน้าที่หมุนใน Widget, เปิด/ปิดได้ในแถบข้าง, ข้อความไทย/อังกฤษ, ค่าตั้งใน `config.wpf.json`

## การตัดสินใจออกแบบ (ให้ผู้ใช้ทักท้วงได้)
- **ปิดเป็นค่าเริ่มต้น** (`show_smart` = false): ต้องใช้ admin และคำสั่ง SMART อาจปลุก HDD ที่หลับ ถ้าเปิดไว้และสแกนทุก 2 นาทีจะกัน HDD ไม่ให้หยุดหมุน
- ใช้ส่วนประกอบเดิมทั้งหมด (`PlannedTab` + `DeviceFact`, `MeterRow`, `Head`) ไม่เพิ่ม XAML ใหม่ จึงตาม theme/ฟอนต์/ภาษาโดยอัตโนมัติ
- แท่ง = อายุที่เหลือ (life %) เมื่อดิสก์รายงาน; ดิสก์ที่ไม่มี (HDD) แสดงแท่งเต็มสีตามสุขภาพ และข้อความเป็นคำตัดสิน ("ดี"/"Good") ไม่ใช่ "100%"
- ไม่มีค่า = ไม่แสดง (ไม่เดา): อุณหภูมิ 0 ถือว่าไม่มีค่า (กฎเดียวกับ storage query) แต่ life 0% เป็นค่าจริง
- เมื่อไม่มีดิสก์ให้แสดง (ไม่ใช่ admin / ไม่มี DLL / รอสแกนแรก / สแกนล้ม) หน้า/แท็บ/section บอกเหตุผล ไม่วาดแท่งปลอม
- ไม่ยกระดับสิทธิ์เอง: ปุ่ม "รันใหม่แบบผู้ดูแลระบบ" เดิมในหน้าตั้งค่าคือทางเดียว

## Scope
`SysMonitor.Wpf`: `Model/SmartInfo.cs` (ใหม่) · `Model/Snapshot.cs` · `Native/SmartCore.cs` (ขยาย `SmartReading`) · `Sensors/SmartBoard.cs` (ใหม่) · `Sensors/Sampler.cs` · `AppConfig.cs` · `ViewModels/WidgetViewModel.cs` (partial) · `ViewModels/WidgetViewModel.Smart.cs` (ใหม่) · `MainWindow.xaml.cs` (Check ในแถบข้าง) · `I18n.cs` · `SysMonitor.Tests/SmartModuleTests.cs` (ใหม่)

## Constraints / Non-goals
- ไม่เพิ่ม package; ไม่แก้ MFC GUI; ไม่ทำกราฟประวัติ SMART, อีเมลแจ้งเตือน, AAM/APM, skin, ตาราง attribute ดิบ
- ไม่แก้ `DiskInfoCore` (ABI v1 เดิมพอ)

## ผลตรวจที่ Coding Owner ทำเอง (2026-10-13)
- `dotnet test SysMonitor.Tests` ผ่าน 394/394 (ฐานก่อนเฟสนี้ 336) และ `build-wpf.cmd` สำเร็จ รวม test ที่รัน `Sampler` จริง: เปิดโมดูลในสิทธิ์ปกติ → state `NeedAdmin`; ปิด → `Off`
- **รันแอปจริงแบบ admin (สคริปต์ยกระดับครั้งเดียวต่อรอบ ผู้ใช้อนุมัติ UAC; สคริปต์สำรองและคืน `config.wpf.json` เอง และเปิดด้วย `--allow-multiple --full/--expanded` เพื่อไม่แตะสำเนาที่ผู้ใช้เปิดอยู่)** แล้วจับภาพหน้าจอ: Full view (ไทย/สว่าง) เห็นแท็บ SMART 0–3 พร้อมข้อมูลจริง (TOSHIBA 40 °C 16,470 ชม. · WDS NVMe 48 °C · Micron 37 °C อายุเหลือ 96% · Seagate 44 °C) · Overall section SMART ครบ 4 ดิสก์ · Widget หมุนผ่านหน้า SMART Disk 3 → Disk 1 … · โหมดมืด + อังกฤษ + ฟอนต์ 140% แสดงถูกต้อง (บรรทัดรายละเอียดของ Overall ถูกตัดด้วย … ที่ฟอนต์ใหญ่ เหมือนแถวดิสก์ของโมดูลอื่น)
- **พบบั๊กจริงจากการรันแอป:** `DiskInfoCore.dll` ข้าง exe โหลดไม่ได้เพราะ search path ของ P/Invoke มีแค่ `ApplicationDirectory` (dependency ใน System32 หาไม่เจอ) → ทั้งโมดูลนี้และทางสำรองอุณหภูมิของเฟส 2b ไม่เคยทำงานจริง แก้เป็น `ApplicationDirectory | System32` + test ล็อก ตรวจซ้ำจนเห็นข้อมูลจริง และเห็นทางสำรองอุณหภูมิทำงาน (E:/F:/H: 45 °C, J: 37 °C หลังรอ ~90 วินาที เพราะค่าเข้าแถวดิสก์ตอนรอบอ่านอุณหภูมิถัดไป 30–60 วินาที)

## ผลรีวิว (2026-10-13)
- Reviewer2 (Codex GPT-6.1, read-only, ส่ง diff ทาง stdin) พบ 2 ข้อ: P1 หน้า Widget "สถานะ" กลายเป็นดิสก์ 1 ตัวพอดี จำนวนหน้าเท่าเดิมจึงไม่ rebuild (แก้: เทียบรายการหน้าไม่ใช่จำนวน + test); P2 test ที่รัน Sampler ตอนปิดโมดูลผ่านได้โดยไม่ได้ sample (แก้: ต้อง `Ready`)
- Reviewer1 (Opus 5.5 High) พบ 5 ข้อ ผมตรวจกับโค้ดแล้วแก้ทั้งหมด: (1) แท็บ SMART เปลี่ยน key (`smart` ↔ `smart<N>`) ทำให้ Full view เด้งไป CPU/ปล่อยว่าง → Reconcile ถือว่าแท็บ `smart*` ใดก็ได้ตรงกับที่ผู้ใช้เลือก (2) เปิดโมดูลแล้วเห็น "ไม่พบดิสก์ที่รายงาน SMART" ผิดเหตุผล → state Off ใช้ข้อความ "กำลังอ่าน" (3) เปิดโมดูลซ้ำแล้วเห็นดิสก์เก่าค้างจากครั้งก่อน → `RequestSmart` ล้างผลเดิม (4) status กับ readings เขียนแยกกัน อ่านได้คู่ไม่ตรงกัน → รวมเป็น record เดียวเขียนครั้งเดียว (5) test ที่ผ่านโดยไม่ได้ทดสอบจริง (เหมือน P2)
- test ที่รัน Sampler จริงของผมเองไม่เสถียร (รอ snapshot แรกได้ไม่ถึง 10 วินาทีตอนเครื่องหนัก) ปรับเป็นรอ 30 วินาที

## ยังไม่ได้ตรวจ (ให้ผู้ใช้ตรวจเอง)
- ปุ่มซ้าย/ขวา/หยุดของ Widget กับหน้า SMART (ผมเห็นหน้าหมุนเอง แต่ไม่ได้กดปุ่ม — โปรเซสยกระดับรับ input จากเชลล์ปกติไม่ได้)
- เทียบอุณหภูมิกับ CrystalDiskInfo ±2 °C (R-003)
- ติ๊ก/เลิกติ๊ก "แสดง SMART ของดิสก์" ด้วยมือแล้วค่าเก็บใน config (ผมเขียน config ตรง ไม่ได้กดช่องจริง)
- ผลของการสแกนทุก 2 นาทีต่อ HDD ที่หลับ

## Commit / diff
ดู branch ข้างบน; ผู้ใช้เป็นผู้สั่ง commit
