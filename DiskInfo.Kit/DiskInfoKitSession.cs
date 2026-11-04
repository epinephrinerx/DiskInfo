using DiskInfo.Kit.Native;

namespace DiskInfo.Kit;

/// <summary>
/// Managed session wrapping a native DiskInfoKit handle.
/// <para>
/// <b>Windows Manifest Requirement:</b> On Windows, the host executable must declare Windows 10
/// support in its application manifest (<c>&lt;supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/&gt;</c>),
/// otherwise Windows version detection reports Windows 8 and NVMe disks are omitted without error.
/// Standard .NET apphost executables include this manifest by default.
/// </para>
/// </summary>
public sealed class DiskInfoKitSession : IDisposable
{
    private static readonly object s_scanLock = new();
    private readonly object _sessionLock = new();
    private KitSafeHandle? _handle;
    private bool _disposed;

    internal KitSafeHandle DangerousHandle => _handle ?? throw new ObjectDisposedException(nameof(DiskInfoKitSession));

    private DiskInfoKitSession(KitSafeHandle handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Gets the native library API version (major.minor).
    /// </summary>
    /// <exception cref="DllNotFoundException">The native DiskInfoKit library could not be loaded.</exception>
    /// <exception cref="EntryPointNotFoundException">The dik_api_version symbol was not found in the native library.</exception>
    public static Version ApiVersion
    {
        get
        {
            uint v = KitNative.dik_api_version();
            return new Version((int)(v >> 16), (int)(v & 0xFFFF));
        }
    }

    /// <summary>
    /// Attempts to retrieve the native library API version without throwing if the library or export is missing,
    /// or if the binary architecture does not match.
    /// </summary>
    /// <param name="version">Receives the resolved version if successful; otherwise, <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> if the version was successfully obtained; <see langword="false"/> if the native library
    /// or entry point was not found or if the architecture does not match (<see cref="BadImageFormatException"/>).
    /// </returns>
    public static bool TryGetApiVersion(out Version? version)
    {
        try
        {
            uint v = KitNative.dik_api_version();
            version = new Version((int)(v >> 16), (int)(v & 0xFFFF));
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            version = null;
            return false;
        }
    }

    /// <summary>
    /// Opens a new DiskInfoKit session.
    /// </summary>
    /// <param name="flags">Configuration flags for initialization (e.g. <see cref="KitOpenFlags.NoWmi"/>).</param>
    /// <returns>A newly allocated <see cref="DiskInfoKitSession"/>.</returns>
    /// <exception cref="DiskInfoKitException">
    /// Thrown with <see cref="KitError.Unsupported"/> if the API major version is not 1,
    /// or with the corresponding error if native session creation fails.
    /// </exception>
    public static DiskInfoKitSession Open(KitOpenFlags flags = KitOpenFlags.None)
    {
        uint ver = KitNative.dik_api_version();
        int major = (int)(ver >> 16);
        if (major != KitConstants.DIK_API_MAJOR)
        {
            throw new DiskInfoKitException(
                KitError.Unsupported,
                KitConstants.DIK_E_UNSUPPORTED,
                $"dik_api_version reported unsupported API major version {major}. Only major version 1 is supported.");
        }

        int res = KitNative.dik_open(out KitSafeHandle handle, (uint)flags);
        DiskInfoKitException.ThrowIfError(res, nameof(KitNative.dik_open));

        return new DiskInfoKitSession(handle);
    }

    /// <summary>
    /// Scans the system for disks and their S.M.A.R.T. attributes.
    /// <para>
    /// This method is blocking and may take several seconds. Scans are serialized process-wide.
    /// </para>
    /// </summary>
    /// <returns>An immutable list of <see cref="DiskSnapshot"/> records.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if this session has been disposed.</exception>
    /// <exception cref="DiskInfoKitException">Thrown if scanning or reading disk details fails.</exception>
    public unsafe IReadOnlyList<DiskSnapshot> Scan()
    {
        lock (_sessionLock)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DiskInfoKitSession));
            }
        }

        lock (s_scanLock)
        {
            lock (_sessionLock)
            {
                if (_disposed || _handle == null || _handle.IsInvalid || _handle.IsClosed)
                {
                    throw new ObjectDisposedException(nameof(DiskInfoKitSession));
                }

                int scanRes = KitNative.dik_scan(_handle);
                DiskInfoKitException.ThrowIfError(scanRes, nameof(KitNative.dik_scan));

                int count = KitNative.dik_disk_count(_handle);
                if (count < 0)
                {
                    DiskInfoKitException.ThrowIfError(count, nameof(KitNative.dik_disk_count));
                }

                var snapshots = new List<DiskSnapshot>(count);
                for (int diskIndex = 0; diskIndex < count; diskIndex++)
                {
                    NativeDisk disk = default;
                    disk.struct_size = (uint)sizeof(NativeDisk);
                    int diskRes = KitNative.dik_disk(_handle, diskIndex, ref disk);
                    DiskInfoKitException.ThrowIfError(diskRes, nameof(KitNative.dik_disk));

                    string smartKey = NativeConvert.GetSmartKey(in disk);
                    int attrCount = disk.attribute_count;
                    var attributes = new List<SmartAttribute>(Math.Max(0, attrCount));

                    for (int rowIndex = 0; rowIndex < attrCount; rowIndex++)
                    {
                        NativeAttribute attr = default;
                        attr.struct_size = (uint)sizeof(NativeAttribute);
                        int attrRes = KitNative.dik_attribute(_handle, diskIndex, rowIndex, ref attr);
                        DiskInfoKitException.ThrowIfError(attrRes, nameof(KitNative.dik_attribute));

                        attributes.Add(NativeConvert.ToAttribute(in attr, smartKey));
                    }

                    snapshots.Add(NativeConvert.ToDisk(in disk, attributes));
                }

                return Array.AsReadOnly(snapshots.ToArray());
            }
        }
    }

    /// <summary>
    /// Initiates a scan asynchronously on a dedicated long-running thread.
    /// </summary>
    /// <returns>A task that represents the asynchronous scan operation.</returns>
    public Task<IReadOnlyList<DiskSnapshot>> ScanAsync()
    {
        return Task.Factory.StartNew(
            () => Scan(),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Closes the native handle and disposes all allocated session resources.
    /// Blocks until any active scan running on this session finishes.
    /// Idempotent; calling Dispose multiple times is safe.
    /// </summary>
    public void Dispose()
    {
        lock (_sessionLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _handle?.Dispose();
            _handle = null;
        }
    }
}
