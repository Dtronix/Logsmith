using Logsmith;

namespace Logsmith.Sample.MelBridge;

[LogCategory("App")]
public static partial class Log
{
    [LogMessage(LogLevel.Information, "Application started")]
    public static partial void AppStarted();

    [LogMessage(LogLevel.Information, "Processing order {orderId} for {customer}")]
    public static partial void ProcessingOrder(string orderId, string customer);

    [LogMessage(LogLevel.Warning, "Slow query detected: {durationMs}ms")]
    public static partial void SlowQuery(int durationMs);

    [LogMessage(LogLevel.Error, "Order processing failed for {orderId}")]
    public static partial void OrderFailed(string orderId, Exception ex);

    // Sampling: only emit 1 in 5 heartbeats
    [LogMessage(LogLevel.Debug, "Heartbeat", SampleRate = 5)]
    public static partial void Heartbeat();

    // Rate limiting: max 2 per second
    [LogMessage(LogLevel.Warning, "Rate limited event", MaxPerSecond = 2)]
    public static partial void RateLimitedEvent();
}
