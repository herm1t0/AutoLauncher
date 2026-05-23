using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AutoLauncher.Services;
using Shared;

namespace AutoLauncher.Helpers;

/// <summary>
/// Provides icon extraction helpers for application executables, DLLs, and ICO files.
/// </summary>
public static class IconHelper
{
    /// <summary>
    /// Loads an icon from an exe/dll/ico file.
    /// Supports "auto" - extracts the icon from the application executable.
    /// null - icon is not shown.
    /// </summary>
    public static BitmapSource? LoadAppIcon(string? iconPath, string? appPath)
    {
        string? pathToExtract;

        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return null;
        }

        if (string.Equals(iconPath, "auto", StringComparison.OrdinalIgnoreCase))
        {
            // "auto" -- extract icon from the application itself
            pathToExtract = appPath;
            if (string.IsNullOrWhiteSpace(pathToExtract)) return null;

            // If name has no path -- search in %PATH%
            if (!pathToExtract.Contains('\\') && !pathToExtract.Contains('/'))
                pathToExtract = LauncherService.FindInPath(pathToExtract);
        }
        else
        {
            pathToExtract = iconPath;
        }

        if (string.IsNullOrWhiteSpace(pathToExtract)) return null;

        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(pathToExtract);
            if (icon == null) return null;
            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(16, 16));
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract icon from '{pathToExtract}': {ex.Message}");
            return null;
        }
    }
}
