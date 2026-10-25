# Brightness Control

A small application for controlling monitor brightness from the Windows system tray. The project is written in C# and WPF.

**Free for personal and other non-commercial use. Commercial use, sale, and paid redistribution are prohibited.** See the [LICENSE](LICENSE) file for the terms. Because this license restricts commercial use, the project is not open source in the generally accepted sense, although its source code is publicly available.

## Features

- Controls external monitors via DDC/CI.
- Controls compatible built-in displays via WMI.
- Supports multiple monitors, light and dark themes, and a system tray icon.
- Includes an option to launch with Windows.

Support depends on the monitor, video cable or dock, and the DDC/CI setting in the monitor’s menu. Some hardware does not allow software brightness control.

## Download

Open [Releases](../../releases) and download the latest `BrightnessControl.exe`. It is a self-contained Windows x64 build with the .NET runtime included. Windows may show a warning the first time you run it because the build is not signed with a publisher certificate.

## Build from source

You need Windows and the .NET 8 SDK. In PowerShell, from the repository root, run:

```powershell
dotnet restore .\BrightnessControl.sln
dotnet publish .\BrightnessControl.App\BrightnessControl.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Platform=x64 -o .\publish\win-x64
```

The resulting executable will be in `publish\win-x64`. This folder is excluded from Git; ready-to-use binaries are attached to GitHub Releases instead of being committed to the repository.

## Feedback

Report bugs and hardware compatibility issues through [Issues](../../issues). For commercial licensing inquiries, contact the repository owner.

## License

The source code may be used, studied, modified, and redistributed for free only under the non-commercial license in the [LICENSE](LICENSE) file. Commercial use requires separate written permission from the copyright holder.
