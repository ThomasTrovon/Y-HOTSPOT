using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotspotApp.Constants;

namespace HotspotApp.Config;

public static class ConfigManager
{
    public const int CurrentSchemaVersion = 1;
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        AppConstants.AppName
    );
    private static readonly string ConfigPath = Path.Combine(
        AppDataFolder,
        AppConstants.ConfigFileName
    );
    private static readonly string UserConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppConstants.AppName, AppConstants.ConfigFileName);

    public static string ConfigFilePath => ConfigPath;
    public static string ConfigFolderPath => AppDataFolder;

    public static async Task<HotspotConfig> LoadConfigAsync(bool forUser = false)
    {
        var config = CreateDefaultConfig();

        // Import the old shared preferences on first use. The service keeps its own file.
        var path = forUser && File.Exists(UserConfigPath) ? UserConfigPath : ConfigPath;
        if (File.Exists(path))
        {
            try
            {
                var encryptedBytes = await File.ReadAllBytesAsync(path);
                var key = GetKey();
                var json = Decrypt(encryptedBytes, key);

                var loadedConfig = JsonSerializer.Deserialize<HotspotConfig>(json);
                if (loadedConfig != null)
                {
                    if (loadedConfig.SchemaVersion != CurrentSchemaVersion)
                    {
                        throw new InvalidOperationException(
                            $"Schema version mismatch. Expected {CurrentSchemaVersion}, got {loadedConfig.SchemaVersion}"
                        );
                    }
                    config = ValidateAndNormalize(loadedConfig);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Config load failed: {ex.Message}");
                throw;
            }
        }

        if (!forUser || !File.Exists(UserConfigPath))
            await SaveConfigAsync(config, forUser);
        return config;
    }

    public static async Task<HotspotConfig?> SaveConfigAsync(HotspotConfig config, bool forUser = false)
    {
        try
        {
            var normalized = ValidateAndNormalize(config);
            var json = JsonSerializer.Serialize(normalized);
            var key = GetKey();
            var encryptedBytes = Encrypt(json, key);
            await WriteAtomicAsync(forUser ? UserConfigPath : ConfigPath, encryptedBytes);
            return normalized;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Config save failed: {ex.Message}");
            return null;
        }
    }

    public static void ResetUserConfig()
    {
        try
        {
            // Write defaults so a reset does not reimport the old shared preferences.
            var defaults = Encrypt(JsonSerializer.Serialize(CreateDefaultConfig()), GetKey());
            WriteAtomicAsync(UserConfigPath, defaults).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Config folder delete failed: {ex.Message}");
        }
    }

    private static HotspotConfig CreateDefaultConfig()
    {
        return new HotspotConfig { SchemaVersion = CurrentSchemaVersion };
    }

    private static HotspotConfig ValidateAndNormalize(HotspotConfig config)
    {
        var normalized = CreateDefaultConfig();

        if (!string.IsNullOrWhiteSpace(config.Ssid))
        {
            normalized.Ssid = config.Ssid.Trim();
        }

        if (!string.IsNullOrWhiteSpace(config.Passphrase) && config.Passphrase.Length >= 8)
        {
            normalized.Passphrase = config.Passphrase;
        }

        if (Enum.IsDefined(config.Band))
        {
            normalized.Band = config.Band;
        }

        if (Enum.IsDefined(config.AuthenticationKind))
        {
            normalized.AuthenticationKind = config.AuthenticationKind;
        }

        if (config.MaxClientCount >= 1 && config.MaxClientCount <= 32)
        {
            normalized.MaxClientCount = config.MaxClientCount;
        }

        if (Enum.IsDefined(config.LogLevel))
        {
            normalized.LogLevel = config.LogLevel;
        }

        normalized.Channel = HotspotApp.Services.Infrastructure.ChannelRecoveryPolicy.IsSupportedChannel(config.Channel) ? config.Channel : 0;
        normalized.UseCustomDns = config.UseCustomDns;
        normalized.UseManualDns = config.UseManualDns;
        normalized.PreferredDns = (config.PreferredDns ?? "").Trim();
        normalized.AlternateDns = (config.AlternateDns ?? "").Trim();
        normalized.DisableAutoConfig = config.DisableAutoConfig;
        normalized.ForceLanFallback = config.ForceLanFallback;
        normalized.ApplySystemOptimizations = config.ApplySystemOptimizations;
        normalized.UpstreamAdapterId = config.UpstreamAdapterId;
        normalized.TargetAdapterId = config.TargetAdapterId;
        if (Enum.IsDefined(config.DisconnectMode))
        {
            normalized.DisconnectMode = config.DisconnectMode;
        }

        return normalized;
    }

    private static async Task WriteAtomicAsync(string path, byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(tempPath, data).ConfigureAwait(false);
            File.Move(tempPath, path, true);
        }
        finally { File.Delete(tempPath); }
    }

    private static byte[] GetKey()
    {
        var keyBytes = Encoding.UTF8.GetBytes(ConfigKeyProvider.GetEncryptionKey());
        return SHA256.HashData(keyBytes);
    }

    private static byte[] Encrypt(string plainText, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        ms.Write(aes.IV); // Prepend IV

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }
        return ms.ToArray();
    }

    private static string Decrypt(byte[] cipherText, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;

        using var ms = new MemoryStream(cipherText);
        var iv = new byte[aes.BlockSize / 8];
        var read = ms.Read(iv, 0, iv.Length);
        if (read < iv.Length)
            throw new InvalidOperationException("Invalid config file");

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        return sr.ReadToEnd();
    }
}
