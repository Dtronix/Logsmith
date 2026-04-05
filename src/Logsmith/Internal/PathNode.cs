using System.Text;
using System.Threading;

namespace Logsmith.Internal;

/// <summary>
/// Linked-list node for hierarchical log paths. Each node holds a mutable segment
/// and a parent pointer. Version-based caching enables efficient UTF-8 path rendering
/// that only rebuilds when segments change.
/// </summary>
internal sealed class PathNode
{
    private string? _segment;
    private int _version;

    internal PathNode? Parent { get; }

    internal string? Segment
    {
        get => Volatile.Read(ref _segment);
        set
        {
            Volatile.Write(ref _segment, value);
            Interlocked.Increment(ref _version);
        }
    }

    internal int Version => Volatile.Read(ref _version);

    internal PathNode(string? segment, PathNode? parent = null)
    {
        _segment = segment;
        _version = 1;
        Parent = parent;
    }

    /// <summary>
    /// Walks the chain and sums all versions. Used for cache invalidation.
    /// </summary>
    internal int CalculateVersionSum()
    {
        var sum = 0;
        var node = this;
        while (node is not null)
        {
            sum += Volatile.Read(ref node._version);
            node = node.Parent;
        }
        return sum;
    }

    /// <summary>
    /// Writes the full path as UTF-8 to the destination span.
    /// Format: "Root|Child|Grandchild" — segments separated by '|'.
    /// Null/empty segments are skipped.
    /// Returns the number of bytes written.
    /// </summary>
    internal int WriteUtf8Path(Span<byte> destination)
    {
        // Pass 1: walk leaf-to-root, compute exact total byte count
        var totalBytes = 0;
        var nonEmptyCount = 0;
        var node = this;
        while (node is not null)
        {
            var seg = node.Segment;
            if (!string.IsNullOrEmpty(seg))
            {
                totalBytes += Encoding.UTF8.GetByteCount(seg!);
                nonEmptyCount++;
            }
            node = node.Parent;
        }

        if (nonEmptyCount == 0)
            return 0;

        totalBytes += nonEmptyCount - 1; // separators

        // Pass 2: walk leaf-to-root, write right-to-left into destination
        var writePos = totalBytes;
        var isFirst = true;
        node = this;
        while (node is not null)
        {
            var seg = node.Segment;
            if (!string.IsNullOrEmpty(seg))
            {
                if (!isFirst)
                {
                    writePos--;
                    destination[writePos] = (byte)'|';
                }

                var byteCount = Encoding.UTF8.GetByteCount(seg!);
                writePos -= byteCount;
                Encoding.UTF8.GetBytes(seg.AsSpan(), destination.Slice(writePos, byteCount));
                isFirst = false;
            }
            node = node.Parent;
        }

        return totalBytes;
    }

    /// <summary>
    /// Calculates the maximum number of UTF-8 bytes needed for the full path.
    /// </summary>
    internal int CalculateMaxByteCount()
    {
        var total = 0;
        var separators = -1; // one less separator than segments
        var node = this;
        while (node is not null)
        {
            var seg = node.Segment;
            if (!string.IsNullOrEmpty(seg))
            {
                total += Encoding.UTF8.GetMaxByteCount(seg!.Length);
                separators++;
            }
            node = node.Parent;
        }

        return total + Math.Max(0, separators);
    }
}
