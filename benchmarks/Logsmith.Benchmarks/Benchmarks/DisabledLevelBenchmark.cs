using BenchmarkDotNet.Attributes;
using Logsmith.Benchmarks.LogDefinitions;
using Serilog.Events;
using ZLogger;
using LogLevel = Logsmith.LogLevel;
using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;
using NLogLevel = NLog.LogLevel;

namespace Logsmith.Benchmarks.Benchmarks;

/// <summary>
/// Measures the cost of logging a Trace message when minimum level is Warning.
/// This tests how efficiently each library's guard check short-circuits.
/// </summary>
[MemoryDiagnoser]
public class DisabledLevelBenchmark : BenchmarkBase
{
    protected override MelLogLevel GetMelMinLevel() => MelLogLevel.Warning;
    protected override NLogLevel GetNLogMinLevel() => NLogLevel.Warn;
    protected override LogEventLevel GetSerilogMinLevel() => LogEventLevel.Warning;
    protected override LogLevel GetLogsmithMinLevel() => LogLevel.Warning;

    [Benchmark(Baseline = true)]
    public void Logsmith()
    {
        LogsmithLog.DisabledMessage();
    }

    [Benchmark]
    public void MEL()
    {
        MelLog.DisabledMessage(MelLogger);
    }

    [Benchmark]
    public void Serilog()
    {
        SerilogLogger.Verbose("Trace-level diagnostic message");
    }

    [Benchmark]
    public void NLog()
    {
        NLogLogger.Trace("Trace-level diagnostic message");
    }

    [Benchmark]
    public void ZLogger()
    {
        ZLoggerLogger.ZLogTrace($"Trace-level diagnostic message");
    }
}
