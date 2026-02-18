using BenchmarkDotNet.Attributes;
using Logsmith.Benchmarks.LogDefinitions;
using ZLogger;

namespace Logsmith.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class TemplatedMessageBenchmark : BenchmarkBase
{
    private const string UserName = "alice";
    private const int ActionId = 42;

    [Benchmark(Baseline = true)]
    public void Logsmith()
    {
        LogsmithLog.TemplatedMessage(UserName, ActionId);
    }

    [Benchmark]
    public void MEL()
    {
        MelLog.TemplatedMessage(MelLogger, UserName, ActionId);
    }

    [Benchmark]
    public void Serilog()
    {
        SerilogLogger.Information("User {userName} performed action {actionId}", UserName, ActionId);
    }

    [Benchmark]
    public void NLog()
    {
        NLogLogger.Info("User {userName} performed action {actionId}", UserName, ActionId);
    }

    [Benchmark]
    public void ZLogger()
    {
        ZLoggerLogger.ZLogInformation($"User {UserName} performed action {ActionId}");
    }
}
