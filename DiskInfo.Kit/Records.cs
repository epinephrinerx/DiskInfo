namespace DiskInfo.Kit;

/// <summary>
/// Immutable snapshot of an individual S.M.A.R.T. attribute entry.
/// </summary>
/// <param name="Id">Attribute ID (e.g. 0x05 for Reallocated Sectors Count).</param>
/// <param name="Name">English attribute name looked up from specification tables, or null if unknown / vendor-specific.</param>
/// <param name="IsGenericName">
/// True if the name was resolved via generic fallback from the standard ATA table for a vendor key; false if exact match.
/// </param>
/// <param name="Current">Current normalized value, or null if not applicable (e.g. NVMe).</param>
/// <param name="Worst">Worst normalized value, or null if not applicable.</param>
/// <param name="Threshold">Threshold value, or null if not available.</param>
/// <param name="Raw">Raw byte sequence (length clamped to 0..8 valid bytes according to device specification).</param>
/// <param name="RawValue">Raw value decoded as a 64-bit little-endian integer.</param>
/// <param name="Status">Health status evaluation of this attribute.</param>
public sealed record SmartAttribute(
    byte Id,
    string? Name,
    bool IsGenericName,
    int? Current,
    int? Worst,
    int? Threshold,
    IReadOnlyList<byte> Raw,
    ulong RawValue,
    AttributeStatus Status);

/// <summary>
/// Immutable snapshot representing complete disk details and its S.M.A.R.T. attributes.
/// </summary>
/// <param name="PhysicalDriveId">Physical drive index (N in \\.\PhysicalDriveN on Windows).</param>
/// <param name="Model">Disk model name (UTF-8 string).</param>
/// <param name="Serial">Disk serial number (UTF-8 string).</param>
/// <param name="Firmware">Firmware revision string (UTF-8 string).</param>
/// <param name="InterfaceName">Descriptive interface string (e.g. "Serial ATA", "NVM Express").</param>
/// <param name="TransferCurrent">Current transfer mode string (e.g. "SATA/600", "PCIe 4.0 x4").</param>
/// <param name="TransferMax">Maximum supported transfer mode string.</param>
/// <param name="Standard">Major and minor ATA/NVMe standard versions (as displayed in the GUI).</param>
/// <param name="DriveLetters">Drive letters mounted on this disk (e.g. "C: D:").</param>
/// <param name="SmartKey">Key identifying the S.M.A.R.T. table (e.g. "Smart", "SmartNVMe").</param>
/// <param name="Interface">Classified interface bus kind.</param>
/// <param name="Media">Storage media classification (HDD vs SSD).</param>
/// <param name="Health">Overall disk health status.</param>
/// <param name="TemperatureC">Current disk temperature in degrees Celsius, or null if not reported.</param>
/// <param name="LifePercent">Estimated remaining drive life percentage (0..100), or null if not reported.</param>
/// <param name="RotationRpm">Nominal media rotation rate in RPM (positive integer > 1), or null for SSDs / unreported.</param>
/// <param name="PowerOnHours">Total accumulated power-on hours, or null if not reported.</param>
/// <param name="PowerOnCount">Total power cycle count, or null if not reported.</param>
/// <param name="CapacityMb">Disk capacity in decimal megabytes (MB = 10^6 bytes as printed by the GUI), or null if 0 / unreported.</param>
/// <param name="CapacityBytes">Exact capacity in bytes (sectors * sector size on ATA), or null on NVMe or if unavailable.</param>
/// <param name="HostReadsGb">Total cumulative host reads in gigabytes (GB of 2^30 bytes), or null if not reported.</param>
/// <param name="HostWritesGb">Total cumulative host writes in gigabytes (GB of 2^30 bytes), or null if not reported.</param>
/// <param name="NandWritesGb">Total cumulative NAND writes in gigabytes (GB of 2^30 bytes), or null if not reported.</param>
/// <param name="Features">Supported and active disk feature bitmask flags.</param>
/// <param name="Access">S.M.A.R.T. access level achieved during enumeration.</param>
/// <param name="Source">Command type or driver access method identifier used to query the disk.</param>
/// <param name="Attributes">List of S.M.A.R.T. attributes associated with this disk.</param>
public sealed record DiskSnapshot(
    int PhysicalDriveId,
    string Model,
    string Serial,
    string Firmware,
    string InterfaceName,
    string TransferCurrent,
    string TransferMax,
    string Standard,
    string DriveLetters,
    string SmartKey,
    InterfaceKind Interface,
    MediaType Media,
    HealthStatus Health,
    int? TemperatureC,
    int? LifePercent,
    int? RotationRpm,
    long? PowerOnHours,
    long? PowerOnCount,
    long? CapacityMb,
    long? CapacityBytes,
    long? HostReadsGb,
    long? HostWritesGb,
    long? NandWritesGb,
    DiskFeatures Features,
    AccessLevel Access,
    uint Source,
    IReadOnlyList<SmartAttribute> Attributes);
