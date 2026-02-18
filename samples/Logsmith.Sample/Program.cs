using Logsmith;
using Logsmith.Sample;
using Logsmith.Sinks;

// Initialize with console and file sinks
LogManager.Initialize(c =>
{
    c.MinimumLevel = LogLevel.Trace;
    c.AddConsoleSink(colored: true);
    c.AddFileSink("sample.log");
});

// Various log levels and parameter types
Log.AppStarted(args.Length);
Log.ProcessingItem("widget", 1);
Log.CacheMiss("user:42", cachedValue: null);
Log.CacheMiss("user:99", cachedValue: 7);
Log.UserLoggedIn(1001, "Alice");
Log.UserLoggedIn(1002, displayName: null);

// Large struct passed by reference — no copy at the call site
var reading = new SensorReading { Temperature = 23.4, Humidity = 61.2, Pressure = 1013.25 };
Log.SensorData(in reading);

// Caller info — file, line, and member auto-populated
Log.Checkpoint();

// Exception logging
try
{
    throw new InvalidOperationException("Something went wrong");
}
catch (Exception ex)
{
    Log.OperationFailed("data-sync", ex);
}

// Reconfigure to raise minimum level
LogManager.Reconfigure(c =>
{
    c.MinimumLevel = LogLevel.Warning;
    c.AddConsoleSink(colored: true);
});

Console.WriteLine();
Console.WriteLine("--- After reconfiguring to Warning ---");
Console.WriteLine();

// These are now below minimum level and will be skipped
Log.AppStarted(0);
Log.ProcessingItem("skipped", 0);

// These still emit
Log.CacheMiss("user:1", cachedValue: null);
Log.ShutdownCritical();
