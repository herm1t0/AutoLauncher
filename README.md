# AutoLauncher

A sequential application launcher with WPF GUI. Launches a list of programs one by one,
waiting for each process to exit before starting the next.

## Usage

```
AutoLauncher.exe                    Start GUI (normal mode)
AutoLauncher.exe --register         Add current directory to user PATH
AutoLauncher.exe --unregister       Remove current directory from user PATH
AutoLauncher.exe --unregister --all Remove ALL AutoLauncher.exe entries from PATH
AutoLauncher.exe --config           Open config file in default editor
AutoLauncher.exe --help             Show this help
```

After `--register`, run just `autolauncher` from any terminal.

## Install

Two options available on the [Releases](https://github.com/herm1t0/AutoLauncher/releases) page:

**Portable** (`AutoLauncher.exe`)
- Download, place anywhere, run `AutoLauncher.exe --register`
- Uninstall: `AutoLauncher.exe --unregister --all`

**Installer** (`AutoLauncher.msi`)
- Installs to `Program Files`, adds to system PATH automatically
- Optional autostart with Windows
- Uninstall via Windows Settings

## Configuration

Config file: `%APPDATA%\AutoLauncher\autolauncher.json`

Override the directory with `AUTOLAUNCHER_CONFIG_HOME` environment variable.

Example:

```json
{
  "apps": [
    {
      "name": "Browser",
      "path": "C:\\Program Files\\Mozilla Firefox\\firefox.exe",
      "args": "",
      "enabled": true,
      "timeoutSeconds": 30
    }
  ]
}
```

## Build

Requires .NET 10.0 SDK. Depends on [Herm1t.Shared](https://www.nuget.org/packages/Herm1t.Shared) NuGet package.

```powershell
dotnet publish -c Release
```

Target: `win-x64`, single-file publish.
