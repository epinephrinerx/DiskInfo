# Handoff — เฟส 2a: โฮสต์วาดหน้าจอของส่วนเสริม + เปิด/ปิดในแถบข้าง + config + ข้อความ th/en (R-004)

จาก: Coding Owner (Claude Code) → ถึง: Writing Owner (agy, Gemini-3.8 Flash Medium) · Reviewer1: Claude Opus 5.5 High · Reviewer2: Codex GPT-6.1 High
Requirement: R-004 (DiskInfo `Requirements.md`) · วันที่ 2026-10-14
Repo ที่แก้: `D:\Dev\Apps\SystemMonitor4.0` branch `agent/agy-sysmon-ext-host` (แตกต่อจาก `agent/agy-sysmon-ext-loader` ที่ commit แล้ว `9ee9f4f`)
อ่านก่อนเขียน: `phase1-extension-loader.md` (ตัวโหลดและโมเดลความเชื่อถือ — **ห้ามผ่อนกฎเหล่านั้น**) และโค้ดเดิมของโมดูล SMART ซึ่งเป็นต้นแบบของสิ่งที่ส่วนเสริมต้องวาดได้: `SysMonitor.Wpf/ViewModels/WidgetViewModel.Smart.cs`

## Objective
ให้ส่วนเสริมที่โหลดแล้ว (เฟส 1) **ขึ้นหน้าจอ** ในสามมุมมองเดิมของ SystemMonitor — หน้า Widget ที่หมุน, section ใน Overall, แท็บใน Full — โดยโฮสต์วาดจากคำอธิบายที่ไม่ผูกกับ UI (ไม่มี WPF ใน contract) ใช้ส่วนประกอบเดิม (`Head`, `Meter`, `MeterRow`, `PlannedTab`, `DeviceFact`) จึงตาม theme/ฟอนต์/ภาษาอัตโนมัติ; เปิด/ปิดส่วนเสริมแต่ละตัวได้ในแถบข้างโดยไม่รีสตาร์ท; จำค่าใน `config.wpf.json`; ส่วนเสริมที่โหลดไม่ได้ต้องบอกเหตุผลเป็นภาษาไทย/อังกฤษ **เฟสนี้ยังไม่มีปุ่มอนุมัติ (เฟส 2b) และยังไม่ย้าย SMART (เฟส 3) — โค้ด SMART เดิมต้องทำงานเหมือนเดิมทุกอย่าง**

## 1) Contract เพิ่มใน `SysMonitor.Extensibility` (net8.0 ล้วน ไม่มี WPF/package)
ใช้ `record` ที่ immutable ทั้งหมด (contract ยังเป็นร่าง `1.0` จนถึงเฟส 5 เพิ่มได้):
- `enum Severity { Neutral = 0, Good = 1, Caution = 2, Bad = 3 }`
- `interface IViewProvider` (ส่วนเสริม implement คู่กับ `IExtension` ในคลาสเดียวกัน): `string GetDisplayName(string language)` และ `ExtensionView GetView(string language)` — **ต้องคืนทันทีและ thread-safe** (อ่านผลล่าสุดที่เก็บไว้; ห้ามทำงานหนัก/บล็อกในนี้ โฮสต์เรียกจากเธรดของ sampler ทุกรอบ); `language` เป็น `"th"` หรือ `"en"` ส่วนเสริมคืนข้อความที่แปลแล้วเอง
- `record Fact(string Name, string Value)`
- `record WidgetPage(string Title, string Verdict, string Line, int? TemperatureC, int Percent, Severity Severity, string? Message = null)` — ถ้า `Message` ไม่ว่าง หน้านั้นเป็นหน้าข้อความล้วน (ไม่วาดแท่ง) เหมือนหน้าสถานะของ SMART
- `record OverviewRow(string Title, string Detail, string ValueText, int Percent, Severity Severity, int? TemperatureC)`
- `record OverviewSection(string Note, IReadOnlyList<OverviewRow> Rows)`
- `record DetailTab(string Key, string Title, string Subtitle, string Detail, string Hardware, IReadOnlyList<Fact> Facts)`
- `record ExtensionView(IReadOnlyList<WidgetPage> WidgetPages, OverviewSection? Overview, IReadOnlyList<DetailTab> Tabs)` พร้อม `static ExtensionView Empty`
- `sealed class StringTable` สำหรับให้ส่วนเสริมถือข้อความ th/en ของตัวเอง: สร้างจาก `IReadOnlyDictionary<string, IReadOnlyDictionary<string,string>>` (ภาษา → key → ข้อความ); `string Get(string language, string key)` (ภาษาที่ไม่รู้ใช้ `en`; key ที่ไม่มีคืน key เอง); `static IReadOnlyList<string> MissingKeys(StringTable)` ที่คืนรายการ key ที่มีในภาษาหนึ่งแต่ไม่มีอีกภาษา (ไว้ใช้ใน parity test ของส่วนเสริมแต่ละตัว)

