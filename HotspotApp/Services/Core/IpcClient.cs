using System.IO.Pipes;
using HotspotApp.Constants;

namespace HotspotApp.Services.Core;

public sealed class IpcClient
{
    private readonly string _pipeName = ServiceConstants.PipeName;

    public async Task<string?> SendRequestAsync(string requestJson, CancellationToken token)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous
            );
            await client.ConnectAsync(1000, token);

            using var reader = new StreamReader(client);
            using var writer = new StreamWriter(client) { AutoFlush = true };

            await writer.WriteLineAsync(requestJson.AsMemory(), token);
            return await reader.ReadLineAsync(token);
        }
        catch
        {
            return null;
        }
    }
}
