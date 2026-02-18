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

        sb.AppendLine($"        private static void WriteProperties_{method.MethodName}(global::System.Text.Json.Utf8JsonWriter writer, {stateTypeName} state)");
        sb.AppendLine("        {");

        foreach (var param in method.Parameters)
        {
            if (param.Kind != ParameterKind.MessageParam)
                continue;

            EmitPropertyWrite(sb, param);
        }

        sb.AppendLine("        }");
        return sb.ToString();
    }

    private static void EmitPropertyWrite(StringBuilder sb, ParameterInfo param)
    {
        string accessor = $"state.{param.Name}";

        if (param.IsNullableValueType)
        {
            sb.AppendLine($"            if ({accessor}.HasValue)");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                writer.WriteString(\"{param.Name}\", {accessor}.Value.ToString());");
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
            sb.AppendLine($"                writer.WriteString(\"{param.Name}\", {accessor}.ToString());");
            sb.AppendLine($"            }}");
            sb.AppendLine($"            else");
            sb.AppendLine($"            {{");
            sb.AppendLine($"                writer.WriteNull(\"{param.Name}\");");
            sb.AppendLine($"            }}");
        }
        else if (param.TypeFullName == "global::System.String" || param.TypeFullName == "string")
        {
            sb.AppendLine($"            writer.WriteString(\"{param.Name}\", {accessor});");
        }
        else
        {
            sb.AppendLine($"            writer.WriteString(\"{param.Name}\", {accessor}.ToString());");
        }
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
