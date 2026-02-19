namespace Logsmith.DynamicLevel;

internal sealed class EnvironmentLevelMonitor : IDisposable
{
    private readonly Timer _timer;
    private readonly string _envVarName;
    private LogLevel _lastLevel;

    internal EnvironmentLevelMonitor(string envVarName, TimeSpan pollInterval)
    {
        _envVarName = envVarName;
        _lastLevel = LogLevel.None;
        _timer = new Timer(Poll, null, pollInterval, pollInterval);
    }

    private void Poll(object? state)
    {
        var value = Environment.GetEnvironmentVariable(_envVarName);
        if (value is not null && Enum.TryParse<LogLevel>(value, ignoreCase: true, out var level))
        {
            if (level != _lastLevel)
            {
                _lastLevel = level;
                LogManager.SetMinimumLevel(level);
            }
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
