using BenchmarkDotNet.Attributes;
using Logsmith.Benchmarks.LogDefinitions;
using ZLogger;

namespace Logsmith.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class SimpleMessageBenchmark : BenchmarkBase
{
    [Benchmark(Baseline = true)]
    public void Logsmith()
    {
        LogsmithLog.SimpleMessage();
    }

    [Benchmark]
    public void MEL()
    {
        MelLog.SimpleMessage(MelLogger);
    }

    [Benchmark]
    public void Serilog()
    {
        SerilogLogger.Information("Simple log message");
    }

    [Benchmark]
    public void NLog()
    {
        NLogLogger.Info("Simple log message");
    }

    [Benchmark]
    public void ZLogger()
    {
        ZLoggerLogger.ZLogInformation($"Simple log message");
    }
}
