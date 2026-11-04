using System.Collections.Frozen;

namespace DiskInfo.Kit;

// S.M.A.R.T. attribute names written from the ATA and NVMe specifications.
// English language only; CrystalDiskInfo language files are not copied.
// Dedicated vendor tables (~48 vendor-specific keys such as SmartSsd, SmartSandForce)
// are future work. Any non-"Smart"/"SmartNVMe" key falls back to the generic "Smart" table
// with IsGeneric set to true. NVMe keys never fall back.

/// <summary>
/// Resolved name information for a S.M.A.R.T. attribute.
/// </summary>
/// <param name="Text">The English name of the attribute.</param>
/// <param name="IsGeneric">
/// <see langword="true"/> if the name was obtained via fallback to the generic ATA table
/// for a vendor-specific key; <see langword="false"/> if it matched an exact specification table.
/// </param>
public readonly record struct AttributeName(string Text, bool IsGeneric);

/// <summary>
/// Provides name lookup for S.M.A.R.T. attributes based on specification tables.
/// </summary>
public static class AttributeNames
{
    private static readonly FrozenDictionary<byte, string> s_nvmeTable = new Dictionary<byte, string>
    {
        [0x01] = "Critical Warning",
        [0x02] = "Composite Temperature",
        [0x03] = "Available Spare",
        [0x04] = "Available Spare Threshold",
        [0x05] = "Percentage Used",
        [0x06] = "Data Units Read",
        [0x07] = "Data Units Written",
        [0x08] = "Host Read Commands",
        [0x09] = "Host Write Commands",
        [0x0A] = "Controller Busy Time",
        [0x0B] = "Power Cycles",
        [0x0C] = "Power On Hours",
        [0x0D] = "Unsafe Shutdowns",
        [0x0E] = "Media and Data Integrity Errors",
        [0x0F] = "Number of Error Information Log Entries",
        [0x10] = "Warning Composite Temperature Time",
        [0x11] = "Critical Composite Temperature Time",
        [0x12] = "Temperature Sensor 1",
        [0x13] = "Temperature Sensor 2",
        [0x14] = "Temperature Sensor 3",
        [0x15] = "Temperature Sensor 4",
        [0x16] = "Temperature Sensor 5",
        [0x17] = "Temperature Sensor 6",
        [0x18] = "Temperature Sensor 7",
        [0x19] = "Temperature Sensor 8",
        [0x1A] = "Thermal Management Temperature 1 Transition Count",
        [0x1B] = "Thermal Management Temperature 2 Transition Count",
        [0x1C] = "Total Time For Thermal Management Temperature 1",
        [0x1D] = "Total Time For Thermal Management Temperature 2",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<byte, string> s_smartTable = new Dictionary<byte, string>
    {
        [0x01] = "Read Error Rate",
        [0x02] = "Throughput Performance",
        [0x03] = "Spin-Up Time",
        [0x04] = "Start/Stop Count",
        [0x05] = "Reallocated Sectors Count",
        [0x06] = "Read Channel Margin",
        [0x07] = "Seek Error Rate",
        [0x08] = "Seek Time Performance",
        [0x09] = "Power-On Hours",
        [0x0A] = "Spin Retry Count",
        [0x0B] = "Recalibration Retries",
        [0x0C] = "Power Cycle Count",
        [0x0D] = "Soft Read Error Rate",
        [0x16] = "Current Helium Level",
        [0xB7] = "SATA Downshift Error Count",
        [0xB8] = "End-to-End Error",
        [0xBB] = "Reported Uncorrectable Errors",
        [0xBC] = "Command Timeout",
        [0xBD] = "High Fly Writes",
        [0xBE] = "Airflow Temperature",
        [0xBF] = "G-Sense Error Rate",
        [0xC0] = "Power-off Retract Count",
        [0xC1] = "Load/Unload Cycle Count",
        [0xC2] = "Temperature",
        [0xC3] = "Hardware ECC Recovered",
        [0xC4] = "Reallocation Event Count",
        [0xC5] = "Current Pending Sector Count",
        [0xC6] = "Uncorrectable Sector Count",
        [0xC7] = "UltraDMA CRC Error Count",
        [0xC8] = "Write Error Rate",
        [0xCA] = "Data Address Mark Errors",
        [0xCB] = "Run Out Cancel",
        [0xCC] = "Soft ECC Correction",
        [0xCD] = "Thermal Asperity Rate",
        [0xCE] = "Flying Height",
        [0xCF] = "Spin High Current",
        [0xD0] = "Spin Buzz",
        [0xD1] = "Offline Seek Performance",
        [0xDC] = "Disk Shift",
        [0xDD] = "G-Sense Error Rate",
        [0xDE] = "Loaded Hours",
        [0xDF] = "Load/Unload Retry Count",
        [0xE0] = "Load Friction",
        [0xE1] = "Load/Unload Cycle Count",
        [0xE2] = "Load 'In'-time",
        [0xE3] = "Torque Amplification Count",
        [0xE4] = "Power-Off Retract Cycle",
        [0xE6] = "GMR Head Amplitude",
        [0xF0] = "Head Flying Hours",
        [0xF1] = "Total LBAs Written",
        [0xF2] = "Total LBAs Read",
        [0xFA] = "Read Error Retry Rate",
        [0xFE] = "Free Fall Protection",
    }.ToFrozenDictionary();

    /// <summary>
    /// Looks up the English S.M.A.R.T. attribute name for a given smart_key and attribute ID.
    /// Comparison of smartKey is ordinal (case-sensitive).
    /// </summary>
    /// <param name="smartKey">The key identifying the attribute table (e.g. "Smart", "SmartNVMe").</param>
    /// <param name="id">The attribute ID.</param>
    /// <returns>
    /// An <see cref="AttributeName"/> instance if found; otherwise, <see langword="null"/>
    /// (caller should display "Vendor Specific").
    /// </returns>
    public static AttributeName? Lookup(string? smartKey, byte id)
    {
        if (string.IsNullOrEmpty(smartKey))
        {
            return null;
        }

        if (string.Equals(smartKey, "SmartNVMe", StringComparison.Ordinal))
        {
            if (s_nvmeTable.TryGetValue(id, out string? nvmeText))
            {
                return new AttributeName(nvmeText, IsGeneric: false);
            }
            return null; // NVMe IDs never fall back
        }

        if (string.Equals(smartKey, "Smart", StringComparison.Ordinal))
        {
            if (s_smartTable.TryGetValue(id, out string? smartText))
            {
                return new AttributeName(smartText, IsGeneric: false);
            }
            return null;
        }

        // Vendor keys fall back to the generic "Smart" table with IsGeneric = true
        if (s_smartTable.TryGetValue(id, out string? fallbackText))
        {
            return new AttributeName(fallbackText, IsGeneric: true);
        }

        return null;
    }
}
