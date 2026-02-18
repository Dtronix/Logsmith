using System.Collections.Generic;
using System.Text;
using Logsmith.Generator.Models;

namespace Logsmith.Generator.Emission;

internal static class StructuredPathEmitter
{
    internal static string EmitWritePropertiesMethod(LogMethodInfo method)
    {
        var sb = new StringBuilder();
        var stateTypeName = GetStateTypeName(method);

        // Build format specifier lookup from template parts
        var formatSpecifiers = new Dictionary<string, string>();
        foreach (var part in method.TemplateParts)
        {
            if (part.IsPlaceholder && part.BoundParameter != null && part.FormatSpecifier != null)
            {
                formatSpecifiers[part.BoundParameter.Name] = part.FormatSpecifier;
            }
        }

        sb.AppendLine($"        private static void WriteProperties_{method.MethodName}(global::System.Text.Json.Utf8JsonWriter writer, {stateTypeName} state)");
        sb.AppendLine("        {");

        foreach (var param in method.Parameters)
        {
            if (param.Kind != ParameterKind.MessageParam)
                continue;

            formatSpecifiers.TryGetValue(param.Name, out var formatSpec);
            EmitPropertyWrite(sb, param, formatSpec);
        }

        sb.AppendLine("        }");
        return sb.ToString();
    }

    private static void EmitPropertyWrite(StringBuilder sb, ParameterInfo param, string formatSpecifier)
    {
        string accessor = $"state.{param.Name}";

        if (param.IsNullableValueType)
        {
            sb.AppendLine($"            if ({accessor}.HasValue)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                {GetStructuredWriteExpression(param.Name, accessor + ".Value", param, formatSpecifier)}");
            sb.AppendLine($"            }}");
            sb.AppendLine($"            else");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                writer.WriteNull(\"{param.Name}\");");
            sb.AppendLine($"            }}");
        }
        else if (param.IsNullableReferenceType)
        {
            sb.AppendLine($"            if ({accessor} is not null)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                {GetStructuredWriteExpression(param.Name, accessor, param, formatSpecifier)}");
            sb.AppendLine($"            }}");
            sb.AppendLine($"            else");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                writer.WriteNull(\"{param.Name}\");");
            sb.AppendLine($"            }}");
        }
        else
        {
            sb.AppendLine($"            {GetStructuredWriteExpression(param.Name, accessor, param, formatSpecifier)}");
        }
    }

    private static string GetStructuredWriteExpression(string propertyName, string accessor, ParameterInfo param, string formatSpecifier)
    {
        if (formatSpecifier == "json")
        {
            return $"writer.WritePropertyName(\"{propertyName}\");\n                global::System.Text.Json.JsonSerializer.Serialize(writer, {accessor});";
        }

        if (formatSpecifier != null)
        {
            return $"writer.WriteString(\"{propertyName}\", {accessor}.ToString(\"{formatSpecifier}\"));";
        }

        if (param.TypeFullName == "global::System.String" || param.TypeFullName == "string")
        {
            return $"writer.WriteString(\"{propertyName}\", {accessor});";
        }

        return $"writer.WriteString(\"{propertyName}\", {accessor}.ToString());";
    }

    internal static string EmitPropertyWrite(ParameterInfo param, bool isStructurable)
    {
        var sb = new StringBuilder();
        string accessor = param.Name;

        if (isStructurable)
        {
            sb.AppendLine($"            writer.WritePropertyName(\"{param.Name}\");");
            sb.AppendLine($"            {accessor}.WriteStructured(writer);");
        }
        else
        {
            sb.AppendLine($"            writer.WriteString(\"{param.Name}\", {accessor}.ToString());");
        }

        return sb.ToString();
    }

    private static string GetStateTypeName(LogMethodInfo method)
    {
        return $"{method.MethodName}State";
    }
}
