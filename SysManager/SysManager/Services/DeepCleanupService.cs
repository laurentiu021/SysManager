// SysManager · DeepCleanupService — safe-by-design scanner & cleaner
// Author: laurentiu021 · https://github.com/laurentiu021/SysManager
// License: MIT

using System.IO;
using SysManager.Models;

namespace SysManager.Services;

/// <summary>
/// Safe deep-cleanup scanner. Scan is read-only; Clean deletes only the
/// opted-in categories. Vendor caches / launcher caches are included but
/// game files, logins and browser data are never touched.
/// </summary>
public sealed class DeepCleanupService
{
    public Task<IReadOnlyList<CleanupCategory>> ScanAsync(CancellationToken ct = default)
        => Task.Run(() => Scan(ct), ct);

    public Task<CleanupResult> CleanAsync(IReadOnlyList<CleanupCategory> categories, CancellationToken ct = default)
        => Task.Run(() => Clean(categories, ct), ct);

    // ---------- scanning ----------

    private static IReadOnlyList<CleanupCategory> Scan(CancellationToken ct)
    {
        var cats = new List<CleanupCategory>();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programData  = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var systemDrive  = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        var windowsDir   = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var tempUser     = Path.GetTempPath();
        var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var pf    = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        // ----- System / drivers / updates -----
        cats.Add(Build("NVIDIA installer leftovers",
            "Extracted driver packages NVIDIA drops on your drive root and in ProgramData during an install. Safe to remove once the driver is installed.",
            new[]
            {
                Path.Combine(systemDrive, "NVIDIA"),
                Path.Combine(programData, "NVIDIA Corporation", "Downloader"),
                Path.Combine(programData, "NVIDIA Corporation", "NV_Cache"),
                Path.Combine(programData, "NVIDIA Corporation", "Installer2"),
                Path.Combine(localAppData, "NVIDIA", "GLCache"),
                Path.Combine(localAppData, "NVIDIA", "DXCache"),
                Path.Combine(localAppData, "NVIDIA", "ComputeCache"),
            }, ct));

        cats.Add(Build("AMD installer leftovers",
            "Unpacked driver installer folder AMD creates on the root of C:\\. Confirmed safe by AMD community docs.",
            new[] { Path.Combine(systemDrive, "AMD") }, ct));

        cats.Add(Build("Intel driver extracts",
            "Temporary driver package extracts from Intel installers.",
            new[] { Path.Combine(systemDrive, "Intel") }, ct));

        cats.Add(Build("Windows Update cache",
            "Previously downloaded Windows Update packages. Windows re-downloads anything it still needs next time.",
            new[] { Path.Combine(windowsDir, "SoftwareDistribution", "Download") }, ct));

        cats.Add(Build("Delivery Optimization cache",
            "Peer-to-peer update cache. Regenerated on demand.",
            new[] { Path.Combine(windowsDir, "SoftwareDistribution", "DeliveryOptimization", "Cache") }, ct));

        cats.Add(Build("Windows Installer patch cache",
            "C:\\Windows\\Installer\\$PatchCache$ stores baseline patch files used only when uninstalling an MSI patch. Safe per Microsoft devblog.",
            new[] { Path.Combine(windowsDir, "Installer", "$PatchCache$") }, ct));

        cats.Add(Build("Temporary files",
            "Per-user and system TEMP folders. Anything still in use is skipped automatically.",
            new[] { tempUser, Path.Combine(windowsDir, "Temp") }, ct));

        cats.Add(Build("Prefetch files",
            "Windows boot/launch prefetch cache. Windows rebuilds it as apps are used.",
            new[] { Path.Combine(windowsDir, "Prefetch") }, ct));

        cats.Add(Build("Crash dumps & error reports",
            "Windows Error Reporting queue and user-mode crash dumps (*.dmp).",
            new[]
            {
                Path.Combine(localAppData, "CrashDumps"),
                Path.Combine(localAppData, "Microsoft", "Windows", "WER", "ReportQueue"),
                Path.Combine(localAppData, "Microsoft", "Windows", "WER", "ReportArchive"),
                Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportQueue"),
                Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportArchive"),
            }, ct));

        cats.Add(BuildOldFiles("Old Windows servicing logs (> 30 days)",
            "CBS logs older than 30 days. Windows keeps rolling ones itself.",
            Path.Combine(windowsDir, "Logs", "CBS"), TimeSpan.FromDays(30), ct));

        cats.Add(Build("DirectX shader cache",
            "Precompiled GPU shaders cached by Windows. Rebuilt automatically the next time games run — clearing can fix stutter.",
            new[] { Path.Combine(localAppData, "D3DSCache") }, ct));

        cats.Add(BuildRecycleBin(ct));

        // ----- Gaming launchers: caches & logs only (never login/game files) -----
        cats.Add(Build("Steam — browser & depot cache",
            "Steam web browser cache, HTML cache, app cache and depot lookup cache. Doesn't touch game files, downloads or logins.",
            SteamCacheDirs(pfx86, pf, localAppData), ct));

        cats.Add(Build("Steam — shader cache",
            "Per-game shader cache under steamapps\\shadercache. Rebuilt on next launch — clearing can fix stutter or shader corruption.",
            SteamShaderCacheDirs(pfx86, pf), ct));

        cats.Add(Build("Epic Games Launcher — webcache & logs",
            "Epic Launcher browser webcache and log files. Doesn't affect your Epic login or installed games.",
            new[]
            {
                Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "webcache"),
                Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "webcache_4147"),
                Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "webcache_4430"),
                Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "Logs"),
                Path.Combine(localAppData, "UnrealEngineLauncher", "Saved", "webcache"),
            }, ct));

        cats.Add(Build("Battle.net — cache",
            "Battle.net agent and Blizzard launcher cache. Doesn't touch installed games or logins.",
            new[]
            {
                Path.Combine(programData, "Battle.net", "Agent", "data", "cache"),
                Path.Combine(programData, "Blizzard Entertainment", "Battle.net", "Cache"),
                Path.Combine(localAppData, "Battle.net", "Cache"),
            }, ct));

        cats.Add(Build("Riot Client / League of Legends — logs",
            "Riot Client and League client logs only. No game files or credentials.",
            new[]
            {
                Path.Combine(localAppData, "Riot Games", "Riot Client", "Logs"),
                Path.Combine(pfx86, "Riot Games", "League of Legends", "Logs"),
                Path.Combine(pf,    "Riot Games", "League of Legends", "Logs"),
            }, ct));

        cats.Add(Build("GOG Galaxy — cache",
            "GOG Galaxy launcher webcache and redists installer cache.",
            new[]
            {
                Path.Combine(localAppData, "GOG.com", "Galaxy", "webcache"),
                Path.Combine(programData, "GOG.com", "Galaxy", "redists"),
            }, ct));

        cats.Add(Build("EA App / Origin — cache",
            "EA Desktop (and legacy Origin) browser cache and logs. Doesn't affect installed games or logins.",
            new[]
            {
                Path.Combine(localAppData, "Electronic Arts", "EA Desktop", "CEF-Cache"),
                Path.Combine(localAppData, "Electronic Arts", "EA Desktop", "Logs"),
                Path.Combine(localAppData, "Origin", "Logs"),
                Path.Combine(programData, "Origin", "Logs"),
            }, ct));

        // ----- Windows.old (irreversible, never checked by default) -----
        var windowsOld = Path.Combine(systemDrive, "Windows.old");
        if (Directory.Exists(windowsOld))
        {
            var size = SafeDirSize(windowsOld, ct);
            cats.Add(new CleanupCategory
            {
                Name = "Windows.old (previous Windows installation)",
                Description = "Remove only if you're sure you don't want to roll back to your previous Windows version. Windows normally auto-deletes this after 10 days.",
                Paths = new[] { windowsOld },
                TotalSizeBytes = size,
                FileCount = SafeFileCount(windowsOld, ct),
                IsDestructiveHint = true,
                IsSelected = false,
            });
        }

        return cats;
    }

    // ---------- launcher roots ----------

    private static string[] SteamRoots(string pfx86, string pf)
    {
        var roots = new List<string>
        {
            Path.Combine(pfx86, "Steam"),
            Path.Combine(pf, "Steam"),
        };
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
            var candidate = Path.Combine(drive.RootDirectory.FullName, "Steam");
            if (Directory.Exists(candidate)) roots.Add(candidate);
        }
        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string[] SteamCacheDirs(string pfx86, string pf, string localAppData)
    {
        var result = new List<string>();
        foreach (var root in SteamRoots(pfx86, pf))
        {
            result.Add(Path.Combine(root, "appcache"));
            result.Add(Path.Combine(root, "htmlcache"));
            result.Add(Path.Combine(root, "depotcache"));
            result.Add(Path.Combine(root, "logs"));
        }
        result.Add(Path.Combine(localAppData, "Steam", "htmlcache"));
        return result.ToArray();
    }

    private static string[] SteamShaderCacheDirs(string pfx86, string pf)
    {
        var result = new List<string>();
        foreach (var root in SteamRoots(pfx86, pf))
            result.Add(Path.Combine(root, "steamapps", "shadercache"));
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
            var candidate = Path.Combine(drive.RootDirectory.FullName, "SteamLibrary", "steamapps", "shadercache");
            if (Directory.Exists(candidate)) result.Add(candidate);
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // ---------- cleaning ----------

    private static CleanupResult Clean(IReadOnlyList<CleanupCategory> categories, CancellationToken ct)
    {
        long freed = 0;
        var errors = new List<string>();
        var filesDeleted = 0;

        foreach (var cat in categories.Where(c => c.IsSelected))
        {
            if (ct.IsCancellationRequested) break;
            var cutoff = cat.OlderThan.HasValue ? DateTime.UtcNow - cat.OlderThan.Value : (DateTime?)null;
            foreach (var path in cat.Paths)
            {
                if (ct.IsCancellationRequested) break;
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) continue;

                try
                {
                    foreach (var file in EnumerateFiles(path, ct))
                    {
                        if (ct.IsCancellationRequested) break;
                        try
                        {
                            if (cutoff.HasValue)
                            {
                                var fi = new FileInfo(file);
                                if (fi.LastWriteTimeUtc >= cutoff.Value) continue;
                            }
                            var len = SafeLength(file);
                            File.SetAttributes(file, FileAttributes.Normal);
                            File.Delete(file);
                            freed += len;
                            filesDeleted++;
                        }
                        catch (Exception ex) { errors.Add($"{file}: {ex.Message}"); }
                    }
                    foreach (var dir in EnumerateDirectoriesDepthFirst(path, ct))
                    {
                        try { Directory.Delete(dir, recursive: false); } catch { }
                    }
                }
                catch (Exception ex) { errors.Add($"{path}: {ex.Message}"); }
            }
        }

        return new CleanupResult { BytesFreed = freed, FilesDeleted = filesDeleted, Errors = errors };
    }

    // ---------- category builders ----------

    private static CleanupCategory Build(string name, string description, IEnumerable<string> paths, CancellationToken ct)
    {
        var existing = paths.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        long total = 0; var files = 0;
        foreach (var p in existing)
        {
            total += SafeDirSize(p, ct);
            files += SafeFileCount(p, ct);
        }
        return new CleanupCategory
        {
            Name = name, Description = description, Paths = existing,
            TotalSizeBytes = total, FileCount = files,
            IsSelected = total > 0
        };
    }

    private static CleanupCategory BuildOldFiles(string name, string description, string dir, TimeSpan olderThan, CancellationToken ct)
    {
        long total = 0; var files = 0;
        if (Directory.Exists(dir))
        {
            var cutoff = DateTime.UtcNow - olderThan;
            foreach (var file in EnumerateFiles(dir, ct))
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var fi = new FileInfo(file);
                    if (fi.LastWriteTimeUtc < cutoff) { total += fi.Length; files++; }
                }
                catch { }
            }
        }
        return new CleanupCategory
        {
            Name = name, Description = description,
            Paths = Directory.Exists(dir) ? new[] { dir } : Array.Empty<string>(),
            TotalSizeBytes = total, FileCount = files,
            IsSelected = total > 0, OlderThan = olderThan
        };
    }

    private static CleanupCategory BuildRecycleBin(CancellationToken ct)
    {
        long total = 0; var files = 0;
        var paths = new List<string>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
            var bin = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
            if (!Directory.Exists(bin)) continue;
            paths.Add(bin);
            total += SafeDirSize(bin, ct);
            files += SafeFileCount(bin, ct);
        }
        return new CleanupCategory
        {
            Name = "Recycle Bin (all drives)",
            Description = "Emptying the recycle bin on every fixed drive.",
            Paths = paths, TotalSizeBytes = total, FileCount = files,
            IsSelected = total > 0
        };
    }

    // ---------- IO helpers ----------

    private static long SafeDirSize(string dir, CancellationToken ct)
    {
        long total = 0;
        try { foreach (var f in EnumerateFiles(dir, ct)) { if (ct.IsCancellationRequested) return total; total += SafeLength(f); } } catch { }
        return total;
    }

    private static int SafeFileCount(string dir, CancellationToken ct)
    {
        var n = 0;
        try { foreach (var _ in EnumerateFiles(dir, ct)) { if (ct.IsCancellationRequested) return n; n++; } } catch { }
        return n;
    }

    private static long SafeLength(string path)
    { try { return new FileInfo(path).Length; } catch { return 0; } }

    private static IEnumerable<string> EnumerateFiles(string root, CancellationToken ct)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0 && !ct.IsCancellationRequested)
        {
            var cur = stack.Pop();
            string[] files = Array.Empty<string>();
            string[] dirs = Array.Empty<string>();
            try { files = Directory.GetFiles(cur); } catch { }
            try { dirs = Directory.GetDirectories(cur); } catch { }
            foreach (var f in files) yield return f;
            foreach (var d in dirs) stack.Push(d);
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesDepthFirst(string root, CancellationToken ct)
    {
        var all = new List<string>();
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0 && !ct.IsCancellationRequested)
        {
            var cur = stack.Pop();
            string[] dirs = Array.Empty<string>();
            try { dirs = Directory.GetDirectories(cur); } catch { }
            foreach (var d in dirs) stack.Push(d);
            if (!string.Equals(cur, root, StringComparison.OrdinalIgnoreCase)) all.Add(cur);
        }
        all.Sort((a, b) => b.Length.CompareTo(a.Length));
        return all;
    }
}
