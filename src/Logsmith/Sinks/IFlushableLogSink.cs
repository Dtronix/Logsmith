namespace Logsmith;

public interface IFlushableLogSink : ILogSink
{
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}
