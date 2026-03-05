namespace Logsmith.Internal;

internal sealed class SinkSet
{
    internal readonly ILogSink[] TextSinks;
    internal readonly IStructuredLogSink[] StructuredSinks;
    internal readonly ILogSink[] AllSinks;

    internal SinkSet(ILogSink[] textSinks, IStructuredLogSink[] structuredSinks, ILogSink[] allSinks)
    {
        TextSinks = textSinks;
        StructuredSinks = structuredSinks;
        AllSinks = allSinks;
    }

    internal static SinkSet Classify(List<ILogSink> sinks)
    {
        var textSinks = new ILogSink[sinks.Count];
        var structuredList = new List<IStructuredLogSink>();

        for (int i = 0; i < sinks.Count; i++)
        {
            textSinks[i] = sinks[i];
            if (sinks[i] is IStructuredLogSink structured)
            {
                structuredList.Add(structured);
            }
        }

        return new SinkSet(textSinks, structuredList.ToArray(), sinks.ToArray());
    }

    internal async ValueTask DisposeSinksAsync()
    {
        foreach (var sink in AllSinks)
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
