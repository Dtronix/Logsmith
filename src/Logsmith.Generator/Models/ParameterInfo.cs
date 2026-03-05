using Logsmith.Generator.Emission;

namespace Logsmith.Generator.Models;

internal sealed class ParameterInfo
{
    public string Name { get; }
    public string TypeFullName { get; }
    public ParameterKind Kind { get; }
    public bool IsNullableValueType { get; }
    public bool IsNullableReferenceType { get; }
    public bool HasDefaultValue { get; }
    public object? DefaultValue { get; }
    public string RefKind { get; }
    public SerializationKind SerializationKind { get; }

    public ParameterInfo(
        string name,
        string typeFullName,
        ParameterKind kind,
        bool isNullableValueType,
        bool isNullableReferenceType,
        bool hasDefaultValue,
        object? defaultValue,
        string refKind = "",
        SerializationKind serializationKind = SerializationKind.Utf8SpanFormattable)
    {
        Name = name;
        TypeFullName = typeFullName;
        Kind = kind;
        IsNullableValueType = isNullableValueType;
        IsNullableReferenceType = isNullableReferenceType;
        HasDefaultValue = hasDefaultValue;
        DefaultValue = defaultValue;
        RefKind = refKind;
        SerializationKind = serializationKind;
    }
}
