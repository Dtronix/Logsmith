namespace Logsmith.Generator.Models;

public sealed class TemplatePart
{
    public bool IsPlaceholder { get; }
    public string Text { get; }
    public string? FormatSpecifier { get; internal set; }
    public ParameterInfo? BoundParameter { get; internal set; }

    public TemplatePart(bool isPlaceholder, string text, string? formatSpecifier = null)
    {
        IsPlaceholder = isPlaceholder;
        Text = text;
        FormatSpecifier = formatSpecifier;
    }
}
