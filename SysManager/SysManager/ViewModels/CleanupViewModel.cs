using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysManager.Helpers;
using SysManager.Services;

namespace SysManager.ViewModels;

public partial class CleanupViewModel : ViewModelBase
{
    private readonly PowerShellRunner _runner;
    private CancellationTokenSource? _cts;

    public ConsoleViewModel Console { get; } = new();

    [ObservableProperty] private bool _isElevated;

    public CleanupViewModel(PowerShellRunner runner)
    {
        _runner = runner;
        _runner.LineReceived += l => Console.Append(l);
        _runner.ProgressChanged += p => Progress = p;
        IsElevated = AdminHelper.IsElevated();
    }

    [RelayCommand]
    private void RelaunchAsAdmin()
    {
        if (AdminHelper.RelaunchAsAdmin())
            System.Windows.Application.Current.Shutdown();
    }

    [RelayCommand]
    private async Task CleanTempAsync()
    {
        IsBusy = true; IsProgressIndeterminate = true;
        StatusMessage = "Cleaning temp folders...";
        _cts = new CancellationTokenSource();
        try
        {
            await _runner.RunScriptViaPwshAsync(@"
                $paths = @($env:TEMP, ""$env:SystemRoot\Temp"")
                $totalBytes = 0
                foreach ($p in $paths) {
                    if (Test-Path $p) {
                        Get-ChildItem -Path $p -Recurse -Force -ErrorAction SilentlyContinue |
                            ForEach-Object {
                                try { $totalBytes += $_.Length } catch {}
                                try { Remove-Item $_.FullName -Force -Recurse -ErrorAction SilentlyContinue } catch {}
                            }
                    }
                }
                ""Freed approximately $([Math]::Round($totalBytes/1MB,1)) MB""
            ", cancellationToken: _cts.Token);
            StatusMessage = "Temp cleanup done";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; IsProgressIndeterminate = false; }
    }

    [RelayCommand]
    private async Task EmptyRecycleBinAsync()
    {
        IsBusy = true; IsProgressIndeterminate = true;
        StatusMessage = "Emptying Recycle Bin...";
        _cts = new CancellationTokenSource();
        try
        {
            await _runner.RunScriptViaPwshAsync(@"
                Clear-RecycleBin -Force -ErrorAction SilentlyContinue
                'Recycle Bin cleared'
            ", cancellationToken: _cts.Token);
            StatusMessage = "Done";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; IsProgressIndeterminate = false; }
    }

    [RelayCommand]
    private async Task RunSfcAsync()
    {
        if (!AdminHelper.IsElevated())
        { StatusMessage = "SFC requires admin"; if (AdminHelper.RelaunchAsAdmin()) System.Windows.Application.Current.Shutdown(); return; }

        IsBusy = true; IsProgressIndeterminate = true;
        StatusMessage = "Running sfc /scannow — this can take several minutes...";
        _cts = new CancellationTokenSource();
        try
        {
            await _runner.RunProcessAsync("sfc.exe", "/scannow", _cts.Token);
            StatusMessage = "SFC complete";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; IsProgressIndeterminate = false; }
    }

    [RelayCommand]
    private async Task RunDismAsync()
    {
        if (!AdminHelper.IsElevated())
        { StatusMessage = "DISM requires admin"; if (AdminHelper.RelaunchAsAdmin()) System.Windows.Application.Current.Shutdown(); return; }

        IsBusy = true; IsProgressIndeterminate = true;
        StatusMessage = "Running DISM /Online /Cleanup-Image /RestoreHealth...";
        _cts = new CancellationTokenSource();
        try
        {
            await _runner.RunProcessAsync("DISM.exe", "/Online /Cleanup-Image /RestoreHealth", _cts.Token);
            StatusMessage = "DISM complete";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsBusy = false; IsProgressIndeterminate = false; }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();
}
