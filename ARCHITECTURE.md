# Architecture

SysManager is a tabbed WPF desktop app on .NET 8, written in C# 12. It follows
a standard MVVM layout with a thin service layer that wraps Windows APIs,
PowerShell, and external CLIs (winget, Ookla `speedtest`).

## Solution layout

```
SysManager/
├── SysManager/                 # main WPF app
│   ├── Models/                 # POCOs (snapshots, samples, reports)
│   ├── Services/               # Windows / PowerShell / CLI wrappers
│   ├── ViewModels/             # one VM per tab + MainWindowViewModel
│   ├── Views/                  # XAML views + code-behind
│   ├── Helpers/                # AdminHelper, converters, gateway lookup
│   ├── Resources/              # icons and assets
│   ├── App.xaml(.cs)
│   ├── MainWindow.xaml(.cs)
│   └── SysManager.csproj
├── SysManager.Tests/           # xUnit unit + integration tests
└── SysManager.UITests/         # FlaUI UI-automation tests
```

## Layers

### Views (WPF / XAML)
Each tab has a dedicated `*View.xaml` with its code-behind limited to
construction and the occasional UI-only event handler. All state lives in the
view model.

### ViewModels
Built on `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`,
`[RelayCommand]`). `ViewModelBase` is the shared base; `MainWindowViewModel`
owns the navigation list and the active view.

One VM per tab:
- `DashboardViewModel`
- `NetworkViewModel`
- `LogsViewModel`
- `SystemHealthViewModel`
- `WindowsUpdateViewModel`
- `AppUpdatesViewModel`
- `CleanupViewModel`
- `DriversViewModel`
- `ConsoleViewModel` (shared output log)

### Services
Thin wrappers around the underlying platform. Each service is designed to be
unit-testable — where possible, they depend on interfaces or accept seams for
swapping the underlying process runner.

Key services:
- `PingMonitorService` / `TracerouteService` / `TracerouteMonitorService` —
  network probes, built on `System.Net.NetworkInformation.Ping` and `tracert`.
- `SpeedTestService` — HTTP speed test against Cloudflare plus the Ookla CLI,
  auto-downloaded on first use.
- `PowerShellRunner` — wraps `System.Management.Automation` to run scripts
  and stream output line-by-line; used by Windows Update, winget, Cleanup.
- `WingetService` — shells out to `winget` and parses its table output.
- `DiskHealthService` — pulls SMART data through WMI (`MSStorageDriver_FailurePredict*`,
  `Win32_DiskDrive`, `MSFT_PhysicalDisk`).
- `MemoryTestService` — scans WHEA / MemoryDiagnostics events and schedules
  the Windows Memory Diagnostic at next boot.
- `EventLogService` + `EventExplainer` — read Windows Event Log and attach
  human-readable explanations and recommended actions.
- `HealthAnalyzer` — turns raw SMART / ping data into verdict pills.
- `SystemInfoService` — OS / CPU / RAM / uptime snapshot for the dashboard.
- `LogService` — Serilog wrapper with rolling file sink under
  `%LOCALAPPDATA%\SysManager\logs`.

### Models
Plain DTOs passed between services and view models. Each model owns its
validation and display-only helpers (colour verdicts, formatted text) so view
models can bind directly.

## Admin elevation

Features that require admin (Windows Update, SFC/DISM, system-wide winget
upgrades) check elevation via `AdminHelper.IsElevated()` and surface a banner
when running unelevated. The banner calls `AdminHelper.RelaunchAsAdmin()`,
which restarts the process with `runas` and the current command-line args.

## Threading

- Long-running work (ping loops, PowerShell runs, winget scans) runs on
  background tasks.
- View-model observable properties are updated on the UI thread via the
  dispatcher captured in `ViewModelBase`.
- `PingMonitorService` uses a single background loop per target and a
  bounded in-memory ring buffer for chart samples.

## Logging

Serilog writes to a rolling file sink at
`%LOCALAPPDATA%\SysManager\logs\sysmanager-.log` (one file per day, 7 days
retained). The in-app Console tab mirrors the same stream.

## Testing

See [TESTING.md](TESTING.md) for details on the xUnit unit / integration
project and the FlaUI UI-automation project.
