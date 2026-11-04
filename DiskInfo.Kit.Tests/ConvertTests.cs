using System.Text;
using DiskInfo.Kit.Native;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiskInfo.Kit.Tests;

[TestClass]
public unsafe class ConvertTests
{
    [TestMethod]
    public void Utf8_DecodesAsciiWithNulTerminator()
    {
        byte[] buffer = new byte[32];
        Encoding.UTF8.GetBytes("Samsung SSD 980\0").CopyTo(buffer, 0);

        string result = NativeConvert.Utf8(buffer);
        Assert.AreEqual("Samsung SSD 980", result);
    }

    [TestMethod]
    public void Utf8_DecodesBufferFullWithoutNul()
    {
        byte[] buffer = new byte[16];
        Array.Fill(buffer, (byte)'A');

        string result = NativeConvert.Utf8(buffer);
        Assert.AreEqual(new string('A', 16), result);
    }

    [TestMethod]
    public void Utf8_DecodesThaiUtf8Text()
    {
        string expected = "ทดสอบดิสก์";
        byte[] encoded = Encoding.UTF8.GetBytes(expected + "\0");
        byte[] buffer = new byte[64];
        encoded.CopyTo(buffer, 0);

        string result = NativeConvert.Utf8(buffer);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Utf8_InvalidSequencesDoNotThrowAndUseReplacementChar()
    {
        byte[] invalidBytes = [0xC0, 0xAF, 0xFE, 0xFF, 0x00];
        string result = NativeConvert.Utf8(invalidBytes);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains('\uFFFD'));
    }

    [TestMethod]
    public void ToDisk_SentinelsMapToNull()
    {
        NativeDisk disk = default;
        disk.temperature_c = KitConstants.DIK_NA;
        disk.life_percent = KitConstants.DIK_NA;
        disk.rotation_rpm = KitConstants.DIK_NA;
        disk.power_on_hours = KitConstants.DIK_NA64;
        disk.power_on_count = KitConstants.DIK_NA64;
        disk.capacity_mb = KitConstants.DIK_NA64;
        disk.capacity_bytes = KitConstants.DIK_NA64;
        disk.host_reads_gb = KitConstants.DIK_NA64;
        disk.host_writes_gb = KitConstants.DIK_NA64;
        disk.nand_writes_gb = KitConstants.DIK_NA64;

        DiskSnapshot snapshot = NativeConvert.ToDisk(in disk, Array.Empty<SmartAttribute>());

        Assert.IsNull(snapshot.TemperatureC);
        Assert.IsNull(snapshot.LifePercent);
        Assert.IsNull(snapshot.RotationRpm);
        Assert.IsNull(snapshot.PowerOnHours);
        Assert.IsNull(snapshot.PowerOnCount);
        Assert.IsNull(snapshot.CapacityMb);
        Assert.IsNull(snapshot.CapacityBytes);
        Assert.IsNull(snapshot.HostReadsGb);
        Assert.IsNull(snapshot.HostWritesGb);
        Assert.IsNull(snapshot.NandWritesGb);
    }

    [TestMethod]
    public void ToDisk_NonSentinelValuesPassThroughUnchanged()
    {
        NativeDisk disk = default;
        disk.physical_drive_id = 1;
        disk.temperature_c = 42;
        disk.life_percent = 99;
        disk.rotation_rpm = 7200;
        disk.power_on_hours = 12345;
        disk.power_on_count = 150;
        disk.capacity_mb = 1000204;
        disk.capacity_bytes = 1000204886016L;
        disk.host_reads_gb = 500;
        disk.host_writes_gb = 450;
        disk.nand_writes_gb = 600;
        disk.source = 10;

        DiskSnapshot snapshot = NativeConvert.ToDisk(in disk, Array.Empty<SmartAttribute>());

        Assert.AreEqual(1, snapshot.PhysicalDriveId);
        Assert.AreEqual(42, snapshot.TemperatureC);
        Assert.AreEqual(99, snapshot.LifePercent);
        Assert.AreEqual(7200, snapshot.RotationRpm);
        Assert.AreEqual(12345L, snapshot.PowerOnHours);
        Assert.AreEqual(150L, snapshot.PowerOnCount);
        Assert.AreEqual(1000204L, snapshot.CapacityMb);
        Assert.AreEqual(1000204886016L, snapshot.CapacityBytes);
        Assert.AreEqual(500L, snapshot.HostReadsGb);
        Assert.AreEqual(450L, snapshot.HostWritesGb);
        Assert.AreEqual(600L, snapshot.NandWritesGb);
        Assert.AreEqual(10u, snapshot.Source);
    }

