using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace DiskInfo.Kit.Native;

/// <summary>
/// Pure conversion helpers between native structs and managed snapshot records.
/// </summary>
internal static class NativeConvert
{
    /// <summary>
    /// Decodes a UTF-8 byte span up to the first NUL byte, or the entire span if no NUL is present.
    /// Invalid UTF-8 byte sequences are replaced with U+FFFD and will never throw.
    /// </summary>
    public static string Utf8(ReadOnlySpan<byte> buffer)
    {
        int nullIndex = buffer.IndexOf((byte)0);
        ReadOnlySpan<byte> slice = nullIndex >= 0 ? buffer[..nullIndex] : buffer;
        return Encoding.UTF8.GetString(slice);
    }

    /// <summary>
    /// Extracts the smart_key string from a native disk structure.
    /// </summary>
    public static unsafe string GetSmartKey(in NativeDisk d)
    {
        fixed (NativeDisk* p = &Unsafe.AsRef(in d))
        {
            return Utf8(new ReadOnlySpan<byte>(p->smart_key, 32));
        }
    }

    /// <summary>
    /// Converts a native disk structure and its associated attributes into an immutable <see cref="DiskSnapshot"/>.
    /// </summary>
    public static unsafe DiskSnapshot ToDisk(in NativeDisk d, IReadOnlyList<SmartAttribute> attributes)
    {
        fixed (NativeDisk* p = &Unsafe.AsRef(in d))
        {
            string model = Utf8(new ReadOnlySpan<byte>(p->model, 96));
            string serial = Utf8(new ReadOnlySpan<byte>(p->serial, 48));
            string firmware = Utf8(new ReadOnlySpan<byte>(p->firmware, 24));
            string interfaceName = Utf8(new ReadOnlySpan<byte>(p->interface_name, 32));
            string transferCurrent = Utf8(new ReadOnlySpan<byte>(p->transfer_current, 32));
            string transferMax = Utf8(new ReadOnlySpan<byte>(p->transfer_max, 32));
            string standard = Utf8(new ReadOnlySpan<byte>(p->standard, 64));
            string driveLetters = Utf8(new ReadOnlySpan<byte>(p->drive_letters, 32));
            string smartKey = GetSmartKey(in d);

            InterfaceKind iface = d.interface_kind switch
            {
                KitConstants.DIK_IF_UNKNOWN => InterfaceKind.Unknown,
                KitConstants.DIK_IF_PATA => InterfaceKind.Pata,
                KitConstants.DIK_IF_SATA => InterfaceKind.Sata,
                KitConstants.DIK_IF_USB => InterfaceKind.Usb,
                KitConstants.DIK_IF_IEEE1394 => InterfaceKind.Ieee1394,
                KitConstants.DIK_IF_SCSI => InterfaceKind.Scsi,
                KitConstants.DIK_IF_NVME => InterfaceKind.Nvme,
                KitConstants.DIK_IF_OTHER => InterfaceKind.Other,
                _ => InterfaceKind.Unknown
            };

            MediaType media = d.media switch
            {
                KitConstants.DIK_MEDIA_UNKNOWN => MediaType.Unknown,
                KitConstants.DIK_MEDIA_HDD => MediaType.Hdd,
                KitConstants.DIK_MEDIA_SSD => MediaType.Ssd,
                _ => MediaType.Unknown
            };

            HealthStatus health = d.health switch
            {
                KitConstants.DIK_HEALTH_UNKNOWN => HealthStatus.Unknown,
                KitConstants.DIK_HEALTH_GOOD => HealthStatus.Good,
                KitConstants.DIK_HEALTH_CAUTION => HealthStatus.Caution,
                KitConstants.DIK_HEALTH_BAD => HealthStatus.Bad,
                _ => HealthStatus.Unknown
            };

            int? tempC = d.temperature_c == KitConstants.DIK_NA ? null : d.temperature_c;
            int? life = d.life_percent == KitConstants.DIK_NA ? null : d.life_percent;
            int? rpm = d.rotation_rpm == KitConstants.DIK_NA ? null : d.rotation_rpm;

            long? poh = d.power_on_hours == KitConstants.DIK_NA64 ? null : d.power_on_hours;
            long? poc = d.power_on_count == KitConstants.DIK_NA64 ? null : d.power_on_count;
            long? capMb = d.capacity_mb == KitConstants.DIK_NA64 ? null : d.capacity_mb;
            long? capBytes = d.capacity_bytes == KitConstants.DIK_NA64 ? null : d.capacity_bytes;
            long? readsGb = d.host_reads_gb == KitConstants.DIK_NA64 ? null : d.host_reads_gb;
            long? writesGb = d.host_writes_gb == KitConstants.DIK_NA64 ? null : d.host_writes_gb;
            long? nandGb = d.nand_writes_gb == KitConstants.DIK_NA64 ? null : d.nand_writes_gb;

            DiskFeatures features = (DiskFeatures)d.features;
            AccessLevel access = (AccessLevel)d.access;

            ReadOnlyCollection<SmartAttribute> safeAttributes =
                Array.AsReadOnly(attributes.ToArray());

            return new DiskSnapshot(
                PhysicalDriveId: d.physical_drive_id,
                Model: model,
                Serial: serial,
                Firmware: firmware,
                InterfaceName: interfaceName,
                TransferCurrent: transferCurrent,
                TransferMax: transferMax,
                Standard: standard,
                DriveLetters: driveLetters,
                SmartKey: smartKey,
                Interface: iface,
                Media: media,
                Health: health,
                TemperatureC: tempC,
                LifePercent: life,
                RotationRpm: rpm,
                PowerOnHours: poh,
                PowerOnCount: poc,
                CapacityMb: capMb,
                CapacityBytes: capBytes,
                HostReadsGb: readsGb,
                HostWritesGb: writesGb,
                NandWritesGb: nandGb,
                Features: features,
                Access: access,
                Source: d.source,
                Attributes: safeAttributes);
        }
    }

