namespace Logsmith;

public interface IStructuredLogSink : ILogSink
{
    void WriteStructured<TState>(
        in LogEntry entry,
        TState state,
        WriteProperties<TState> propertyWriter)
        where TState : allows ref struct;
}
