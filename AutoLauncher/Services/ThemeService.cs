using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Shared;

namespace AutoLauncher.Services;

/// <summary>
/// Manages UI theme switching (Catppuccin Mocha/Latte, Windows Dark/Light, system).
/// </summary>
public class ThemeService
{
    /// <summary>
    /// Applies the specified theme by replacing brush resources in Application.Current.Resources.
    /// </summary>
    /// <param name="prefix">Resource prefix: WinDark, WinLight, Mocha, Latte</param>
    /// <param name="isLight">true for light theme (affects GIF recoloring)</param>
    public void ApplyTheme(string prefix, bool isLight)
    {
        // Replace brushes entirely (XAML brushes are frozen, .Color cannot be mutated)
        foreach (var key in new[]
                 {
                     "Base", "Surface0", "Surface1", "Overlay0", "Subtext0", "Subtext1", "Text", "Pink", "Mauve", "Red",
                     "Green", "Blue"
                 })
        {
            if (Application.Current.Resources[$"{prefix}{key}"] is Color color)
            {
                // Create a new unfrozen brush -- DynamicResource will pick it up automatically
                Application.Current.Resources[key] = new SolidColorBrush(color);
            }
        }
    }

    /// <summary>
    /// Returns the pink color for the progress bar glow effect based on the active theme.
    /// </summary>
    public Color GetProgressGlowColor(string prefix, bool isLight)
    {
        if (Application.Current.Resources[$"{prefix}Pink"] is Color pink)
            return pink;

        return isLight
            ? Color.FromRgb(0xEA, 0x76, 0xCB)
            : Color.FromRgb(0xCF, 0x93, 0xD9);
    }

    /// <summary>
    /// Resolves the theme prefix and light/dark flag from the config string.
    /// </summary>
    public (string Prefix, bool IsLight) ResolveTheme(string theme)
    {
        switch (theme)
        {
            case "mocha":
                return ("Mocha", false);
            case "latte":
                return ("Latte", true);
            case "light":
                return ("WinLight", true);
            case "dark":
                return ("WinDark", false);
            default: // "system" or any other value
                if (IsWindowsLightTheme())
                    return ("WinLight", true);
                return ("WinDark", false);
        }
    }

    /// <summary>
    /// Detects whether Windows is using a light theme via the registry.
    /// </summary>
    public static bool IsWindowsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
                return value == 1;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to read Windows theme registry key: {ex.Message}");
        }

        return false;
    }
}
