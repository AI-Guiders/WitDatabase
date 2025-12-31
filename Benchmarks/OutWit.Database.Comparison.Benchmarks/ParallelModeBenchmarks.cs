using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using OutWit.Database.AdoNet;

namespace OutWit.Database.Comparison.Benchmarks;

/// <summary>
/// Benchmarks demonstrating that parallel mode provides thread SAFETY, not speedup.
/// 
/// Key findings to expect:
/// - Single-threaded: ParallelMode.None is fastest (no lock overhead)
/// - Multi-threaded: ParallelMode is REQUIRED for correctness, not speed
/// - Write throughput is limited by I/O, not parallelism
/// </summary>
[Config(typeof(ParallelBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class ParallelModeBenchmarks : IDisposable
{
    private string _singlePath = null!;
    private string _parallelPath = null!;

    [Params(1000, 5000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _singlePath = Path.Combine(Path.GetTempPath(), $"lsm_single_{Guid.NewGuid():N}");
        _parallelPath = Path.Combine(Path.GetTempPath(), $"lsm_parallel_{Guid.NewGuid():N}");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Clean directories
        CleanDirectory(_singlePath);
        CleanDirectory(_parallelPath);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        CleanDirectory(_singlePath);
        CleanDirectory(_parallelPath);
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    private static void CleanDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // Ignore errors during cleanup
        }
    }

    /// <summary>
    /// Baseline: Single-threaded writes without parallel mode.
    /// This should be the fastest for single-threaded access.
    /// </summary>
    [Benchmark(Description = "LSM Single-Thread (No Parallel)", Baseline = true)]
    public int SingleThreadNoParallel()
    {
        using var conn = new WitDbConnection($"Data Source={_singlePath};Store=lsm;Transactions=false;Parallel Mode=None");
        conn.Open();
        
        using (var c = conn.CreateCommand())
        {
            c.CommandText = "CREATE TABLE T (Id INT PRIMARY KEY, N VARCHAR(100), V DOUBLE)";
            c.ExecuteNonQuery();
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO T (Id, N, V) VALUES (@i, @n, @v)";
        var pi = cmd.CreateParameter(); pi.ParameterName = "@i"; cmd.Parameters.Add(pi);
        var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; cmd.Parameters.Add(pn);
        var pv = cmd.CreateParameter(); pv.ParameterName = "@v"; cmd.Parameters.Add(pv);

        for (int i = 0; i < RowCount; i++)
        {
            pi.Value = i;
            pn.Value = $"N{i}";
            pv.Value = i * 1.5;
            cmd.ExecuteNonQuery();
        }

        return RowCount;
    }

    /// <summary>
    /// Single-threaded writes WITH parallel mode.
    /// Should be slightly slower than baseline due to buffering overhead.
    /// </summary>
    [Benchmark(Description = "LSM Single-Thread (Parallel Mode)")]
    public int SingleThreadWithParallel()
    {
        using var conn = new WitDbConnection($"Data Source={_parallelPath};Store=lsm;Transactions=false;Parallel Mode=Buffered");
        conn.Open();
        
        using (var c = conn.CreateCommand())
        {
            c.CommandText = "CREATE TABLE T (Id INT PRIMARY KEY, N VARCHAR(100), V DOUBLE)";
            c.ExecuteNonQuery();
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO T (Id, N, V) VALUES (@i, @n, @v)";
        var pi = cmd.CreateParameter(); pi.ParameterName = "@i"; cmd.Parameters.Add(pi);
        var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; cmd.Parameters.Add(pn);
        var pv = cmd.CreateParameter(); pv.ParameterName = "@v"; cmd.Parameters.Add(pv);

        for (int i = 0; i < RowCount; i++)
        {
            pi.Value = i;
            pn.Value = $"N{i}";
            pv.Value = i * 1.5;
            cmd.ExecuteNonQuery();
        }

        return RowCount;
    }

    public void Dispose() => IterationCleanup();
}

public class ParallelBenchmarkConfig : ManualConfig
{
    public ParallelBenchmarkConfig()
    {
        SummaryStyle = SummaryStyle.Default
            .WithRatioStyle(RatioStyle.Trend)
            .WithTimeUnit(Perfolizer.Horology.TimeUnit.Millisecond);
        HideColumns(Column.Error, Column.StdDev, Column.RatioSD);
    }
}

/// <summary>
/// Benchmarks for BTree with parallel mode.
/// </summary>
[Config(typeof(ParallelBenchmarkConfig))]
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class BTreeParallelModeBenchmarks : IDisposable
{
    private string _singlePath = null!;
    private string _parallelPath = null!;

    [Params(1000, 5000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _singlePath = Path.Combine(Path.GetTempPath(), $"btree_single_{Guid.NewGuid():N}.witdb");
        _parallelPath = Path.Combine(Path.GetTempPath(), $"btree_parallel_{Guid.NewGuid():N}.witdb");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Clean files with retry
        DeleteFileWithRetry(_singlePath);
        DeleteFileWithRetry(_parallelPath);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        DeleteFileWithRetry(_singlePath);
        DeleteFileWithRetry(_parallelPath);
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    private static void DeleteFileWithRetry(string path, int maxRetries = 5)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                return;
            }
            catch
            {
                if (i < maxRetries - 1)
                {
                    Thread.Sleep(100);
                }
            }
        }
    }

    /// <summary>
    /// Baseline: Single-threaded writes without parallel mode.
    /// </summary>
    [Benchmark(Description = "BTree Single-Thread (No Parallel)", Baseline = true)]
    public int SingleThreadNoParallel()
    {
        using var conn = new WitDbConnection($"Data Source={_singlePath};Store=btree;Transactions=false;Parallel Mode=None");
        conn.Open();
        
        using (var c = conn.CreateCommand())
        {
            c.CommandText = "CREATE TABLE T (Id INT PRIMARY KEY, N VARCHAR(100), V DOUBLE)";
            c.ExecuteNonQuery();
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO T (Id, N, V) VALUES (@i, @n, @v)";
        var pi = cmd.CreateParameter(); pi.ParameterName = "@i"; cmd.Parameters.Add(pi);
        var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; cmd.Parameters.Add(pn);
        var pv = cmd.CreateParameter(); pv.ParameterName = "@v"; cmd.Parameters.Add(pv);

        for (int i = 0; i < RowCount; i++)
        {
            pi.Value = i;
            pn.Value = $"N{i}";
            pv.Value = i * 1.5;
            cmd.ExecuteNonQuery();
        }

        return RowCount;
    }

    /// <summary>
    /// Single-threaded writes WITH parallel mode.
    /// Should have slight overhead due to RW lock.
    /// </summary>
    [Benchmark(Description = "BTree Single-Thread (Parallel Mode)")]
    public int SingleThreadWithParallel()
    {
        using var conn = new WitDbConnection($"Data Source={_parallelPath};Store=btree;Transactions=false;Parallel Mode=Latched");
        conn.Open();
        
        using (var c = conn.CreateCommand())
        {
            c.CommandText = "CREATE TABLE T (Id INT PRIMARY KEY, N VARCHAR(100), V DOUBLE)";
            c.ExecuteNonQuery();
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO T (Id, N, V) VALUES (@i, @n, @v)";
        var pi = cmd.CreateParameter(); pi.ParameterName = "@i"; cmd.Parameters.Add(pi);
        var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; cmd.Parameters.Add(pn);
        var pv = cmd.CreateParameter(); pv.ParameterName = "@v"; cmd.Parameters.Add(pv);

        for (int i = 0; i < RowCount; i++)
        {
            pi.Value = i;
            pn.Value = $"N{i}";
            pv.Value = i * 1.5;
            cmd.ExecuteNonQuery();
        }

        return RowCount;
    }

    public void Dispose() => IterationCleanup();
}

/// <summary>
/// Correctness test: Multi-threaded access with parallel mode.
/// This benchmark demonstrates that parallel mode allows safe concurrent access.
/// </summary>
[Config(typeof(ParallelBenchmarkConfig))]
[MemoryDiagnoser]
public class ParallelModeCorrectnessComparisonBenchmarks : IDisposable
{
    private string _path = null!;

    [Params(4)]
    public int ThreadCount { get; set; }

    [Params(1000)]
    public int RowsPerThread { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"parallel_correctness_{Guid.NewGuid():N}");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        if (Directory.Exists(_path))
        {
            try { Directory.Delete(_path, true); } catch { }
        }
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        try { Directory.Delete(_path, true); } catch { }
    }

    [GlobalCleanup]
    public void GlobalCleanup() => IterationCleanup();

    /// <summary>
    /// Multi-threaded writes WITH parallel mode (safe).
    /// All writes should complete without exceptions or data corruption.
    /// </summary>
    [Benchmark(Description = "Multi-Thread Safe (Parallel Mode)")]
    public int MultiThreadWithParallel()
    {
        using var conn = new WitDbConnection($"Data Source={_path};Store=lsm;Transactions=false;Parallel Mode=Buffered");
        conn.Open();
        
        using (var c = conn.CreateCommand())
        {
            c.CommandText = "CREATE TABLE T (Id INT PRIMARY KEY, ThreadId INT, N VARCHAR(100))";
            c.ExecuteNonQuery();
        }

        var nextId = 0;
        var errors = 0;

        var tasks = Enumerable.Range(0, ThreadCount).Select(threadId => Task.Run(() =>
        {
            try
            {
                // Each thread uses its own command but same connection
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO T (Id, ThreadId, N) VALUES (@i, @t, @n)";
                var pi = cmd.CreateParameter(); pi.ParameterName = "@i"; cmd.Parameters.Add(pi);
                var pt = cmd.CreateParameter(); pt.ParameterName = "@t"; cmd.Parameters.Add(pt);
                var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; cmd.Parameters.Add(pn);

                for (int i = 0; i < RowsPerThread; i++)
                {
                    var id = Interlocked.Increment(ref nextId);
                    pi.Value = id;
                    pt.Value = threadId;
                    pn.Value = $"T{threadId}_Row{i}";
                    cmd.ExecuteNonQuery();
                }
            }
            catch
            {
                Interlocked.Increment(ref errors);
            }
        })).ToArray();

        Task.WaitAll(tasks);

        // Verify all rows written
        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM T";
        var count = Convert.ToInt32(countCmd.ExecuteScalar());

        return errors == 0 ? count : -errors;
    }

    public void Dispose() => IterationCleanup();
}
