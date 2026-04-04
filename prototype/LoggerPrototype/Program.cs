using LoggerPrototype;

ILogger logger = new Logger("MyApp");

Console.WriteLine("=== 1. Interpolated strings (handler overload) ===");
Console.WriteLine();

int drawCallId = 42;
double elapsedMs = 3.7;
logger.Debug($"Draw call {drawCallId} completed in {elapsedMs}ms");

Console.WriteLine();
string userId = "user-123";
int itemCount = 5;
logger.Information($"User {userId} processing {itemCount} items");

Console.WriteLine();
decimal price = 19.99m;
logger.Information($"Price updated to {price:F2}");

Console.WriteLine();
Console.WriteLine("=== 2. Plain strings (string overload) ===");
Console.WriteLine();

logger.Debug("Application started");

Console.WriteLine();
string prebuilt = $"Pre-built with value {drawCallId}";
logger.Debug(prebuilt);

Console.WriteLine();
Console.WriteLine("=== 3. Error with exception ===");
Console.WriteLine();

var ex = new InvalidOperationException("Something went wrong");
string operationName = "SaveOrder";
logger.Error(ex, $"Operation {operationName} failed after {elapsedMs:F1}ms");

Console.WriteLine();
Console.WriteLine("=== 4. Short-circuit when level disabled ===");
Console.WriteLine();

ILogger warningOnly = new Logger("Filtered", LogLevel.Warning);
Console.WriteLine("  Logging Debug to a Warning-level logger (should not appear):");
warningOnly.Debug($"This should NOT appear: {drawCallId}");
Console.WriteLine("  (nothing above = short-circuit works)");

Console.WriteLine();
warningOnly.Warning($"This SHOULD appear: {drawCallId}");

Console.WriteLine();
Console.WriteLine("=== 5. Various types (JIT-specialized JSON writes) ===");
Console.WriteLine();

int intVal = 42;
long longVal = 123456789L;
double doubleVal = 3.14159;
float floatVal = 2.718f;
bool boolVal = true;
DateTime dateVal = DateTime.UtcNow;
Guid guidVal = Guid.NewGuid();

logger.Debug($"int={intVal} long={longVal} double={doubleVal}");
Console.WriteLine();
logger.Debug($"float={floatVal} bool={boolVal}");
Console.WriteLine();
logger.Debug($"date={dateVal} guid={guidVal}");

Console.WriteLine();
Console.WriteLine("=== 6. Nullable values ===");
Console.WriteLine();

int? nullableWithValue = 42;
int? nullableNull = null;
string? nullableString = null;
logger.Debug($"hasValue={nullableWithValue} isNull={nullableNull} nullStr={nullableString}");

Console.WriteLine();
Console.WriteLine("=== 7. Expression as property name ===");
Console.WriteLine();

var order = new { Items = new { Count = 3 }, Total = 99.95m };
logger.Information($"Order has {order.Items.Count} items totaling {order.Total:F2}");

Console.WriteLine();
Console.WriteLine("=== 8. All log levels ===");
Console.WriteLine();

logger.Trace($"Trace message {drawCallId}");
Console.WriteLine();
logger.Debug($"Debug message {drawCallId}");
Console.WriteLine();
logger.Information($"Information message {drawCallId}");
Console.WriteLine();
logger.Warning($"Warning message {drawCallId}");
Console.WriteLine();
logger.Error($"Error message {drawCallId}");
Console.WriteLine();
logger.Critical($"Critical message {drawCallId}");

Console.WriteLine();
Console.WriteLine("=== Done ===");
