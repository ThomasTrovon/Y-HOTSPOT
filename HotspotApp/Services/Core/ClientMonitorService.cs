using HotspotApp.Localization;
using HotspotApp.Models;
using HotspotApp.Services.Infrastructure;
using Microsoft.Extensions.Logging;

namespace HotspotApp.Services.Core;

public sealed partial class ClientMonitorService(
    ArpTableProvider arpTableProvider,
    HotspotTetheringService tetheringService,
    ILogger<ClientMonitorService> logger
)
{
    public async Task<List<ConnectedDevice>> GetConnectedClientsAsync()
    {
        var list = new List<ConnectedDevice>();
        try
        {
            var arpTableTask = arpTableProvider.GetArpTableAsync();
            var clients = tetheringService.GetClients();
            var arpTable = await arpTableTask;

            foreach (var client in clients)
            {
                var mac = client.MacAddress;
                var normalizedMac = mac.Replace(":", "").Replace("-", "").ToUpperInvariant();
                var ip = arpTable.TryGetValue(normalizedMac, out var foundIp)
                    ? foundIp
                    : AppStrings.Get("Text_StatusUnknown");

                list.Add(
                    new ConnectedDevice
                    {
                        MacAddress = mac,
                        HostName =
                            client.HostNames[0]?.DisplayName
                            ?? AppStrings.Get("Text_StatusUnknown"),
                        IpAddress = ip,
                    }
                );
            }
        }
        catch (Exception ex)
        {
            LogGetClientsFailed(ex);
        }

        return list;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get connected clients.")]
    private partial void LogGetClientsFailed(Exception ex);
}
