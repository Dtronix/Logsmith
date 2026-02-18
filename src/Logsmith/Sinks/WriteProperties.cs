using System.Text.Json;

namespace Logsmith;

public delegate void WriteProperties<TState>(
    Utf8JsonWriter writer,
    TState state)
    where TState : allows ref struct;
