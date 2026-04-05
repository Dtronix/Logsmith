using WhenTest;

ILogger logger = new Logger();

// Test 1: Direct call — interceptor fires, handler enabled
Console.WriteLine("=== Test 1: logger.Debug($\"...\") — direct, enabled ===");
int drawCallId = 42;
logger.Debug($"Draw call {drawCallId} completed");
Console.WriteLine();

// Test 2: When(true) — same logger returned, interceptor fires, handler enabled
Console.WriteLine("=== Test 2: logger.When(true).Debug($\"...\") — condition true ===");
logger.When(true).Debug($"Draw call {drawCallId} completed");
Console.WriteLine();

// Test 3: When(false) — NullLogger returned, interceptor fires, handler DISABLED
Console.WriteLine("=== Test 3: logger.When(false).Debug($\"...\") — condition false ===");
logger.When(false).Debug($"Draw call {drawCallId} completed");
Console.WriteLine();

// Test 4: String overload with When(false) — not intercepted
Console.WriteLine("=== Test 4: logger.When(false).Debug(string) — string overload ===");
logger.When(false).Debug("Static message");
Console.WriteLine();

Console.WriteLine("=== Done ===");
