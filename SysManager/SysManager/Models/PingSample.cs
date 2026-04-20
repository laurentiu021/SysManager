namespace SysManager.Models;

/// <summary>
/// A single ping result for one target at one point in time.
/// LatencyMs is null on timeout/error so the chart can render a gap.
/// </summary>
public record PingSample(DateTime Timestamp, string Host, double? LatencyMs, string Status);
