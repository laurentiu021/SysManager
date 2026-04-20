using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysManager.Helpers;
using SysManager.Models;
using SysManager.Services;

namespace SysManager.ViewModels;

public partial class SystemHealthViewModel : ViewModelBase
{
    private readonly SystemInfoService _sys;
    private readonly DiskHealthService _diskHealth = new();
    private readonly MemoryTestService _memTest = new();
    private readonly PowerShellRunner _runner = new();
    private CancellationTokenSource? _cts;

    public ObservableCollection<MemoryModule> Modules { get; } = new();
    public ObservableCollection<DiskInfo> Disks { get; } = new();
    public ObservableCollection<DiskHealthReport> DiskHealth { get; } = new();

    public ConsoleViewModel Console { get; } = new();

    [ObservableProperty] private OsInfo? _os;
    [ObservableProperty] private CpuInfo? _cpu;
    [ObservableProperty] private MemoryInfo? _memory;
    [ObservableProperty] private string _summary = "Press 'Scan' to collect system info";
    [ObservableProperty] private bool _isElevated;

    // Memory diagnostic summary
    [ObservableProperty] private int _wheaMemoryErrors;
    [ObservableProperty] private int _memoryDiagnosticResults;
    [ObservableProperty] private string _memoryHealthVerdict = "Click 'Check memory errors' to inspect.";
    [ObservableProperty] private string _memoryHealthColorHex = "#9AA0A6";

    public SystemHealthViewModel(SystemInfoService sys)
    {
        _sys = sys;
        IsElevated = AdminHelper.IsElevated();
        _runner.LineReceived += l => Console.Append(l);
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        IsBusy = true;
        IsProgressIndeterminate = true;
        StatusMessage = "Collecting system info...";
        try
        {
            var snap = await _sys.CaptureAsync();
            Os = snap.Os;
            Cpu = snap.Cpu;
            Memory = snap.Memory;
            Modules.Clear();
            foreach (var m in snap.Memory.Modules) Modules.Add(m);
            Disks.Clear();
            foreach (var d in snap.Disks) Disks.Add(d);
            Summary = $"OS {snap.Os.Caption}  —  CPU {snap.Cpu.Name} ({snap.Cpu.Cores}c/{snap.Cpu.LogicalProcessors}t)  —  RAM {snap.Memory.UsedGB:0.0}/{snap.Memory.TotalGB:0.0} GB  —  Disks {snap.Disks.Count}";
            StatusMessage = $"Scan at {snap.CapturedAt:HH:mm:ss}";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; IsProgressIndeterminate = false; }
    }

    [RelayCommand]
    private async Task CheckDiskHealthAsync()
    {
        IsBusy = true;
        IsProgressIndeterminate = true;
        StatusMessage = "Reading SMART data...";
        DiskHealth.Clear();
        try
        {
            var reports = await _diskHealth.CollectAsync();
            foreach (var r in reports) DiskHealth.Add(r);
            StatusMessage = $"Collected {reports.Count} disk report(s).";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; IsProgressIndeterminate = false; }
    }

    [RelayCommand]
    private async Task CheckMemoryErrorsAsync()
    {
        IsBusy = true;
        IsProgressIndeterminate = true;
        StatusMessage = "Scanning event log for memory errors...";
        try
        {
            var summary = await _memTest.CheckErrorLogsAsync();
            WheaMemoryErrors = summary.WheaMemoryErrors;
            MemoryDiagnosticResults = summary.MemoryDiagnosticResults;

            if (summary.WheaMemoryErrors > 0)
            {
                MemoryHealthVerdict = $"{summary.WheaMemoryErrors} hardware-error event(s) in the last 30 days. Test your RAM.";
                MemoryHealthColorHex = "#EF4444";
            }
            else if (summary.MemoryDiagnosticResults > 0)
            {
                MemoryHealthVerdict = $"Memory diagnostic has run {summary.MemoryDiagnosticResults} time(s) recently. Check results.";
                MemoryHealthColorHex = "#F59E0B";
            }
            else
            {
                MemoryHealthVerdict = "No memory errors reported in the last 30 days.";
                MemoryHealthColorHex = "#22C55E";
            }
            StatusMessage = "Memory scan done.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; IsProgressIndeterminate = false; }
    }

    [RelayCommand]
    private void ScheduleMemoryTest()
    {
        // mdsched.exe shows a dialog asking "Restart now" or "at next boot".
        // We just open it; the user confirms.
        var ok = _memTest.ScheduleAtNextBoot();
        StatusMessage = ok
            ? "Windows Memory Diagnostic launched — choose a schedule option."
            : "Failed to launch mdsched.exe.";
    }

    [RelayCommand]
    private void OpenMemTest86()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://www.memtest86.com/download.htm")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task RunChkdskAsync(string? driveLetter)
    {
        if (string.IsNullOrWhiteSpace(driveLetter)) driveLetter = "C:";
        IsBusy = true;
        IsProgressIndeterminate = true;
        StatusMessage = $"Running chkdsk {driveLetter} (read-only)...";
        _cts = new CancellationTokenSource();
        try
        {
            // Read-only scan — safe to run without admin, reports without repairing.
            await _runner.RunProcessAsync("chkdsk.exe", $"{driveLetter} /scan", _cts.Token);
            StatusMessage = "chkdsk done.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; IsProgressIndeterminate = false; }
    }

    [RelayCommand]
    private void RelaunchAsAdmin()
    {
        if (AdminHelper.RelaunchAsAdmin())
            System.Windows.Application.Current.Shutdown();
    }

    [RelayCommand]
    private void CancelScan() => _cts?.Cancel();
}