## 2) ตัวโหลด (`SysMonitor.Wpf/Extensions`) — เพิ่ม ไม่ลดกฎ
- `ExtensionStatus.Disabled` ใหม่; `ExtensionLoaderOptions.IsEnabled` (`Func<string,bool>?`, `null` = ทุกตัวเปิด)
- ลำดับ: ผ่านการตรวจ manifest/contract/path/ความเชื่อถือทั้งหมดแล้ว **ถ้า id ไม่ถูกเปิด → `Disabled`** (ยังไม่อ่าน/hash ไฟล์ ยังไม่โหลดอะไร) แต่ถ้าความเชื่อถือไม่ผ่าน (`NotApproved`/`NotTrustedWhenElevated`) ให้รายงานสถานะนั้นแม้ถูกปิด เพื่อให้แถบข้างบอกผู้ใช้ได้
- `ExtensionLoadResult` เพิ่ม `Manifest` (`ExtensionManifest?` — มีเมื่อ parse สำเร็จ) เพื่อให้ UI แสดงชื่อ/เวอร์ชันของตัวที่ไม่ได้โหลด
- `public static ExtensionLoadResult LoadFolder(string folder, ExtensionLoaderOptions options)` โหลดโฟลเดอร์เดียวด้วยกฎเดียวกับ `LoadAll` (ไม่โยน exception ออกมา) ไว้ใช้เปิดตัวเดียวทีหลัง

## 3) `ExtensionHost` (ใหม่, `SysMonitor.Wpf/Extensions/ExtensionHost.cs`)
เจ้าของวงจรชีวิตส่วนเสริมทั้งหมดของแอป:
- ตัวสร้างรับ `AppConfig`, `ExtensionLoaderOptions` พื้นฐาน (ผู้เรียกใส่ root/เวอร์ชัน/elevated/allowlist/approval store/log) เพื่อทดสอบได้ด้วย path ชั่วคราว
- `Task StartAsync()` สแกนและโหลดตัวที่เปิดไว้บนเธรดพื้นหลัง (ไม่บล็อก UI ตอนเริ่ม); มีรายการผล (`Entries`) ที่อ่านได้จากทุกเธรดโดยไม่ล็อกนาน (swap รายการ immutable ด้วย `Volatile`)
- `Entries`: `IReadOnlyList<ExtensionEntry>` — `Id`, `Name` (จาก manifest; ถ้าโหลดแล้วใช้ `GetDisplayName` ตามภาษา), `Version`, `Status`, `Detail`, `Enabled` (จาก config)
- `SetEnabled(string id, bool enabled)`: เขียน config (`config.Extensions[id].Enabled`) แล้วโหลด/Dispose เฉพาะตัวนั้นทันที (ผ่าน `LoadFolder` / `LoadedExtension.Dispose`) บนเธรดพื้นหลัง; **ปิด = Dispose จริง** (สั่ง `Shutdown` ปล่อย native) ไม่ใช่แค่ซ่อน
- `CollectViews(string language)` → `IReadOnlyList<ExtensionViewData>` (`Id`, `DisplayName`, `ExtensionView View`) สำหรับตัวที่โหลดแล้วและเป็น `IViewProvider`: เรียก `GetDisplayName`/`GetView` ใน try/catch ต่อตัว (ตัวที่โยน exception ถูกข้ามรอบนั้นและ log ครั้งเดียวต่อข้อความ ไม่ท่วมไฟล์ log) และ **ตัดทอนตามเพดาน**: หน้า Widget ≤ 32, แถว Overview ≤ 64, แท็บ ≤ 32, fact ต่อแท็บ ≤ 64, ความยาวข้อความ ≤ 512 ตัวอักษร, `Percent` บีบเข้า 0–100, `TemperatureC` นอกช่วง −50..150 ถือเป็น null, `Key` ของแท็บต้องไม่ว่างและไม่ซ้ำ (ตัวที่ซ้ำ/ว่างถูกทิ้ง); เพดานนี้เป็นค่าคงที่ที่มีชื่อและมี test
- `Dispose()` ปิดทุกตัว (เรียกตอน `App.OnExit`)
- ไม่เรียกโค้ดของส่วนเสริมบนเธรด UI ยกเว้น `Dispose`/`Shutdown` ตอนปิดแอป

