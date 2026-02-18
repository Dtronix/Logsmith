namespace Logsmith.Internal;

internal sealed class SinkSet
{
    internal readonly ILogSink[] TextSinks;
    internal readonly IStructuredLogSink[] StructuredSinks;

    internal SinkSet(ILogSink[] textSinks, IStructuredLogSink[] structuredSinks)
    {
        TextSinks = textSinks;
        StructuredSinks = structuredSinks;
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

        return new SinkSet(textSinks, structuredList.ToArray());
    }
}
