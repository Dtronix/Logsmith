using System.Diagnostics;

namespace InterceptorTest;

public class TestLogger
{
    [Conditional("TRACE")]
    public void TraceLog(string message)
    {
        Console.WriteLine($"  [ORIGINAL] TraceLog: {message}");
    }

    [Conditional("DEBUG")]
    public void DebugLog(string message)
    {
        Console.WriteLine($"  [ORIGINAL] DebugLog: {message}");
    }

    public void InfoLog(string message)
    {
        Console.WriteLine($"  [ORIGINAL] InfoLog: {message}");
    }
}
