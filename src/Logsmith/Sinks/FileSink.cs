using System.Buffers;
using Logsmith.Formatting;

namespace Logsmith.Sinks;

public class FileSink : BufferedLogSink
{
    private readonly string _basePath;
    private readonly long _maxFileSizeBytes;
    private readonly ILogFormatter _formatter;
    private readonly bool _shared;
    private FileStream? _fileStream;
    private long _currentSize;

    public FileSink(string path, LogLevel minimumLevel = LogLevel.Trace,
                    long maxFileSizeBytes = 10 * 1024 * 1024,
                    ILogFormatter? formatter = null, bool shared = false)
        : base(minimumLevel)
    {
        _basePath = Path.GetFullPath(path);
        _maxFileSizeBytes = maxFileSizeBytes;
        _formatter = formatter ?? new DefaultLogFormatter(includeDate: true);
        _shared = shared;
        EnsureFileOpen();
    }

    protected override async Task WriteBufferedAsync(BufferedEntry entry, CancellationToken ct)
    {
        if (_fileStream is null)
            EnsureFileOpen();

        var logEntry = new LogEntry(
            entry.Level, entry.EventId, entry.TimestampTicks, entry.Category,
            entry.Exception, entry.CallerFile, entry.CallerLine, entry.CallerMember,
            entry.ThreadId, entry.ThreadName);

        var buffer = new ArrayBufferWriter<byte>(256);
        _formatter.FormatPrefix(in logEntry, buffer);
        var prefixBytes = buffer.WrittenMemory;

        var suffixBuffer = new ArrayBufferWriter<byte>(64);
        _formatter.FormatSuffix(in logEntry, suffixBuffer);
        var suffixBytes = suffixBuffer.WrittenMemory;

        var totalBytes = prefixBytes.Length + entry.Utf8Message.Length + suffixBytes.Length;

        // In shared mode, seek to end to account for external writes
        if (_shared)
        {
            _fileStream!.Seek(0, SeekOrigin.End);
            _currentSize = _fileStream.Position;
        }

        if (_currentSize + totalBytes > _maxFileSizeBytes && _currentSize > 0)
        {
            await RollFileAsync(ct);
        }

        await _fileStream!.WriteAsync(prefixBytes, ct);
        await _fileStream.WriteAsync(entry.Utf8Message, ct);
        await _fileStream.WriteAsync(suffixBytes, ct);
        await _fileStream.FlushAsync(ct);
        _currentSize += totalBytes;
    }

    private void EnsureFileOpen()
    {
        var dir = Path.GetDirectoryName(_basePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var fileShare = _shared ? FileShare.ReadWrite : FileShare.Read;
        _fileStream = new FileStream(_basePath, FileMode.Append, FileAccess.Write, fileShare);
        _currentSize = _fileStream.Length;
    }

    protected override async ValueTask OnDisposeAsync()
    {
        if (_fileStream is not null)
        {
            await _fileStream.FlushAsync();
            await _fileStream.DisposeAsync();
            _fileStream = null;
        }
    }

    private async Task RollFileAsync(CancellationToken ct)
    {
        if (_fileStream is not null)
        {
            await _fileStream.FlushAsync(ct);
            await _fileStream.DisposeAsync();
            _fileStream = null;
        }

        var dir = Path.GetDirectoryName(_basePath)!;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(_basePath);
        var ext = Path.GetExtension(_basePath);
        var rolledName = $"{nameWithoutExt}.{DateTime.UtcNow:yyyyMMdd-HHmmss}{ext}";
        var rolledPath = Path.Combine(dir, rolledName);

        try
        {
            File.Move(_basePath, rolledPath, overwrite: false);
        }
        catch (IOException) when (_shared)
        {
            // In shared mode, another process may have already rolled.
            // Just reopen the base path — the other process created a fresh file.
        }

        EnsureFileOpen();
    }
}
