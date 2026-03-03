using HotspotApp.Constants;

namespace HotspotApp.Config;

public static class ConfigKeyProvider
{
    public const string EncryptionKeyEnvironmentVariable = "HOTSPOTAPP_CONFIG_KEY";

    public static string GetEncryptionKey()
    {
        var envKey = Environment.GetEnvironmentVariable(EncryptionKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            return envKey;
        }

        return AppConstants.EncryptionKey;
    }
}
