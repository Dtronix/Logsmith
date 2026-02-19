namespace Logsmith.Generator.Models;

public sealed class ContainingTypeInfo(string name, string modifiers, string keyword)
{
    public string Name { get; } = name;
    public string Modifiers { get; } = modifiers;

    /// <summary>
    /// The type keyword: "class", "struct", "record class", or "record struct".
    /// </summary>
    public string Keyword { get; } = keyword;
}
