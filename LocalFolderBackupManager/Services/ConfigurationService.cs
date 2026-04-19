using LocalFolderBackupManager.Models;
using Newtonsoft.Json;
using System.IO;

namespace LocalFolderBackupManager.Services;

public class ConfigurationService
{
    private static readonly string ConfigFileName = "backup_config.json";
    private static string ConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

    public BackupConfig LoadConfiguration()
    {
        if (!File.Exists(ConfigFilePath))
        {
            var defaultConfig = CreateDefaultConfiguration();
            SaveConfiguration(defaultConfig);
            return defaultConfig;
        }

        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            return JsonConvert.DeserializeObject<BackupConfig>(json) ?? CreateDefaultConfiguration();
        }
        catch
        {
            return CreateDefaultConfiguration();
        }
    }

    public void SaveConfiguration(BackupConfig config)
    {
        try
        {
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
        }
    }

    private BackupConfig CreateDefaultConfiguration()
    {
        return new BackupConfig();
    }
}
