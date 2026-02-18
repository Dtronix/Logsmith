using System.Text.Json;

namespace Logsmith;

public interface ILogStructurable
{
    void WriteStructured(Utf8JsonWriter writer);
}
