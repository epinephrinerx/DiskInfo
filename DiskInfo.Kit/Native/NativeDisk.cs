using System.Runtime.InteropServices;

namespace DiskInfo.Kit.Native;

/// <summary>
/// Unsafe native structure matching dik_disk_t in diskinfokit.h (size 496 bytes).
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 496)]
internal unsafe struct NativeDisk
{
    [FieldOffset(0)]
    public uint struct_size;

    [FieldOffset(4)]
    public int physical_drive_id;

    [FieldOffset(8)]
    public fixed byte model[96];

    [FieldOffset(104)]
    public fixed byte serial[48];

    [FieldOffset(152)]
    public fixed byte firmware[24];

    [FieldOffset(176)]
    public fixed byte interface_name[32];

    [FieldOffset(208)]
    public fixed byte transfer_current[32];

    [FieldOffset(240)]
    public fixed byte transfer_max[32];

    [FieldOffset(272)]
    public fixed byte standard[64];

    [FieldOffset(336)]
    public fixed byte drive_letters[32];

    [FieldOffset(368)]
    public fixed byte smart_key[32];

    [FieldOffset(400)]
    public int interface_kind;

    [FieldOffset(404)]
    public int media;

    [FieldOffset(408)]
    public int health;

    [FieldOffset(412)]
    public int temperature_c;

    [FieldOffset(416)]
    public int life_percent;

    [FieldOffset(420)]
    public int rotation_rpm;

    [FieldOffset(424)]
    public long power_on_hours;

    [FieldOffset(432)]
    public long power_on_count;

    [FieldOffset(440)]
    public long capacity_mb;

    [FieldOffset(448)]
    public long capacity_bytes;

    [FieldOffset(456)]
    public long host_reads_gb;

    [FieldOffset(464)]
    public long host_writes_gb;

    [FieldOffset(472)]
    public long nand_writes_gb;

    [FieldOffset(480)]
    public uint features;

    [FieldOffset(484)]
    public uint access;

    [FieldOffset(488)]
    public uint source;

    [FieldOffset(492)]
    public int attribute_count;
}
