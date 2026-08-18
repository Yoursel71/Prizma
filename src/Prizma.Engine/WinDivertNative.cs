using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Prizma.Engine;

internal static unsafe partial class WinDivertNative
{
    internal const int NetworkLayer = 0;
    internal const uint ShutdownBoth = 3;
    internal static readonly nint InvalidHandle = new(-1);

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    internal struct Address
    {
        [FieldOffset(0)] public long Timestamp;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint Reserved2;
        [FieldOffset(16)] public uint InterfaceIndex;
        [FieldOffset(20)] public uint SubInterfaceIndex;

        public readonly bool Outbound => (Flags & (1u << 17)) != 0;
    }

    [LibraryImport("WinDivert.dll", EntryPoint = "WinDivertOpen", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    internal static partial nint Open(string filter, int layer, short priority, ulong flags);

    [LibraryImport("WinDivert.dll", EntryPoint = "WinDivertRecv", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool Receive(nint handle, byte* packet, uint packetLength, out uint receiveLength, ref Address address);

    [LibraryImport("WinDivert.dll", EntryPoint = "WinDivertSend", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool Send(nint handle, byte* packet, uint packetLength, out uint sendLength, ref Address address);

    [LibraryImport("WinDivert.dll", EntryPoint = "WinDivertHelperCalcChecksums", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CalculateChecksums(byte* packet, uint packetLength, ref Address address, ulong flags);

    [LibraryImport("WinDivert.dll", EntryPoint = "WinDivertShutdown")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool Shutdown(nint handle, uint how);

    [LibraryImport("WinDivert.dll", EntryPoint = "WinDivertClose")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool Close(nint handle);

    internal static Win32Exception LastError(string operation)
    {
        var error = Marshal.GetLastWin32Error();
        return new Win32Exception(error, $"{operation} (Win32 {error}: {new Win32Exception(error).Message})");
    }
}
