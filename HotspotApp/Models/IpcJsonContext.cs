using System.Text.Json.Serialization;
using HotspotApp.Config;

namespace HotspotApp.Models;

[JsonSerializable(typeof(IpcRequestEnvelope))]
[JsonSerializable(typeof(IpcResponseEnvelope))]
[JsonSerializable(typeof(GetStatusRequest))]
[JsonSerializable(typeof(GetStatusResponse))]
[JsonSerializable(typeof(UpdateConfigRequest))]
[JsonSerializable(typeof(UpdateConfigResponse))]
[JsonSerializable(typeof(RestartHotspotRequest))]
[JsonSerializable(typeof(RestartHotspotResponse))]
[JsonSerializable(typeof(HotspotConfig))]
public partial class IpcJsonContext : JsonSerializerContext { }