    [TestMethod]
    public void ToDisk_UndefinedEnumValuesMapToUnknown()
    {
        NativeDisk disk = default;
        disk.interface_kind = 999;
        disk.media = 888;
        disk.health = 777;

        DiskSnapshot snapshot = NativeConvert.ToDisk(in disk, Array.Empty<SmartAttribute>());

        Assert.AreEqual(InterfaceKind.Unknown, snapshot.Interface);
        Assert.AreEqual(MediaType.Unknown, snapshot.Media);
        Assert.AreEqual(HealthStatus.Unknown, snapshot.Health);
    }

    [TestMethod]
    public void ToDisk_FeaturesBitmaskCastAsIs()
    {
        NativeDisk disk = default;
        disk.features = KitConstants.DIK_FEAT_SMART | KitConstants.DIK_FEAT_TRIM | KitConstants.DIK_FEAT_DEVSLEEP;

        DiskSnapshot snapshot = NativeConvert.ToDisk(in disk, Array.Empty<SmartAttribute>());

        DiskFeatures expected = DiskFeatures.Smart | DiskFeatures.Trim | DiskFeatures.DevSleep;
        Assert.AreEqual(expected, snapshot.Features);
    }

    [TestMethod]
    public void ToDisk_AccessLevelCastAsIs()
    {
        NativeDisk disk = default;
        disk.access = KitConstants.DIK_ACCESS_PARTIAL;

        DiskSnapshot snapshot = NativeConvert.ToDisk(in disk, Array.Empty<SmartAttribute>());
        Assert.AreEqual(AccessLevel.Partial, snapshot.Access);
    }

    [TestMethod]
    public void ToAttribute_SentinelsMapToNull()
    {
        NativeAttribute attr = default;
        attr.current = KitConstants.DIK_NA;
        attr.worst = KitConstants.DIK_NA;
        attr.threshold = KitConstants.DIK_NA;

        SmartAttribute result = NativeConvert.ToAttribute(in attr, "Smart");

        Assert.IsNull(result.Current);
        Assert.IsNull(result.Worst);
        Assert.IsNull(result.Threshold);
    }

    [TestMethod]
    public void ToAttribute_StatusMapping()
    {
        NativeAttribute attr0 = default; attr0.status = 0;
        NativeAttribute attr1 = default; attr1.status = 1;
        NativeAttribute attr2 = default; attr2.status = 2;
        NativeAttribute attr3 = default; attr3.status = 3;
        NativeAttribute attrX = default; attrX.status = 999;

        Assert.AreEqual(AttributeStatus.Unknown, NativeConvert.ToAttribute(in attr0, "Smart").Status);
        Assert.AreEqual(AttributeStatus.Good, NativeConvert.ToAttribute(in attr1, "Smart").Status);
        Assert.AreEqual(AttributeStatus.Caution, NativeConvert.ToAttribute(in attr2, "Smart").Status);
        Assert.AreEqual(AttributeStatus.Bad, NativeConvert.ToAttribute(in attr3, "Smart").Status);
        Assert.AreEqual(AttributeStatus.Unknown, NativeConvert.ToAttribute(in attrX, "Smart").Status);
    }

