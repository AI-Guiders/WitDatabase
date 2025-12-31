using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;

namespace OutWit.Database.AdoNet.Benchmarks;

/// <summary>
/// Base configuration for ADO.NET benchmarks.
/// </summary>
public class AdoNetBenchmarkConfig : ManualConfig
{
    public AdoNetBenchmarkConfig()
    {
        SummaryStyle = SummaryStyle.Default
            .WithRatioStyle(RatioStyle.Trend)
            .WithTimeUnit(Perfolizer.Horology.TimeUnit.Microsecond);
        HideColumns(Column.Error, Column.StdDev, Column.RatioSD);
    }
}

/// <summary>
/// Database provider type for benchmarking.
/// WitDb and LiteDB are both pure managed .NET implementations.
/// SQLite is a native C library with .NET bindings.
/// </summary>
public enum DbProviderType
{
    /// <summary>Pure managed .NET embedded database</summary>
    WitDb,
    
    /// <summary>Native C library with .NET bindings (baseline for speed)</summary>
    SQLite,
    
    /// <summary>Pure managed .NET NoSQL database (baseline for managed memory)</summary>
    LiteDB
}

/// <summary>
/// Helper class for generating unique benchmark paths.
/// </summary>
public static class BenchmarkPathHelper
{
    /// <summary>
    /// Generates a unique path for benchmark database files.
    /// Uses a new GUID each time to guarantee uniqueness.
    /// </summary>
    public static string GenerateUniquePath(string prefix, string extension = "")
    {
        var filename = $"{prefix}_{Guid.NewGuid():N}{extension}";
        return Path.Combine(Path.GetTempPath(), filename);
    }

    /// <summary>
    /// Safely cleans up a path (file or directory) with retries.
    /// </summary>
    public static void SafeCleanup(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return;
            
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
                else if (File.Exists(path))
                    File.Delete(path);
                return; // Success
            }
            catch
            {
                if (attempt < 4)
                    Thread.Sleep(50 * (attempt + 1)); // Increasing delay
            }
        }
    }
}
