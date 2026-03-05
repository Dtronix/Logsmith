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
                EmitParameterWrite(sb, param, part.FormatSpecifier);
            }
        }

        return sb.ToString();
    }

    private static void EmitParameterWrite(StringBuilder sb, ParameterInfo param, string? formatSpecifier)
    {
        string writeExpr = GetWriteExpression(param, param.Name, formatSpecifier);
        string nullWriteExpr = "writer.Write(\"null\"u8);";

        if (param.IsNullableValueType)
        {
            sb.AppendLine($"            if ({param.Name}.HasValue)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                {GetWriteExpression(param, param.Name + ".Value", formatSpecifier)}");
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

    private static string GetWriteExpression(ParameterInfo param, string accessor, string? formatSpecifier)
    {
        // :json format specifier — serialize to UTF-8 bytes via JsonSerializer
        if (formatSpecifier == "json")
        {
            return $"writer.Write(global::System.Text.Json.JsonSerializer.SerializeToUtf8Bytes({accessor}));";
        }

        if (param.TypeFullName == "global::System.String" || param.TypeFullName == "string")
        {
            return $"writer.WriteString({accessor});";
        }

        if (param.TypeFullName == "global::System.Boolean" || param.TypeFullName == "bool")
        {
            return $"writer.Write({accessor} ? \"true\"u8 : \"false\"u8);";
        }

        // Standard format specifier
        if (formatSpecifier != null)
        {
            if (param.SerializationKind == SerializationKind.Utf8SpanFormattable)
                return $"writer.WriteFormatted({accessor}, \"{EscapeUtf8Literal(formatSpecifier)}\");";

            return $"writer.WriteString({accessor}.ToString(\"{EscapeUtf8Literal(formatSpecifier)}\"));";
        }

        if (param.SerializationKind == SerializationKind.Utf8SpanFormattable)
            return $"writer.WriteFormatted({accessor});";

        return $"writer.WriteString({accessor}.ToString());";
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
