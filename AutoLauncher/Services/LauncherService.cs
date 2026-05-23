using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using AutoLauncher.Models;
using Shared;

namespace AutoLauncher.Services;

/// <summary>
/// Status of a single launch operation. Used by the UI layer to format display text.
/// </summary>
public enum LaunchStatus
{
    Launching,
    Success,
    Failed,
    Skipped,
    Completed
}

/// <summary>
/// Launch progress event arguments.
/// </summary>
public class LaunchProgressEventArgs : EventArgs
{
    public int CurrentIndex { get; init; }
    public int TotalCount { get; init; }
    public LaunchStatus Status { get; init; }
    public string? AppName { get; init; }
    public string? AppIconPath { get; init; }
    public string? AppPath { get; init; }
    public double ProgressPercent { get; init; }
    public bool IsComplete { get; init; }

    /// <summary>Validation error message (only for Skipped status).</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Launches applications sequentially with progress reporting.
/// </summary>
public class LauncherService
{
    private static readonly ConcurrentDictionary<string, string?> PathCache = new();

    /// <summary>
    /// Launch progress event.
    /// </summary>
    public event EventHandler<LaunchProgressEventArgs>? ProgressChanged;

    /// <summary>
    /// Launches all applications in order.
    /// </summary>
    public async Task LaunchAllAsync(List<AppEntry> apps, CancellationToken ct = default)
    {
        var started = 0;
        var total = apps.Count;

        for (var i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            var app = apps[i];

            // Validate entry -- skip invalid ones with logging
            if (!app.IsValid)
            {
                var error = app.ValidationError ?? "Unknown validation error";
                Logger.Error($"app[{i}]: {error}");

                FireProgress(CreateProgress(i + 1, total, LaunchStatus.Skipped,
                    app.Name, app.Icon, app.Path, (double)(i + 1) / total * 100,
                    isComplete: false, errorMessage: error));
                continue;
            }

            var exeName = Path.GetFileName(app.Path);

            // Progress: launching
            FireProgress(CreateProgress(i + 1, total, LaunchStatus.Launching,
                app.Name, app.Icon, app.Path, (double)i / total * 100,
                isComplete: false));

            try
            {
                await LaunchSingleAsync(app, ct);
                started++;

                FireProgress(CreateProgress(i + 1, total, LaunchStatus.Success,
                    app.Name, app.Icon, app.Path, (double)(i + 1) / total * 100,
                    isComplete: false));
            }
            catch (Exception ex)
            {
                FireProgress(CreateProgress(i + 1, total, LaunchStatus.Failed,
                    app.Name, app.Icon, app.Path, (double)(i + 1) / total * 100,
                    isComplete: false, errorMessage: ex.Message));

                Logger.Error($"{exeName}: {ex.Message}");
            }

            // Delay between launches (only if waiting for completion)
            if (i < total - 1 && app.Wait)
                await Task.Delay(app.DelayMs > 0 ? app.DelayMs : 2000, ct).ConfigureAwait(false);
        }

        // Final message
        FireProgress(CreateProgress(started, total, LaunchStatus.Completed,
            appName: null, appIconPath: null, appPath: null,
            progressPercent: 100, isComplete: true));

        // Smooth stop
        await Task.Delay(3000, ct).ConfigureAwait(false);
    }

    private static LaunchProgressEventArgs CreateProgress(
        int currentIndex, int totalCount, LaunchStatus status,
        string? appName, string? appIconPath, string? appPath,
        double progressPercent, bool isComplete, string? errorMessage = null)
    {
        return new LaunchProgressEventArgs
        {
            CurrentIndex = currentIndex,
            TotalCount = totalCount,
            Status = status,
            AppName = appName,
            AppIconPath = appIconPath,
            AppPath = appPath,
            ProgressPercent = progressPercent,
            IsComplete = isComplete,
            ErrorMessage = errorMessage
        };
    }

    private static async Task LaunchSingleAsync(AppEntry app, CancellationToken ct)
    {
        var exePath = app.Path?.Trim() ?? "";

        // Determine if we need to search in PATH (name without a path)
        var isSimpleName = !exePath.Contains('\\') && !exePath.Contains('/') && !exePath.Contains('"');

        // For simple names (aliases) -- resolve full path via FindInPath
        // to support .cmd/.bat (e.g. "code" -> code.cmd).
        // ShellExecuteEx (UseShellExecute=true) does NOT search PATH,
        // and CreateProcess (UseShellExecute=false) only finds .exe.
        if (isSimpleName)
        {
            var resolved = FindInPath(exePath);
            if (resolved != null)
                exePath = resolved;
        }

        if (!isSimpleName || app.Admin)
        {
            // ShellExecute (UseShellExecute=true):
            //   - Needed for runas (admin)
            //   - Needed for full paths (file associations)
            //   - Does NOT search PATH (so we resolve above)

            // Protect against spaces (only if not already quoted)
            if (exePath.Contains(' ') && !exePath.StartsWith('"'))
                exePath = $"\"{exePath}\"";

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = app.Arguments ?? "",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            if (app.Admin)
                psi.Verb = "runas";

            // Process is wrapped in 'using' -- guaranteed disposal even on exception
            using var process = Process.Start(psi)
                                ?? throw new InvalidOperationException($"Failed to start: {exePath}");

            if (app.Wait)
                await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        else
        {
            // CreateProcess (UseShellExecute=false):
            //   - Searches PATH automatically
            //   - Does not support runas
            //   - Suitable for simple names without admin
            //   - CreateNoWindow hides the console window for .cmd/.bat
            // Process is wrapped in 'using' -- guaranteed disposal even on exception
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = app.Arguments ?? "",
                UseShellExecute = false,
                CreateNoWindow = true,
            }) ?? throw new InvalidOperationException($"Failed to start: {exePath}");

            if (app.Wait)
                await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Searches for an executable in %PATH% directories by full path.
    /// Results are cached in a static dictionary.
    /// Needed for ShellExecute (admin), since ShellExecuteEx does not search PATH.
    /// </summary>
    public static string? FindInPath(string fileName)
    {
        return PathCache.GetOrAdd(fileName, key =>
        {
            var extensions = string.IsNullOrEmpty(Path.GetExtension(key))
                ? new[] { ".exe", ".cmd", ".bat", ".com" }
                : new[] { "" };

            var pathDirs = Environment.GetEnvironmentVariable("PATH")?
                               .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                           ?? [];

            foreach (var dir in pathDirs)
            {
                var dirPath = dir.Trim();
                foreach (var ext in extensions)
                {
                    var fullPath = Path.Combine(dirPath, key + ext);
                    if (File.Exists(fullPath))
                        return fullPath;
                }
            }

            return null;
        });
    }

    private void FireProgress(LaunchProgressEventArgs e)
    {
        ProgressChanged?.Invoke(this, e);
    }

}
