using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using OutWit.Database.Core.Tests.Benchmarks;

// Custom config for cleaner output
var config = ManualConfig.Create(DefaultConfig.Instance)
    .WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(BenchmarkDotNet.Columns.RatioStyle.Trend))
    .AddExporter(MarkdownExporter.GitHub)
    .WithOption(ConfigOptions.DisableOptimizationsValidator, true);

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
