using System.Text.Json;
using HotspotApp.Config;

namespace HotspotApp.Models;

public sealed class IpcRequestEnvelope
{
    public int Version { get; set; } = IpcProtocol.CurrentVersion;
    public string Command { get; set; } = string.Empty;
    public JsonElement? Payload { get; set; }
}

public sealed class IpcResponseEnvelope
{
    public int Version { get; set; } = IpcProtocol.CurrentVersion;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public JsonElement? Payload { get; set; }
}

public sealed class GetStatusRequest { }

public sealed class GetStatusResponse
{
    public HotspotStatusDto Status { get; set; } = new();
}

public sealed class UpdateConfigRequest
{
    public HotspotConfig Config { get; set; } = new();
}

public sealed class UpdateConfigResponse
{
    public bool Restarted { get; set; }
}

public sealed class RestartHotspotRequest { }

public sealed class RestartHotspotResponse
{
    public bool Restarted { get; set; }
}

public static class IpcProtocol
{
    public const int CurrentVersion = 1;

    public static JsonElement? SerializePayload(object? payload)
    {
        return payload == null
            ? null
            : JsonSerializer.SerializeToElement(payload, payload.GetType(), IpcJsonContext.Default);
    }

    public static string SerializeRequest(
        string command,
        object? payload = null,
        int? version = null
    )
    {
        var envelope = new IpcRequestEnvelope
        {
            Version = version ?? CurrentVersion,
            Command = command,
            Payload = SerializePayload(payload),
        };

        return JsonSerializer.Serialize(envelope, IpcJsonContext.Default.IpcRequestEnvelope);
    }

    public static string SerializeResponse(
        bool success,
        object? payload = null,
        string? error = null,
        int? version = null
    )
    {
        var envelope = new IpcResponseEnvelope
        {
            Version = version ?? CurrentVersion,
            Success = success,
            Error = error,
            Payload = SerializePayload(payload),
        };

        return JsonSerializer.Serialize(envelope, IpcJsonContext.Default.IpcResponseEnvelope);
    }

    public static IpcRequestEnvelope? DeserializeRequest(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, IpcJsonContext.Default.IpcRequestEnvelope);
        }
        catch
        {
            return null;
        }
    }

    public static IpcResponseEnvelope? DeserializeResponse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, IpcJsonContext.Default.IpcResponseEnvelope);
        }
        catch
        {
            return null;
        }
    }

    public static T? DeserializePayload<T>(JsonElement? payload)
    {
        if (payload == null)
        {
            return default;
        }

        try
        {
            return payload.Value.Deserialize<T>(IpcJsonContext.Default.Options);
        }
        catch
        {
            return default;
        }
    }
}
