using BenchmarkDotNet.Running;

namespace OutWit.Database.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        // Run all benchmarks or use command line filters
        var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

        // Or run specific benchmarks:
        // BenchmarkRunner.Run<QueryBenchmarks>();
        // BenchmarkRunner.Run<InsertBenchmarks>();
        // BenchmarkRunner.Run<UpdateBenchmarks>();
        // BenchmarkRunner.Run<JoinBenchmarks>();
        // BenchmarkRunner.Run<AggregateBenchmarks>();
        // BenchmarkRunner.Run<IndexBenchmarks>();
        // BenchmarkRunner.Run<TransactionBenchmarks>();
    }
}
