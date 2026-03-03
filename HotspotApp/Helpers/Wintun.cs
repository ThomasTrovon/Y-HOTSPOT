using System.Runtime.InteropServices;

namespace HotspotApp.Helpers;

public static partial class Wintun
{
    private const string LibraryName = "wintun";

    static Wintun()
    {
        NativeLibrary.SetDllImportResolver(
            typeof(Wintun).Assembly,
            (libraryName, assembly, searchPath) =>
            {
                if (libraryName != LibraryName)
                {
                    return IntPtr.Zero;
                }

                string arch = RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => "x64",
                    Architecture.X86 => "x86",
                    Architecture.Arm64 => "arm64",
                    Architecture.Arm => "arm",
                    _ => throw new PlatformNotSupportedException("Unsupported architecture"),
                };

                string path = Path.Combine(AppContext.BaseDirectory, "wintun", arch, "wintun.dll");
                return NativeLibrary.Load(path);
            }
        );
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NET_LUID
    {
        public ulong Value;
    }

    // Functions
    [LibraryImport(
        LibraryName,
        EntryPoint = "WintunCreateAdapter",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16
    )]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial IntPtr CreateAdapter(
        string name,
        string tunnelType,
        in Guid requestedGuid
    );

    [LibraryImport(
        LibraryName,
        EntryPoint = "WintunOpenAdapter",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16
    )]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial IntPtr OpenAdapter(string name);

    [LibraryImport(LibraryName, EntryPoint = "WintunCloseAdapter", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial void CloseAdapter(IntPtr adapter);

    [LibraryImport(
        LibraryName,
        EntryPoint = "WintunGetAdapterLUID",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16
    )]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    public static partial void GetAdapterLuid(IntPtr adapter, out NET_LUID luid);
}
