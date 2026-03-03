using HotspotApp.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace HotspotApp.Services;

public static class AppServices
{
    private static ServiceProvider? _services;

    public static ServiceProvider Services => _services ??= ConfigureServices();

    public static void Initialize()
    {
        _services ??= ConfigureServices();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddAppServices();
        return services.BuildServiceProvider();
    }
}
