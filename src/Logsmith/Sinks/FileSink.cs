using System.Buffers;
using Logsmith.Formatting;

namespace Logsmith.Sinks;

public class FileSink : BufferedLogSink
{
    private readonly string _basePath;
    private readonly long _maxFileSizeBytes;
    private readonly ILogFormatter _formatter;
    private readonly bool _shared;
    private readonly RollingInterval _rollingInterval;
    private FileStream? _fileStream;
    private long _currentSize;
    private DateTime _currentPeriodStart;
    private int _sizeRollCount;

    public FileSink(string path, LogLevel minimumLevel = LogLevel.Trace,
                    long maxFileSizeBytes = 10 * 1024 * 1024,
                    ILogFormatter? formatter = null, bool shared = false,
                    RollingInterval rollingInterval = RollingInterval.None)
        : base(minimumLevel)
    {
        _basePath = Path.GetFullPath(path);
        _maxFileSizeBytes = maxFileSizeBytes;
        _formatter = formatter ?? new DefaultLogFormatter(includeDate: true);
        _shared = shared;
        _rollingInterval = rollingInterval;
        _currentPeriodStart = GetPeriodStart(DateTime.UtcNow, _rollingInterval);
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

        // Time-based roll check (before size check)
        if (_rollingInterval != RollingInterval.None)
        {
            var entryTime = new DateTime(entry.TimestampTicks, DateTimeKind.Utc);
            var entryPeriod = GetPeriodStart(entryTime, _rollingInterval);
            if (entryPeriod > _currentPeriodStart)
            {
                await RollFileAsync(ct, GetTimeRolledName(entryPeriod));
                _currentPeriodStart = entryPeriod;
                _sizeRollCount = 0;
            }
        }

        // In shared mode, seek to end to account for external writes
        if (_shared)
        {
            _fileStream!.Seek(0, SeekOrigin.End);
            _currentSize = _fileStream.Position;
        }

        // Size-based roll check
        if (_currentSize + totalBytes > _maxFileSizeBytes && _currentSize > 0)
        {
            _sizeRollCount++;
            await RollFileAsync(ct, GetSizeRolledName());
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

        _fileStream = new FileStream(_basePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
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

    private string GetTimeRolledName(DateTime periodStart)
    {
        var dir = Path.GetDirectoryName(_basePath)!;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(_basePath);
        var ext = Path.GetExtension(_basePath);

        var periodSuffix = FormatPeriodSuffix(_rollingInterval, _currentPeriodStart);
        return Path.Combine(dir, $"{nameWithoutExt}.{periodSuffix}{ext}");
    }

    private string GetSizeRolledName()
    {
        var dir = Path.GetDirectoryName(_basePath)!;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(_basePath);
        var ext = Path.GetExtension(_basePath);

        if (_rollingInterval != RollingInterval.None)
        {
            var periodSuffix = FormatPeriodSuffix(_rollingInterval, _currentPeriodStart);
            return Path.Combine(dir, $"{nameWithoutExt}.{periodSuffix}.{_sizeRollCount}{ext}");
        }

        // Size-only roll: timestamp-based name (original behavior)
        return Path.Combine(dir, $"{nameWithoutExt}.{DateTime.UtcNow:yyyyMMdd-HHmmss}{ext}");
    }

    private async Task RollFileAsync(CancellationToken ct, string rolledPath)
    {
        if (_fileStream is not null)
        {
            await _fileStream.FlushAsync(ct);
            await _fileStream.DisposeAsync();
            _fileStream = null;
        }

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

    private static string FormatPeriodSuffix(RollingInterval interval, DateTime periodStart) => interval switch
    {
        RollingInterval.Hourly => periodStart.ToString("yyyy-MM-dd-HH"),
        RollingInterval.Daily => periodStart.ToString("yyyy-MM-dd"),
        RollingInterval.Weekly => periodStart.ToString("yyyy-MM-dd"),
        RollingInterval.Monthly => periodStart.ToString("yyyy-MM"),
        _ => periodStart.ToString("yyyyMMdd-HHmmss")
    };

    private static DateTime GetPeriodStart(DateTime utcNow, RollingInterval interval) => interval switch
    {
        RollingInterval.Hourly => new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0, DateTimeKind.Utc),
        RollingInterval.Daily => new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc),
        RollingInterval.Weekly => StartOfWeek(utcNow),
        RollingInterval.Monthly => new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc),
        _ => DateTime.MinValue
    };

    private static DateTime StartOfWeek(DateTime utcNow)
    {
        int diff = ((int)utcNow.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(-diff);
    }
}
