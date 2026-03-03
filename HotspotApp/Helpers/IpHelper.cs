using System.Runtime.InteropServices;

namespace HotspotApp.Helpers;

public static partial class IpHelper
{
    // MIB_UNICASTIPADDRESS_ROW structure
    [StructLayout(LayoutKind.Sequential)]
    public struct MIB_UNICASTIPADDRESS_ROW
    {
        public SOCKADDR_INET Address;
        public ulong InterfaceLuid;
        public uint InterfaceIndex;
        public uint PrefixOrigin;
        public uint SuffixOrigin;
        public uint ValidLifetime;
        public uint PreferredLifetime;
        public byte OnLinkPrefixLength;
        public bool SkipAsSource;
        public uint DadState;
        public uint ScopeId;
        public long CreationTimeStamp;
    }

    // MIB_IPFORWARD_ROW2 structure
    [StructLayout(LayoutKind.Sequential)]
    public struct MIB_IPFORWARD_ROW2
    {
        public ulong InterfaceLuid;
        public uint InterfaceIndex;
        public SOCKADDR_INET DestinationPrefix;
        public SOCKADDR_INET NextHop;
        public byte SitePrefixLength;
        public uint ValidLifetime;
        public uint PreferredLifetime;
        public uint Metric;
        public uint Protocol;
        public bool Loopback;
        public bool AutoconfigureAddress;
        public bool Publish;
        public bool Immortal;
        public uint Age;
        public uint Origin;
    }

    // SOCKADDR_INET structure - Explicit layout required for union
    [StructLayout(LayoutKind.Explicit)]
    public struct SOCKADDR_INET
    {
        [FieldOffset(0)]
        public ushort si_family;

        [FieldOffset(2)]
        public ushort Ipv4Port;

        [FieldOffset(4)]
        public uint Ipv4Address;

        [FieldOffset(0)]
        public SOCKADDR_IN6 Ipv6;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SOCKADDR_IN6
    {
        public ushort sin6_family;
        public ushort sin6_port;
        public uint sin6_flowinfo;
        public fixed byte sin6_addr[16];
        public uint sin6_scope_id;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct MIB_IPNET_ROW2
    {
        public SOCKADDR_INET Address;
        public uint InterfaceIndex;
        public ulong InterfaceLuid;

        public fixed byte PhysicalAddress[32];
        public uint PhysicalAddressLength;
        public uint State;
        public uint Flags;
        public uint ReachabilityTime;
    }

    [LibraryImport("iphlpapi.dll", SetLastError = true)]
    public static partial int CreateUnicastIpAddressEntry(ref MIB_UNICASTIPADDRESS_ROW Row);

    [LibraryImport("iphlpapi.dll", SetLastError = true)]
    public static partial int CreateIpForwardEntry2(ref MIB_IPFORWARD_ROW2 Row);

    [LibraryImport("iphlpapi.dll", SetLastError = true)]
    public static partial int InitializeUnicastIpAddressEntry(ref MIB_UNICASTIPADDRESS_ROW Row);

    [LibraryImport("iphlpapi.dll", SetLastError = true)]
    public static partial int InitializeIpForwardEntry(ref MIB_IPFORWARD_ROW2 Row);

    [LibraryImport("iphlpapi.dll", SetLastError = true)]
    public static partial int GetIpNetTable2(ushort Family, out IntPtr Table);

    [LibraryImport("iphlpapi.dll", SetLastError = true)]
    public static partial void FreeMibTable(IntPtr Memory);

    public const ushort AF_INET = 2;
    public const int NO_ERROR = 0;
}
