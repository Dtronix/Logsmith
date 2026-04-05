using System;
using System.Threading;

// Test 1: ref struct implementing IDisposable
Console.WriteLine("=== ref struct : IDisposable ===");
{
    using var scope = new TimingScope("operation");
    Thread.Sleep(50);
    Console.WriteLine("  doing work...");
}

// Test 2: ref struct implementing custom interface
Console.WriteLine();
Console.WriteLine("=== ref struct : custom interface ===");
{
    using var scope = new ScopeStruct("RequestId", "abc-123");
    Console.WriteLine($"  key={scope.Key} value={scope.Value}");
}

// Test 3: generic method with allows ref struct
Console.WriteLine();
Console.WriteLine("=== allows ref struct constraint ===");
PrintDisposable(new TimingScope("generic-test"));

Console.WriteLine();
Console.WriteLine("Done");

static void PrintDisposable<T>(T item) where T : IDisposable, allows ref struct
{
    Console.WriteLine($"  Got a disposable, disposing...");
    item.Dispose();
}

ref struct TimingScope : IDisposable
{
    private readonly string _name;
    private readonly long _startTicks;

    public TimingScope(string name)
    {
        _name = name;
        _startTicks = Environment.TickCount64;
        Console.WriteLine($"  [{_name}] started");
    }

    public void Dispose()
    {
        var elapsed = Environment.TickCount64 - _startTicks;
        Console.WriteLine($"  [{_name}] completed in {elapsed}ms");
    }
}

interface IScopeInfo
{
    string Key { get; }
    string Value { get; }
}

ref struct ScopeStruct : IDisposable, IScopeInfo
{
    public string Key { get; }
    public string Value { get; }

    public ScopeStruct(string key, string value)
    {
        Key = key;
        Value = value;
        Console.WriteLine($"  scope pushed: {key}={value}");
    }

    public void Dispose()
    {
        Console.WriteLine($"  scope popped: {Key}");
    }
}
