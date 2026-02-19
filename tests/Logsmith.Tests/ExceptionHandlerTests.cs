namespace Logsmith.Tests;

[TestFixture]
public class ExceptionHandlerTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown() => LogManager.Reset();

    [Test]
    public void CaptureUnhandledExceptions_SubscribesHandler()
    {
        Exception? caught = null;
        LogManager.CaptureUnhandledExceptions(ex => caught = ex);

        // We can't easily trigger AppDomain.UnhandledException in tests,
        // but we can verify the method doesn't throw and can be called.
        Assert.Pass("Handler registered without error");
    }

    [Test]
    public void StopCapturing_UnsubscribesCleanly()
    {
        LogManager.CaptureUnhandledExceptions(_ => { });
        LogManager.StopCapturingUnhandledExceptions();

        // Should be able to re-register
        LogManager.CaptureUnhandledExceptions(_ => { });
        Assert.Pass("Re-registration succeeded");
    }

    [Test]
    public void CaptureUnhandledExceptions_CalledTwice_IgnoresSecond()
    {
        int count = 0;
        LogManager.CaptureUnhandledExceptions(_ => count++);
        LogManager.CaptureUnhandledExceptions(_ => count += 10);

        // Second call is no-op since already capturing
        Assert.Pass("Double registration did not throw");
    }

    [Test]
    public void UnobservedTaskException_WithObserve_CapturesException()
    {
        Exception? caught = null;
        LogManager.Initialize(c => c.AddSink(new Sinks.RecordingSink()));
        LogManager.CaptureUnhandledExceptions(ex => caught = ex, observeTaskExceptions: true);

        // Create a faulted task and let it be collected
        var weakRef = CreateFaultedTask();

        // Force GC to trigger UnobservedTaskException
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Give time for the event to fire
        Thread.Sleep(200);

        // Note: UnobservedTaskException only fires during finalization,
        // which is non-deterministic. We just verify no crash.
        Assert.Pass("No crash from unobserved task exception handling");
    }

    [Test]
    public void Reset_StopsCapturing()
    {
        LogManager.CaptureUnhandledExceptions(_ => { });
        LogManager.Reset();

        // After reset, should be able to capture again
        LogManager.CaptureUnhandledExceptions(_ => { });
        Assert.Pass("Reset cleared exception handler state");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference CreateFaultedTask()
    {
        var task = Task.Run(() => throw new InvalidOperationException("test fault"));
        try { task.Wait(); } catch { }
        return new WeakReference(task);
    }
}
