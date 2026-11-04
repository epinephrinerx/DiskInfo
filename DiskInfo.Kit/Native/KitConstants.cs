namespace DiskInfo.Kit.Native;

/// <summary>
/// Native constants mirroring diskinfokit.h.
/// </summary>
internal static class KitConstants
{
    public const int DIK_API_MAJOR = 1;
    public const int DIK_API_MINOR = 0;

    public const int DIK_NA = int.MinValue;
    public const long DIK_NA64 = long.MinValue;

    public const int DIK_OK = 0;
    public const int DIK_E_ARG = -1;
    public const int DIK_E_ACCESS = -2;
    public const int DIK_E_NOT_FOUND = -3;
    public const int DIK_E_IO = -4;
    public const int DIK_E_UNSUPPORTED = -5;
    public const int DIK_E_STRUCT_SIZE = -6;

    public const uint DIK_OPEN_NO_WMI = 0x00000001u;

    public const int DIK_IF_UNKNOWN = 0;
    public const int DIK_IF_PATA = 1;
    public const int DIK_IF_SATA = 2;
    public const int DIK_IF_USB = 3;
    public const int DIK_IF_IEEE1394 = 4;
    public const int DIK_IF_SCSI = 5;
    public const int DIK_IF_NVME = 6;
    public const int DIK_IF_OTHER = 7;

    public const int DIK_MEDIA_UNKNOWN = 0;
    public const int DIK_MEDIA_HDD = 1;
    public const int DIK_MEDIA_SSD = 2;

    public const int DIK_HEALTH_UNKNOWN = 0;
    public const int DIK_HEALTH_GOOD = 1;
    public const int DIK_HEALTH_CAUTION = 2;
    public const int DIK_HEALTH_BAD = 3;

    public const uint DIK_ACCESS_FULL = 0u;
    public const uint DIK_ACCESS_PARTIAL = 1u;

    public const uint DIK_FEAT_SMART = 0x0001u;
    public const uint DIK_FEAT_APM = 0x0002u;
    public const uint DIK_FEAT_AAM = 0x0004u;
    public const uint DIK_FEAT_NCQ = 0x0008u;
    public const uint DIK_FEAT_TRIM = 0x0010u;
    public const uint DIK_FEAT_DEVSLEEP = 0x0020u;
    public const uint DIK_FEAT_STREAMING = 0x0040u;
    public const uint DIK_FEAT_GPL = 0x0080u;
    public const uint DIK_FEAT_VWC = 0x0100u;
    public const uint DIK_FEAT_LBA48 = 0x0200u;
    public const uint DIK_FEAT_NVCACHE = 0x0400u;
    public const uint DIK_FEAT_APM_ON = 0x0800u;
    public const uint DIK_FEAT_AAM_ON = 0x1000u;

    public const int DIK_ATTR_STATUS_UNKNOWN = 0;
    public const int DIK_ATTR_STATUS_GOOD = 1;
    public const int DIK_ATTR_STATUS_CAUTION = 2;
    public const int DIK_ATTR_STATUS_BAD = 3;

    public const int DISK_STRUCT_SIZE = 496;
    public const int ATTRIBUTE_STRUCT_SIZE = 48;
}
