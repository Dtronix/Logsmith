namespace Logsmith;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class LogMessageAttribute : Attribute
{
    public LogLevel Level { get; }
    public string Message { get; }
    public int EventId { get; set; }
    public bool AlwaysEmit { get; set; }

    public LogMessageAttribute(LogLevel level, string message = "")
    {
        Level = level;
        Message = message;
    }
}