    [TestMethod]
    [DataRow(0, InterfaceKind.Unknown)]
    [DataRow(1, InterfaceKind.Pata)]
    [DataRow(2, InterfaceKind.Sata)]
    [DataRow(3, InterfaceKind.Usb)]
    [DataRow(4, InterfaceKind.Ieee1394)]
    [DataRow(5, InterfaceKind.Scsi)]
    [DataRow(6, InterfaceKind.Nvme)]
    [DataRow(7, InterfaceKind.Other)]
    public void ToDisk_DefinedInterfaceKind_MapsCorrectly(int rawInterface, InterfaceKind expected)
    {
        NativeDisk disk = default;
        disk.interface_kind = rawInterface;
        DiskSnapshot snapshot = NativeConvert.ToDisk(in disk, Array.Empty<SmartAttribute>());
        Assert.AreEqual(expected, snapshot.Interface);
    }

    [TestMethod]
    [DataRow(0, MediaType.Unknown)]
    [DataRow(1, MediaType.Hdd)]
    [DataRow(2, MediaType.Ssd)]
    public void ToDisk_DefinedMedia_MapsCorrectly(int rawMedia, MediaType expected)
    {
        NativeDisk disk = default;
        disk.media = rawMedia;
        DiskSnapshot snapshot = NativeConvert.ToDisk(in disk, Array.Empty<SmartAttribute>());
        Assert.AreEqual(expected, snapshot.Media);
    }

    [TestMethod]
    [DataRow(0, HealthStatus.Unknown)]
    [DataRow(1, HealthStatus.Good)]
    [DataRow(2, HealthStatus.Caution)]
    [DataRow(3, HealthStatus.Bad)]
    public void ToDisk_DefinedHealth_MapsCorrectly(int rawHealth, HealthStatus expected)
    {
        NativeDisk disk = default;
        disk.health = rawHealth;
        DiskSnapshot snapshot = NativeConvert.ToDisk(in disk, Array.Empty<SmartAttribute>());
        Assert.AreEqual(expected, snapshot.Health);
    }

    [TestMethod]
    public void Snapshots_Collections_Are_Not_Mutable_Arrays_Or_Lists()
    {
        NativeAttribute attr = default;
        attr.raw_len = 6;
        SmartAttribute smartAttr = NativeConvert.ToAttribute(in attr, "Smart");
        Assert.IsFalse(smartAttr.Raw is byte[], "SmartAttribute.Raw must not be castable to byte[]");
        Assert.IsFalse(smartAttr.Raw is List<byte>, "SmartAttribute.Raw must not be castable to List<byte>");

        NativeDisk disk = default;
        var attrList = new List<SmartAttribute> { smartAttr };
        DiskSnapshot snapshot = NativeConvert.ToDisk(in disk, attrList);
        Assert.IsFalse(snapshot.Attributes is SmartAttribute[], "DiskSnapshot.Attributes must not be castable to SmartAttribute[]");
        Assert.IsFalse(snapshot.Attributes is List<SmartAttribute>, "DiskSnapshot.Attributes must not be castable to List<SmartAttribute>");
    }

