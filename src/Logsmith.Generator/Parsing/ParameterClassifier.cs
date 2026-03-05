using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Logsmith.Generator.Emission;
using Logsmith.Generator.Models;

namespace Logsmith.Generator.Parsing;

internal static class ParameterClassifier
{
    internal static IReadOnlyList<ParameterInfo> Classify(
        IMethodSymbol method,
        Compilation compilation)
    {
        var utf8SpanFormattable = compilation.GetTypeByMetadataName("System.IUtf8SpanFormattable");
        var results = new List<ParameterInfo>(method.Parameters.Length);

        for (int i = 0; i < method.Parameters.Length; i++)
        {
            var param = method.Parameters[i];
            var kind = ClassifyParameter(param, compilation, i);

            bool isNullableValueType = param.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
            bool isNullableReferenceType = param.NullableAnnotation == NullableAnnotation.Annotated
                && !param.Type.IsValueType;

            var underlyingType = isNullableValueType
                ? ((INamedTypeSymbol)param.Type).TypeArguments[0]
                : param.Type;

            string typeFullName = underlyingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            string refKind = param.RefKind == RefKind.In ? "in " : "";

            var serializationKind = ResolveSerializationKind(underlyingType, typeFullName, utf8SpanFormattable);

            results.Add(new ParameterInfo(
                name: param.Name,
                typeFullName: typeFullName,
                kind: kind,
                isNullableValueType: isNullableValueType,
                isNullableReferenceType: isNullableReferenceType,
                hasDefaultValue: param.HasExplicitDefaultValue,
                defaultValue: param.HasExplicitDefaultValue ? param.ExplicitDefaultValue : null,
                refKind: refKind,
                serializationKind: serializationKind));
        }

        return results;
    }

    private static SerializationKind ResolveSerializationKind(
        ITypeSymbol type,
        string typeFullName,
        INamedTypeSymbol? utf8SpanFormattable)
    {
        if (typeFullName == "global::System.String" || typeFullName == "string")
            return SerializationKind.String;

        if (utf8SpanFormattable != null && IsOrImplements(type, utf8SpanFormattable))
            return SerializationKind.Utf8SpanFormattable;

        return SerializationKind.ToString;
    }

    private static ParameterKind ClassifyParameter(
        IParameterSymbol param,
        Compilation compilation,
        int index)
    {
        // 1. ILogSink check (only valid as first parameter)
        if (index == 0)
        {
            var logSinkType = compilation.GetTypeByMetadataName("Logsmith.ILogSink");
            if (logSinkType != null && IsOrImplements(param.Type, logSinkType))
                return ParameterKind.Sink;
        }

        // 2. Exception check
        var exceptionType = compilation.GetTypeByMetadataName("System.Exception");
        if (exceptionType != null && IsOrDerivesFrom(param.Type, exceptionType))
            return ParameterKind.Exception;

        // 3-5. Caller info attributes
        foreach (var attr in param.GetAttributes())
        {
            var attrName = attr.AttributeClass?.ToDisplayString();
            if (attrName == "System.Runtime.CompilerServices.CallerFilePathAttribute")
                return ParameterKind.CallerFile;
            if (attrName == "System.Runtime.CompilerServices.CallerLineNumberAttribute")
                return ParameterKind.CallerLine;
            if (attrName == "System.Runtime.CompilerServices.CallerMemberNameAttribute")
                return ParameterKind.CallerMember;
        }

        // 6. Default
        return ParameterKind.MessageParam;
    }

    private static bool IsOrImplements(ITypeSymbol type, ITypeSymbol interfaceType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, interfaceType))
            return true;
        foreach (var iface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, interfaceType))
                return true;
        }
        return false;
    }

    private static bool IsOrDerivesFrom(ITypeSymbol type, ITypeSymbol baseType)
    {
        var current = type;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
            current = current.BaseType;
        }
        return false;
    }
}
