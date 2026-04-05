using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using Logsmith.Internal;

namespace Logsmith;

/// <summary>
/// Central dispatch hub for all logging operations. Holds category, path node,
/// and references to the shared config via LogManager. Both [LogMessage] generated code
/// and the new ILogger API dispatch through LoggerContext.
/// </summary>
public sealed class LoggerContext
{
    private readonly string _category;
    private readonly PathNode? _pathNode;
    private readonly LoggerContext? _parent;
    private readonly ConcurrentDictionary<string, LoggerContext> _children = new();

    // Path cache — only rebuilds when version sum changes
    private byte[]? _cachedPathBytes;
    private int _cachedPathVersion;

    /// <summary>
    /// The log category for this context (typically a class name or namespace).
    /// </summary>
    public string Category => _category;

    /// <summary>
    /// The parent context, if this is a child context.
    /// </summary>
    public LoggerContext? Parent => _parent;

    internal PathNode? PathNode => _pathNode;

    internal LoggerContext(string category, PathNode? pathNode = null, LoggerContext? parent = null)
    {
        _category = category;
        _pathNode = pathNode;
        _parent = parent;
    }

    /// <summary>
    /// Checks if the given level is enabled for this context's category.
    /// </summary>
    public bool IsEnabled(LogLevel level)
    {
        return LogManager.IsEnabled(level, _category);
    }

    /// <summary>
    /// Dispatches a log entry to all configured sinks. Fills in category, path,
    /// timestamp, and thread info from context state.
    /// </summary>
    public void Dispatch(in DispatchInfo info)
    {
        // Build the full info with context-owned fields
        var fullInfo = info;
        fullInfo.Category = _category;
        fullInfo.TimestampTicks = DateTime.UtcNow.Ticks;
        fullInfo.ThreadId = Environment.CurrentManagedThreadId;
        fullInfo.ThreadName = Thread.CurrentThread.Name;

        // Build path from PathNode if present
        if (_pathNode is not null)
        {
            var pathBytes = GetCachedPathBytes();
            if (pathBytes is not null && pathBytes.Length > 0)
                fullInfo.Utf8Path = pathBytes;
        }

        LogManager.Dispatch(in fullInfo);
    }

    /// <summary>
    /// Creates a child context with a new path segment appended.
    /// </summary>
    public LoggerContext CreateChild(string? segment)
    {
        var childPathNode = new PathNode(segment, _pathNode);
        return new LoggerContext(_category, childPathNode, this);
    }

    /// <summary>
    /// Gets or creates a named child context. Named children are cached for reuse.
    /// </summary>
    internal LoggerContext GetOrCreateChild(string segment)
    {
        return _children.GetOrAdd(segment, seg => CreateChild(seg));
    }

    /// <summary>
    /// Gets or sets the path segment for this context's path node.
    /// </summary>
    public string? PathSegment
    {
        get => _pathNode?.Segment;
        set
        {
            if (_pathNode is not null)
                _pathNode.Segment = value;
        }
    }

    private byte[]? GetCachedPathBytes()
    {
        if (_pathNode is null) return null;

        var currentVersion = _pathNode.CalculateVersionSum();
        if (Volatile.Read(ref _cachedPathVersion) == currentVersion)
        {
            var cachedBytes = _cachedPathBytes;
            if (cachedBytes is not null)
                return cachedBytes;
        }

        var maxBytes = _pathNode.CalculateMaxByteCount();
        if (maxBytes == 0)
        {
            _cachedPathBytes = null;
            Volatile.Write(ref _cachedPathVersion, currentVersion);
            return null;
        }

        var buffer = new byte[maxBytes];
        var written = _pathNode.WriteUtf8Path(buffer);
        _cachedPathBytes = buffer.AsSpan(0, written).ToArray();
        Volatile.Write(ref _cachedPathVersion, currentVersion);
        return _cachedPathBytes;
    }
}
