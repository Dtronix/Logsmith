using Logsmith.Internal;

namespace Logsmith;

public static class LogManager
{
    private static volatile LogConfig? _config;
    private static int _initialized;
    private static int _exceptionsCaptured;
    private static int _processExitRegistered;
    private static int _shutdownCompleted;

    private static readonly TimeSpan ProcessExitTimeout = TimeSpan.FromSeconds(5);

    public static void Initialize(Action<LogConfigBuilder> configure)
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
            throw new InvalidOperationException("LogManager has already been initialized. Use Reconfigure to change configuration.");

        var builder = new LogConfigBuilder();
        configure(builder);
        _config = builder.Build();

        StartCapturingExceptions(_config);

        Interlocked.Exchange(ref _shutdownCompleted, 0);
        RegisterProcessExitHook();
    }

    public static void Reconfigure(Action<LogConfigBuilder> configure)
    {
        StopCapturingExceptions();

        var old = _config;
        var builder = new LogConfigBuilder();
        configure(builder);
        _config = builder.Build();

        StartCapturingExceptions(_config);

        if (old is not null)
            Task.Run(async () => await old.DisposeAllAsync());
    }

    public static async ValueTask ReconfigureAsync(Action<LogConfigBuilder> configure)
    {
        StopCapturingExceptions();

        var old = _config;
        var builder = new LogConfigBuilder();
        configure(builder);
        _config = builder.Build();

        StartCapturingExceptions(_config);

        if (old is not null)
            await old.DisposeAllAsync().ConfigureAwait(false);
    }

    public static async ValueTask ShutdownAsync(TimeSpan? timeout = null)
    {
        if (Interlocked.CompareExchange(ref _shutdownCompleted, 1, 0) != 0)
            return;

        StopCapturingExceptions();

        var old = Interlocked.Exchange(ref _config, null);
        Interlocked.Exchange(ref _initialized, 0);

        if (old is null)
            return;

        if (timeout is null)
        {
            await old.DisposeAllAsync().ConfigureAwait(false);
        }
        else
        {
            using var cts = new CancellationTokenSource(timeout.Value);
            try
            {
                await old.DisposeAllAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Disposal timed out — best effort, move on
            }
        }
    }

    public static void Shutdown(TimeSpan? timeout = null)
    {
        ShutdownAsync(timeout).AsTask().GetAwaiter().GetResult();
    }

    public static async ValueTask FlushAsync(TimeSpan? timeout = null)
    {
        var config = _config;
        if (config is null) return;

        CancellationTokenSource? cts = timeout.HasValue
            ? new CancellationTokenSource(timeout.Value)
            : null;

        try
        {
            var token = cts?.Token ?? CancellationToken.None;
            var tasks = new List<Task>();

            foreach (var sink in config.Sinks.AllSinks)
            {
                if (sink is IFlushableLogSink flushable)
                    tasks.Add(flushable.FlushAsync(token).AsTask());
            }

            if (tasks.Count > 0)
                await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        finally
        {
            cts?.Dispose();
        }
    }

    public static bool IsEnabled(LogLevel level)
    {
        var config = _config;
        if (config is null) return false;
        return level >= config.MinimumLevel;
    }

    public static bool IsEnabled(LogLevel level, string category)
    {
        var config = _config;
        if (config is null) return false;

        if (config.CategoryOverrides.TryGetValue(category, out var categoryLevel))
            return level >= categoryLevel;

        return level >= config.MinimumLevel;
    }

    public static void Dispatch<TState>(
        in LogEntry entry,
        ReadOnlySpan<byte> utf8Message,
        TState state,
        WriteProperties<TState> propertyWriter)
        where TState : allows ref struct
    {
        var config = _config;
        if (config is null) return;

        var sinkSet = config.Sinks;

        var errorHandler = config.ErrorHandler;

        // Build scoped message only when scopes are active
        if (LogScope.Current is not null)
        {
            Span<byte> scopeBuffer = stackalloc byte[512];
            var scopeWriter = new Utf8LogWriter(scopeBuffer);
            scopeWriter.Write(utf8Message);
            LogScope.WriteScopeToUtf8(ref scopeWriter);
            DispatchToSinks(in entry, scopeWriter.GetWritten(), state, propertyWriter, sinkSet, errorHandler);
        }
        else
        {
            DispatchToSinks(in entry, utf8Message, state, propertyWriter, sinkSet, errorHandler);
        }
    }

    private static void DispatchToSinks<TState>(
        in LogEntry entry,
        ReadOnlySpan<byte> utf8Message,
        TState state,
        WriteProperties<TState> propertyWriter,
        Internal.SinkSet sinkSet,
        Action<Exception>? errorHandler)
        where TState : allows ref struct
    {
        var textSinks = sinkSet.TextSinks;
        for (int i = 0; i < textSinks.Length; i++)
        {
            if (textSinks[i].IsEnabled(entry.Level))
            {
                try
                {
                    textSinks[i].Write(in entry, utf8Message);
                }
                catch (Exception ex)
                {
                    errorHandler?.Invoke(ex);
                }
            }
        }

        var structuredSinks = sinkSet.StructuredSinks;
        for (int i = 0; i < structuredSinks.Length; i++)
        {
            if (structuredSinks[i].IsEnabled(entry.Level))
            {
                try
                {
                    structuredSinks[i].WriteStructured(in entry, state, propertyWriter);
                }
                catch (Exception ex)
                {
                    errorHandler?.Invoke(ex);
                }
            }
        }
    }

    private static void StartCapturingExceptions(LogConfig config)
    {
        if (!config.CaptureUnhandledExceptions) return;
        if (Interlocked.CompareExchange(ref _exceptionsCaptured, 1, 0) != 0) return;

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        if (config.ObserveTaskExceptions)
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskExceptionObserve;
        else
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void StopCapturingExceptions()
    {
        if (Interlocked.CompareExchange(ref _exceptionsCaptured, 0, 1) != 1)
            return;

        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskExceptionObserve;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var handler = _config?.ErrorHandler;
        if (handler is null) return;

        if (e.ExceptionObject is Exception ex)
        {
            try { handler(ex); } catch { }
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var handler = _config?.ErrorHandler;
        if (handler is null) return;

        try { handler(e.Exception); } catch { }
    }

    private static void OnUnobservedTaskExceptionObserve(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var handler = _config?.ErrorHandler;
        if (handler is null) return;

        try { handler(e.Exception); } catch { }
        e.SetObserved();
    }

    private static void RegisterProcessExitHook()
    {
        if (Interlocked.CompareExchange(ref _processExitRegistered, 1, 0) != 0)
            return;

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        Shutdown(ProcessExitTimeout);
    }

    internal static void SetMinimumLevel(LogLevel level)
    {
        var current = _config;
        if (current is null) return;

        var newConfig = new LogConfig(level, current.CategoryOverrides.ToDictionary(), current.Sinks, current.ErrorHandler, current.Monitors,
            current.CaptureUnhandledExceptions, current.ObserveTaskExceptions);
        _config = newConfig;
    }

    internal static void SetCategoryOverrides(Dictionary<string, LogLevel> overrides)
    {
        var current = _config;
        if (current is null) return;

        var newConfig = new LogConfig(current.MinimumLevel, overrides, current.Sinks, current.ErrorHandler, current.Monitors,
            current.CaptureUnhandledExceptions, current.ObserveTaskExceptions);
        _config = newConfig;
    }

    // For testing: reset state so Initialize can be called again.
    internal static void Reset()
    {
        StopCapturingExceptions();
        var old = _config;
        _config = null;
        Interlocked.Exchange(ref _initialized, 0);
        Interlocked.Exchange(ref _shutdownCompleted, 0);

        if (old is not null)
            old.DisposeAllAsync().AsTask().GetAwaiter().GetResult();
    }
}
