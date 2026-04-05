namespace Logsmith;

/// <summary>
/// Simple ILogger wrapper around a LoggerContext.
/// Created by LogManager.GetLogger() for user-facing API.
/// </summary>
internal sealed class LoggerInstance : ILogger
{
    public LoggerContext Context { get; }

    internal LoggerInstance(LoggerContext context)
    {
        Context = context;
    }
}
