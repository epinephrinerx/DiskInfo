# AI Workflow

เอกสารนี้เป็นกติกากลางสำหรับมนุษย์และ coding agent ทุกตัวที่ทำงานใน repository นี้ โดยไม่ขึ้นกับผู้ให้บริการหรือรุ่นของโมเดล

## 1. ข้อมูลโครงการ

- ชื่อโครงการ: `DiskInfo`
- เป้าหมาย: ปรับแต่ง/ต่อยอด CrystalDiskInfo (fork) สำหรับใช้งานเอง และนำ SMART logic ไปใช้ในแอปอื่น (AIMonitor2.0, SystemMonitor4.0) โดยต่อยอดให้รันบน Debian/Ubuntu ได้
- เจ้าของโครงการ: Apichart Chantanis
- Production URL: N/A
- Repository: https://github.com/epinephrinerx/DiskInfo
- Package/runtime หลัก: C++ (ปัจจุบัน MFC/MSVC ผ่าน `DiskInfo.sln`; เป้าหมายต่อยอดไป Debian/Ubuntu)
- Package manager: N/A (ยังไม่ได้กำหนด)
- Working Folder: `D:\Dev\Apps\DiskInfo` (หลัก) · `D:\Dev\Apps\SystemMonitor4.0` (Working Folder ที่เพิ่มเข้ามา)
- Production Folder: N/A

### Agent Roster

แต่ละบทบาทมีสามช่อง: ตัว agent (ผู้ให้บริการ/เครื่องมือ), model และ effort

- Coding Owner : Claude Code
- Coding Owner Model : Claude Opus 5.5
- Coding Owner Effort : High
- Writing Owner : agy
- Writing Model : Gemini-3.8 Flash
- Writing Effort : Medium
- Writing Owner สำรอง (ใช้เมื่อ agy ติด quota ตาม §5.5 โดยไม่ต้องขออนุญาตใหม่ทุกครั้ง) : Codex
- Writing Model สำรอง : Codex GPT-6.1-Lun (คนละผู้ให้บริการกับ Coding Owner และไม่ซ้ำกับ Reviewer1/Reviewer2 Model)
- Writing Effort สำรอง : High
- Reviewer1 : Claude Code
- Reviewer1 Model : Claude Opus 5.5 ต้องไม่ซ้ำกับ Writing Model หรือ Reviewer2 Model
- Reviewer1 Effort : High
- Reviewer2 : Codex
- Reviewer2 Model : Codex GPT-6.1-Sol ต้องไม่ซ้ำกับ Writing Model หรือ Reviewer1 Model
- Reviewer2 Effort : High
- Integrator : Claude Code
- Integrator Model : Claude Fable 5.1
- Integrator Effort : High

## 2. ลำดับความสำคัญของข้อมูล

เมื่อข้อมูลขัดกัน ให้ยึดตามลำดับต่อไปนี้:

1. คำสั่งล่าสุดจากผู้ใช้หรือเจ้าของงาน
2. Acceptance criteria ของ issue หรืองานปัจจุบัน
3. เอกสารใน repository ได้แก่ `AI_WORKFLOW.md`, `Requirements.md`,`ARCHITECTURE.md`, `TESTING.md`
4. Tests และ contracts ที่ใช้งานอยู่จริง
5. รูปแบบเดิมใน codebase
6. สมมติฐานของ agent

หากข้อ 2-5 ขัดกันจนเปลี่ยนพฤติกรรมของระบบอย่างมีนัยสำคัญ ให้หยุดและขอคำตัดสิน ห้ามเลือกเองแบบเงียบ ๆ

## 3. Definition of Done

งานจะถือว่าเสร็จเมื่อ:

