using HotspotApp.Constants;
using HotspotApp.Extensions;
using HotspotApp.Logging;
using HotspotApp.Services.Core;
using HotspotApp.Services.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HotspotApp.Services;

public static class ServiceLauncher
{
    public static void Run(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = AppConstants.AppName;
        });

        var logPath = LogPaths.GetServiceLogPath();
        LoggingBootstrapper.ConfigureFileLogging(builder.Logging, logPath);

        LoggingBootstrapper.InitializeLogLevelAsync().GetAwaiter().GetResult();

        builder.Services.AddSharedServices();

        // Service-only services
        builder.Services.AddSingleton<ArpTableProvider>();
        builder.Services.AddSingleton<VirtualAdapterService>();
        builder.Services.AddSingleton<NetworkInitializer>();
        builder.Services.AddSingleton<HotspotTetheringService>();
        builder.Services.AddSingleton<ClientMonitorService>();
        builder.Services.AddSingleton<RetryPolicy>();
        builder.Services.AddSingleton<HotspotManager>();
        builder.Services.AddHostedService<HotspotWorker>();

        var host = builder.Build();
        host.Run();
    }
}
