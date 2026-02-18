using System.Text;

namespace Logsmith.Sinks;

public class FileSink : BufferedLogSink
{
    private static readonly ReadOnlyMemory<byte> Newline = new byte[] { (byte)'\n' };

    private readonly string _basePath;
    private readonly long _maxFileSizeBytes;
    private FileStream? _fileStream;
    private long _currentSize;

    public FileSink(string path, LogLevel minimumLevel = LogLevel.Trace, long maxFileSizeBytes = 10 * 1024 * 1024)
        : base(minimumLevel)
    {
        _basePath = Path.GetFullPath(path);
        _maxFileSizeBytes = maxFileSizeBytes;
        EnsureFileOpen();
    }

    protected override async Task WriteBufferedAsync(BufferedEntry entry, CancellationToken ct)
    {
        if (_fileStream is null)
            EnsureFileOpen();

        var timestamp = new DateTime(entry.TimestampTicks, DateTimeKind.Utc);
        var levelTag = entry.Level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???"
        };

        var prefix = $"[{timestamp:yyyy-MM-dd HH:mm:ss.fff} {levelTag} {entry.Category}] ";
        var prefixBytes = Encoding.UTF8.GetBytes(prefix);
        var totalBytes = prefixBytes.Length + entry.Utf8Message.Length + Newline.Length;

        if (_currentSize + totalBytes > _maxFileSizeBytes && _currentSize > 0)
        {
            await RollFileAsync(ct);
        }

        await _fileStream!.WriteAsync(prefixBytes, ct);
        await _fileStream.WriteAsync(entry.Utf8Message, ct);
        await _fileStream.WriteAsync(Newline, ct);
        await _fileStream.FlushAsync(ct);
        _currentSize += totalBytes;
    }

    private void EnsureFileOpen()
    {
        var dir = Path.GetDirectoryName(_basePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _fileStream = new FileStream(_basePath, FileMode.Append, FileAccess.Write, FileShare.Read);
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

        File.Move(_basePath, rolledPath);

        EnsureFileOpen();
    }
}