## 4) Snapshot / Sampler / config
- `Snapshot.Extensions` (`IReadOnlyList<ExtensionViewData>`, ค่าเริ่ม `Array.Empty`); `Sampler` ใส่ผลจาก `ExtensionHost.CollectViews(_config.Lang)` ทุกรอบเมื่อมี host (`Sampler(AppConfig, ExtensionHost? extensions = null)` — ตัวสร้างเดิมต้องใช้ได้ต่อ test เดิมทั้งหมด)
- `AppConfig.Extensions`: `Dictionary<string, ExtensionSetting>` (`ExtensionSetting.Enabled`, ค่าเริ่ม `false` — ส่วนเสริมใหม่ **ปิดไว้ก่อนเสมอ**); JSON: `"extensions": { "<id>": { "enabled": true } }` คีย์ id ต้องไม่ถูกแปลงชื่อ; ไฟล์ config เก่าที่ไม่มีคีย์นี้ต้องโหลดได้เหมือนเดิม; key `show_smart` เดิมไม่แตะ (เฟส 3 ย้าย)
- `App.OnStartup` สร้าง `ExtensionHost` (root = `<AppContext.BaseDirectory>\extensions`, เวอร์ชัน = เวอร์ชัน assembly ของแอป, `HostIsElevated` = `Win32.IsElevated()`, `FirstParty = FirstPartyAllowlist.FromAssembly(...)`, `Approvals = new ApprovalStore(ApprovalStore.DefaultPath)`, `Log = Diag.Write`) ส่งให้ `Sampler` และ `MainWindow` แล้ว `StartAsync()` โดยไม่รอ; `OnExit` เรียก `Dispose()`; การหยุดแอปต้องไม่ค้างถ้าส่วนเสริมช้า (ให้เวลาจำกัดแล้วปล่อย)

## 5) การวาด (ใหม่ `ViewModels/WidgetViewModel.Extensions.cs` + จุดเกี่ยวใน `WidgetViewModel.cs` เท่าที่จำเป็น)
ทำให้ทั่วไปตาม `WidgetViewModel.Smart.cs` โดย **ไม่ลบหรือแก้พฤติกรรมของ SMART เดิม**:
- Widget: ทุกส่วนเสริมที่เปิดและมี `WidgetPages` เพิ่มหน้าชนิด `ext:<id>` (index = ลำดับหน้า) ต่อท้ายหน้าที่มี; `Severity`→สีเหมือน `HealthColor` ของ SMART (Good=`Palette.TempOk`, Caution=`TempWarm`, Bad=`TempHot`, Neutral=`AccentDisk`); หน้า `Message` วาดแบบหน้าข้อความ; รายการหน้าที่ stale (จำนวนหรือชุดเปลี่ยน) ต้อง rebuild เหมือน `SmartViewsStale` — เทียบรายการหน้า ไม่ใช่แค่จำนวน
- Overall: section ต่อส่วนเสริมที่มี `Overview` ชื่อ section = `DisplayName`; แถว = `OverviewRow` (`Meter(row, Title, Percent, color, Detail, TemperatureC, false)` แล้ว `ValueText`)
- Full: แท็บต่อ `DetailTab` คีย์จริง = `ext:<id>:<tab.Key>`; ชื่อแท็บ/หัวรอง/รายละเอียด/ฮาร์ดแวร์/facts ตามฟิลด์; การเลือกแท็บที่จำไว้ (`_config.FullTab`) ต้องรอดเมื่อ key ภายในส่วนเสริมเปลี่ยน — ถ้าแท็บที่จำไว้ขึ้นต้น `ext:<id>:` และไม่มีแล้ว ให้เลือกแท็บแรกของส่วนเสริมนั้น (ทำเป็นกฎทั่วไปคู่กับกฎ `smart*` เดิม)
- ส่วนเสริมที่ปิด/โหลดไม่ได้/เป็น `Disabled` ต้องไม่เหลือหน้า/section/แท็บค้าง และไม่ทำให้ Full view เด้งไปแท็บอื่นโดยไม่จำเป็น
- ถ้าไม่มีส่วนเสริมเลย ทุกมุมมองต้องเหมือนเดิมทุกพิกเซล (test เดิมทั้งหมดผ่าน)

