namespace Logsmith;

/// <summary>
/// Extension methods that return concrete struct types to avoid boxing.
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Creates a scoped logger that pushes a path segment.
    /// Dispose the returned scope to clear the segment.
    /// </summary>
    public static LogScope Scoped(this ILogger logger, string segment)
    {
        return new LogScope(logger.Context, segment);
    }

    /// <summary>
    /// Creates a timed operation that logs elapsed time on completion.
    /// </summary>
    public static TimingOperation TimeOperation(this ILogger logger, string name)
    {
        return new TimingOperation(logger.Context, name);
    }
}
