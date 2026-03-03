using System.Runtime.CompilerServices;
using HotspotApp.Services;

[assembly: DisableRuntimeMarshalling]

namespace HotspotApp;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppLauncher.Run(args);
    }
}
