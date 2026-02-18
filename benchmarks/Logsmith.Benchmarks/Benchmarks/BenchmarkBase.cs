using BenchmarkDotNet.Attributes;
using Logsmith.Benchmarks.Sinks;
using Microsoft.Extensions.Logging;
using NLog.Config;
using Serilog;
using ZLogger;
using LogLevel = Logsmith.LogLevel;
using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;
using NLogLevel = NLog.LogLevel;

namespace Logsmith.Benchmarks.Benchmarks;

public abstract class BenchmarkBase
{
    // MEL
    protected Microsoft.Extensions.Logging.ILogger MelLogger = null!;
    private ILoggerFactory _melFactory = null!;

    // Serilog
    protected Serilog.ILogger SerilogLogger = null!;

    // NLog
    protected NLog.Logger NLogLogger = null!;

    // ZLogger (backed by MEL)
    protected Microsoft.Extensions.Logging.ILogger ZLoggerLogger = null!;
    private ILoggerFactory _zloggerFactory = null!;

    protected virtual MelLogLevel GetMelMinLevel() => MelLogLevel.Trace;
    protected virtual NLogLevel GetNLogMinLevel() => NLogLevel.Trace;
    protected virtual Serilog.Events.LogEventLevel GetSerilogMinLevel() => Serilog.Events.LogEventLevel.Verbose;
    protected virtual LogLevel GetLogsmithMinLevel() => LogLevel.Trace;

    [GlobalSetup]
    public virtual void Setup()
    {
        // --- Logsmith ---
        Logsmith.LogManager.Reset();
        Logsmith.LogManager.Initialize(c =>
        {
            c.MinimumLevel = GetLogsmithMinLevel();
            c.AddSink(new DevNullSink());
        });

        // --- MEL ---
        _melFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(GetMelMinLevel());
            builder.AddProvider(new DevNullMelLoggerProvider());
        });
        MelLogger = _melFactory.CreateLogger("Benchmark");

        // --- Serilog ---
        SerilogLogger = new Serilog.LoggerConfiguration()
            .MinimumLevel.Is(GetSerilogMinLevel())
            .WriteTo.Sink(new DevNullSerilogSink())
            .CreateLogger();

        // --- NLog ---
        var nlogConfig = new LoggingConfiguration();
        var devNullTarget = new DevNullNLogTarget { Name = "devnull" };
        nlogConfig.AddTarget(devNullTarget);
        nlogConfig.AddRule(GetNLogMinLevel(), NLogLevel.Fatal, devNullTarget);
        NLog.LogManager.Configuration = nlogConfig;
        NLogLogger = NLog.LogManager.GetLogger("Benchmark");

        // --- ZLogger ---
        _zloggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(GetMelMinLevel());
            builder.AddZLoggerStream(Stream.Null);
        });
        ZLoggerLogger = _zloggerFactory.CreateLogger("Benchmark");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Logsmith.LogManager.Reset();
        _melFactory?.Dispose();
        (SerilogLogger as IDisposable)?.Dispose();
        NLog.LogManager.Shutdown();
        _zloggerFactory?.Dispose();
    }
}
