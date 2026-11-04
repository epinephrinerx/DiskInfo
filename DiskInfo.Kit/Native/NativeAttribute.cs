using System.Runtime.InteropServices;

namespace DiskInfo.Kit.Native;

/// <summary>
/// Unsafe native structure matching dik_attribute_t in diskinfokit.h (size 48 bytes).
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 48)]
internal unsafe struct NativeAttribute
{
    [FieldOffset(0)]
    public uint struct_size;

    [FieldOffset(4)]
    public byte id;

    [FieldOffset(5)]
    public byte raw_len;

    [FieldOffset(6)]
    public fixed byte reserved[2];

    [FieldOffset(8)]
    public int current;

    [FieldOffset(12)]
    public int worst;

    [FieldOffset(16)]
    public int threshold;

    [FieldOffset(20)]
    public fixed byte raw[8];

    [FieldOffset(28)]
    public fixed byte reserved2[4];

    [FieldOffset(32)]
    public ulong raw_value;

    [FieldOffset(40)]
    public int status;

    [FieldOffset(44)]
    public fixed byte reserved3[4];
}
