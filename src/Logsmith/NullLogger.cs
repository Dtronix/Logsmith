namespace Logsmith;

/// <summary>
/// Singleton no-op logger. IsEnabled always returns false, all operations are no-ops.
/// Returned by <see cref="ILogger.When"/> when condition is false.
/// </summary>
public sealed class NullLogger : ILogger
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static readonly NullLogger Instance = new();

    private static readonly LoggerContext NullContext = new("Null");

    private NullLogger() { }

    public LoggerContext Context => NullContext;

    public bool IsEnabled(LogLevel level) => false;

    public ILogger CreateChild(string? segment) => this;

    public string? PathSegment
    {
        get => null;
        set { }
    }
}
