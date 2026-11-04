using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiskInfo.Kit.Tests;

[TestClass]
public class AttributeNamesTests
{
    [TestMethod]
    public void Lookup_SmartGenericTable_ExactMatch()
    {
        AttributeName? name = AttributeNames.Lookup("Smart", 0x05);
        Assert.IsNotNull(name);
        Assert.AreEqual("Reallocated Sectors Count", name.Value.Text);
        Assert.IsFalse(name.Value.IsGeneric);
    }

    [TestMethod]
    public void Lookup_SmartNVMe_ExactMatches()
    {
        AttributeName? name02 = AttributeNames.Lookup("SmartNVMe", 0x02);
        Assert.IsNotNull(name02);
        Assert.AreEqual("Composite Temperature", name02.Value.Text);
        Assert.IsFalse(name02.Value.IsGeneric);

        AttributeName? name12 = AttributeNames.Lookup("SmartNVMe", 0x12);
        Assert.IsNotNull(name12);
        Assert.AreEqual("Temperature Sensor 1", name12.Value.Text);
        Assert.IsFalse(name12.Value.IsGeneric);

        AttributeName? name1D = AttributeNames.Lookup("SmartNVMe", 0x1D);
        Assert.IsNotNull(name1D);
        Assert.AreEqual("Total Time For Thermal Management Temperature 2", name1D.Value.Text);
        Assert.IsFalse(name1D.Value.IsGeneric);
    }

    [TestMethod]
    public void Lookup_VendorKey_FallsBackWithIsGenericTrue()
    {
        // Vendor key "SmartSsd" looking up 0x05 (Reallocated Sectors Count in generic Smart table)
        AttributeName? name = AttributeNames.Lookup("SmartSsd", 0x05);
        Assert.IsNotNull(name);
        Assert.AreEqual("Reallocated Sectors Count", name.Value.Text);
        Assert.IsTrue(name.Value.IsGeneric);

        // Vendor key "SmartSandForce" looking up 0x09 (Power-On Hours)
        AttributeName? nameSf = AttributeNames.Lookup("SmartSandForce", 0x09);
        Assert.IsNotNull(nameSf);
        Assert.AreEqual("Power-On Hours", nameSf.Value.Text);
        Assert.IsTrue(nameSf.Value.IsGeneric);
    }

    [TestMethod]
    public void Lookup_SmartNVMe_UnknownId_NeverFallsBack()
    {
        // 0xC5 exists in the generic Smart table ("Current Pending Sector Count"),
        // but is not an NVMe ID. Lookup for SmartNVMe must return null without falling back.
        AttributeName? nameC5 = AttributeNames.Lookup("SmartNVMe", 0xC5);
        Assert.IsNull(nameC5);

        // 0x50 is not an NVMe ID. It must return null without falling back.
        AttributeName? name50 = AttributeNames.Lookup("SmartNVMe", 0x50);
        Assert.IsNull(name50);
    }

    [TestMethod]
    public void Lookup_UnknownKey_UnknownId_ReturnsNull()
    {
        // 0x99 is not present in the generic Smart table
        AttributeName? name = AttributeNames.Lookup("SmartUnknownVendor", 0x99);
        Assert.IsNull(name);
    }

    [TestMethod]
    public void Lookup_NullOrEmptyKey_ReturnsNull()
    {
        Assert.IsNull(AttributeNames.Lookup(null, 0x05));
        Assert.IsNull(AttributeNames.Lookup("", 0x05));
    }

    [TestMethod]
    public void Lookup_KeyComparisonIsCaseSensitive()
    {
        // "smart" in lowercase is not "Smart". It will be treated as an unknown vendor key
        // and fall back to the generic Smart table with IsGeneric = true.
        AttributeName? nameSmartLower = AttributeNames.Lookup("smart", 0x05);
        Assert.IsNotNull(nameSmartLower);
        Assert.AreEqual("Reallocated Sectors Count", nameSmartLower.Value.Text);
        Assert.IsTrue(nameSmartLower.Value.IsGeneric);

        // "smartnvme" in lowercase is not "SmartNVMe". It won't find NVMe-only IDs (e.g. 0x1D)
        // because it falls back to the generic Smart table which lacks 0x1D.
        AttributeName? nameNvmeLower1D = AttributeNames.Lookup("smartnvme", 0x1D);
        Assert.IsNull(nameNvmeLower1D);
    }
}
