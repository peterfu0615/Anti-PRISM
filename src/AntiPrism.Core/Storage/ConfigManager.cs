using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AntiPrism.Core.Models;
using Serilog;

namespace AntiPrism.Core.Storage;

public class ConfigManager
{
    private static readonly string DefaultDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Anti-PRISM");

    private static readonly string ConfigFilePath = Path.Combine(DefaultDir, "config.json");
    private static readonly object FileLock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string GetConfigDirectory() => DefaultDir;

    public static AntiPrismConfig LoadConfig()
    {
        lock (FileLock)
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    var defaultConfig = new AntiPrismConfig();
                    SaveConfig(defaultConfig);
                    return defaultConfig;
                }

                var content = File.ReadAllText(ConfigFilePath, Encoding.UTF8);
                // Check if file is DPAPI protected (starts with "DPAPI:")
                if (content.StartsWith("DPAPI:", StringComparison.Ordinal))
                {
                    var base64 = content.Substring(6);
                    var encryptedBytes = Convert.FromBase64String(base64);
                    var decryptedBytes = ProtectedData.Unprotect(
                        encryptedBytes,
                        null,
                        DataProtectionScope.LocalMachine);
                    var decryptedJson = Encoding.UTF8.GetString(decryptedBytes);
                    return JsonSerializer.Deserialize<AntiPrismConfig>(decryptedJson, JsonOptions) ?? new AntiPrismConfig();
                }

                // Plain JSON fallback
                var config = JsonSerializer.Deserialize<AntiPrismConfig>(content, JsonOptions);
                return config ?? new AntiPrismConfig();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load configuration from {Path}, returning default", ConfigFilePath);
                return new AntiPrismConfig();
            }
        }
    }

    public static void SaveConfig(AntiPrismConfig config, bool useDpapi = true)
    {
        lock (FileLock)
        {
            try
            {
                if (!Directory.Exists(DefaultDir))
                {
                    Directory.CreateDirectory(DefaultDir);
                }

                var json = JsonSerializer.Serialize(config, JsonOptions);

                if (useDpapi && OperatingSystem.IsWindows())
                {
                    var rawBytes = Encoding.UTF8.GetBytes(json);
                    var protectedBytes = ProtectedData.Protect(
                        rawBytes,
                        null,
                        DataProtectionScope.LocalMachine);
                    var base64 = "DPAPI:" + Convert.ToBase64String(protectedBytes);
                    File.WriteAllText(ConfigFilePath, base64, Encoding.UTF8);
                }
                else
                {
                    File.WriteAllText(ConfigFilePath, json, Encoding.UTF8);
                }

                Log.Information("Configuration successfully saved to {Path}", ConfigFilePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save configuration to {Path}", ConfigFilePath);
            }
        }
    }
}
