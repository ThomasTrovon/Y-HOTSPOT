using HotspotApp.Services.Infrastructure;

namespace HotspotApp.Services.UI;

public sealed class FileExplorerService(IProcessRunner processRunner)
{
    public void OpenFolder(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            processRunner.StartDetached("explorer.exe", path);
    }
}
