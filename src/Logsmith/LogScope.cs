using System.Text.Json;

namespace Logsmith;

public sealed class LogScope : IDisposable
{
    private static readonly AsyncLocal<LogScope?> _current = new();

    public static LogScope? Current
    {
        get => _current.Value;
        private set => _current.Value = value;
    }

    private readonly LogScope? _parent;
    private readonly string _key;
    private readonly string _value;

    private LogScope(string key, string value, LogScope? parent)
    {
        _key = key;
        _value = value;
        _parent = parent;
    }

    public static LogScope Push(string key, string value)
    {
        var scope = new LogScope(key, value, Current);
        Current = scope;
        return scope;
    }

    public static LogScope Push(ReadOnlySpan<KeyValuePair<string, string>> properties)
    {
        LogScope? first = null;
        foreach (var kvp in properties)
        {
            var scope = new LogScope(kvp.Key, kvp.Value, Current);
            Current = scope;
            first ??= scope;
        }

        // Return a wrapper that restores the parent of the first pushed scope
        return first ?? Push("", "");
    }

    public void Dispose()
    {
        // Walk back to this scope's parent, discarding everything pushed after it
        Current = _parent;
    }

    public static ScopeEnumerator EnumerateProperties() => new(Current);

    public ref struct ScopeEnumerator
    {
        private LogScope? _current;
        private KeyValuePair<string, string> _entry;

        internal ScopeEnumerator(LogScope? start)
        {
            _current = start;
            _entry = default;
        }

        public bool MoveNext()
        {
            if (_current is null)
                return false;

            _entry = new KeyValuePair<string, string>(_current._key, _current._value);
            _current = _current._parent;
            return true;
        }

        public KeyValuePair<string, string> Current => _entry;
    }

    internal static void WriteScopeToUtf8(ref Utf8LogWriter writer)
    {
        var scope = Current;
        if (scope is null) return;

        var enumerator = EnumerateProperties();
        while (enumerator.MoveNext())
        {
            var prop = enumerator.Current;
            if (prop.Key.Length == 0) continue;
            writer.Write(" ["u8);
            writer.WriteString(prop.Key);
            writer.Write("="u8);
            writer.WriteString(prop.Value);
            writer.Write("]"u8);
        }
    }

    internal static void WriteToJson(Utf8JsonWriter writer)
    {
        var scope = Current;
        if (scope is null) return;

        var enumerator = EnumerateProperties();
        while (enumerator.MoveNext())
        {
            var prop = enumerator.Current;
            if (prop.Key.Length == 0) continue;
            writer.WriteString(prop.Key, prop.Value);
        }
    }
}
