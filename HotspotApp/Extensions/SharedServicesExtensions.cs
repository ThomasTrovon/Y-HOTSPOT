using HotspotApp.Services.Core;
using HotspotApp.Services.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace HotspotApp.Extensions;

public static class SharedServicesExtensions
{
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        services.AddSingleton<ProcessExecutablePathProvider>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<ServiceManager>();
        services.AddSingleton<ServiceStateManager>();
        services.AddSingleton<HotspotOptimizer>();
        return services;
    }
}
