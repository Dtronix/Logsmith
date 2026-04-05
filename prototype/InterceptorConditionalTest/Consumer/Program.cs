using InterceptorTest;

Console.Write("Defined symbols: ");
#if TRACE
Console.Write("TRACE ");
#endif
#if DEBUG
Console.Write("DEBUG ");
#endif
#if NOOP
Console.Write("NOOP ");
#endif
Console.WriteLine();
Console.WriteLine();

var logger = new TestLogger();

// Test 1: TraceLog — interceptor has [Conditional("TRACE")], TRACE is NOT defined
Console.WriteLine("=== Test 1: TraceLog (interceptor has [Conditional(\"TRACE\")]) ===");
int sideEffect1 = 0;
logger.TraceLog($"Value is {++sideEffect1}");
Console.WriteLine($"  sideEffect1 = {sideEffect1}");
Console.WriteLine($"  (0 = interceptor's [Conditional] removed the call)");
Console.WriteLine($"  (1 = original method still called despite interceptor's [Conditional])");

// Test 2: DebugLog — interceptor has [Conditional("DEBUG")], DEBUG is NOT defined
Console.WriteLine();
Console.WriteLine("=== Test 2: DebugLog (interceptor has [Conditional(\"DEBUG\")]) ===");
int sideEffect2 = 0;
logger.DebugLog($"Value is {++sideEffect2}");
Console.WriteLine($"  sideEffect2 = {sideEffect2}");

// Test 3: InfoLog — no interceptor, no [Conditional], should always run
Console.WriteLine();
Console.WriteLine("=== Test 3: InfoLog (no interceptor, control) ===");
int sideEffect3 = 0;
logger.InfoLog($"Value is {++sideEffect3}");
Console.WriteLine($"  sideEffect3 = {sideEffect3} (should be 1)");

Console.WriteLine();
Console.WriteLine("Done");