## 6) แถบข้าง (`MainWindow.xaml.cs` ใกล้ช่อง `show_smart`) + ข้อความ
- เมื่อ `ExtensionHost.Entries` ไม่ว่าง แสดงหัวข้อ "ส่วนเสริม"/"Extensions" ตามด้วยช่องติ๊กต่อส่วนเสริม (ชื่อ + เวอร์ชัน) ที่เรียก `SetEnabled` และ `Refresh()`; ใต้ช่องของตัวที่ไม่ได้โหลดแสดงบรรทัดเหตุผลตาม `Status` (ใช้ `Paragraph`/ส่วนประกอบเดิม) — ข้อความเหล่านี้ต้องมีทั้งไทย/อังกฤษใน `I18n.cs`: `NotApproved` ("ยังไม่ได้รับการอนุมัติ"), `NotTrustedWhenElevated` ("ในโหมดผู้ดูแลระบบโหลดได้เฉพาะส่วนเสริมที่มากับโปรแกรม"), `ContractMismatch`, `HostTooOld`, `HashMismatch` ("ไฟล์ไม่ตรงกับที่ระบุ ไม่ได้โหลด"), `UnsafePath`, `FileMissing`, `ManifestInvalid`, `EntryNotFound`, `LoadFailed`, `DuplicateId`
- แถบข้างต้องอัปเดตเมื่อสถานะเปลี่ยน (หลังโหลดเสร็จ/สลับ) โดยไม่ทำให้หน้าต่างกระตุกหรือสร้างใหม่ทุกวินาที (สร้างใหม่เมื่อรายการ/สถานะเปลี่ยนจริงเท่านั้น)
- ข้อความของ host (`I18n`) ต้องผ่าน test parity เดิมที่เทียบ key ไทย/อังกฤษ; ส่วนเสริมถือข้อความของตัวเองด้วย `StringTable`

## Scope
`SysMonitor.Extensibility/*` (เพิ่มไฟล์) · `SysMonitor.Wpf/Extensions/*` (ExtensionHost ใหม่; แก้ ExtensionLoader/Options/Result/Status) · `SysMonitor.Wpf/Model/Snapshot.cs` (เพิ่ม `Extensions`) · `Sensors/Sampler.cs` (เฉพาะส่วนใส่ views) · `AppConfig.cs` · `App.xaml.cs` · `ViewModels/WidgetViewModel.Extensions.cs` (ใหม่) + จุดเกี่ยวใน `WidgetViewModel.cs` · `MainWindow.xaml.cs` (แถบข้างเท่านั้น) · `I18n.cs` · tests ใน `SysMonitor.Tests/` (ใหม่) · `SysMonitor.Tests.DummyExtension/` (เพิ่ม dummy ที่เป็น `IViewProvider`)
**ห้ามแก้:** `WidgetViewModel.Smart.cs`, `SmartBoard`, `SmartCore`, การทำงานของ SMART เดิม, ตัวติดตั้ง, repo DiskInfo, กฎความเชื่อถือในเฟส 1

