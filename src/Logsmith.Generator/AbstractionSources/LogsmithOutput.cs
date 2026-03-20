namespace Logsmith;

/// <summary>
/// Static wiring point for abstraction mode. Consumers set <see cref="Logger"/>
/// at application startup to receive log entries from the library.
/// </summary>
public static class LogsmithOutput
{
    private static volatile ILogsmithLogger? _logger;

    /// <summary>
    /// Gets or sets the logger implementation. When null, all log calls are no-ops.
    /// </summary>
    public static ILogsmithLogger? Logger
    {
        get => _logger;
        set => _logger = value;
    }
}