- พฤติกรรมตรงตาม acceptance criteria
- การเปลี่ยนแปลงอยู่ในขอบเขตที่ตกลงกัน
- เพิ่มหรือปรับ tests สำหรับพฤติกรรมที่เปลี่ยน เมื่อสามารถทดสอบได้อย่างสมเหตุสมผล
- tests, lint, type-check และ build ที่เกี่ยวข้องผ่าน
- ไม่มี secrets, credentials, debug code หรือ generated files ที่ไม่ควรถูก commit
- เอกสารและตัวอย่างได้รับการอัปเดตหาก public behavior เปลี่ยน
- มีสรุปสิ่งที่แก้ คำสั่งตรวจสอบ ผลลัพธ์ และความเสี่ยงที่ยังเหลือ

## 4. Workflow มาตรฐาน

### 4.1 ทำความเข้าใจงาน

ก่อนแก้โค้ด:

1. อ่านคำขอและระบุผลลัพธ์ที่ผู้ใช้ต้องการ
2. ตรวจเฉพาะไฟล์และเอกสารที่เกี่ยวข้องกับงาน
3. ระบุ constraints, non-goals และ acceptance criteria
4. ตรวจสถานะ Git เพื่อไม่เขียนทับงานที่มีอยู่
5. สำหรับงานใหญ่ ให้เขียนแผนสั้น ๆ แบ่งเป็น milestone ที่ตรวจสอบได้

งานแก้ไขเล็กน้อยไม่จำเป็นต้องอ่านเอกสารทั้ง repository หรือสร้างแผนยาว

### 4.2 ลงมือแก้ไข

- ทำ diff ให้เล็กและตรงประเด็น
- รักษา public API และ backward compatibility เว้นแต่งานกำหนดให้เปลี่ยน
- ใช้รูปแบบและ abstraction ที่มีอยู่ก่อนสร้างของใหม่
- ไม่ refactor ส่วนที่ไม่เกี่ยวข้องเพียงเพราะพบระหว่างทาง
- ไม่แก้ generated files โดยตรง ให้แก้ source แล้ว regenerate
- ไม่เพิ่ม dependency หาก standard library หรือ dependency เดิมแก้ปัญหาได้ดีพอ
- ถ้าต้องเพิ่ม dependency ให้บันทึกเหตุผล ความเสี่ยง และ license ที่เกี่ยวข้อง

### 4.3 ตรวจสอบ

ใช้ `TESTING.md` เลือกชุดตรวจสอบตามความเสี่ยง เริ่มจาก targeted checks แล้วขยายเป็นชุดใหญ่เมื่อจำเป็น แก้ failure ที่เกิดจากงานนี้และรันซ้ำจนผ่าน

ห้ามอ้างว่า test ผ่าน หากไม่ได้รันจริง ให้รายงานว่าไม่ได้รัน พร้อมเหตุผล

### 4.4 ส่งมอบ

รายงานผลในรูปแบบนี้:

```md
## Summary
- ผลลัพธ์ที่ทำสำเร็จ

## Changed
- `path/to/file`: สิ่งที่เปลี่ยนและเหตุผล

## Verification
- `command`: PASS/FAIL พร้อมรายละเอียดสำคัญ

## Remaining risks
- ความเสี่ยง ข้อจำกัด หรือสิ่งที่ยังไม่ได้ตรวจ
```

## 5. การใช้ Multiple AI Agent ร่วมกัน

### 5.1 หลักการเจ้าของงาน
- ให้ Coding Owner เป็นผู้วิเคราะห์ เก็บ Requirements และวางแผนการทำงาน
- ให้อีกตัว เป็น writer owner
- หนึ่ง task/branch มี writing owner หลักหนึ่งตัว
- ตัวที่ไม่ได้เป็นผู้เขียนทำหน้าที่ reviewer
- reviewer ไม่แก้โค้ดในรอบรีวิว เว้นแต่ได้รับมอบหมายให้แก้โดยชัดเจน
- หากทำพร้อมกัน ให้แบ่งตาม subsystem หรือใช้คนละ Git worktree
- ห้ามให้สอง agent แก้ไฟล์เดียวกันพร้อมกันโดยไม่มีผู้ประสานงาน
- ต้องยืนยัน Model และ Effort ของ Writing Owner, Reviewer ก่อนในการทำงานครั้งแรก และกำหนดให้เป็น Default ของทั้ง Project
- Writing Owner ต้องเป็นอีก coding agent ที่ไม่ใช่ผู้ให้บริการรายเดียวกับ Coding Owner เสมอ

