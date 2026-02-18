using Logsmith.Generator.Models;

namespace Logsmith.Generator.Emission;

internal static class NullableEmitter
{
    internal static string EmitNullGuard(
        ParameterInfo param,
        string writeExpression,
        string nullExpression)
    {
        if (param.IsNullableValueType)
        {
            return $@"if ({param.Name}.HasValue)
            {{
                {writeExpression}
            }}
            else
            {{
                {nullExpression}
            }}";
        }

        if (param.IsNullableReferenceType)
        {
            return $@"if ({param.Name} is not null)
            {{
                {writeExpression}
            }}
            else
            {{
                {nullExpression}
            }}";
        }

        // Not nullable — just emit the write expression directly
        return writeExpression;
    }
}
