namespace Logsmith;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class LogCategoryAttribute : Attribute
{
    public string Name { get; }

    public LogCategoryAttribute(string name)
    {
        Name = name;
    }
}
