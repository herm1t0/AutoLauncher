using System.Text.Json.Serialization;

namespace AutoLauncher.Models;

/// <summary>
/// Application entry model for launching.
/// </summary>
public class AppEntry
{
    /// <summary>Name (displayed in the UI).</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    /// <summary>Path to the executable.</summary>
    [JsonPropertyName("path")]
    public string Path { get; init; } = "";

    /// <summary>Command-line arguments.</summary>
    [JsonPropertyName("arguments")]
    public string? Arguments { get; init; }

    /// <summary>Run as administrator.</summary>
    [JsonPropertyName("admin")]
    public bool Admin { get; init; }

    /// <summary>Wait for process to complete.</summary>
    [JsonPropertyName("wait")]
    public bool Wait { get; init; }

    /// <summary>Delay after launch (ms).</summary>
    [JsonPropertyName("delayMs")]
    public int DelayMs { get; init; } = 1000;

    /// <summary>Path to icon (exe/ico/dll). Optional.</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>
    /// Checks that the entry contains the required fields.
    /// </summary>
    [JsonIgnore]
    public bool IsValid => !string.IsNullOrWhiteSpace(Name)
                           && !string.IsNullOrWhiteSpace(Path);

    /// <summary>
    /// Returns the validation error description, or null if valid.
    /// </summary>
    [JsonIgnore]
    public string? ValidationError => this switch
    {
        { Name: null or "" } => "Name is empty",
        { Path: null or "" } => $"Path is empty for '{Name}'",
        _ => null
    };
}