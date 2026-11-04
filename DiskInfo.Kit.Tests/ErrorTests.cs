using DiskInfo.Kit.Native;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiskInfo.Kit.Tests;

[TestClass]
public class ErrorTests
{
    [TestMethod]
    public void ThrowIfError_Zero_DoesNotThrow()
    {
        DiskInfoKitException.ThrowIfError(0, "dik_test");
    }

    [TestMethod]
    [DataRow(-1, KitError.Argument)]
    [DataRow(-2, KitError.Access)]
    [DataRow(-3, KitError.NotFound)]
    [DataRow(-4, KitError.Io)]
    [DataRow(-5, KitError.Unsupported)]
    [DataRow(-6, KitError.StructSize)]
    public void ThrowIfError_StandardCodes_MapToCorrectKitError(int nativeCode, KitError expectedError)
    {
        var ex = Assert.ThrowsException<DiskInfoKitException>(() =>
            DiskInfoKitException.ThrowIfError(nativeCode, "dik_function"));

        Assert.AreEqual(expectedError, ex.Error);
        Assert.AreEqual(nativeCode, ex.NativeCode);
        Assert.IsTrue(ex.Message.Contains("dik_function", StringComparison.Ordinal));

        if (expectedError == KitError.Access)
        {
            Assert.IsTrue(ex.Message.Contains("administrator rights", StringComparison.OrdinalIgnoreCase));
        }
    }

    [TestMethod]
    [DataRow(-99)]
    [DataRow(42)]
    public void ThrowIfError_UnknownCode_MapsToIoWithNativeCodePreserved(int unknownCode)
    {
        var ex = Assert.ThrowsException<DiskInfoKitException>(() =>
            DiskInfoKitException.ThrowIfError(unknownCode, "dik_unknown_test"));

        Assert.AreEqual(KitError.Io, ex.Error);
        Assert.AreEqual(unknownCode, ex.NativeCode);
        Assert.IsTrue(ex.Message.Contains("dik_unknown_test", StringComparison.Ordinal));
    }
}
