using BenchmarkDotNet.Running;

namespace OutWit.Database.EntityFramework.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
