using System.Net;
using System.Runtime.InteropServices;
using HotspotApp.Helpers;
using Microsoft.Extensions.Logging;

namespace HotspotApp.Services.Infrastructure;

public sealed class ArpTableProvider(ILogger<ArpTableProvider> logger)
{
    public Dictionary<string, string> GetArpTable()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        IntPtr tablePtr = IntPtr.Zero;

        try
        {
            var result = IpHelper.GetIpNetTable2(IpHelper.AF_INET, out tablePtr);
            if (result != IpHelper.NO_ERROR)
            {
                logger.LogError("GetIpNetTable2 failed with error {Error}", result);
                return map;
            }

            unsafe
            {
                // MIB_IPNET_TABLE2 structure layout:
                // ULONG NumEntries
                // MIB_IPNET_ROW2 Table[ANY_SIZE]
                //
                // On x64, MIB_IPNET_ROW2 has 8-byte alignment (due to ulong InterfaceLuid).
                // So NumEntries (4 bytes) is followed by 4 bytes padding.

                uint numEntries = *(uint*)tablePtr;

                // Skip NumEntries (4 bytes) and potential padding
                // If 64-bit, skip 8 bytes total. If 32-bit, skip 4 bytes.
                byte* rowPtr = (byte*)tablePtr + (IntPtr.Size == 8 ? 8 : 4);

                var rowSize = sizeof(IpHelper.MIB_IPNET_ROW2);

                for (uint i = 0; i < numEntries; i++)
                {
                    IpHelper.MIB_IPNET_ROW2* row = (IpHelper.MIB_IPNET_ROW2*)rowPtr;

                    if (row->PhysicalAddressLength > 0 && row->PhysicalAddressLength <= 32)
                    {
                        // Optimization: Use Span directly to avoid array allocation
                        var macLength = (int)row->PhysicalAddressLength;
                        var macSpan = new ReadOnlySpan<byte>(row->PhysicalAddress, macLength);
                        var mac = Convert.ToHexString(macSpan).ToUpperInvariant();

                        // IP Address
                        // row->Address is SOCKADDR_INET.
                        // We requested AF_INET, so use Ipv4Address.
                        var ipVal = row->Address.Ipv4Address;
                        var ip = new IPAddress(ipVal).ToString();

                        map[mac] = ip;
                    }

                    rowPtr += rowSize;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ArpTableReadFailed");
        }
        finally
        {
            if (tablePtr != IntPtr.Zero)
            {
                IpHelper.FreeMibTable(tablePtr);
            }
        }

        return map;
    }

    public Task<Dictionary<string, string>> GetArpTableAsync()
    {
        return Task.Run(GetArpTable);
    }
}
