using System.Text;
using System.Text.Json;
using SampleLib.Logging;

var tags = new Dictionary<string, string>
{
    ["region"] = "us-west-2",
    ["env"] = "staging"
};

// ── 1. Text-only logger ─────────────────────────────────────────────
Console.WriteLine("═══ Text-only logging (ILogsmithLogger) ═══");
Console.WriteLine();

LogsmithOutput.Logger = new ConsoleLogsmithLogger();

SampleLog.AppStarted(42);
SampleLog.ProcessingItem("item-001", 5);
SampleLog.DeploymentInfo("v2.1.0", tags);

try { throw new InvalidOperationException("test error"); }
catch (Exception ex) { SampleLog.OperationFailed("TestOp", ex); }

// ── 2. Structured logger — same calls now also emit JSON properties ─
Console.WriteLine();
Console.WriteLine("═══ Text + Structured logging (IStructuredLogsmithLogger) ═══");
Console.WriteLine();

LogsmithOutput.Logger = new StructuredConsoleLogger();

SampleLog.AppStarted(42);
SampleLog.ProcessingItem("item-001", 5);
SampleLog.DeploymentInfo("v2.1.0", tags);

try { throw new InvalidOperationException("test error"); }
catch (Exception ex) { SampleLog.OperationFailed("TestOp", ex); }

Console.WriteLine();
Console.WriteLine("Abstraction mode sample completed.");

/// <summary>
/// Text-only logger — receives pre-formatted UTF-8 messages.
/// </summary>
sealed class ConsoleLogsmithLogger : ILogsmithLogger
{
    public bool IsEnabled(LogLevel level, string category) => true;

    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        var message = Encoding.UTF8.GetString(utf8Message);
        Console.WriteLine($"[{entry.Level}] {entry.Category}: {message}");
    }
}

/// <summary>
/// Structured logger — receives both the text message and typed properties.
/// The generator dispatches to WriteStructured when it detects this interface.
/// </summary>
sealed class StructuredConsoleLogger : IStructuredLogsmithLogger
{
    public bool IsEnabled(LogLevel level, string category) => true;

    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        var message = Encoding.UTF8.GetString(utf8Message);
        Console.WriteLine($"[{entry.Level}] {entry.Category}: {message}");
    }

    public void WriteStructured<TState>(
        in LogEntry entry,
        ReadOnlySpan<byte> utf8Message,
        TState state,
        WriteProperties<TState> propertyWriter)
        where TState : allows ref struct
    {
        // Text output
        var message = Encoding.UTF8.GetString(utf8Message);
        Console.WriteLine($"[{entry.Level}] {entry.Category}: {message}");

        // Structured JSON properties
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        propertyWriter(writer, state);
        writer.WriteEndObject();
        writer.Flush();

        var json = Encoding.UTF8.GetString(stream.ToArray());
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  [structured] ");
        Console.ResetColor();
        Console.WriteLine(json);
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

    // :json format specifier — Dictionary serialized as JSON in both text and structured paths
    [LogMessage(LogLevel.Information, "Deployment info: version={version}, tags={tags:json}")]
    public static partial void DeploymentInfo(string version, Dictionary<string, string> tags);
}
