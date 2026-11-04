using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DiskInfo.Kit.Native;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiskInfo.Kit.Tests;

[TestClass]
public unsafe class LayoutTests
{
    [TestMethod]
    public void NativeDisk_Size_Is_496()
    {
        Assert.AreEqual(496, sizeof(NativeDisk));
        Assert.AreEqual(496, Unsafe.SizeOf<NativeDisk>());
    }

    [TestMethod]
    public void NativeAttribute_Size_Is_48()
    {
        Assert.AreEqual(48, sizeof(NativeAttribute));
        Assert.AreEqual(48, Unsafe.SizeOf<NativeAttribute>());
    }

    [TestMethod]
    public void NativeDisk_FieldOffsets_Match_Contract()
    {
        Assert.AreEqual(0, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.struct_size)).ToInt32());
        Assert.AreEqual(4, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.physical_drive_id)).ToInt32());
        Assert.AreEqual(8, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.model)).ToInt32());
        Assert.AreEqual(104, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.serial)).ToInt32());
        Assert.AreEqual(152, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.firmware)).ToInt32());
        Assert.AreEqual(176, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.interface_name)).ToInt32());
        Assert.AreEqual(208, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.transfer_current)).ToInt32());
        Assert.AreEqual(240, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.transfer_max)).ToInt32());
        Assert.AreEqual(272, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.standard)).ToInt32());
        Assert.AreEqual(336, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.drive_letters)).ToInt32());
        Assert.AreEqual(368, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.smart_key)).ToInt32());
        Assert.AreEqual(400, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.interface_kind)).ToInt32());
        Assert.AreEqual(404, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.media)).ToInt32());
        Assert.AreEqual(408, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.health)).ToInt32());
        Assert.AreEqual(412, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.temperature_c)).ToInt32());
        Assert.AreEqual(416, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.life_percent)).ToInt32());
        Assert.AreEqual(420, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.rotation_rpm)).ToInt32());
        Assert.AreEqual(424, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.power_on_hours)).ToInt32());
        Assert.AreEqual(432, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.power_on_count)).ToInt32());
        Assert.AreEqual(440, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.capacity_mb)).ToInt32());
        Assert.AreEqual(448, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.capacity_bytes)).ToInt32());
        Assert.AreEqual(456, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.host_reads_gb)).ToInt32());
        Assert.AreEqual(464, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.host_writes_gb)).ToInt32());
        Assert.AreEqual(472, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.nand_writes_gb)).ToInt32());
        Assert.AreEqual(480, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.features)).ToInt32());
        Assert.AreEqual(484, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.access)).ToInt32());
        Assert.AreEqual(488, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.source)).ToInt32());
        Assert.AreEqual(492, Marshal.OffsetOf<NativeDisk>(nameof(NativeDisk.attribute_count)).ToInt32());
    }

    [TestMethod]
    public void NativeAttribute_FieldOffsets_Match_Contract()
    {
        Assert.AreEqual(0, Marshal.OffsetOf<NativeAttribute>(nameof(NativeAttribute.struct_size)).ToInt32());
        Assert.AreEqual(4, Marshal.OffsetOf<NativeAttribute>(nameof(NativeAttribute.id)).ToInt32());
        Assert.AreEqual(5, Marshal.OffsetOf<NativeAttribute>(nameof(NativeAttribute.raw_len)).ToInt32());
        Assert.AreEqual(6, Marshal.OffsetOf<NativeAttribute>(nameof(NativeAttribute.reserved)).ToInt32());
        Assert.AreEqual(8, Marshal.OffsetOf<NativeAttribute>(nameof(NativeAttribute.current)).ToInt32());
        Assert.AreEqual(12, Marshal.OffsetOf<NativeAttribute>(nameof(NativeAttribute.worst)).ToInt32());
        Assert.AreEqual(16, Marshal.OffsetOf<NativeAttribute>(nameof(NativeAttribute.threshold)).ToInt32());
        Assert.AreEqual(20, Marshal.OffsetOf<NativeAttribute>(nameof(NativeAttribute.raw)).ToInt32());
        Assert.AreEqual(28, Marshal.OffsetOf<NativeAttribute>(nameof(NativeAttribute.reserved2)).ToInt32());
        Assert.AreEqual(32, Marshal.OffsetOf<NativeAttribute>(nameof(NativeAttribute.raw_value)).ToInt32());
        Assert.AreEqual(40, Marshal.OffsetOf<NativeAttribute>(nameof(NativeAttribute.status)).ToInt32());
        Assert.AreEqual(44, Marshal.OffsetOf<NativeAttribute>(nameof(NativeAttribute.reserved3)).ToInt32());
    }
}
