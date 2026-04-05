namespace Logsmith.Internal;

internal sealed class SinkSet
{
    internal readonly ILogSink[] Sinks;

    internal SinkSet(ILogSink[] sinks)
    {
        Sinks = sinks;
    }

    internal static SinkSet Create(List<ILogSink> sinks)
    {
        return new SinkSet(sinks.ToArray());
    }

    internal async ValueTask DisposeSinksAsync()
    {
        foreach (var sink in Sinks)
        {
            try
            {
                if (sink is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                else
                    sink.Dispose();
            }
            catch
            {
                // Swallow — same pattern as DisposeMonitors()
            }
        }
    }
}