    [TestMethod]
    public void ToDisk_FullLengthTextFields_MatchDistinctContent()
    {
        NativeDisk disk = default;
        string expectedModel = new string('M', 96);
        string expectedSerial = new string('S', 48);
        string expectedFirmware = new string('F', 24);
        string expectedInterfaceName = new string('I', 32);
        string expectedTransferCurrent = new string('C', 32);
        string expectedTransferMax = new string('X', 32);
        string expectedStandard = new string('T', 64);
        string expectedDriveLetters = new string('D', 32);
        string expectedSmartKey = new string('K', 32);

        NativeDisk* p = &disk;
        Encoding.ASCII.GetBytes(expectedModel).CopyTo(new Span<byte>(p->model, 96));
        Encoding.ASCII.GetBytes(expectedSerial).CopyTo(new Span<byte>(p->serial, 48));
        Encoding.ASCII.GetBytes(expectedFirmware).CopyTo(new Span<byte>(p->firmware, 24));
        Encoding.ASCII.GetBytes(expectedInterfaceName).CopyTo(new Span<byte>(p->interface_name, 32));
        Encoding.ASCII.GetBytes(expectedTransferCurrent).CopyTo(new Span<byte>(p->transfer_current, 32));
        Encoding.ASCII.GetBytes(expectedTransferMax).CopyTo(new Span<byte>(p->transfer_max, 32));
        Encoding.ASCII.GetBytes(expectedStandard).CopyTo(new Span<byte>(p->standard, 64));
        Encoding.ASCII.GetBytes(expectedDriveLetters).CopyTo(new Span<byte>(p->drive_letters, 32));
        Encoding.ASCII.GetBytes(expectedSmartKey).CopyTo(new Span<byte>(p->smart_key, 32));
        p->interface_kind = 0x41414141;

        Assert.AreEqual(expectedSmartKey, NativeConvert.GetSmartKey(in disk));

        DiskSnapshot snapshot = NativeConvert.ToDisk(in disk, Array.Empty<SmartAttribute>());

        Assert.AreEqual(expectedModel, snapshot.Model);
        Assert.AreEqual(expectedSerial, snapshot.Serial);
        Assert.AreEqual(expectedFirmware, snapshot.Firmware);
        Assert.AreEqual(expectedInterfaceName, snapshot.InterfaceName);
        Assert.AreEqual(expectedTransferCurrent, snapshot.TransferCurrent);
        Assert.AreEqual(expectedTransferMax, snapshot.TransferMax);
        Assert.AreEqual(expectedStandard, snapshot.Standard);
        Assert.AreEqual(expectedDriveLetters, snapshot.DriveLetters);
        Assert.AreEqual(expectedSmartKey, snapshot.SmartKey);
        Assert.AreEqual(InterfaceKind.Unknown, snapshot.Interface);
    }

    [TestMethod]
    [DataRow((byte)6, 6)]
    [DataRow((byte)7, 7)]
    [DataRow((byte)8, 8)]
    [DataRow((byte)0, 0)]
    [DataRow((byte)200, 8)]
    public void ToAttribute_RawLenClamping(byte rawLen, int expectedSlices)
    {
        NativeAttribute attr = default;
        attr.raw_len = rawLen;
        attr.raw[0] = 0x11;
        attr.raw[1] = 0x22;
        attr.raw[2] = 0x33;
        attr.raw[3] = 0x44;
        attr.raw[4] = 0x55;
        attr.raw[5] = 0x66;
        attr.raw[6] = 0x77;
        attr.raw[7] = 0x88;
        attr.raw_value = 0x8877665544332211UL;

        SmartAttribute result = NativeConvert.ToAttribute(in attr, "Smart");

        Assert.AreEqual(expectedSlices, result.Raw.Count);
        for (int i = 0; i < expectedSlices; i++)
        {
            Assert.AreEqual(attr.raw[i], result.Raw[i]);
        }
        Assert.AreEqual(0x8877665544332211UL, result.RawValue);
    }

    [TestMethod]
    public void ToDisk_AlwaysCopiesAttributes_ClearingCallerListDoesNotAffectSnapshot()
    {
        NativeDisk disk = default;
        NativeAttribute attr = default;
        attr.id = 1;
        SmartAttribute smartAttr = NativeConvert.ToAttribute(in attr, "Smart");

        var callerList = new List<SmartAttribute> { smartAttr };
        var readOnlyWrapper = new System.Collections.ObjectModel.ReadOnlyCollection<SmartAttribute>(callerList);

        DiskSnapshot snapshotFromList = NativeConvert.ToDisk(in disk, callerList);
        DiskSnapshot snapshotFromRoc = NativeConvert.ToDisk(in disk, readOnlyWrapper);

        Assert.AreEqual(1, snapshotFromList.Attributes.Count);
        Assert.AreEqual(1, snapshotFromRoc.Attributes.Count);

        callerList.Clear();
        Assert.AreEqual(0, callerList.Count);

        Assert.AreEqual(1, snapshotFromList.Attributes.Count);
        Assert.AreEqual(1, snapshotFromRoc.Attributes.Count);
    }
}
