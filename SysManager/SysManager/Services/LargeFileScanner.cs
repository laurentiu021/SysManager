// SysManager · LargeFileScanner — read-only biggest-files discovery
// Author: laurentiu021 · https://github.com/laurentiu021/SysManager
// License: MIT

using System.IO;
using SysManager.Models;

namespace SysManager.Services;

/// <summary>
/// Finds the biggest files on a drive or folder. Read-only — this scanner
/// never modifies or deletes anything. The UI only offers "Show in Explorer"
/// and "Copy path" on the results.
/// </summary>
public sealed class LargeFileScanner
{
    // Skip well-known system subtrees where poking around is slow and pointless.
    private static readonly string[] SkipSegments =
    {
        @"\$recycle.bin", @"\system volume information", @"\windows\winsxs",
        @"\windows\system32\config", @"\windows\csc", @"\pagefile.sys",
        @"\hiberfil.sys", @"\swapfile.sys"
    };

    public Task<IReadOnlyList<LargeFileEntry>> ScanAsync(
        string rootPath,
        long minSizeBytes,
        int top = 100,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
        => Task.Run(() => Scan(rootPath, minSizeBytes, top, progress, ct), ct);

    private static IReadOnlyList<LargeFileEntry> Scan(
        string rootPath,
        long minSizeBytes,
        int top,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            return Array.Empty<LargeFileEntry>();

        // Min-heap by size — only keep the top N largest seen so far.
        // Using a sorted list keyed by size + path for uniqueness.
        var heap = new SortedSet<(long Size, string Path)>(Comparer<(long, string)>.Create(
            (a, b) =>
            {
                var c = a.Item1.CompareTo(b.Item1);
                return c != 0 ? c : string.CompareOrdinal(a.Item2, b.Item2);
            }));
        var meta = new Dictionary<string, LargeFileEntry>(StringComparer.OrdinalIgnoreCase);

        var stack = new Stack<string>();
        stack.Push(rootPath);
        var scanned = 0;

        while (stack.Count > 0 && !ct.IsCancellationRequested)
        {
            var cur = stack.Pop();
            if (ShouldSkip(cur)) continue;

            string[] files = Array.Empty<string>();
            string[] dirs = Array.Empty<string>();
            try { files = Directory.GetFiles(cur); } catch { }
            try { dirs  = Directory.GetDirectories(cur); } catch { }

            foreach (var f in files)
            {
                if (ct.IsCancellationRequested) break;
                scanned++;
                if ((scanned & 0x3FF) == 0) progress?.Report($"Scanned {scanned:N0} files...");

                try
                {
                    var fi = new FileInfo(f);
                    if (fi.Length < minSizeBytes) continue;

                    if (heap.Count < top)
                    {
                        heap.Add((fi.Length, f));
                        meta[f] = new LargeFileEntry
                        {
                            Path = f,
                            Name = fi.Name,
                            SizeBytes = fi.Length,
                            LastModified = fi.LastWriteTime
                        };
                    }
                    else
                    {
                        var smallest = heap.Min;
                        if (fi.Length > smallest.Size)
                        {
                            heap.Remove(smallest);
                            meta.Remove(smallest.Path);
                            heap.Add((fi.Length, f));
                            meta[f] = new LargeFileEntry
                            {
                                Path = f,
                                Name = fi.Name,
                                SizeBytes = fi.Length,
                                LastModified = fi.LastWriteTime
                            };
                        }
                    }
                }
                catch { /* unreachable / locked — skip */ }
            }

            foreach (var d in dirs) stack.Push(d);
        }

        // Return top → bottom.
        return heap.Reverse().Select(h => meta[h.Path]).ToList();
    }

    private static bool ShouldSkip(string path)
    {
        var lower = path.ToLowerInvariant();
        foreach (var seg in SkipSegments)
            if (lower.Contains(seg)) return true;
        return false;
    }
}