## Acceptance criteria (test ต้องครอบ — Coding Owner รันเอง)
- [ ] ไม่มีส่วนเสริม → ทุก test เดิมผ่าน (ฐาน 463 ผ่าน 1 ข้าม) และไม่มีหัวข้อ "ส่วนเสริม" ในแถบข้าง
- [ ] ส่วนเสริม dummy ที่เป็น `IViewProvider`: เปิดแล้ว (a) หน้า Widget ขึ้นต่อท้ายตามลำดับ (b) section ใน Overall (c) แท็บใน Full พร้อม facts; ปิดแล้วทั้งสามหายและ `Dispose` ถูกเรียก (ตรวจ `Shutdown`); เปิดซ้ำได้ (load ใหม่ ไม่ใช่ของค้าง)
- [ ] ข้อความและสีตาม `language`/`Severity`; หน้า `Message` ไม่วาดแท่ง; `Percent` นอกช่วงถูกบีบ
- [ ] เพดานต่าง ๆ (จำนวน/ความยาว/ช่วงอุณหภูมิ/Key ซ้ำหรือว่าง) มี test; ส่วนเสริมที่ `GetView` โยน exception ไม่ทำให้ sampler/UI ล้ม และ log ไม่ท่วม
- [ ] การเลือกแท็บ `ext:<id>:*` รอดเมื่อ key เปลี่ยน; เปลี่ยนจำนวนหน้า/ชุดหน้าแล้ว Widget rebuild ถูก (เทียบชุด ไม่ใช่แค่จำนวน)
- [ ] `Disabled` ไม่อ่านไฟล์ส่วนเสริม (ตรวจด้วย hook `NativeLoadingForTesting`/ไฟล์ที่เปลี่ยนไม่ได้ hash); `NotApproved` ที่ถูกปิดยังรายงาน `NotApproved`
- [ ] config: ไฟล์เก่าไม่มี `extensions` โหลดได้; ค่าเปิด/ปิดเขียนแล้วอ่านกลับตรง; id ไม่ถูกแปลงชื่อ
- [ ] I18n parity ผ่านด้วยคีย์ใหม่; `StringTable.MissingKeys` มี test
- [ ] `dotnet test SysMonitor.Tests` ผ่านทั้งหมดและ `build-wpf.cmd` สำเร็จ; exe single-file เปิดได้

## Constraints / Non-goals
- ไม่เพิ่ม package; ไม่มี WPF type ใน contract; ไม่ยกระดับสิทธิ์; ไม่ทำปุ่มอนุมัติ (2b); ไม่ย้าย SMART (เฟส 3); ไม่ทำ hot-reload เมื่อไฟล์ส่วนเสริมเปลี่ยนระหว่างรัน (เปลี่ยนแล้วรีสตาร์ท หรือสลับปิด-เปิด)
- ห้ามโหลด/เรียกโค้ดส่วนเสริมบนเธรด UI นอกจาก `Dispose` ตอนปิดแอป; ห้ามให้ส่วนเสริมถือ reference ของ WPF/ViewModel
- รูปแบบโค้ด/คอมเมนต์ตามไฟล์ข้างเคียง; อย่าแก้ไฟล์นอก Scope
- agy รันคำสั่ง shell ไม่ได้ (ใช้เฉพาะเครื่องมืออ่าน/ค้น/แก้/สร้างไฟล์) ผมบูรณาการและรัน build/test เอง

## Commit / diff
ยังไม่มี; Coding Owner commit เมื่อ test และรีวิวผ่าน (ผู้ใช้สั่ง commit เป็นรายเฟสแล้ว)

## Open questions / risks
- `GetView` ถูกเรียกบนเธรด sampler: ส่วนเสริมที่ช้าจะหน่วง sampler (ไม่ใช่ UI) — contract ระบุว่าต้องคืนทันที; ยังไม่มี watchdog (ถ้าจำเป็นเป็นงานแยก)
- ส่วนเสริมรันใน process เดียวกับโฮสต์ (native crash ฆ่าแอป) ตามที่บันทึกใน R-004 ว่านอกขอบเขต
