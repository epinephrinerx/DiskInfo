using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DiskInfo.Kit.Native;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiskInfo.Kit.Tests;

[TestClass]
public class NativeTests
{
    private static void EnsureNativeDll()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            Assert.Inconclusive($"Process architecture is {RuntimeInformation.ProcessArchitecture}, but only x64 DiskInfoKit.dll is copied. Native integration tests skipped.");
        }

        string dllPath = Path.Combine(AppContext.BaseDirectory, "DiskInfoKit.dll");
        if (!File.Exists(dllPath))
        {
            Assert.Inconclusive("DiskInfoKit.dll is not present in the test output directory. Native integration tests skipped.");
        }
    }

    [TestMethod]
    public void ApiVersion_Matches_OnePointZero()
    {
        EnsureNativeDll();

        Version ver = DiskInfoKitSession.ApiVersion;
        Assert.AreEqual(new Version(1, 0), ver);
    }

    [TestMethod]
    public void TryGetApiVersion_Returns_True_And_Version()
    {
        EnsureNativeDll();

        bool success = DiskInfoKitSession.TryGetApiVersion(out Version? version);
        Assert.IsTrue(success);
        Assert.AreEqual(new Version(1, 0), version);
    }

    [TestMethod]
    public void Open_And_Dispose_Succeeds()
    {
        EnsureNativeDll();

        using DiskInfoKitSession session = DiskInfoKitSession.Open();
        Assert.IsNotNull(session);
    }

    [TestMethod]
    public void Double_Dispose_Is_Idempotent()
    {
        EnsureNativeDll();

        DiskInfoKitSession session = DiskInfoKitSession.Open();
        session.Dispose();
        session.Dispose(); // must not throw
    }

    [TestMethod]
    [Timeout(180000)]
    public void Scan_After_Dispose_Throws_ObjectDisposedException()
    {
        EnsureNativeDll();

        DiskInfoKitSession session = DiskInfoKitSession.Open();
        session.Dispose();

        Assert.ThrowsException<ObjectDisposedException>(() => session.Scan());
    }

    [TestMethod]
    [Timeout(180000)]
    public async Task Scan_Elevation_Behavior()
    {
        EnsureNativeDll();

        using DiskInfoKitSession session = DiskInfoKitSession.Open();

        if (!Environment.IsPrivilegedProcess)
        {
            var ex = Assert.ThrowsException<DiskInfoKitException>(() => session.Scan());
            Assert.AreEqual(KitError.Access, ex.Error);

            var asyncEx = await Assert.ThrowsExceptionAsync<DiskInfoKitException>(() => session.ScanAsync());
            Assert.AreEqual(KitError.Access, asyncEx.Error);
        }
        else
        {
            IReadOnlyList<DiskSnapshot> disks = session.Scan();
            ValidateDisksAgainstSession(session, disks);

            IReadOnlyList<DiskSnapshot> asyncDisks = await session.ScanAsync();
            ValidateDisksAgainstSession(session, asyncDisks);
        }
    }

    private static void ValidateDisksAgainstSession(DiskInfoKitSession session, IReadOnlyList<DiskSnapshot> disks)
    {
        for (int i = 0; i < disks.Count; i++)
        {
            DiskSnapshot disk = disks[i];
            Assert.IsFalse(string.IsNullOrWhiteSpace(disk.Model));

            int nativeAttrCount = GetNativeAttributeCount(session, i);
            Assert.AreEqual(nativeAttrCount, disk.Attributes.Count);

            // Probe past the last attribute to verify no row was skipped
            NativeAttribute probeAttr = default;
            probeAttr.struct_size = (uint)Unsafe.SizeOf<NativeAttribute>();
            int attrProbeRes = KitNative.dik_attribute(session.DangerousHandle, i, nativeAttrCount, ref probeAttr);
            Assert.AreEqual(-3, attrProbeRes, $"dik_attribute probe at index {nativeAttrCount} for disk {i} must return NotFound (-3).");
        }

        // Probe past the last disk to verify no disk was skipped
        NativeDisk probeDisk = default;
        probeDisk.struct_size = (uint)Unsafe.SizeOf<NativeDisk>();
        int diskProbeRes = KitNative.dik_disk(session.DangerousHandle, disks.Count, ref probeDisk);
        Assert.AreEqual(-3, diskProbeRes, $"dik_disk probe at index {disks.Count} must return NotFound (-3).");
    }

    private static int GetNativeAttributeCount(DiskInfoKitSession session, int diskIndex)
    {
        NativeDisk nativeDisk = default;
        nativeDisk.struct_size = (uint)Unsafe.SizeOf<NativeDisk>();
        int res = KitNative.dik_disk(session.DangerousHandle, diskIndex, ref nativeDisk);
        Assert.AreEqual(0, res);
        return nativeDisk.attribute_count;
    }

    [TestMethod]
    [Timeout(180000)]
    public void Parallel_Scans_Finish_Without_Deadlock()
    {
        EnsureNativeDll();

        var session1 = DiskInfoKitSession.Open();
        var session2 = DiskInfoKitSession.Open();

        var task1 = Task.Run(() =>
        {
            try
            {
                return session1.Scan();
            }
            catch (DiskInfoKitException ex) when (ex.Error == KitError.Access)
            {
                return (IReadOnlyList<DiskSnapshot>)Array.Empty<DiskSnapshot>();
            }
        });

        var task2 = Task.Run(() =>
        {
            try
            {
                return session2.Scan();
            }
            catch (DiskInfoKitException ex) when (ex.Error == KitError.Access)
            {
                return (IReadOnlyList<DiskSnapshot>)Array.Empty<DiskSnapshot>();
            }
        });

        bool completed = Task.WaitAll([task1, task2], TimeSpan.FromSeconds(120));
        Assert.IsTrue(completed, "Parallel scans timed out after 120 seconds (potential deadlock).");

        session1.Dispose();
        session2.Dispose();
    }
}
