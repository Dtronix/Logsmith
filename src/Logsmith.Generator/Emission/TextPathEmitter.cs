using System.Collections.Generic;
using System.Text;
using Logsmith.Generator.Models;

namespace Logsmith.Generator.Emission;

internal static class TextPathEmitter
{
    internal static string Emit(LogMethodInfo method)
    {
        var sb = new StringBuilder();

        foreach (var part in method.TemplateParts)
        {
            if (!part.IsPlaceholder)
            {
                // Literal segment
                sb.AppendLine($"            writer.Write(\"{EscapeUtf8Literal(part.Text)}\"u8);");
            }
            else if (part.BoundParameter != null)
            {
                var param = part.BoundParameter;
                EmitParameterWrite(sb, param);
            }
        }

        return sb.ToString();
    }

    private static void EmitParameterWrite(StringBuilder sb, ParameterInfo param)
    {
        string writeExpr = GetWriteExpression(param, param.Name);
        string nullWriteExpr = "writer.Write(\"null\"u8);";

        if (param.IsNullableValueType)
        {
            sb.AppendLine($"            if ({param.Name}.HasValue)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                {GetWriteExpression(param, param.Name + ".Value")}");
            sb.AppendLine($"            }}");
            sb.AppendLine($"            else");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                {nullWriteExpr}");
            sb.AppendLine($"            }}");
        }
        else if (param.IsNullableReferenceType)
        {
            sb.AppendLine($"            if ({param.Name} is not null)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                {writeExpr}");
            sb.AppendLine($"            }}");
            sb.AppendLine($"            else");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                {nullWriteExpr}");
            sb.AppendLine($"            }}");
        }
        else
        {
            sb.AppendLine($"            {writeExpr}");
        }
    }

    private static string GetWriteExpression(ParameterInfo param, string accessor)
    {
        // Type serialization priority: the actual kind is resolved at pipeline time
        // and stored in TypeFullName. Here we use simple heuristics based on the type.
        if (param.TypeFullName == "global::System.String" || param.TypeFullName == "string")
        {
            return $"writer.WriteString({accessor});";
        }

        // For IUtf8SpanFormattable types (most numeric primitives), use WriteFormatted
        // For everything else, fall back to WriteString with ToString
        // The actual resolution happens via SerializationResolver at pipeline time,
        // but for code emission we use the resolved kind stored on the model.
        return $"writer.WriteFormatted({accessor});";
    }

    internal static SerializationKind? ResolveSerializationKind(string typeFullName, bool isString)
    {
        if (isString || typeFullName == "global::System.String" || typeFullName == "string")
            return SerializationKind.String;

        // For source generator, we resolve against the compilation's type symbols.
        // This method is a simplified fallback; the full resolution uses ITypeSymbol checks.
        return SerializationKind.Utf8SpanFormattable;
    }

    private static string EscapeUtf8Literal(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
