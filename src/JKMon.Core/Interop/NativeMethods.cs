using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace JKMon.Core.Interop;

[SupportedOSPlatform("windows")]
internal static partial class NativeMethods
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryStatusEx
    {
        internal uint Length;
        internal uint MemoryLoad;
        internal ulong TotalPhys;
        internal ulong AvailPhys;
        internal ulong TotalPageFile;
        internal ulong AvailPageFile;
        internal ulong TotalVirtual;
        internal ulong AvailVirtual;
        internal ulong AvailExtendedVirtual;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    internal const uint PdhFmtDouble = 0x00000200;
    internal const uint PdhFmtNoCap100 = 0x00008000;

    [StructLayout(LayoutKind.Sequential)]
    internal struct PdhFmtCounterValue
    {
        internal uint CStatus;
        internal double DoubleValue;
    }

    [LibraryImport("pdh.dll")]
    internal static partial uint PdhOpenQueryW(IntPtr dataSource, IntPtr userData, out IntPtr query);

    /// <summary>English counter paths keep collection working regardless of the Windows display language.</summary>
    [LibraryImport("pdh.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint PdhAddEnglishCounterW(IntPtr query, string fullCounterPath, IntPtr userData, out IntPtr counter);

    [LibraryImport("pdh.dll")]
    internal static partial uint PdhCollectQueryData(IntPtr query);

    [LibraryImport("pdh.dll")]
    internal static partial uint PdhGetFormattedCounterValue(IntPtr counter, uint format, IntPtr type, out PdhFmtCounterValue value);

    [LibraryImport("pdh.dll")]
    internal static partial uint PdhCloseQuery(IntPtr query);

    internal const int CfSyncRootInfoStandard = 1;

    [StructLayout(LayoutKind.Sequential)]
    internal struct IoCounters
    {
        internal ulong ReadOperationCount;
        internal ulong WriteOperationCount;
        internal ulong OtherOperationCount;
        internal ulong ReadTransferCount;
        internal ulong WriteTransferCount;
        internal ulong OtherTransferCount;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetProcessIoCounters(IntPtr process, out IoCounters counters);

    [LibraryImport("CldApi.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int CfGetSyncRootInfoByPath(
        string filePath,
        int infoClass,
        IntPtr infoBuffer,
        uint infoBufferLength,
        out uint returnedLength);
}
