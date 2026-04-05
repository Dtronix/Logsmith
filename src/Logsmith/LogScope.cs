namespace Logsmith;

/// <summary>
/// Struct-based scoped logger that pushes a path segment on creation
/// and clears it on dispose. Use via <see cref="LoggerExtensions.Scoped"/>.
/// </summary>
public struct LogScope : ILogger, IDisposable
{
    private readonly LoggerContext _context;
    private bool _disposed;

    internal LogScope(LoggerContext parentContext, string segment)
    {
        _context = parentContext.CreateChild(segment);
        _disposed = false;
    }

    public LoggerContext Context => _context;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _context.PathSegment = null;
    }
}
