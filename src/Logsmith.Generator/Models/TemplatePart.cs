namespace Logsmith.Generator.Models;

public sealed class TemplatePart
{
    public bool IsPlaceholder { get; }
    public string Text { get; }
    public ParameterInfo? BoundParameter { get; internal set; }

    public TemplatePart(bool isPlaceholder, string text)
    {
        IsPlaceholder = isPlaceholder;
        Text = text;
    }
}