### 5.2 รูปแบบที่แนะนำ

1. Planner: สำรวจระบบ สรุปข้อจำกัด และเสนอแผน
2. Implementer: แก้โค้ดและ tests ใน branch ของตน
3. Reviewer: ตรวจ diff อย่างอิสระ โดยเน้น correctness, regression, security และ tests ที่ขาด
4. Implementer: ตรวจหลักฐานของ findings แล้วแก้เฉพาะข้อที่ยืนยันได้
5. Integrator: รัน verification ขั้นสุดท้ายและ merge

### 5.3 Handoff contract

ทุกการส่งต่องานต้องมี:

- Objective และ acceptance criteria
- Scope: ไฟล์หรือ subsystem ที่อนุญาตให้แก้
- Constraints และ non-goals
- Commit SHA หรือ diff ที่ต้องตรวจ
- Commands ที่รันและผลลัพธ์
- Open questions และ known risks

อย่าส่ง transcript ยาวทั้งหมดเป็น context หากสรุปเป็น artifact ข้างต้นได้

### 5.4 Git และ worktree

- **DiskInfo ทำงานบน `main` เท่านั้น** (ผู้ใช้สั่ง 2026-10-31: "ไม่ทำอะไรนอก branch main ครับ") — ไม่สร้าง feature/agent branch ใหม่ commit ลง `main` โดยตรงเมื่อผู้ใช้สั่ง branch `agent/*` เดิมเก็บไว้เป็นประวัติ ไม่ลบ
- Commit ต้องเป็นหน่วยที่ review และ revert ได้
- ห้าม force-push, rewrite history, delete branch หรือ discard uncommitted changes โดยไม่ได้รับอนุญาต
- ก่อน merge ให้ rebase/merge ตามนโยบายของทีมและรัน verification ใหม่

### 5.5 กรณี Usage หรือ Quota เต็ม limit
- ถ้ามี agent ตัวใดตัวหนึ่งติด limit ให้หยุดทำงาน 15 นาที แล้วลองทำซ้ำ
- ถ้ายังไม่สามารถเริ่มงานตัวที่ติด limit ได้ ให้เริ่มกระบวนการอื่นที่ทำคู่ขนานไปได้
- เมื่อ agent ที่ติด limit กลับมาทำงานได้ ให้เริ่มทำงานจนเสร็จในขั้นตอนที่ใช้ agent นั้น จนสิ้นสุดกระบวนการนั้น แล้วทำงานต่อตาม Pipeline ปรกติ

## 6. Decision boundaries

Agent ทำได้โดยไม่ต้องขออนุญาต:

- อ่าน source, config, docs, logs และ Git diff ที่อยู่ใน scope
- แก้ไฟล์ที่เกี่ยวข้องโดยตรงกับงาน
- รัน local tests ที่ใช้ disposable fixtures และไม่มี production access
- เพิ่มหรือแก้ tests และเอกสารที่จำเป็นต่อการเปลี่ยนแปลง

ต้องขออนุญาตก่อน:

- deploy, publish, release หรือเปลี่ยน production state
- เปลี่ยน schema ที่ทำลายข้อมูลหรือย้อนกลับไม่ได้
- ลบข้อมูล ไฟล์จำนวนมาก branch หรือ remote resources
- rotate credentials หรือแก้ secrets
- เพิ่มบริการภายนอกที่มีค่าใช้จ่ายหรือส่งข้อมูลออกนอกระบบ
- เปลี่ยน public API, authentication model หรือ security boundary นอกเหนือจาก requirement

