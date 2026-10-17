# Handoff — เฟส 1: สัญญาส่วนเสริม + manifest + ตัวโหลด + โมเดลความเชื่อถือ (R-004)

จาก: Coding Owner (Claude Code) → ถึง: Writing Owner (agy, Gemini-3.8 Flash Medium) · Reviewer1: Claude Opus 5.5 High · Reviewer2: Codex GPT-6.1 High
Requirement: R-004 (DiskInfo `Requirements.md`; ตัวหลักอยู่ที่นั่น) · วันที่ 2026-10-14
Repo ที่แก้: `D:\Dev\Apps\SystemMonitor4.0` branch `agent/agy-sysmon-ext-loader` (แตกต่อจาก `agent/claude-sysmon-smart-module`)
อ่านก่อนเขียน: `D:\Dev\Apps\DiskInfo\docs\handoff\phase0-extension-spike.md` (ผล spike ที่พิสูจน์แล้วว่าทำอย่างไรได้)

## Objective
สร้างตัวโหลดส่วนเสริม .NET DLL ที่ปลอดภัยตามโมเดลที่ผู้ใช้อนุมัติ พร้อม test ครบ **ยังไม่ผูกกับ UI ของโฮสต์** (เฟส 2) และ **ยังไม่ย้าย SMART** (เฟส 3) ผลคือชุดคลาสที่ "ให้โฟลเดอร์ + ตัวเลือก → ได้รายการส่วนเสริมที่โหลดแล้ว หรือถูกข้ามพร้อมเหตุผล"

