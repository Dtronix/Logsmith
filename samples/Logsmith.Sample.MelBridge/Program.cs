using Logsmith;
using Logsmith.Extensions.Logging;
using Logsmith.Sample.MelBridge;
using Logsmith.Sinks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ── 1. Initialize Logsmith ─────────────────────────────────────────────
LogManager.Initialize(c =>
{
    c.MinimumLevel = Logsmith.LogLevel.Trace;
    c.AddConsoleSink(colored: true);
});

Console.WriteLine("═══ Logsmith + MEL Bridge Sample ═══");
Console.WriteLine();

// ── 2. Source-generated logging (direct Logsmith) ──────────────────────
Console.WriteLine("── Direct Logsmith logging ──");
Log.AppStarted();
Log.ProcessingOrder("ORD-001", "Alice");

// ── 3. Scoped context ──────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── Scoped context (LogScope) ──");
using (LogScope.Push("RequestId", "req-abc-123"))
{
    using (LogScope.Push("UserId", "user-42"))
    {
        Log.ProcessingOrder("ORD-002", "Bob");
        Log.SlowQuery(1500);
    }
}
// Scope is gone — no enrichment
Log.ProcessingOrder("ORD-003", "Charlie");

// ── 4. MEL bridge ──────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── Microsoft.Extensions.Logging bridge ──");

var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
    builder.AddLogsmith();
});

using var sp = services.BuildServiceProvider();
var melLogger = sp.GetRequiredService<ILogger<Program>>();

melLogger.LogInformation("Hello from MEL bridge");
melLogger.LogWarning("Order {OrderId} took {Duration}ms", "ORD-004", 2300);

// MEL scopes flow into LogScope
using (melLogger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = "corr-xyz" }))
{
    melLogger.LogInformation("Inside MEL scope");
    // Source-generated logs also pick up the scope
    Log.ProcessingOrder("ORD-005", "Diana");
}

melLogger.LogInformation("Outside MEL scope — no enrichment");

// ── 5. Exception logging via MEL ───────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── Exception logging ──");
try
{
    throw new InvalidOperationException("Payment gateway timeout");
}
catch (Exception ex)
{
    melLogger.LogError(ex, "Payment failed for order {OrderId}", "ORD-006");
    Log.OrderFailed("ORD-006", ex);
}

// ── 6. Sampling ────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── Sampling (1-in-5, sending 10 heartbeats) ──");
for (int i = 0; i < 10; i++)
    Log.Heartbeat();

// ── 7. Rate limiting ───────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── Rate limiting (max 2/sec, sending 5 rapidly) ──");
for (int i = 0; i < 5; i++)
    Log.RateLimitedEvent();

// ── 8. Dynamic level switching ─────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── Dynamic level switching ──");
Console.WriteLine("   (Setting minimum to Warning...)");
LogManager.Reconfigure(c =>
{
    c.MinimumLevel = Logsmith.LogLevel.Warning;
    c.AddConsoleSink(colored: true);
});

Log.AppStarted();                              // Information — filtered
Log.ProcessingOrder("ORD-007", "Eve");         // Information — filtered
Log.SlowQuery(3000);                           // Warning — visible
melLogger.LogDebug("This debug is filtered");  // Debug — filtered
melLogger.LogWarning("This warning is shown"); // Warning — visible

Console.WriteLine();
Console.WriteLine("═══ Done ═══");
