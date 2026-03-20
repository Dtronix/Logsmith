using SampleLib.Logging;

// Wire up a consumer-provided logger implementation
LogsmithOutput.Logger = new ConsoleLogsmithLogger();

// Use the generated log methods
SampleLog.AppStarted(42);
SampleLog.ProcessingItem("item-001", 5);

try
{
    throw new InvalidOperationException("test error");
}
catch (Exception ex)
{
    SampleLog.OperationFailed("TestOp", ex);
}

Console.WriteLine("Abstraction mode sample completed.");

/// <summary>
/// Consumer-provided ILogsmithLogger implementation.
/// In a real app, this would bridge to the consumer's logging framework.
/// </summary>
sealed class ConsoleLogsmithLogger : ILogsmithLogger
{
    public bool IsEnabled(LogLevel level, string category) => true;

    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        var message = System.Text.Encoding.UTF8.GetString(utf8Message);
        Console.WriteLine($"[{entry.Level}] {entry.Category}: {message}");
    }
}

[LogCategory("Sample")]
static partial class SampleLog
{
    [LogMessage(LogLevel.Information, "Application started with {argCount} arg(s)")]
    public static partial void AppStarted(int argCount);

    [LogMessage(LogLevel.Debug, "Processing item {itemId}, count={count}")]
    public static partial void ProcessingItem(string itemId, int count);

    [LogMessage(LogLevel.Error, "Operation {name} failed")]
    public static partial void OperationFailed(string name, Exception ex);
}
