using System.Text;
using System.Text.Json;
using SampleLib.Logging;

// Wire up a structured logger implementation
LogsmithOutput.Logger = new StructuredConsoleLogger();

// Use the generated log methods
SampleLog.AppStarted(42);
SampleLog.ProcessingItem("item-001", 5);

// Structured parameters
var tags = new Dictionary<string, string>
{
    ["region"] = "us-west-2",
    ["env"] = "staging"
};
SampleLog.DeploymentInfo("v2.1.0", tags);

// Exception logging
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
/// Consumer-provided IStructuredLogsmithLogger implementation.
/// Receives both the pre-formatted UTF-8 message and typed structured properties.
/// In a real app, this would bridge to the consumer's logging framework.
/// </summary>
sealed class StructuredConsoleLogger : IStructuredLogsmithLogger
{
    public bool IsEnabled(LogLevel level, string category) => true;

    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        var message = Encoding.UTF8.GetString(utf8Message);
        Console.WriteLine($"[{entry.Level}] {entry.Category}: {message}");
    }

    /// <summary>
    /// Structured path — the generator dispatches here when it detects
    /// that the logger implements IStructuredLogsmithLogger. The propertyWriter
    /// delegate writes each log parameter as a named JSON property.
    /// </summary>
    public void WriteStructured<TState>(
        in LogEntry entry,
        ReadOnlySpan<byte> utf8Message,
        TState state,
        WriteProperties<TState> propertyWriter)
        where TState : allows ref struct
    {
        var message = Encoding.UTF8.GetString(utf8Message);
        Console.WriteLine($"[{entry.Level}] {entry.Category}: {message}");

        // Also emit structured JSON properties
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
