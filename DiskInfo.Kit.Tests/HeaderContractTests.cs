using System.Globalization;
using System.Text.RegularExpressions;
using DiskInfo.Kit.Native;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiskInfo.Kit.Tests;

[TestClass]
public class HeaderContractTests
{
    private static readonly HashSet<string> AllowedNonNumericMacros = new(StringComparer.Ordinal)
    {
        "DIK_CALL",
    };

    [TestMethod]
    public void DiskinfokitHeader_Defines_Match_Managed_Enums_And_Constants()
    {
        string? dir = AppContext.BaseDirectory;
        string? headerPath = null;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "DiskInfoKit", "include", "diskinfokit.h");
            if (File.Exists(candidate))
            {
                headerPath = candidate;
                break;
            }
            DirectoryInfo? parent = Directory.GetParent(dir);
            dir = parent?.FullName;
        }

        Assert.IsNotNull(headerPath, $"diskinfokit.h was not found by walking up from {AppContext.BaseDirectory}.");

        string[] lines = File.ReadAllLines(headerPath);
        var defines = new Dictionary<string, long>(StringComparer.Ordinal);

        var regex = new Regex(@"^\s*#define\s+(DIK_\w+)(?:\s+(.*))?$", RegexOptions.Compiled);

        foreach (string line in lines)
        {
            Match match = regex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            string name = match.Groups[1].Value;
            string valStr = match.Groups[2].Success ? match.Groups[2].Value.Trim() : string.Empty;

            // Strip C-style and single-line comments
            int commentIdx = valStr.IndexOf("/*", StringComparison.Ordinal);
            if (commentIdx >= 0)
            {
                valStr = valStr[..commentIdx].Trim();
            }
            int lineCommentIdx = valStr.IndexOf("//", StringComparison.Ordinal);
            if (lineCommentIdx >= 0)
            {
                valStr = valStr[..lineCommentIdx].Trim();
            }

            bool isNumeric = false;
            long val = 0;

            if (valStr.Contains("INT32_MIN", StringComparison.Ordinal))
            {
                isNumeric = true;
                val = int.MinValue;
            }
            else if (valStr.Contains("INT64_MIN", StringComparison.Ordinal))
            {
                isNumeric = true;
                val = long.MinValue;
            }
            else
            {
                string stripped = valStr.Replace("(", "").Replace(")", "").Trim();
                var matchInt = Regex.Match(stripped, @"^([+-])?\s*(0x[0-9a-fA-F]+|\d+)[uUlL]*$", RegexOptions.Compiled);
                if (matchInt.Success)
                {
                    bool isNegative = matchInt.Groups[1].Value == "-";
                    string numStr = matchInt.Groups[2].Value;
                    if (numStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        val = unchecked((long)ulong.Parse(numStr[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        val = long.Parse(numStr, CultureInfo.InvariantCulture);
                    }

                    if (isNegative)
                    {
                        val = -val;
                    }

                    isNumeric = true;
                }
            }

            if (!isNumeric)
            {
                if (!AllowedNonNumericMacros.Contains(name))
                {
                    Assert.Fail($"Non-numeric macro '{name}' (value: '{valStr}') is not in the allowed non-numeric macros list.");
                }
                continue;
            }

            defines[name] = val;
        }

        var assertedNames = new HashSet<string>(StringComparer.Ordinal);

        void AssertDefine(string name, long expected)
        {
            Assert.IsTrue(defines.TryGetValue(name, out long actual), $"Header is missing expected numeric define '{name}'.");
            Assert.AreEqual(expected, actual, $"Value mismatch for macro '{name}'.");
            assertedNames.Add(name);
        }

        // Verify API version
        AssertDefine("DIK_API_MAJOR", KitConstants.DIK_API_MAJOR);
        AssertDefine("DIK_API_MINOR", KitConstants.DIK_API_MINOR);

        // Verify Sentinels
        AssertDefine("DIK_NA", KitConstants.DIK_NA);
        AssertDefine("DIK_NA64", KitConstants.DIK_NA64);

        // Verify Errors
        AssertDefine("DIK_OK", KitConstants.DIK_OK);
        Assert.AreEqual(KitConstants.DIK_OK, (int)KitError.Ok);

        AssertDefine("DIK_E_ARG", KitConstants.DIK_E_ARG);
        Assert.AreEqual(KitConstants.DIK_E_ARG, (int)KitError.Argument);

        AssertDefine("DIK_E_ACCESS", KitConstants.DIK_E_ACCESS);
        Assert.AreEqual(KitConstants.DIK_E_ACCESS, (int)KitError.Access);

        AssertDefine("DIK_E_NOT_FOUND", KitConstants.DIK_E_NOT_FOUND);
        Assert.AreEqual(KitConstants.DIK_E_NOT_FOUND, (int)KitError.NotFound);

        AssertDefine("DIK_E_IO", KitConstants.DIK_E_IO);
        Assert.AreEqual(KitConstants.DIK_E_IO, (int)KitError.Io);

        AssertDefine("DIK_E_UNSUPPORTED", KitConstants.DIK_E_UNSUPPORTED);
        Assert.AreEqual(KitConstants.DIK_E_UNSUPPORTED, (int)KitError.Unsupported);

        AssertDefine("DIK_E_STRUCT_SIZE", KitConstants.DIK_E_STRUCT_SIZE);
        Assert.AreEqual(KitConstants.DIK_E_STRUCT_SIZE, (int)KitError.StructSize);

        // Verify Open flags
        AssertDefine("DIK_OPEN_NO_WMI", KitConstants.DIK_OPEN_NO_WMI);
        Assert.AreEqual(KitConstants.DIK_OPEN_NO_WMI, (uint)KitOpenFlags.NoWmi);

        // Verify InterfaceKind
        AssertDefine("DIK_IF_UNKNOWN", KitConstants.DIK_IF_UNKNOWN);
        Assert.AreEqual(KitConstants.DIK_IF_UNKNOWN, (int)InterfaceKind.Unknown);

        AssertDefine("DIK_IF_PATA", KitConstants.DIK_IF_PATA);
        Assert.AreEqual(KitConstants.DIK_IF_PATA, (int)InterfaceKind.Pata);

        AssertDefine("DIK_IF_SATA", KitConstants.DIK_IF_SATA);
        Assert.AreEqual(KitConstants.DIK_IF_SATA, (int)InterfaceKind.Sata);

        AssertDefine("DIK_IF_USB", KitConstants.DIK_IF_USB);
        Assert.AreEqual(KitConstants.DIK_IF_USB, (int)InterfaceKind.Usb);

        AssertDefine("DIK_IF_IEEE1394", KitConstants.DIK_IF_IEEE1394);
        Assert.AreEqual(KitConstants.DIK_IF_IEEE1394, (int)InterfaceKind.Ieee1394);

        AssertDefine("DIK_IF_SCSI", KitConstants.DIK_IF_SCSI);
        Assert.AreEqual(KitConstants.DIK_IF_SCSI, (int)InterfaceKind.Scsi);

        AssertDefine("DIK_IF_NVME", KitConstants.DIK_IF_NVME);
        Assert.AreEqual(KitConstants.DIK_IF_NVME, (int)InterfaceKind.Nvme);

        AssertDefine("DIK_IF_OTHER", KitConstants.DIK_IF_OTHER);
        Assert.AreEqual(KitConstants.DIK_IF_OTHER, (int)InterfaceKind.Other);

        // Verify MediaType
        AssertDefine("DIK_MEDIA_UNKNOWN", KitConstants.DIK_MEDIA_UNKNOWN);
        Assert.AreEqual(KitConstants.DIK_MEDIA_UNKNOWN, (int)MediaType.Unknown);

        AssertDefine("DIK_MEDIA_HDD", KitConstants.DIK_MEDIA_HDD);
        Assert.AreEqual(KitConstants.DIK_MEDIA_HDD, (int)MediaType.Hdd);

        AssertDefine("DIK_MEDIA_SSD", KitConstants.DIK_MEDIA_SSD);
        Assert.AreEqual(KitConstants.DIK_MEDIA_SSD, (int)MediaType.Ssd);

        // Verify HealthStatus
        AssertDefine("DIK_HEALTH_UNKNOWN", KitConstants.DIK_HEALTH_UNKNOWN);
        Assert.AreEqual(KitConstants.DIK_HEALTH_UNKNOWN, (int)HealthStatus.Unknown);

        AssertDefine("DIK_HEALTH_GOOD", KitConstants.DIK_HEALTH_GOOD);
        Assert.AreEqual(KitConstants.DIK_HEALTH_GOOD, (int)HealthStatus.Good);

        AssertDefine("DIK_HEALTH_CAUTION", KitConstants.DIK_HEALTH_CAUTION);
        Assert.AreEqual(KitConstants.DIK_HEALTH_CAUTION, (int)HealthStatus.Caution);

        AssertDefine("DIK_HEALTH_BAD", KitConstants.DIK_HEALTH_BAD);
        Assert.AreEqual(KitConstants.DIK_HEALTH_BAD, (int)HealthStatus.Bad);

        // Verify AccessLevel
        AssertDefine("DIK_ACCESS_FULL", KitConstants.DIK_ACCESS_FULL);
        Assert.AreEqual(KitConstants.DIK_ACCESS_FULL, (uint)AccessLevel.Full);

        AssertDefine("DIK_ACCESS_PARTIAL", KitConstants.DIK_ACCESS_PARTIAL);
        Assert.AreEqual(KitConstants.DIK_ACCESS_PARTIAL, (uint)AccessLevel.Partial);

        // Verify DiskFeatures
        AssertDefine("DIK_FEAT_SMART", KitConstants.DIK_FEAT_SMART);
        Assert.AreEqual(KitConstants.DIK_FEAT_SMART, (uint)DiskFeatures.Smart);

        AssertDefine("DIK_FEAT_APM", KitConstants.DIK_FEAT_APM);
        Assert.AreEqual(KitConstants.DIK_FEAT_APM, (uint)DiskFeatures.Apm);

        AssertDefine("DIK_FEAT_AAM", KitConstants.DIK_FEAT_AAM);
        Assert.AreEqual(KitConstants.DIK_FEAT_AAM, (uint)DiskFeatures.Aam);

        AssertDefine("DIK_FEAT_NCQ", KitConstants.DIK_FEAT_NCQ);
        Assert.AreEqual(KitConstants.DIK_FEAT_NCQ, (uint)DiskFeatures.Ncq);

        AssertDefine("DIK_FEAT_TRIM", KitConstants.DIK_FEAT_TRIM);
        Assert.AreEqual(KitConstants.DIK_FEAT_TRIM, (uint)DiskFeatures.Trim);

        AssertDefine("DIK_FEAT_DEVSLEEP", KitConstants.DIK_FEAT_DEVSLEEP);
        Assert.AreEqual(KitConstants.DIK_FEAT_DEVSLEEP, (uint)DiskFeatures.DevSleep);

        AssertDefine("DIK_FEAT_STREAMING", KitConstants.DIK_FEAT_STREAMING);
        Assert.AreEqual(KitConstants.DIK_FEAT_STREAMING, (uint)DiskFeatures.Streaming);

        AssertDefine("DIK_FEAT_GPL", KitConstants.DIK_FEAT_GPL);
        Assert.AreEqual(KitConstants.DIK_FEAT_GPL, (uint)DiskFeatures.Gpl);

        AssertDefine("DIK_FEAT_VWC", KitConstants.DIK_FEAT_VWC);
        Assert.AreEqual(KitConstants.DIK_FEAT_VWC, (uint)DiskFeatures.VolatileWriteCache);

        AssertDefine("DIK_FEAT_LBA48", KitConstants.DIK_FEAT_LBA48);
        Assert.AreEqual(KitConstants.DIK_FEAT_LBA48, (uint)DiskFeatures.Lba48);

        AssertDefine("DIK_FEAT_NVCACHE", KitConstants.DIK_FEAT_NVCACHE);
        Assert.AreEqual(KitConstants.DIK_FEAT_NVCACHE, (uint)DiskFeatures.NvCache);

        AssertDefine("DIK_FEAT_APM_ON", KitConstants.DIK_FEAT_APM_ON);
        Assert.AreEqual(KitConstants.DIK_FEAT_APM_ON, (uint)DiskFeatures.ApmEnabled);

        AssertDefine("DIK_FEAT_AAM_ON", KitConstants.DIK_FEAT_AAM_ON);
        Assert.AreEqual(KitConstants.DIK_FEAT_AAM_ON, (uint)DiskFeatures.AamEnabled);

        // Verify AttributeStatus
        AssertDefine("DIK_ATTR_STATUS_UNKNOWN", KitConstants.DIK_ATTR_STATUS_UNKNOWN);
        Assert.AreEqual(KitConstants.DIK_ATTR_STATUS_UNKNOWN, (int)AttributeStatus.Unknown);

        AssertDefine("DIK_ATTR_STATUS_GOOD", KitConstants.DIK_ATTR_STATUS_GOOD);
        Assert.AreEqual(KitConstants.DIK_ATTR_STATUS_GOOD, (int)AttributeStatus.Good);

        AssertDefine("DIK_ATTR_STATUS_CAUTION", KitConstants.DIK_ATTR_STATUS_CAUTION);
        Assert.AreEqual(KitConstants.DIK_ATTR_STATUS_CAUTION, (int)AttributeStatus.Caution);

        AssertDefine("DIK_ATTR_STATUS_BAD", KitConstants.DIK_ATTR_STATUS_BAD);
        Assert.AreEqual(KitConstants.DIK_ATTR_STATUS_BAD, (int)AttributeStatus.Bad);

        // Fail if the header has any numeric DIK_* defines that were not checked
        var unassertedNames = defines.Keys.Except(assertedNames).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.AreEqual(0, unassertedNames.Count,
            $"The following numeric DIK_* defines from the header were not checked by the test: {string.Join(", ", unassertedNames)}");
    }
}
