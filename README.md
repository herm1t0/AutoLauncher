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

### Root fields

| Key      | Type   | Default    | Description |
|----------|--------|------------|-------------|
| `theme`  | string | `"system"` | UI theme. Values: `"dark"`, `"light"`, `"system"` (follows Windows), `"mocha"` (Catppuccin Mocha), `"latte"` (Catppuccin Latte) |
| `topmost`| bool   | `true`     | Keep window always on top of others |
| `apps`   | array  | `[]`       | List of applications to launch |

### App entry fields (elements of `apps`)

| Key          | Type    | Default | Required | Description |
|--------------|---------|---------|----------|-------------|
| `name`       | string  | `""`    | Yes      | Display name shown in the UI |
| `path`       | string  | `""`    | Yes      | Path to the executable |
| `arguments`  | string  | `null`  | No       | Command-line arguments passed to the executable |
| `admin`      | bool    | `false` | No       | Launch as administrator |
| `wait`       | bool    | `false` | No       | Wait for the process to exit before starting the next app |
| `delayMs`    | int     | `1000`  | No       | Additional delay after launch, in milliseconds |
| `icon`       | string  | `null`  | No       | Path to a custom icon (`.exe`, `.ico`, `.dll`). If not set, extracted from the executable |

### Example

```json
{
  "theme": "system",
  "topmost": true,
  "apps": [
    {
      "name": "Browser",
      "path": "C:\\Program Files\\Mozilla Firefox\\firefox.exe",
      "arguments": "--private-window",
      "admin": false,
      "wait": false,
      "delayMs": 1000,
      "icon": null
    },
    {
      "name": "Terminal",
      "path": "C:\\Program Files\\Windows Terminal\\wt.exe",
      "arguments": "-d C:\\projects",
      "admin": false,
      "wait": false,
      "delayMs": 500,
      "icon": null
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
