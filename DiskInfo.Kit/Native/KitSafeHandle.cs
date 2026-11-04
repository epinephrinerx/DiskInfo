using Microsoft.Win32.SafeHandles;

namespace DiskInfo.Kit.Native;

/// <summary>
/// SafeHandle wrapping a native dik_handle pointer.
/// Calls dik_close upon release.
/// </summary>
internal sealed class KitSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public KitSafeHandle() : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
    {
        KitNative.dik_close(handle);
        return true;
    }
}
