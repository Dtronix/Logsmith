using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Logsmith.Extensions.Logging;

public sealed class LogsmithLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, LogsmithLogger> _loggers = new();

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new LogsmithLogger(name));
    }

    public void Dispose()
    {
        _loggers.Clear();
    }
}
