using Logsmith;

namespace Logsmith.Sample;

[LogCategory("Sample")]
public static partial class Log
{
    // Message template with string and int parameters
    [LogMessage(LogLevel.Information, "Application started with {argCount} argument(s)")]
    public static partial void AppStarted(int argCount);

    // Template-free logging — message auto-generated from method name and parameters
    [LogMessage(LogLevel.Debug)]
    public static partial void ProcessingItem(string itemName, int index);

    // Warning with a nullable value type
    [LogMessage(LogLevel.Warning, "Cache miss for key {key}, last value was {cachedValue}")]
    public static partial void CacheMiss(string key, int? cachedValue);

    // Error with exception parameter (classified automatically)
    [LogMessage(LogLevel.Error, "Operation failed for {operation}")]
    public static partial void OperationFailed(string operation, Exception ex);

    // Trace with caller info (auto-populated by the compiler)
    [LogMessage(LogLevel.Trace, "Checkpoint reached")]
    public static partial void Checkpoint(
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0,
        [System.Runtime.CompilerServices.CallerMemberName] string member = "");

    // Critical with no parameters
    [LogMessage(LogLevel.Critical, "System is shutting down")]
    public static partial void ShutdownCritical();

    // Information with nullable reference type
    [LogMessage(LogLevel.Information, "User {userId} logged in, display name: {displayName}")]
    public static partial void UserLoggedIn(int userId, string? displayName);
}
