namespace Logsmith;

/// <summary>
/// Extended logging interface that receives typed property access
/// via a structured write callback. Optional — consumers implement
/// this if they need structured/JSON property data beyond the text message.
/// </summary>
public interface IStructuredLogsmithLogger : ILogsmithLogger
{
    /// <summary>
    /// Writes a log entry with both the pre-formatted UTF-8 message and
    /// a typed property writer for structured sinks.
    /// </summary>
    void WriteStructured<TState>(
        in LogEntry entry,
        System.ReadOnlySpan<byte> utf8Message,
        TState state,
        WriteProperties<TState> propertyWriter)
        where TState : allows ref struct;
}
