using HotspotApp.Config;
using HotspotApp.Constants;
using HotspotApp.Localization;
using HotspotApp.Models;
using Microsoft.Extensions.Logging;

namespace HotspotApp.Services.Core;

/// <summary>
/// Client service for communicating with the background Hotspot Service via Named Pipes.
/// </summary>
public class ServiceClient(ServiceManager serviceManager, ILogger<ServiceClient> logger)
{
    private readonly IpcClient _ipcClient = new();

    public async Task<ServiceResponse<HotspotStatusDto>> GetStatusAsync(
        CancellationToken token = default
    )
    {
        return await SendAsync(
            ServiceConstants.PipeCommandGetStatus,
            new GetStatusRequest(),
            payload => IpcProtocol.DeserializePayload<GetStatusResponse>(payload)?.Status,
            token
        );
    }

    public async Task<ServiceResponse<UpdateConfigResponse>> UpdateConfigAsync(
        HotspotConfig config,
        CancellationToken token = default
    )
    {
        var request = new UpdateConfigRequest { Config = config };
        return await SendAsync(
            ServiceConstants.PipeCommandUpdateConfig,
            request,
            IpcProtocol.DeserializePayload<UpdateConfigResponse>,
            token
        );
    }

    public async Task<ServiceResponse<RestartHotspotResponse>> RestartHotspotAsync(
        CancellationToken token = default
    )
    {
        var request = new RestartHotspotRequest();
        return await SendAsync(
            ServiceConstants.PipeCommandRestartHotspot,
            request,
            IpcProtocol.DeserializePayload<RestartHotspotResponse>,
            token
        );
    }

    public async Task<bool> WaitForHotspotActiveAsync(
        int maxAttempts = 60,
        int delayMs = 1000,
        CancellationToken token = default
    )
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var response = await GetStatusAsync(token);
            if (response.Success)
            {
                if (response.Data?.IsActive == true)
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(response.Data?.LastError))
                {
                    throw new Exception(response.Data.LastError);
                }
            }

            try
            {
                await Task.Delay(delayMs, token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
        return false;
    }

    private async Task<ServiceResponse<TResponse>> SendAsync<TRequest, TResponse>(
        string command,
        TRequest payload,
        Func<System.Text.Json.JsonElement?, TResponse?> payloadParser,
        CancellationToken token
    )
    {
        var requestJson = IpcProtocol.SerializeRequest(command, payload);
        var responseJson = await _ipcClient.SendRequestAsync(requestJson, token);
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            logger.LogWarning("ServiceNoResponse {Command}", command);
            return ServiceResponse<TResponse>.Fail(await GetNoResponseErrorAsync());
        }

        var response = IpcProtocol.DeserializeResponse(responseJson);
        if (response == null)
        {
            logger.LogWarning("ServiceInvalidResponse {Command}", command);
            return ServiceResponse<TResponse>.Fail(
                AppStrings.Get("Text_ServiceInvalidResponseMessage")
            );
        }

        if (!response.Success)
        {
            logger.LogWarning("ServiceErrorResponse {Command} {Error}", command, response.Error);
            return ServiceResponse<TResponse>.Fail(
                response.Error ?? AppStrings.Get("Text_ServiceErrorMessage")
            );
        }

        var parsedPayload = payloadParser(response.Payload);
        if (parsedPayload == null)
        {
            logger.LogWarning("ServiceInvalidPayload {Command}", command);
            return ServiceResponse<TResponse>.Fail(
                AppStrings.Get("Text_ServiceInvalidPayloadMessage")
            );
        }

        return ServiceResponse<TResponse>.Ok(parsedPayload);
    }

    private async Task<string?> GetNoResponseErrorAsync()
    {
        var installed = await serviceManager.IsServiceInstalledAsync(AppConstants.AppName);
        return installed
            ? AppStrings.Get("Text_ServiceNoResponseMessage")
            : AppStrings.Get("Text_ServiceStatusNotInstalled");
    }
}
