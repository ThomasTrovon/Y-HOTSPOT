using System.IO.Pipes;
using HotspotApp.Config;
using HotspotApp.Constants;
using HotspotApp.Localization;
using HotspotApp.Logging;
using HotspotApp.Models;
using Microsoft.Extensions.Logging;

namespace HotspotApp.Services.Core;

public sealed partial class IpcServer(
    HotspotManager hotspotManager,
    ILogger<IpcServer> logger,
    Func<CancellationToken, Task<bool>> restartHotspotAsync
)
{
    private readonly string _pipeName = ServiceConstants.PipeName;

    public async Task RunServerAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var server = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous
            );
            try
            {
                await server.WaitForConnectionAsync(token);
                _ = HandleConnectionAsync(server, token);
            }
            catch (OperationCanceledException)
            {
                server.Dispose();
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "IpcServerAcceptFailed");
                server.Dispose();
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream server, CancellationToken token)
    {
        try
        {
            using var reader = new StreamReader(server);
            using var writer = new StreamWriter(server) { AutoFlush = true };

            var line = await reader.ReadLineAsync(token);
            if (string.IsNullOrWhiteSpace(line))
                return;

            var request = IpcProtocol.DeserializeRequest(line);
            IpcResponseEnvelope response =
                request == null
                    ? new IpcResponseEnvelope { Success = false, Error = "Invalid request" }
                    : await HandleRequestAsync(request, token);

            var responseJson = IpcProtocol.SerializeResponse(
                response.Success,
                response.Payload,
                response.Error,
                response.Version
            );
            await writer.WriteLineAsync(responseJson.AsMemory(), token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "IpcConnectionHandlerFailed");
        }
        finally
        {
            server.Dispose();
        }
    }

    private async Task<IpcResponseEnvelope> HandleRequestAsync(
        IpcRequestEnvelope request,
        CancellationToken token
    )
    {
        try
        {
            switch (request.Command)
            {
                case ServiceConstants.PipeCommandGetStatus:
                    LogIpcRequestReceived(request.Command);
                    var status = await hotspotManager.GetStatusAsync();
                    return new IpcResponseEnvelope
                    {
                        Success = true,
                        Payload = IpcProtocol.SerializePayload(
                            new GetStatusResponse { Status = status }
                        ),
                    };

                case ServiceConstants.PipeCommandUpdateConfig:
                    LogIpcRequestReceived(request.Command);
                    var payload = IpcProtocol.DeserializePayload<UpdateConfigRequest>(
                        request.Payload
                    );
                    if (payload?.Config == null)
                    {
                        return new IpcResponseEnvelope
                        {
                            Success = false,
                            Error = AppStrings.Get("Text_IpcInvalidConfigPayload"),
                        };
                    }

                    await ConfigManager.SaveConfigAsync(payload.Config);
                    AsyncFileLogger.CurrentLogLevel = payload.Config.LogLevel;
                    var restarted = await restartHotspotAsync(token);
                    return new IpcResponseEnvelope
                    {
                        Success = true,
                        Payload = IpcProtocol.SerializePayload(
                            new UpdateConfigResponse { Restarted = restarted }
                        ),
                    };

                case ServiceConstants.PipeCommandRestartHotspot:
                    LogIpcRequestReceived(request.Command);
                    var restartResult = await restartHotspotAsync(token);
                    return new IpcResponseEnvelope
                    {
                        Success = true,
                        Payload = IpcProtocol.SerializePayload(
                            new RestartHotspotResponse { Restarted = restartResult }
                        ),
                    };

                default:
                    return new IpcResponseEnvelope
                    {
                        Success = false,
                        Error = AppStrings.Get("Text_IpcUnknownCommand"),
                    };
            }
        }
        catch (Exception ex)
        {
            LogIpcHandlerFailure(ex);
            return new IpcResponseEnvelope { Success = false, Error = ex.Message };
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "IpcRequestReceived {Command}")]
    private partial void LogIpcRequestReceived(string command);

    [LoggerMessage(Level = LogLevel.Error, Message = "IPC handler failure")]
    private partial void LogIpcHandlerFailure(Exception ex);
}
