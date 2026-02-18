using Microsoft.Extensions.Logging;

namespace Logsmith.Benchmarks.LogDefinitions;

public static partial class MelLog
{
    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Simple log message")]
    public static partial void SimpleMessage(ILogger logger);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "User {userName} performed action {actionId}")]
    public static partial void TemplatedMessage(ILogger logger, string userName, int actionId);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "{method} {path} completed in {elapsed}ms with status {statusCode}")]
    public static partial void MultiParameter(ILogger logger, string method, string path, double elapsed, int statusCode);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Operation {operationName} failed")]
    public static partial void ExceptionMessage(ILogger logger, Exception ex, string operationName);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Trace, Message = "Trace-level diagnostic message")]
    public static partial void DisabledMessage(ILogger logger);
}
