using BenchmarkDotNet.Attributes;
using Logsmith.Benchmarks.LogDefinitions;
using Logsmith.Benchmarks.Sinks;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using ZLogger;

namespace Logsmith.Benchmarks.Benchmarks;

/// <summary>
/// Measures the cost of logging a templated message when one ambient scope property
/// is active. Each library's native scope mechanism is used so the benchmark reflects
/// real-world overhead of scoped context during log dispatch.
/// </summary>
[MemoryDiagnoser]
public class ScopedContextBenchmark : BenchmarkBase
{
    private const string UserName = "alice";
    private const int ActionId = 42;

    private IDisposable? _logsmithScope;
    private IDisposable? _melScope;
    private IDisposable? _serilogScope;
    private IDisposable? _nlogScope;
    private IDisposable? _zloggerScope;

    public override void Setup()
    {
        base.Setup();

        // Re-create Serilog with FromLogContext enricher so PushProperty is observed.
        (SerilogLogger as IDisposable)?.Dispose();
        SerilogLogger = new LoggerConfiguration()
            .MinimumLevel.Is(GetSerilogMinLevel())
            .Enrich.FromLogContext()
            .WriteTo.Sink(new DevNullSerilogSink())
            .CreateLogger();

        // Push one scope property per library.
        _logsmithScope = LogScope.Push("RequestId", "bench-001");
        _melScope = MelLogger.BeginScope(new KeyValuePair<string, object>("RequestId", "bench-001"));
        _serilogScope = LogContext.PushProperty("RequestId", "bench-001");
        _nlogScope = global::NLog.ScopeContext.PushProperty("RequestId", "bench-001");
        _zloggerScope = ZLoggerLogger.BeginScope(new KeyValuePair<string, object>("RequestId", "bench-001"));
    }

    public override void Cleanup()
    {
        _zloggerScope?.Dispose();
        _nlogScope?.Dispose();
        _serilogScope?.Dispose();
        _melScope?.Dispose();
        _logsmithScope?.Dispose();
        base.Cleanup();
    }

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
