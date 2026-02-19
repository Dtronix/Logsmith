namespace Logsmith.Generator.Models;

public sealed class ContainingTypeInfo(string name, string modifiers)
{
    public string Name { get; } = name;
    public string Modifiers { get; } = modifiers;
}