    /// <summary>
    /// Converts a native attribute structure into an immutable <see cref="SmartAttribute"/>.
    /// </summary>
    public static unsafe SmartAttribute ToAttribute(in NativeAttribute a, string smartKey)
    {
        AttributeName? nameLookup = AttributeNames.Lookup(smartKey, a.id);
        string? name = nameLookup?.Text;
        bool isGeneric = nameLookup?.IsGeneric ?? false;

        int? current = a.current == KitConstants.DIK_NA ? null : a.current;
        int? worst = a.worst == KitConstants.DIK_NA ? null : a.worst;
        int? threshold = a.threshold == KitConstants.DIK_NA ? null : a.threshold;

        int len = Math.Clamp((int)a.raw_len, 0, 8);
        byte[] rawBytes = new byte[len];
        fixed (NativeAttribute* p = &Unsafe.AsRef(in a))
        {
            for (int i = 0; i < len; i++)
            {
                rawBytes[i] = p->raw[i];
            }
        }

        AttributeStatus status = a.status switch
        {
            KitConstants.DIK_ATTR_STATUS_UNKNOWN => AttributeStatus.Unknown,
            KitConstants.DIK_ATTR_STATUS_GOOD => AttributeStatus.Good,
            KitConstants.DIK_ATTR_STATUS_CAUTION => AttributeStatus.Caution,
            KitConstants.DIK_ATTR_STATUS_BAD => AttributeStatus.Bad,
            _ => AttributeStatus.Unknown
        };

        return new SmartAttribute(
            Id: a.id,
            Name: name,
            IsGenericName: isGeneric,
            Current: current,
            Worst: worst,
            Threshold: threshold,
            Raw: Array.AsReadOnly(rawBytes),
            RawValue: a.raw_value,
            Status: status);
    }
}