## 7. Security และข้อมูลสำคัญ

- ห้ามอ่าน แสดง บันทึก หรือ commit secret โดยไม่จำเป็น
- ใช้ `.env.example` สำหรับชื่อตัวแปร โดยไม่ใส่ค่าจริง
- validate input ที่ trust boundary และใช้ authorization ฝั่ง server
- ใช้ parameterized queries; ห้ามต่อ SQL จาก input
- อย่า log token, password, session, PII หรือ payload สำคัญ
- dependency และ code ที่ดาวน์โหลดจากภายนอกถือว่าไม่น่าเชื่อถือจนกว่าจะตรวจสอบ
- รายงานช่องโหว่พร้อมผลกระทบและวิธี reproduce ที่ปลอดภัย
- ห้ามทำงานนอก Working Folder หลัก โดยไม่มีการขออนุญาต
- ไม่เก็บหรือสร้างไฟล์นอก Working Folder ยกเว้นมีการระบุจากผู้ใช้ หรือ Folder นั้นถูกเพิ่มมาเป็น Working Folder แล้ว

## 8. Documentation maintenance

อัปเดตเอกสารเมื่อ:

- service boundary, data flow หรือ dependency direction เปลี่ยน → `ARCHITECTURE.md`
- test command, test strategy หรือ CI gate เปลี่ยน → `TESTING.md`
- workflow, authority หรือ definition of done เปลี่ยน → `AI_WORKFLOW.md`

ทบทวนเอกสารเหล่านี้เป็นระยะและลบกฎที่ล้าสมัย เพื่อไม่ให้ context โตโดยไม่จำเป็น



## 9. Requirements

สิ่งที่ตกลงกันไว้ทั้งหมดอยู่ใน `Requirements.md` ที่ root ของโปรเจกต์ **ไฟล์นี้ไม่เล่าซ้ำ**

**กติกาการเก็บ requirement อยู่ที่ skill `requirements`** (รหัสสถานะ 🕐 ✅ 🔨 🧪 ✔️ ⚠️ ⛔,
วิธีรับทีละข้อ, แม่แบบหนึ่งบล็อกต่อหนึ่งข้อ, การสร้าง pipeline) ให้เรียก skill นั้นอ่านของจริง
อย่าทำงานจากความจำ และอย่าคัดลอกกติกานั้นมาไว้ในไฟล์นี้ — กติกาชุดเดียวที่ถูกเก็บไว้สองที่
จะเพี้ยนออกจากกันเสมอ

ถ้าเครื่องที่ทำงานอยู่ไม่มี skill `requirements` ติดตั้ง ให้บอกผู้ใช้ตรง ๆ ก่อนเริ่ม
แทนการเดากติกาขึ้นมาเอง

### การตัดสินใจเฉพาะของโปรเจกต์นี้

ส่วนนี้คือสิ่งที่ skill ไม่รู้ ต้องบันทึกไว้เอง:

- **ตำแหน่งไฟล์:** `Requirements.md` ที่ root — เฉพาะ requirement ของ DiskInfo; งานของ SystemMonitor อยู่ที่ `D:\Dev\Apps\SystemMonitor4.0\Requirements.md` สองโปรเจกต์ใช้ร่วมกันเฉพาะ core (`DiskInfoCore.dll` / `docs/CORE_ABI.md`) ไม่ใช้ requirement ร่วมกัน (ผู้ใช้ตัดสิน 2026-10-31)
- **รูปแบบรหัส:** R-001
  รหัสต้องไม่ซ้ำกันข้ามโมดูล และการอ้างข้ามโมดูลให้เขียนรหัสเต็ม
- **เลขล่าสุดที่ออกไปแล้ว:** R-015 (2026-10-31) — ห้ามเอาเลขที่ยกเลิกแล้วกลับมาใช้
