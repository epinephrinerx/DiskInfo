using DiskInfo.Kit.Native;

namespace DiskInfo.Kit;

/// <summary>
/// Exception thrown when a native DiskInfoKit operation returns an error code.
/// </summary>
public sealed class DiskInfoKitException : Exception
{
    /// <summary>
    /// Gets the standardized high-level error code.
    /// </summary>
    public KitError Error { get; }

    /// <summary>
    /// Gets the raw native return code from the native library.
    /// </summary>
    public int NativeCode { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DiskInfoKitException"/> with specified error code and message.
    /// </summary>
    public DiskInfoKitException(KitError error, int nativeCode, string message)
        : base(message)
    {
        Error = error;
        NativeCode = nativeCode;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DiskInfoKitException"/> with specified error code, message, and inner exception.
    /// </summary>
    public DiskInfoKitException(KitError error, int nativeCode, string message, Exception? innerException)
        : base(message, innerException)
    {
        Error = error;
        NativeCode = nativeCode;
    }

    /// <summary>
    /// Checks the native return code and throws an appropriate <see cref="DiskInfoKitException"/> if non-zero.
    /// </summary>
    /// <param name="returnCode">The integer return code from the native C function.</param>
    /// <param name="functionName">The name of the native C function that returned the code.</param>
    internal static void ThrowIfError(int returnCode, string functionName)
    {
        if (returnCode == KitConstants.DIK_OK)
        {
            return;
        }

        KitError error = returnCode switch
        {
            KitConstants.DIK_E_ARG => KitError.Argument,
            KitConstants.DIK_E_ACCESS => KitError.Access,
            KitConstants.DIK_E_NOT_FOUND => KitError.NotFound,
            KitConstants.DIK_E_IO => KitError.Io,
            KitConstants.DIK_E_UNSUPPORTED => KitError.Unsupported,
            KitConstants.DIK_E_STRUCT_SIZE => KitError.StructSize,
            _ => KitError.Io
        };

        string message = returnCode switch
        {
            KitConstants.DIK_E_ARG => $"{functionName} failed: invalid argument or null pointer (error code {returnCode}).",
            KitConstants.DIK_E_ACCESS => $"{functionName} failed: administrator rights are required to access disk information (error code {returnCode}).",
            KitConstants.DIK_E_NOT_FOUND => $"{functionName} failed: disk or attribute index not found (error code {returnCode}).",
            KitConstants.DIK_E_IO => $"{functionName} failed: I/O or COM operation failed (error code {returnCode}).",
            KitConstants.DIK_E_UNSUPPORTED => $"{functionName} failed: operation or interface is not supported (error code {returnCode}).",
            KitConstants.DIK_E_STRUCT_SIZE => $"{functionName} failed: struct size is smaller than supported version (error code {returnCode}).",
            _ => $"{functionName} failed with native error code {returnCode}."
        };

        throw new DiskInfoKitException(error, returnCode, message);
    }
}
