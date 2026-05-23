using System.IO;
using System.Text.Json;
using AutoLauncher.Models;
using Shared;

namespace AutoLauncher.Services;

/// <summary>
/// Loads and saves the unified autolauncher.json configuration file,
/// which contains both settings and the application list.
/// </summary>
public class ConfigService
{
    private static readonly string FilePath = Path.Combine(Logger.AppDataDirectory, "autolauncher.json");

    /// <summary>
    /// Loads the full config from autolauncher.json.
    /// If the file does not exist - creates a default one and saves it.
    /// </summary>
    public async Task<AutoLauncherConfig> LoadAsync()
    {
        if (!File.Exists(FilePath))
        {
            var defaults = GetDefaultConfig();
            await SaveAsync(defaults).ConfigureAwait(false);
            return defaults;
        }

        var text = await File.ReadAllTextAsync(FilePath).ConfigureAwait(false);
        return Deserialize(text);
    }

    /// <summary>
    /// Saves the full config to autolauncher.json.
    /// </summary>
    public async Task SaveAsync(AutoLauncherConfig config)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(FilePath, json).ConfigureAwait(false);
    }

    private static AutoLauncherConfig Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AutoLauncherConfig>(json)
                   ?? GetDefaultConfig();
        }
        catch (JsonException ex)
        {
            Logger.Error($"Failed to parse config '{FilePath}': {ex.Message}");
            return GetDefaultConfig();
        }
    }

    /// <summary>
    /// Returns a default configuration with an empty app list.
    /// The user is expected to edit the config file to add applications.
    /// </summary>
    private static AutoLauncherConfig GetDefaultConfig()
    {
        return new AutoLauncherConfig
        {
            Theme = "system",
            Topmost = true,
            Apps = []
        };
    }
}