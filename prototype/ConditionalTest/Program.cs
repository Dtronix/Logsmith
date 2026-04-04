using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

// Test 1: [Conditional] on a regular method
Console.WriteLine("=== Test 1: [Conditional] on regular method ===");
int sideEffect1 = 0;
ConditionalMethods.TraceLog($"Value is {++sideEffect1}");
Console.WriteLine($"  sideEffect1 = {sideEffect1} (0 = call site removed, 1 = still evaluated)");

// Test 2: [Conditional] on a method that takes an interpolated string handler
Console.WriteLine();
Console.WriteLine("=== Test 2: [Conditional] with InterpolatedStringHandler ===");
int sideEffect2 = 0;
ConditionalMethods.TraceLogHandler($"Value is {++sideEffect2}");
Console.WriteLine($"  sideEffect2 = {sideEffect2} (0 = handler+call removed, 1 = handler still ran)");

// Test 3: Regular call for comparison
Console.WriteLine();
Console.WriteLine("=== Test 3: Non-conditional call (control) ===");
int sideEffect3 = 0;
ConditionalMethods.AlwaysLog($"Value is {++sideEffect3}");
Console.WriteLine($"  sideEffect3 = {sideEffect3} (should be 1)");

// Test 4: [Conditional] on method with ref struct handler parameter
Console.WriteLine();
Console.WriteLine("=== Test 4: [Conditional] with ref struct handler ===");
int sideEffect4 = 0;
ConditionalMethods.TraceLogRefHandler($"Value is {++sideEffect4}");
Console.WriteLine($"  sideEffect4 = {sideEffect4} (0 = everything removed, 1 = handler still ran)");

Console.WriteLine();
Console.WriteLine("Done");

static class ConditionalMethods
{
    [Conditional("TRACE")]
    public static void TraceLog(string message)
    {
        Console.WriteLine($"  TraceLog called: {message}");
    }

    [Conditional("TRACE")]
    public static void TraceLogHandler([InterpolatedStringHandlerArgument] ref TraceHandler handler)
    {
        Console.WriteLine($"  TraceLogHandler called");
    }

    [Conditional("TRACE")]
    public static void TraceLogRefHandler([InterpolatedStringHandlerArgument] ref TraceRefHandler handler)
    {
        Console.WriteLine($"  TraceLogRefHandler called");
    }

    public static void AlwaysLog([InterpolatedStringHandlerArgument] ref TraceHandler handler)
    {
        Console.WriteLine($"  AlwaysLog called");
    }
}

[InterpolatedStringHandler]
public struct TraceHandler
{
    private bool _enabled;

    public TraceHandler(int literalLength, int formattedCount, out bool isEnabled)
    {
        Console.WriteLine($"  TraceHandler ctor called (literalLength={literalLength}, formattedCount={formattedCount})");
        _enabled = isEnabled = true;
    }

    public void AppendLiteral(string s)
    {
        Console.WriteLine($"  TraceHandler.AppendLiteral(\"{s}\")");
    }

    public void AppendFormatted<T>(T value)
    {
        Console.WriteLine($"  TraceHandler.AppendFormatted({value})");
    }
}

[InterpolatedStringHandler]
public ref struct TraceRefHandler
{
    private bool _enabled;

    public TraceRefHandler(int literalLength, int formattedCount, out bool isEnabled)
    {
        Console.WriteLine($"  TraceRefHandler ctor called");
        _enabled = isEnabled = true;
    }

    public void AppendLiteral(string s)
    {
        Console.WriteLine($"  TraceRefHandler.AppendLiteral(\"{s}\")");
    }

    public void AppendFormatted<T>(T value)
    {
        Console.WriteLine($"  TraceRefHandler.AppendFormatted({value})");
    }
}
