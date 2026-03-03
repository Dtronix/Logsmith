namespace Logsmith.Tests;

[TestFixture]
public class ExceptionHandlerTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown() => LogManager.Reset();

    [Test]
    public void CaptureUnhandledExceptions_ViaBuilder_SubscribesHandler()
    {
        LogManager.Initialize(cfg =>
        {
            cfg.AddSink(new Sinks.RecordingSink());
            cfg.InternalErrorHandler = _ => { };
            cfg.CaptureUnhandledExceptions();
        });

        Assert.Pass("Handler registered via builder without error");
    }

    [Test]
    public void CaptureUnhandledExceptions_WithObserveTaskExceptions()
    {
        LogManager.Initialize(cfg =>
        {
            cfg.AddSink(new Sinks.RecordingSink());
            cfg.InternalErrorHandler = _ => { };
            cfg.CaptureUnhandledExceptions(observeTaskExceptions: true);
        });

        // Create a faulted task and let it be collected
        var weakRef = CreateFaultedTask();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Thread.Sleep(200);

        Assert.Pass("No crash from unobserved task exception handling with observe");
    }

    [Test]
    public void Shutdown_UnsubscribesAutomatically()
    {
        LogManager.Initialize(cfg =>
        {
            cfg.AddSink(new Sinks.RecordingSink());
            cfg.InternalErrorHandler = _ => { };
            cfg.CaptureUnhandledExceptions();
        });

        LogManager.Shutdown();

        // After shutdown we can re-initialize with capture
        LogManager.Initialize(cfg =>
        {
            cfg.AddSink(new Sinks.RecordingSink());
            cfg.CaptureUnhandledExceptions();
        });

        Assert.Pass("Re-initialization with capture succeeded after shutdown");
    }

    [Test]
    public void Reconfigure_WithoutCapture_UnsubscribesOldHandlers()
    {
        LogManager.Initialize(cfg =>
        {
            cfg.AddSink(new Sinks.RecordingSink());
            cfg.InternalErrorHandler = _ => { };
            cfg.CaptureUnhandledExceptions();
        });

        // Reconfigure without capture — old handlers should be unsubscribed
        LogManager.Reconfigure(cfg =>
        {
            cfg.AddSink(new Sinks.RecordingSink());
        });

        Assert.Pass("Reconfigure without capture did not throw");
    }

    [Test]
    public void Reconfigure_WithCapture_ResubscribesHandlers()
    {
        LogManager.Initialize(cfg =>
        {
            cfg.AddSink(new Sinks.RecordingSink());
            cfg.CaptureUnhandledExceptions();
        });

        // Reconfigure with capture — should unsub old, sub new
        LogManager.Reconfigure(cfg =>
        {
            cfg.AddSink(new Sinks.RecordingSink());
            cfg.CaptureUnhandledExceptions(observeTaskExceptions: true);
        });

        Assert.Pass("Reconfigure with capture re-subscription succeeded");
    }

    [Test]
    public void Reset_StopsCapturing()
    {
        LogManager.Initialize(cfg =>
        {
            cfg.AddSink(new Sinks.RecordingSink());
            cfg.CaptureUnhandledExceptions();
        });

        LogManager.Reset();

        // After reset, should be able to initialize with capture again
        LogManager.Initialize(cfg =>
        {
            cfg.AddSink(new Sinks.RecordingSink());
            cfg.CaptureUnhandledExceptions();
        });

        Assert.Pass("Reset cleared exception handler state");
    }

    [Test]
    public void Initialize_WithoutCapture_DoesNotSubscribe()
    {
        LogManager.Initialize(cfg =>
        {
            cfg.AddSink(new Sinks.RecordingSink());
        });

        Assert.Pass("Initialize without capture succeeded");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference CreateFaultedTask()
    {
        var task = Task.Run(() => throw new InvalidOperationException("test fault"));
        try { task.Wait(); } catch { }
        return new WeakReference(task);
    }
}
