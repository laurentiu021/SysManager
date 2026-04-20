// SysManager · DeepCleanupViewModel — opt-in cleanup + read-only large files
// Author: laurentiu021 · https://github.com/laurentiu021/SysManager
// License: MIT

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysManager.Models;
using SysManager.Services;

namespace SysManager.ViewModels;

public partial class DeepCleanupViewModel : ViewModelBase
{
    private readonly DeepCleanupService _cleanup = new();
    private readonly LargeFileScanner _largeFiles = new();
    private readonly FixedDriveService _drives = new();
    private CancellationTokenSource? _cts;

    public ObservableCollection<CleanupCategory> Categories { get; } = new();
    public ObservableCollection<LargeFileEntry> LargeFiles { get; } = new();
    public ObservableCollection<ScanLocation> ScanLocations { get; } = new();

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isCleaning;
    [ObservableProperty] private bool _isLargeScanning;

    [ObservableProperty] private string _scanSummary = "Press 'Scan' to discover what can be safely freed.";
    [ObservableProperty] private string _cleanSummary = string.Empty;
    [ObservableProperty] private string _largeScanStatus = string.Empty;
    [ObservableProperty] private int _minSizeMB = 500;
    [ObservableProperty] private ScanLocation? _selectedLocation;
    [ObservableProperty] private int _topCount = 100;

    public long TotalSelectedBytes => Categories.Where(c => c.IsSelected).Sum(c => c.TotalSizeBytes);
    public string TotalSelectedDisplay => CleanupCategory.HumanSize(TotalSelectedBytes);

    public DeepCleanupViewModel()
    {
        _ = LoadLocationsAsync();
    }

    private async Task LoadLocationsAsync()
    {
        try
        {
            ScanLocations.Clear();

            // Preset user folders first — the most common "where did my space go"
            // spots that don't include any system files.
            AddLocation("📥  Downloads", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads");
            AddLocation("📄  Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            AddLocation("🖥️  Desktop",   Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            AddLocation("🎬  Videos",    Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
            AddLocation("🖼️  Pictures",  Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
            AddLocation("🎵  Music",     Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));

            var pf    = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            AddLocation("💼  Program Files",      pf);
            AddLocation("💼  Program Files (x86)", pfx86);

            // Then all fixed drives (whole drive scan).
            var drives = await _drives.EnumerateAsync();
            foreach (var d in drives)
                AddLocation($"💾  Whole drive  {d.Letter}  ({d.SizeGB:F0} GB)", d.Letter + @"\");

            SelectedLocation = ScanLocations.FirstOrDefault();
        }
        catch { }
    }

    private void AddLocation(string label, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.Directory.Exists(path)) return;
        ScanLocations.Add(new ScanLocation(label, path));
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning) return;
        IsScanning = true;
        ScanSummary = "Scanning safe cleanup locations...";
        _cts = new CancellationTokenSource();
        try
        {
            var cats = await _cleanup.ScanAsync(_cts.Token);
            Categories.Clear();
            foreach (var c in cats)
            {
                c.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(CleanupCategory.IsSelected))
                    {
                        OnPropertyChanged(nameof(TotalSelectedBytes));
                        OnPropertyChanged(nameof(TotalSelectedDisplay));
                    }
                };
                Categories.Add(c);
            }
            var total = cats.Sum(c => c.TotalSizeBytes);
            ScanSummary = $"Found {CleanupCategory.HumanSize(total)} across {cats.Count} categories. Untick anything you want to keep.";
            OnPropertyChanged(nameof(TotalSelectedBytes));
            OnPropertyChanged(nameof(TotalSelectedDisplay));
        }
        catch (Exception ex) { ScanSummary = $"Scan failed: {ex.Message}"; }
        finally { IsScanning = false; }
    }

    [RelayCommand]
    private async Task CleanAsync()
    {
        if (IsCleaning || !Categories.Any(c => c.IsSelected)) return;
        IsCleaning = true;
        CleanSummary = "Cleaning selected categories — in progress...";
        _cts = new CancellationTokenSource();
        try
        {
            var result = await _cleanup.CleanAsync(Categories, _cts.Token);
            CleanSummary = result.Summary;
            // Re-scan so users see updated sizes.
            await ScanAsync();
        }
        catch (Exception ex) { CleanSummary = $"Clean failed: {ex.Message}"; }
        finally { IsCleaning = false; }
    }

    [RelayCommand]
    private void SelectAll(bool? value)
    {
        var on = value ?? true;
        foreach (var c in Categories) c.IsSelected = on && !c.IsDestructiveHint;
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    // ---------- large files finder ----------

    [RelayCommand]
    private async Task ScanLargeFilesAsync()
    {
        if (IsLargeScanning) return;
        if (SelectedLocation == null)
        {
            LargeScanStatus = "Pick a location first.";
            return;
        }

        IsLargeScanning = true;
        LargeFiles.Clear();
        LargeScanStatus = "Scanning — this may take a minute on large folders...";
        _cts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<string>(s => LargeScanStatus = s);
            var list = await _largeFiles.ScanAsync(
                rootPath: SelectedLocation.Path,
                minSizeBytes: (long)MinSizeMB * 1024L * 1024L,
                top: TopCount,
                progress: progress,
                ct: _cts.Token);
            foreach (var f in list) LargeFiles.Add(f);
            LargeScanStatus = $"Found {list.Count} files ≥ {MinSizeMB} MB in {SelectedLocation.Label.Trim()}.";
        }
        catch (Exception ex) { LargeScanStatus = $"Error: {ex.Message}"; }
        finally { IsLargeScanning = false; }
    }

    [RelayCommand]
    private void ShowInExplorer(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }); }
        catch { }
    }

    [RelayCommand]
    private void CopyPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try { System.Windows.Clipboard.SetText(path); } catch { }
    }
}

/// <summary>Labelled location the user can pick in the large-files finder.</summary>
public sealed record ScanLocation(string Label, string Path);
