namespace DiskInfo.Kit;

/// <summary>
/// Status and error codes returned by native DiskInfoKit operations.
/// </summary>
public enum KitError
{
    /// <summary>Operation completed successfully.</summary>
    Ok = 0,

    /// <summary>Invalid argument or null pointer supplied.</summary>
    Argument = -1,

    /// <summary>Access denied; administrator rights are required on Windows.</summary>
    Access = -2,

    /// <summary>Disk or attribute index was not found / out of range.</summary>
    NotFound = -3,

    /// <summary>I/O or COM communication failure.</summary>
    Io = -4,

    /// <summary>Unsupported operation or library version mismatch.</summary>
    Unsupported = -5,

    /// <summary>Struct size passed is smaller than supported version.</summary>
    StructSize = -6
}

/// <summary>
/// Disk bus and connection interface kinds.
/// </summary>
public enum InterfaceKind
{
    /// <summary>Unknown or unrecognized interface.</summary>
    Unknown,

    /// <summary>Parallel ATA (IDE/PATA).</summary>
    Pata,

    /// <summary>Serial ATA (SATA).</summary>
    Sata,

    /// <summary>Universal Serial Bus (USB).</summary>
    Usb,

    /// <summary>IEEE 1394 (FireWire).</summary>
    Ieee1394,

    /// <summary>Small Computer System Interface (SCSI / SAS).</summary>
    Scsi,

    /// <summary>NVM Express (NVMe).</summary>
    Nvme,

    /// <summary>Other interface type.</summary>
    Other
}

/// <summary>
/// Storage media types.
/// </summary>
public enum MediaType
{
    /// <summary>Unknown or indeterminate media type.</summary>
    Unknown,

    /// <summary>Hard Disk Drive (rotational magnetic media).</summary>
    Hdd,

    /// <summary>Solid State Drive (flash/solid-state media).</summary>
    Ssd
}

/// <summary>
/// Disk overall health assessment.
/// </summary>
public enum HealthStatus
{
    /// <summary>Health status is unknown or undetermined.</summary>
    Unknown,

    /// <summary>Disk health is good.</summary>
    Good,

    /// <summary>Caution; one or more warning thresholds or error counters triggered.</summary>
    Caution,

    /// <summary>Bad; critical failure or threshold exceeded.</summary>
    Bad
}

/// <summary>
/// Health status assessment of an individual S.M.A.R.T. attribute.
/// </summary>
public enum AttributeStatus
{
    /// <summary>Status unknown or not assessed.</summary>
    Unknown,

    /// <summary>Attribute is within normal operating limits.</summary>
    Good,

    /// <summary>Attribute indicates caution or potential degradation.</summary>
    Caution,

    /// <summary>Attribute indicates failure or critical condition.</summary>
    Bad
}

/// <summary>
/// S.M.A.R.T. access level attained during enumeration.
/// </summary>
public enum AccessLevel : uint
{
    /// <summary>Full access: S.M.A.R.T. data read successfully and checksum verified.</summary>
    Full = 0,

    /// <summary>Partial access: disk found, but S.M.A.R.T. data missing or checksum error.</summary>
    Partial = 1
}

/// <summary>
/// Bitmask flags representing supported and enabled disk features.
/// </summary>
[Flags]
public enum DiskFeatures : uint
{
    /// <summary>No features enabled or detected.</summary>
    None = 0,

    /// <summary>S.M.A.R.T. monitoring supported.</summary>
    Smart = 0x1,

    /// <summary>Advanced Power Management (APM) supported.</summary>
    Apm = 0x2,

    /// <summary>Automatic Acoustic Management (AAM) supported.</summary>
    Aam = 0x4,

    /// <summary>Native Command Queuing (NCQ) supported.</summary>
    Ncq = 0x8,

    /// <summary>TRIM dataset management supported.</summary>
    Trim = 0x10,

    /// <summary>Device Sleep (DevSleep) supported.</summary>
    DevSleep = 0x20,

    /// <summary>Streaming feature set supported.</summary>
    Streaming = 0x40,

    /// <summary>General Purpose Logging (GPL) supported.</summary>
    Gpl = 0x80,

    /// <summary>Volatile write cache present.</summary>
    VolatileWriteCache = 0x100,

    /// <summary>48-bit LBA addressing supported.</summary>
    Lba48 = 0x200,

    /// <summary>NV Cache feature set supported.</summary>
    NvCache = 0x400,

    /// <summary>Advanced Power Management (APM) currently enabled.</summary>
    ApmEnabled = 0x800,

    /// <summary>Automatic Acoustic Management (AAM) currently enabled.</summary>
    AamEnabled = 0x1000
}

/// <summary>
/// Options and flags for opening a DiskInfoKit session.
/// </summary>
[Flags]
public enum KitOpenFlags : uint
{
    /// <summary>Default options.</summary>
    None = 0,

    /// <summary>Skip disk enumeration via WMI / COM.</summary>
    NoWmi = 0x1
}
