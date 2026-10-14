# Handoff — เฟส 0: สำรวจการโหลดส่วนเสริมใน exe แบบ single-file (R-004)

จาก: Coding Owner (Claude Code) → ถึง: Writing Owner เฟส 1 · Reviewer1 · Reviewer2
Requirement: R-004 · วันที่ 2026-10-14
ประเภทงาน: spike ทิ้งได้ — ไม่แตะโค้ดผลิตภัณฑ์ของ SystemMonitor/DiskInfo และไม่ commit โค้ด spike (อยู่ใน scratchpad ของ session)

## คำถามที่ต้องตอบ (go/no-go)
exe ของ SystemMonitor เป็น framework-dependent **single-file** (`PublishSingleFile=true --self-contained false`) โหลดส่วนเสริม .NET DLL ที่วางไว้ข้างนอกตอนรันได้จริงหรือไม่ โดย (1) type ของสัญญา (contract) เป็นตัวเดียวกันทั้งสองฝั่ง (2) ส่วนเสริมเรียก native DLL (`DiskInfoCore.dll`) จากโฟลเดอร์ของตัวเองได้ (3) ตรวจ hash ก่อนโหลดได้ (4) ส่วนเสริมที่เสียไม่ทำให้โฮสต์ล้ม

## ผล: **GO** — ทำได้ทุกข้อด้วยสิ่งที่มากับ .NET (ไม่เพิ่ม package)

สร้างโปรเจกต์จำลอง 3 ชิ้นใน scratchpad (net8.0, SDK 10.0.401): `Contract` (interface `IExt`), `Ext` (ส่วนเสริมที่ P/Invoke `DiskInfoCore.dll`), `Host` (publish แบบเดียวกับ SystemMonitor; ทดสอบทั้งแบบ console เปล่าและแบบเปิด `UseWPF`+`UseWindowsForms` เหมือนโปรเจกต์จริง ผลตรงกัน)

| # | สถานการณ์ | ผลที่เห็นจริง |
|---|---|---|
| A | โหลดด้วย `LoadFromAssemblyPath` | สำเร็จ; `typeof(IExt).Assembly.Location` ของโฮสต์ว่าง (อยู่ใน bundle) อยู่ใน Default ALC |
| B | โหลดจาก byte[] ที่อ่านและ hash มาแล้ว (`LoadFromStream`) | สำเร็จ; `Assembly.Location` ของส่วนเสริมว่าง (ไม่มีไฟล์ให้สลับระหว่าง hash กับโหลด) |
| — | type identity | `Activator.CreateInstance(t) as IExt` ไม่เป็น null → ใช้ contract ตัวเดียวกัน เมื่อ `Load()` ของ ALC คืน null สำหรับ `Spike.Contract` |
| — | native DLL | `di_abi_version()` ได้ `0x00010000` (ABI 1.0), `di_open` = 0, `di_scan` = −2 (DI_E_ACCESS เพราะไม่ใช่ admin — ตรงกับที่ DiskInfoCore ควรตอบ) โหลดผ่าน `LoadUnmanagedDll` ของ ALC; imports ใน System32 ของ DLL หาเจอตามปกติ |
| C | hash ไม่ตรง | `REFUSED` ก่อนสร้าง ALC |
| D | ส่วนเสริมโยน exception | จับได้ โฮสต์ไม่ล้ม |
| E | unload | ALC แบบ `isCollectible: true` ถูก GC เก็บหลังไม่มีการอ้างอิง (alive=False) |
| F | ส่วนเสริมคอมไพล์กับ contract เวอร์ชัน 2.0.0.0 แต่โฮสต์มี 1.0.0.0 | `FileNotFoundException` ที่จับได้ ("Could not load file or assembly 'Spike.Contract, Version=2.0.0.0'") → ข้ามพร้อมข้อความได้ |

## ผลที่ต้องเอาไปออกแบบเฟส 1
1. **ตัวโหลด:** สร้าง `AssemblyLoadContext` ต่อส่วนเสริม (`isCollectible: true`) override `Load(AssemblyName)` ให้คืน `null` สำหรับ assembly ของ contract (เพื่อใช้ตัวที่ฝังใน exe) และโหลด dependency อื่นจากโฟลเดอร์ส่วนเสริมเท่านั้น; override `LoadUnmanagedDll` ให้โหลด native จากโฟลเดอร์ส่วนเสริม (`LoadUnmanagedDllFromPath`)
2. **ตรวจ hash แล้วโหลดจาก byte[] เดียวกัน** (`ReadAllBytes` → SHA-256 → `LoadFromStream`) แทนการ hash แล้วค่อย `LoadFromAssemblyPath` เพื่อกันการสลับไฟล์ระหว่างสองขั้น
3. **ช่องโหว่ที่ spike นี้ยังไม่ปิด:** native DLL (`DiskInfoCore.dll`) ยังโหลดจาก path หลังตรวจ hash — ต้อง hash ไฟล์ native ใน manifest เหมือน managed และตรวจก่อน `LoadUnmanagedDllFromPath` (ยังมีช่วงสลับไฟล์ที่เล็กมาก ยอมรับได้ภายใต้โมเดลความปลอดภัยที่ตกลง และบันทึกใน ADR)
4. **เวอร์ชัน contract:** ตั้ง `AssemblyVersion` ของ assembly contract เป็น `major.0.0.0` เท่านั้น (ไม่เปลี่ยนตาม minor/patch) ไม่งั้นส่วนเสริมที่ build กับ 1.1 จะโหลดกับโฮสต์ 1.0 ไม่ได้; ตรวจ contract version จาก manifest ก่อนโหลดเพื่อให้ข้อความอ่านง่ายกว่า exception ข้างบน
5. **ส่วนเสริมต้องไม่อ้างอิง WPF** ตามที่ออกแบบ (ส่งคำอธิบายหน้าจอแบบไม่ผูก UI) — spike ไม่ได้ทดสอบส่วนเสริมที่อ้าง WPF
6. **Unload:** ทำได้ แต่รอบแรกไม่ต้องใช้ (วางแล้วรันใหม่ตามเกณฑ์) — ถ้าจะทำ hot-unload ต้องระวังเธรดสแกนที่ยังถือ native handle

## ยังไม่ได้ตรวจ / ข้อจำกัดของ spike
- ไม่ได้ทดสอบกับ `SystemMonitor.exe` จริง (ทดสอบกับโฮสต์จำลองที่ publish ด้วยธงเดียวกัน) — ต้องตรวจซ้ำเมื่อเฟส 1 ทำตัวโหลดจริง
- ไม่ได้รันแบบ admin จึงไม่เห็นข้อมูลดิสก์ผ่านส่วนเสริม (เส้นทางเดียวกับ P/Invoke เดิมที่พิสูจน์แล้วในแอปจริงเมื่อ 2026-10-13)
- ไม่ได้ทดสอบการ publish แบบ self-contained หรือ `IncludeNativeLibrariesForSelfExtract` (ไม่ใช้ในโปรเจกต์นี้)
- ไม่ได้วัดเวลาโหลด/หน่วยความจำ
- ความเสี่ยงที่ยังอยู่: ส่วนเสริมรันใน process เดียวกับโฮสต์ — native crash ในส่วนเสริมฆ่าแอปทั้งตัว (ตามที่บันทึกไว้ใน R-004 ว่านอกขอบเขต)