## โมเดลความเชื่อถือที่ตกลงแล้ว (ห้ามเปลี่ยนเอง)
- โหลดเฉพาะจาก `<โฟลเดอร์ exe>\extensions\<id>\` โฟลเดอร์ละหนึ่งส่วนเสริม ชื่อโฟลเดอร์ต้องเท่ากับ `id` ใน manifest
- **ชั้น 1 (first-party):** exe ฝังรายการ SHA-256 ของ `extension.json` ของส่วนเสริมที่เราส่งมา (embedded resource ชื่อ `first-party-extensions.json`; ในเฟสนี้ว่าง/ไม่มีไฟล์ = รายการว่าง; เฟส 4 จะสร้างตอน build)
- **โหมด admin (โฮสต์ยกระดับ):** โหลดเฉพาะส่วนเสริมที่ hash ของ manifest อยู่ในรายการ first-party เท่านั้น ส่วนเสริมอื่น (แม้เคยอนุมัติ) → ข้ามด้วยสถานะ `NotTrustedWhenElevated`
- **โหมดปกติ:** โหลดส่วนเสริม first-party หรือส่วนเสริมที่ผู้ใช้อนุมัติแล้ว (hash ของ manifest อยู่ใน approval store) ที่เหลือ → `NotApproved`
- **ชั้น 2 (approval store):** ไฟล์ `%ProgramData%\SystemMonitor\approved-extensions.json` ต้องมี ACL ที่ผู้ใช้ปกติเขียนไม่ได้ (เฉพาะ Administrators/SYSTEM มีสิทธิ์เขียน) ถ้า ACL ไม่เป็นไปตามนั้น ให้ถือว่า store ว่างและรายงานเหตุผล (ไม่เชื่อ store ที่ใครก็เขียนได้)
- manifest ระบุ SHA-256 ของ **ทุกไฟล์** ของส่วนเสริม (managed และ native); ตัวโหลดตรวจทุกไฟล์ก่อนโหลดอะไรก็ตาม; ไฟล์ที่ไม่อยู่ใน manifest ไม่ถูกโหลดเลย (ทั้ง managed dependency และ native)
- ไม่ยกระดับสิทธิ์เอง ไม่มีการดาวน์โหลดอะไรจากเน็ต

## ข้อกำหนดเชิงเทคนิค
**โปรเจกต์ใหม่ `SysMonitor.Extensibility`** (ที่ root ข้าง `SysMonitor.Wpf`; `net8.0` ล้วน ไม่มี WPF ไม่มี package) เก็บเฉพาะสิ่งที่ผู้เขียนส่วนเสริมต้องอ้างอิง:
- `ContractInfo` — `Version` ปัจจุบัน (1.0) และ `AssemblyVersion` ของ assembly **ต้องตั้งเป็น `1.0.0.0` คงที่ใน csproj** (เปลี่ยนเฉพาะ major; ดู spike ข้อ 4)
- `IExtension` — `void Initialize(IExtensionContext context)` และ `void Shutdown()`
- `IExtensionContext` — `string ExtensionDirectory`, `string Language` ("th"/"en"), `bool HostIsElevated`, `Version HostContractVersion`, `void Log(string message)`
- ห้ามใส่ type ที่เกี่ยวกับหน้าจอ/ข้อมูลดิสก์ในเฟสนี้ (เฟส 2–3 จะเพิ่มลง contract; ตอนนี้ contract ยังเป็นร่างจนถึงเฟส 5)

**ตัวโหลด** อยู่ใน `SysMonitor.Wpf/Extensions/` (namespace `SysMonitor.Extensions`) โปรเจกต์ Wpf อ้างอิง `SysMonitor.Extensibility` (ProjectReference) และ publish แบบ single-file ต้องยังทำงาน (assembly contract ถูกฝังใน exe):
- `ExtensionManifest` — อ่านจาก `extension.json` ด้วย `System.Text.Json` (snake_case_lower ตามรูปแบบ config เดิม): `id`, `name`, `version`, `contract` (เช่น `"1.0"`), `min_host` (เวอร์ชันโฮสต์ต่ำสุด, ไม่บังคับ), `entry` {`assembly`, `type`}, `files` [ {`path`, `sha256`, `native` (bool, ค่าเริ่มต้น false)} ] — parse ล้มหรือฟิลด์จำเป็นขาด/รูปแบบผิด (id ต้องเป็น `[a-z0-9][a-z0-9._-]{0,63}` (no leading - . _), sha256 ต้อง hex 64 ตัว) = `ManifestInvalid`
- `ExtensionStatus` enum: `Loaded`, `ManifestInvalid`, `ContractMismatch`, `HostTooOld`, `FileMissing`, `HashMismatch`, `UnsafePath`, `NotApproved`, `NotTrustedWhenElevated`, `DuplicateId`, `EntryNotFound`, `LoadFailed`
- `ExtensionLoadResult` — `FolderName`, `Id` (null ถ้าอ่านไม่ได้), `Status`, `Detail` (ข้อความอังกฤษเพื่อวินิจฉัย — การแปลไทย/อังกฤษเป็นเรื่องของเฟส 2), `ManifestSha256`, `LoadedExtension?`
- `LoadedExtension : IDisposable` — `Manifest`, `Instance` (`IExtension`), `Dispose()` เรียก `Shutdown()` (กลืน exception แล้วบันทึก) แล้ว unload ALC
- `ExtensionLoaderOptions` — `ExtensionsRoot`, `HostVersion`, `HostIsElevated`, `Language`, `FirstParty` (`FirstPartyAllowlist`), `Approvals` (`ApprovalStore?`), `Log` (Action<string>)
- `ExtensionLoader.LoadAll()` → `IReadOnlyList<ExtensionLoadResult>` เรียงตามชื่อโฟลเดอร์; ไม่มีโฟลเดอร์ `extensions` = รายการว่าง ไม่ใช่ error; **ห้ามโยน exception ออกนอก `LoadAll`** ไม่ว่าส่วนเสริมเสียแบบไหน (รวม `Initialize` โยน exception → `LoadFailed` และต้อง unload ALC ที่สร้างไว้)
- ลำดับตรวจต่อโฟลเดอร์ (หยุดที่ข้อแรกที่ไม่ผ่าน): โฟลเดอร์เป็น reparse point/junction → `UnsafePath` · อ่าน+parse manifest · id = ชื่อโฟลเดอร์ (ไม่ใช่ → `ManifestInvalid`) · contract major ตรงกับโฮสต์ และ minor ของส่วนเสริม ≤ ของโฮสต์ (ไม่ใช่ → `ContractMismatch`) · `min_host` ≤ เวอร์ชันโฮสต์ (`HostTooOld`) · path ของทุกไฟล์ใน manifest ต้องเป็น relative ไม่มี `..` ไม่ใช่ absolute/ไม่มี drive/UNC และ resolve แล้วอยู่ในโฟลเดอร์ส่วนเสริม ไฟล์ที่ resolve ได้ต้องไม่ใช่ reparse point (`UnsafePath`) · ไฟล์ครบ (`FileMissing`) · **ความเชื่อถือ** (first-party / approved / elevated rule) ตัดสินจาก SHA-256 ของไฟล์ `extension.json` ที่อ่านเป็น byte[] เดียวกับที่ parse · อ่านทุกไฟล์เป็น byte[] ครั้งเดียว คำนวณ SHA-256 เทียบ manifest (`HashMismatch`) · `entry.assembly` ต้องอยู่ใน `files` และไม่ใช่ native · `DuplicateId` ถ้า id ซ้ำกับตัวที่โหลดแล้ว (ในรอบเดียวกัน) · สร้าง `AssemblyLoadContext` (`isCollectible: true`) ต่อส่วนเสริม
- `ExtensionLoadContext` (internal): `Load(AssemblyName)` คืน `null` สำหรับ `SysMonitor.Extensibility`; สำหรับชื่ออื่นให้โหลดจาก **byte[] ที่ตรวจ hash แล้วของไฟล์ managed ที่ลงใน manifest** (`LoadFromStream`) ถ้าไม่มีใน manifest คืน `null`; `LoadUnmanagedDll(name)` โหลดเฉพาะไฟล์ที่ manifest ระบุ `native: true` ชื่อตรงกัน (เทียบชื่อไฟล์ ไม่สนตัวพิมพ์) ผ่าน `LoadUnmanagedDllFromPath` ไม่มีใน manifest → `IntPtr.Zero`
- ตรวจ type identity: instance ต้อง cast เป็น `IExtension` ได้ (ถ้า type ไม่มี/ไม่ implement → `EntryNotFound`)
- `FirstPartyAllowlist` — constructor จาก `IEnumerable<string>` (hex hash) และ `static FromAssembly(Assembly)` ที่อ่าน embedded resource `first-party-extensions.json` (อาร์เรย์ของ hex string; ไม่มี resource = ว่าง; รูปแบบเสีย = ว่าง + log)
- `ApprovalStore` — constructor รับ path และตัวตรวจ ACL (`Func<string, bool>`; ค่าเริ่มต้นคือตัวตรวจจริง); `IsApproved(id, manifestSha256)`; `Approve(id, name, manifestSha256)` เขียนไฟล์ได้ก็ต่อเมื่อผู้เรียกมีสิทธิ์เขียนจริง (ถ้าเขียนไม่ได้คืนผลล้มพร้อมเหตุผล **ห้ามกลืนเงียบ**) และสร้างโฟลเดอร์/ไฟล์พร้อมตั้ง ACL เป็น admin-only-writable; ตัวตรวจ ACL จริง `AclPolicy.IsAdminOnlyWritable(path)` คืน false ถ้ามี ACE ที่ให้ผู้ใช้ทั่วไป (Users, Authenticated Users, Everyone, หรือผู้ใช้ปัจจุบันที่ไม่ใช่ admin) สิทธิ์เขียน/แก้/ลบ/เปลี่ยน ACL บนไฟล์หรือโฟลเดอร์แม่ — ใช้ `System.IO.FileSystemAclExtensions` + `System.Security.AccessControl` ที่มากับ .NET ไม่เพิ่ม package; store เสียรูปแบบ = ว่าง + log
- เวอร์ชันโฮสต์: อ่านจาก assembly ของโฮสต์ (ผ่าน options) ไม่ hard-code

## Scope
- ใหม่: `SysMonitor.Extensibility/` · `SysMonitor.Wpf/Extensions/*.cs` · `SysMonitor.Tests/Extensions/*.cs` · โปรเจกต์ dummy สำหรับ test `SysMonitor.Tests.DummyExtension/` (ที่ root; `net8.0`; อ้าง `SysMonitor.Extensibility`; มี entry type ปกติ 1 ตัว, ตัวที่โยน exception ใน `Initialize` 1 ตัว, ตัวที่เรียก P/Invoke ไป native DLL ที่ test จัดให้ 1 ตัว หรือใช้ `kernel32`/ไฟล์ native ปลอมตามที่เหมาะสมเพื่อพิสูจน์เส้นทาง `LoadUnmanagedDll`)
- แก้: `SysMonitor.Wpf/SysMonitor.Wpf.csproj` (เพิ่ม ProjectReference เท่านั้น), `SysMonitor.Tests/SysMonitor.Tests.csproj` (reference ไปโปรเจกต์ใหม่/dummy ให้ build และคัดลอก dll ของ dummy ไปที่ output ของ test ได้จาก clean โดยไม่เขียนทับกัน), `build-wpf.cmd` เฉพาะถ้าจำเป็นต่อการ build
- **ห้ามแก้:** `WidgetViewModel*`, `Sampler`, `MainWindow*`, `SmartCore/SmartBoard`, `I18n`, installer, DiskInfo repo

## Acceptance criteria (test ต้องครอบ)
- [ ] ไม่มีโฟลเดอร์ `extensions` → รายการว่าง ไม่โยน exception
- [ ] ส่วนเสริม dummy ปกติ (first-party ผ่าน allowlist) โหลดได้ `Initialize` ถูกเรียกพร้อม context ถูกต้อง; `Dispose` เรียก `Shutdown` และ ALC ถูก unload (ตรวจด้วย `WeakReference` + GC)
- [ ] type identity: ส่วนเสริมใช้ `IExtension` ตัวเดียวกับโฮสต์ (cast สำเร็จ)
- [ ] แก้ไข byte ใน DLL ของส่วนเสริมหลังสร้าง manifest → `HashMismatch` ไม่โหลด; แก้ byte ในไฟล์ native → `HashMismatch`
- [ ] `extension.json` ถูกแก้หลังฝัง hash ใน allowlist → ไม่ใช่ first-party (โหมดปกติ → `NotApproved`, โหมด admin → `NotTrustedWhenElevated`)
- [ ] โหมด admin: ส่วนเสริมที่อนุมัติใน store แต่ไม่อยู่ใน allowlist → `NotTrustedWhenElevated`; โหมดปกติ: ตัวเดียวกัน → `Loaded`
- [ ] approval store: ACL ผิด (ผู้ใช้ปกติเขียนได้) → store ถือว่าว่างและส่วนเสริมที่อนุมัติไว้ → `NotApproved` (ทดสอบทั้งด้วยตัวตรวจ ACL ปลอมและตัวตรวจจริงกับไฟล์ที่สร้างในโฟลเดอร์ temp ซึ่งต้องคืน false); `Approve` ที่เขียนไม่ได้คืนผลล้มพร้อมเหตุผล ไม่เงียบ
- [ ] manifest เสีย (JSON ผิด, id ผิดรูปแบบ, sha256 ผิดรูปแบบ, ฟิลด์ขาด), id ไม่ตรงชื่อโฟลเดอร์ → `ManifestInvalid`; contract major ไม่ตรง หรือ minor สูงกว่าโฮสต์ → `ContractMismatch`; `min_host` สูงกว่าโฮสต์ → `HostTooOld`
- [ ] path `..\evil.dll`, absolute path, `C:\x`, `\\server\share\x`, ไฟล์ที่เป็น symlink/junction → `UnsafePath`; ไฟล์ใน manifest หายไป → `FileMissing`
- [ ] `Initialize` โยน exception → `LoadFailed` และ ALC ถูก unload; ส่วนเสริมตัวอื่นยังโหลดได้ตามปกติในรอบเดียวกัน; `DuplicateId` ทำงาน
- [ ] native: ไฟล์ `native: true` ที่ผ่าน hash ถูกโหลดผ่าน ALC ได้จริงจากโฟลเดอร์ส่วนเสริม; ไฟล์ native ที่ไม่อยู่ใน manifest ไม่ถูกโหลด
- [ ] `dotnet test SysMonitor.Tests` ผ่านทั้งหมด (ฐาน 394/394 + test ใหม่) และ `build-wpf.cmd` สำเร็จ; exe ที่ publish แบบ single-file ยังเปิดได้ (Coding Owner ตรวจเอง)

## Constraints / Non-goals
- ไม่เพิ่ม NuGet package (ใน SystemMonitor ใช้เฉพาะที่มากับ .NET; MSTest มีอยู่แล้ว)
- ไม่ทำ UI, ไม่แตะ config.wpf.json, ไม่ทำ i18n ข้อความผู้ใช้, ไม่ทำปุ่มอนุมัติ (เฟส 2), ไม่ฝัง allowlist ตอน build (เฟส 4), ไม่ย้าย SMART (เฟส 3)
- ไม่ใช้ `Assembly.Load*` นอก `ExtensionLoadContext`; ไม่ใช้ reflection เรียก private
- ห้ามโหลด dependency จากไดเรกทอรีอื่นนอกโฟลเดอร์ส่วนเสริม ห้าม probing path
- รูปแบบโค้ดตามไฟล์ข้างเคียง (คอมเมนต์อธิบาย "ทำไม" สั้น ๆ ภาษาอังกฤษเหมือนไฟล์เดิม)

## Commit / diff
ยังไม่มี ผู้ใช้เป็นผู้สั่ง commit; ผมบูรณาการและรัน test เอง (agy รันคำสั่งไม่ได้ใน `--print`)

## Open questions / risks
- test ที่สร้างไฟล์ ACL/junction ต้องไม่ต้องใช้สิทธิ์ admin (ใช้ไดเรกทอรี temp ของผู้ใช้); ส่วนที่ต้อง admin จริงให้ตัวตรวจ ACL ปลอมแทน
- การสลับไฟล์ native ระหว่าง hash กับ `LoadUnmanagedDllFromPath` ยังมีช่วงสั้น ๆ ยอมรับตามโมเดลความปลอดภัย (บันทึกใน ADR เฟส 5)

---

# รอบที่ 2 — แก้ตามผลรีวิว (2026-10-14)

Coding Owner ตรวจผลรีวิวของ Reviewer1 (Opus 5.5 High) และ Reviewer2 (Codex GPT-6.1 High) กับโค้ดและเครื่องจริงแล้ว ผลฐานก่อนแก้: `dotnet test` 428/428, `build-wpf.cmd` ผ่าน, exe single-file ขนาด 555,172 B เปิดได้ (Extensibility ถูกฝังใน exe ไม่มีไฟล์หลุด)

## ที่ต้องแก้ (รับแล้ว — ทั้งสอง reviewer เห็นตรงกันหรือ Coding Owner ยืนยัน)
1. **ACL mask ผิด (High):** `WriteOrModifyRights` รวม `Modify`/`FullControl`/`Write` ซึ่งมีบิตอ่านและ Synchronize ทำให้ ACE `Users: Read` (ที่ `SetAdminOnly*` สร้างเอง) ถูกมองว่าเขียนได้ → ตัวตรวจจริงปฏิเสธ store ที่เพิ่งสร้าง และ test ใช้ `aclChecker: _ => true` เลยไม่เห็น **แก้:** mask ประกอบจากบิตเขียนล้วน ๆ เท่านั้น: `WriteData, CreateFiles, AppendData, WriteExtendedAttributes, WriteAttributes, Delete, DeleteSubdirectoriesAndFiles, ChangePermissions, TakeOwnership` (ห้ามใช้ composite `Write/Modify/FullControl`) และเคารพ `PropagationFlags.InheritOnly` เมื่อประเมินตัวโฟลเดอร์เอง (ACE ที่ inherit-only ไม่ใช้กับตัวโฟลเดอร์; ใช้กับไฟล์ลูก)
2. **ไม่ตรวจเจ้าของ (High):** เจ้าของ object มี `WRITE_DAC` โดยปริยาย และผู้ใช้ปกติสร้าง `C:\ProgramData\SystemMonitor` เองได้ (ProgramData ให้ Users เขียน + CREATOR OWNER) **แก้:** `IsAdminOnlyWritable` ต้องตรวจว่าเจ้าของของ **ไฟล์และโฟลเดอร์ store** เป็น `BUILTIN\Administrators` หรือ `SYSTEM` (เทียบ SID) ไม่ใช่ → false
3. **บรรพบุรุษ (P2):** โฟลเดอร์ store ที่ ACL แน่นแต่ปู่ย่าที่ผู้ใช้ลบ/เปลี่ยนชื่อลูกได้ ยังถูกแทนที่ได้ **แก้:** เดินขึ้นทุก ancestor ถึง root ของไดรฟ์ และ ancestor ต้องไม่มี ACE ของผู้ที่ไม่ใช่ Administrators/SYSTEM ที่ให้ `DeleteSubdirectoriesAndFiles, Delete, ChangePermissions, TakeOwnership` (ancestor ไม่ต้องห้าม `CreateFiles/AppendData` เพราะ `C:\ProgramData` ให้ Users สร้างโฟลเดอร์ได้ตามปกติ) และเคารพ InheritOnly
4. **`Approve` ตามลิงก์/โฟลเดอร์ไม่ปลอดภัยด้วยสิทธิ์ admin (High):** ผู้ใช้ปกติสร้างโฟลเดอร์ store ล่วงหน้าเป็น junction ได้ **แก้:** (ก) ถ้าโฟลเดอร์ store หรือไฟล์ store หรือ ancestor ใด ๆ เป็น reparse point → `Approve` ล้มพร้อมเหตุผล ไม่เขียน (ข) ถ้าโฟลเดอร์ store มีอยู่แล้วแต่ตัวตรวจ ACL/เจ้าของไม่ผ่าน → ล้มพร้อมเหตุผล (fail closed; ไม่พยายามแก้เอง) (ค) อ่านรายการเดิมมารวมเฉพาะเมื่อไฟล์เดิมผ่านตัวตรวจ ACL/เจ้าของ; ไม่ผ่าน/เสียรูปแบบ → เริ่มจากรายการว่างและ log (ไม่กลืนเงียบ) (ง) เขียนไฟล์ชั่วคราวในโฟลเดอร์ store แล้วตั้ง ACL แล้ว `File.Move(..., overwrite: true)`; `IsApproved` ปฏิเสธเมื่อโฟลเดอร์/ไฟล์ store เป็น reparse point
5. **native imports ถูกค้นจากโฟลเดอร์ส่วนเสริมโดยไม่ตรวจ hash (High):** `LoadUnmanagedDllFromPath` ใช้ `LOAD_WITH_ALTERED_SEARCH_PATH` **แก้สองชั้น:** (ก) **โฟลเดอร์ส่วนเสริมห้ามมีไฟล์ใด ๆ นอกจาก `extension.json` และไฟล์ที่อยู่ใน `files` ของ manifest** (ทั้งต้นไม้ย่อย; ไม่ผ่าน → `UnsafePath` ระบุไฟล์ที่เกิน; ทุกโฟลเดอร์ย่อยก็ต้องไม่เป็น reparse point) (ข) โหลด native ด้วย `LoadLibraryExW` เองผ่าน P/Invoke (`kernel32`, `DefaultDllImportSearchPaths(System32)`) ด้วยแฟลก `LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_SYSTEM32` (0x100 | 0x800) แทน `LoadUnmanagedDllFromPath`
6. **ช่วงสลับไฟล์ native ไม่มีขอบเขต (Medium):** `LoadUnmanagedDll` ทำงานตอน P/Invoke ครั้งแรก ซึ่งอาจช้ากว่า hash เป็นนาที **แก้:** โหลด native ที่ลงใน manifest **ทันทีหลัง hash ผ่านและก่อน `Initialize`** โดยถือ `FileStream` (`FileShare.Read`, อ่านแล้ว hash จาก stream นั้น) ค้างไว้จนกว่า `LoadLibraryExW` เสร็จ เก็บ handle ใน `ExtensionLoadContext` แล้ว `LoadUnmanagedDll` คืน handle ที่โหลดไว้ (ชื่อที่ไม่ลง manifest คืน `IntPtr.Zero` ตามเดิม); `LoadedExtension.Dispose` (และเส้นทางล้มเหลวทุกทางที่ unload ALC) ต้อง `FreeLibrary` handle เหล่านั้นหลัง `Shutdown`/unload ถ้า `LoadLibraryExW` ล้ม → `LoadFailed` พร้อม error code
7. **logger โยน exception ทะลุ `LoadAll` และขวางการ unload (Medium):** ทุกการเรียก `Log` ในตัวโหลด/`LoadedExtension`/`ApprovalStore`/`FirstPartyAllowlist` ต้องผ่าน helper ที่กลืน exception; ลำดับในเส้นทางล้มเหลวต้อง unload/FreeLibrary ก่อนเรียก log; `LoadAll(null)` ให้ `ArgumentNullException` (ความผิดของผู้เรียก ไม่ใช่ส่วนเสริม)
8. **null ใน array (Medium/Low):** `"files":[null]` ต้องเป็น `ManifestInvalid`; store ที่มี `[null]` ต้องถือว่าเสียรูปแบบทั้งไฟล์ (ว่าง + log) ไม่ใช่โยน/short-circuit
9. **ลำดับสถานะ (Medium):** ย้ายการตรวจ "entry.assembly อยู่ใน files และไม่ใช่ native" ออกจาก parse ของ manifest ไปทำในตัวโหลดหลังขั้นตอน hash (ก่อนสร้าง ALC) ตามลำดับที่ spec เขียนไว้ (`ManifestInvalid` จาก parse ยังคงใช้กับรูปแบบผิด)
10. **`min_host` (Low):** เทียบ `Version` หลัง normalize ส่วนที่ขาด (`Build/Revision` = -1 → 0) ทั้งสองฝั่ง เพื่อให้ `4.2.0` = `4.2.0.0`
11. **ExtensionDirectory (Medium):** ส่ง path แบบ absolute/canonical ให้ `IExtensionContext.ExtensionDirectory`
12. **test (Medium):**
    - junction test: ถ้าสร้าง junction ไม่ได้ให้ `Assert.Inconclusive` ไม่ใช่ผ่านเงียบ
    - เพิ่ม test: file-level symlink/junction และ junction ของโฟลเดอร์ย่อยกลาง → `UnsafePath`; ไฟล์เกินใน manifest (unlisted) → `UnsafePath`; `sha256` ผิดรูปแบบ/ฟิลด์จำเป็นขาด → `ManifestInvalid`; `files:[null]`; `min_host` `4.2.0` vs `4.2.0.0`; ค่า context (`Language="th"`, `HostIsElevated`, `HostContractVersion`, `ExtensionDirectory` absolute) ตรวจจริงจาก dummy (เขียนค่าลงไฟล์ marker แล้วอ่านกลับ); logger ที่โยน exception ไม่ทำให้ `LoadAll` โยน และ ALC ยัง unload
    - `AclPolicy`: ทดสอบตัวประเมินกับ `FileSecurity/DirectorySecurity` ที่สร้างในหน่วยความจำ (ไม่แตะดิสก์ ไม่ต้อง admin): admin-owned + Admins/SYSTEM Full + Users Read → true; มี Users Write/Modify/Delete → false; เจ้าของเป็นผู้ใช้ปกติ → false; ACE InheritOnly ของ Users บนโฟลเดอร์ → ไม่นับกับตัวโฟลเดอร์; ancestor ที่ Users ลบลูกได้ → false; มีกรณีที่รอดจาก bug ข้อ 1 โดยเฉพาะ (Users Read ต้องผ่าน); ตัวตรวจจริงกับไฟล์ที่ผู้ใช้ปัจจุบันเป็นเจ้าของใน temp ต้อง false
    - native test ที่โหลดสำเร็จต้อง `Dispose` ผลก่อน cleanup (FreeLibrary ปล่อยไฟล์) และ `Cleanup` ลบซ้ำได้ (retry สั้น ๆ) ไม่กลืนความล้มเหลวเงียบ ๆ จนไฟล์ค้าง; ลบ temp dir `SysMon_*` ที่ค้างจาก test รอบก่อนไม่ใช่หน้าที่ของ test
    - test ของ unlisted native ใช้ข้อ 5(ก) แล้ว: ไฟล์ native ที่ไม่ลง manifest ทำให้โฟลเดอร์ถูกปฏิเสธเป็น `UnsafePath` (ปรับ test เดิมและตัวอย่าง dummy ให้สอดคล้อง; ลบ `DummyUnlistedNativeExtension` ได้ถ้าไม่ใช้)

## ที่ไม่รับ (พร้อมเหตุผล — ห้ามแก้)
- **"native/managed ที่ไม่ลง manifest ยังตกไปที่การค้นหาปกติของ OS/default ALC" (Codex P1-1, Opus F6):** ไม่ใช่ช่องโหว่ภายใต้โมเดลที่ตกลง ส่วนเสริมที่ผ่านการตรวจแล้วเรียก `kernel32`/`user32`/assembly ของ .NET ได้ตามปกติ (จำเป็น) การบล็อก fall-through จะทำให้ P/Invoke ระบบทุกตัวพัง ไฟล์ที่ผู้โจมตีวางในโฟลเดอร์ติดตั้งของโฮสต์อยู่ในข้อจำกัดที่ยอมรับแล้ว (ชั้น 3 / ADR) ปรับเฉพาะ **คำอธิบาย** ใน XML doc ของ `ExtensionLoadContext` ให้ตรงความจริง (ข้อกำหนดคือ "ไฟล์ในโฟลเดอร์ส่วนเสริมที่ไม่ลง manifest ไม่ถูกโหลด" ซึ่งข้อ 5(ก) บังคับแล้ว)
- **ยังไม่ทำ:** root `extensions` เป็น junction (ผลกระทบผ่านข้อ 6 ที่ปิดแล้วเท่านั้น); `DuplicateId` เข้าถึงผ่าน `LoadAll` ไม่ได้จริง (คงไว้เป็นเกราะชั้นใน)

## ข้อจำกัด
ห้ามแก้ไขนอก Scope เดิม ห้ามเพิ่ม package; ทำเสร็จแล้วสรุปไฟล์ที่แก้ ผมรัน build/test/publish เอง

---

# ผลหลังรอบที่ 2 (2026-10-14)

**ผู้เขียน:** agy เขียนรอบ 1 และรอบ 2 (โควตารีเซ็ตแล้ว ไม่ใช้ข้อยกเว้นของ workflow); Coding Owner แก้ต่อเองเฉพาะส่วนที่บูรณาการแล้ว test ล้ม/ผลรีวิวรอบ 2 ชี้ (ดูข้างล่าง) เป็น diff เล็กและมี test ล็อก

**ผลรีวิวรอบ 2:** Reviewer1 (Opus 5.5 High) ยืนยันข้อ 1–12 ถูกต้อง ไม่พบ High; Reviewer2 (Codex GPT-6.1 High) พบ P1 สองข้อ (race ตอนสร้างโฟลเดอร์ store, เจ้าของ ancestor) กับ P2 อีก 5 ข้อ ทั้งหมดตรวจกับโค้ดแล้ว **รับและแก้ครบ**:
- exception ที่ `Message` โยนเองหลุดจาก `LoadAll`/`Dispose` → `Describe()` กลืน และ `Dispose` ใช้ try/finally ให้ `FreeLibrary` ทำงานเสมอ (รวมกรณี handler ของ `Unloading` โยน)
- `Approve` แบบไม่ใช่ admin ทิ้งโฟลเดอร์ที่ผู้ใช้เป็นเจ้าของไว้จนขวางการอนุมัติทุกครั้งหลังจากนั้น → ลบโฟลเดอร์ที่ตัวเองเพิ่งสร้างเมื่อ secure ไม่สำเร็จ และตรวจ reparse point + ACL ของโฟลเดอร์ซ้ำหลังเตรียมเสร็จ
- ancestor ต้องมีเจ้าของเป็น Administrators/SYSTEM/TrustedInstaller (ไม่เช่นนั้นเจ้าของแก้ DACL ได้เสมอ)
- path ของ store ถูกแปลงเป็น absolute ครั้งเดียวในตัวสร้าง (ตรวจและอ่านไฟล์เดียวกันเสมอ)
- manifest ที่มี path ซ้ำ/ชื่อไฟล์ซ้ำ (ไม่สนตัวพิมพ์) → `ManifestInvalid` (ไม่เช่นนั้น native ถูกโหลดซ้ำแล้ว `FreeLibrary` ครั้งเดียว โมดูลค้าง)
- คอมเมนต์เรื่อง "ทุกไฟล์ในโฟลเดอร์ถูก hash แล้ว" ปรับให้ตรงความจริง (ช่วงก่อนโหลดยังแก้ได้ภายใต้ข้อจำกัดโฟลเดอร์ติดตั้งที่ผู้ใช้เขียนได้ ซึ่งอยู่ใน ADR)
- test: ใช้ hook `NativeLoadingForTesting` พิสูจน์ว่าไม่มีการโหลด native ก่อน hash ครบ (จับกรณีโหลดแล้วค่อย free ด้วย), test junction ของ store มีรายการอนุมัติที่ตรงจริงพร้อม control, cleanup ลบ junction ก่อน (ไม่ทิ้ง `%TEMP%\SysMon_*` อีก), ไฟล์ native จำลองเปลี่ยนจากสำเนา `kernel32.dll` (ทำให้ test host ล้มเมื่อโหลด/free ซ้ำหลายรอบ) เป็นสำเนา `shlwapi.dll` ที่เรียก `StrToIntW("42")` ตรวจค่าจริง

**ผลตรวจของ Coding Owner:**
- `dotnet test SysMonitor.Tests`: **463 ผ่าน, 1 ข้าม, 0 ล้ม** (ฐานก่อนเฟส 394; รัน 4 รอบติดไม่ล้ม; ไม่มีโฟลเดอร์ `%TEMP%\SysMon_*` ค้างหลังรัน) — ที่ข้ามคือ `LoadAll_FileLevelSymlink_ReturnsUnsafePath` เพราะเครื่องนี้สร้าง file symlink ไม่ได้โดยไม่มีสิทธิ์ (Inconclusive)
- `build-wpf.cmd` สำเร็จ: `SystemMonitor-4.2.0.exe` 563,364 B (ฝัง `SysMonitor.Extensibility` ไว้ใน exe ไม่มี dll หลุด), exe เปิดและตอบสนองได้ (smoke 6 วินาที) ที่ขนาด 555,172 B ก่อนแก้รอบ 2 (รอบ 2 ไม่แตะเส้นทางเริ่มโปรแกรม)
- **ตัวโหลดจริง + `DiskInfoCore.dll` จริง:** harness ชั่วคราวใน scratchpad เรียก `ExtensionLoader.LoadAll` กับส่วนเสริมทดลองที่ P/Invoke `DiskInfoCore.dll` (ลงใน manifest เป็น native) → `Loaded`, `abi=0x00010000 open=0 scan=-2` (ไม่ใช่ admin จึง −2 ซึ่งถูก), `ExtensionDirectory` เป็น absolute, `Language=th` ถูกส่งถึง — ยืนยันว่าแฟลก `LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | SYSTEM32` หา imports ของ DiskInfoCore ใน System32 ได้

## ยังไม่ได้ตรวจ / ความเสี่ยงที่เหลือ
- **ACL ด้านบวกของจริง** ตรวจกับ descriptor ในหน่วยความจำ + ผลอ่านจริงของ ancestor `C:\` และ `C:\ProgramData` (Reviewer1 ยืนยันว่าสายนั้นผ่าน) แต่ยังไม่เคยให้ `Approve` ตัวจริงสร้างไฟล์ใต้ `%ProgramData%\SystemMonitor` ด้วยสิทธิ์ admin แล้ว `IsApproved` อ่านกลับ — ต้องทำตอนเฟส 2 ที่มีปุ่มอนุมัติ (ผมไม่ได้รัน เพราะสิทธิ์ admin ที่ผู้ใช้ให้จำกัดอยู่ในโฟลเดอร์งานนี้ ส่วน `C:\ProgramData` อยู่นอกโฟลเดอร์)
- test กรณี file symlink ที่ **ลงใน manifest** ไม่มี (ทดสอบได้เฉพาะเมื่อมีสิทธิ์สร้าง symlink); ทางตรวจ reparse point ของไฟล์ยังถูกคุ้มโดยการสแกนทั้งต้นไม้ย่อยที่มี test ผ่านกรณี junction โฟลเดอร์ย่อย
- test ที่โหลด native สำเร็จพิสูจน์ว่า P/Invoke ผ่านไลบรารีที่ preload ไว้ แต่ไม่ได้พิสูจน์แยกว่า `LoadUnmanagedDll` ถูกเรียก (ระบบปฏิบัติการจับคู่โมดูลที่โหลดแล้วตามชื่อ) — ยอมรับ
- ยังไม่มีการเชื่อมกับโฮสต์ (เฟส 2): ตัวโหลดยังไม่ถูกเรียกใช้โดย `SystemMonitor.exe` จริง
