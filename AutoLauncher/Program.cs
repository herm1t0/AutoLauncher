using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using AutoLauncher.Services;
using Shared;

namespace AutoLauncher;

/// <summary>
/// Entry point. Handles CLI arguments before deciding whether to start the GUI.
///
/// Usage:
///   AutoLauncher.exe                    start GUI (normal mode)
///   AutoLauncher.exe --register         add current directory to user PATH
///   AutoLauncher.exe --unregister       remove current directory from user PATH
///   AutoLauncher.exe --unregister --all remove ALL AutoLauncher.exe entries from PATH
///   AutoLauncher.exe --config           open config file in default editor
///   AutoLauncher.exe --help             show this help
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var configDir = Environment.GetEnvironmentVariable("AUTOLAUNCHER_CONFIG_HOME");
        Logger.Init("AutoLauncher", string.IsNullOrWhiteSpace(configDir) ? null : configDir);

        if (args.Length == 0)
        {
            StartGui();
            return;
        }

        var command = args[0];

        switch (command.ToLowerInvariant())
        {
            case "--register":
                HandleRegister();
                break;

            case "--unregister":
                HandleUnregister(args);
                break;

            case "--config":
                HandleConfig();
                break;

            case "--help":
                HandleHelp();
                break;

            default:
                HandleUnknown(command);
                break;
        }
    }

    private static void StartGui()
    {
        var services = new ServiceCollection();

        // Register services
        services.AddSingleton<ConfigService>();
        services.AddSingleton<LauncherService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<MainWindow>();

        var provider = services.BuildServiceProvider();

        var app = new App();
        app.InitializeComponent();
        app.Run(provider.GetRequiredService<MainWindow>());
    }

    // - CLI handlers

    private static void HandleRegister() => CliHandler.HandleRegister();

    private static void HandleUnregister(string[] args) => CliHandler.HandleUnregister(args);

    private static void HandleConfig()
    {
        ConsoleHelper.EnsureConsole();
        try
        {
            var configPath = Path.Combine(Logger.AppDataDirectory, "autolauncher.json");
            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine($"Config file not found: {configPath}");
                Console.Error.WriteLine("Run AutoLauncher once without arguments to create a default config, or create it manually.");
                return;
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = configPath,
                UseShellExecute = true
            });
        }
        finally
        {
            NativeMethods.FreeConsole();
        }
    }

    private static void HandleHelp() => CliHandler.HandleHelp("""
                              AutoLauncher - Sequential Application Launcher
                              ==============================================

                              Usage:
                                AutoLauncher.exe                     Start GUI (normal mode)
                                AutoLauncher.exe --register          Add current directory to user PATH
                                AutoLauncher.exe --unregister        Remove current directory from user PATH
                                AutoLauncher.exe --unregister --all  Remove ALL AutoLauncher.exe entries from PATH
                                AutoLauncher.exe --config            Open config file in default editor
                                AutoLauncher.exe --help              Show this help message

                              Configuration:
                                Edit the file: %APPDATA%\AutoLauncher\autolauncher.json

                                Or set environment variable AUTOLAUNCHER_CONFIG_HOME
                                to override the config directory.
                              """);

    private static void HandleUnknown(string arg) => CliHandler.HandleUnknown(arg);
}
