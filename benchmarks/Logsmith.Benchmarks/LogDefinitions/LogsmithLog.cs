using Logsmith;

namespace Logsmith.Benchmarks.LogDefinitions;

[LogCategory("Benchmark")]
public static partial class LogsmithLog
{
    [LogMessage(LogLevel.Information, "Simple log message")]
    public static partial void SimpleMessage();

    [LogMessage(LogLevel.Information, "User {userName} performed action {actionId}")]
    public static partial void TemplatedMessage(string userName, int actionId);

    [LogMessage(LogLevel.Information, "{method} {path} completed in {elapsed}ms with status {statusCode}")]
    public static partial void MultiParameter(string method, string path, double elapsed, int statusCode);

    [LogMessage(LogLevel.Error, "Operation {operationName} failed")]
    public static partial void ExceptionMessage(string operationName, Exception ex);

    [LogMessage(LogLevel.Trace, "Trace-level diagnostic message")]
    public static partial void DisabledMessage();
}
