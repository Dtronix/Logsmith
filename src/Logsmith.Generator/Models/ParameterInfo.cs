namespace Logsmith.Generator.Models;

public sealed class ParameterInfo
{
    public string Name { get; }
    public string TypeFullName { get; }
    public ParameterKind Kind { get; }
    public bool IsNullableValueType { get; }
    public bool IsNullableReferenceType { get; }
    public bool HasDefaultValue { get; }
    public object? DefaultValue { get; }

    public ParameterInfo(
        string name,
        string typeFullName,
        ParameterKind kind,
        bool isNullableValueType,
        bool isNullableReferenceType,
        bool hasDefaultValue,
        object? defaultValue)
    {
        Name = name;
        TypeFullName = typeFullName;
        Kind = kind;
        IsNullableValueType = isNullableValueType;
        IsNullableReferenceType = isNullableReferenceType;
        HasDefaultValue = hasDefaultValue;
        DefaultValue = defaultValue;
    }
}
