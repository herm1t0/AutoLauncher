using System.Text.Json.Serialization;

namespace AutoLauncher.Models;

/// <summary>
/// Root configuration object for autolauncher.json.
/// Contains both launcher settings and the application list.
/// </summary>
public class AutoLauncherConfig
{
    /// <summary>
    /// UI theme:<br/>
    ///   "dark"  -- classic dark Windows<br/>
    ///   "light" -- classic light Windows<br/>
    ///   "system"-- follows Windows setting (dark or light)<br/>
    ///   "mocha" -- Catppuccin Mocha (dark)<br/>
    ///   "latte" -- Catppuccin Latte (light)
    /// </summary>
    [JsonPropertyName("theme")]
    public string Theme { get; init; } = "system";

    /// <summary>Always on top.</summary>
    [JsonPropertyName("topmost")]
    public bool Topmost { get; init; } = true;

    /// <summary>List of applications to launch.</summary>
    [JsonPropertyName("apps")]
    public List<AppEntry> Apps { get; init; } = [];
}