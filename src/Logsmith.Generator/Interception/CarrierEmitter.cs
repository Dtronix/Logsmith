using System.Linq;
using System.Text;

namespace Logsmith.Generator.Interception;

/// <summary>
/// Emits per-chain-shape carrier types that implement ILogger.
/// Carriers accumulate chain state (tag, etc.) and are pooled via ThreadStatic.
/// </summary>
internal static class CarrierEmitter
{
    /// <summary>
    /// Emits a carrier class for a given chain shape.
    /// </summary>
    internal static string EmitCarrier(string shapeId, InterceptorChain exampleChain)
    {
        var sb = new StringBuilder();
        string className = $"LogCarrier_{shapeId}";

        // Determine what fields the carrier needs based on chain methods
        bool hasTag = exampleChain.Steps.Any(s => s.MethodName == "Tagged");

        sb.AppendLine($"    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        sb.AppendLine($"    internal sealed class {className} : global::Logsmith.ILogger");
        sb.AppendLine("    {");
        sb.AppendLine($"        [global::System.ThreadStatic] private static {className}? __pool;");
        sb.AppendLine();
        sb.AppendLine("        internal global::Logsmith.LoggerContext __context = null!;");

        // Chain state fields
        if (hasTag)
            sb.AppendLine("        internal string? __tag;");

        sb.AppendLine();
        sb.AppendLine($"        global::Logsmith.LoggerContext global::Logsmith.ILogger.Context => __context;");
        sb.AppendLine();

        // Rent method
        sb.AppendLine($"        internal static {className} Rent(global::Logsmith.LoggerContext context)");
        sb.AppendLine("        {");
        sb.AppendLine($"            var __c = __pool ?? new {className}();");
        sb.AppendLine("            __pool = null;");
        sb.AppendLine("            __c.__context = context;");
        sb.AppendLine("            return __c;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Return method
        sb.AppendLine("        internal void Return()");
        sb.AppendLine("        {");
        if (hasTag)
            sb.AppendLine("            __tag = null;");
        sb.AppendLine("            __context = null!;");
        sb.AppendLine($"            __pool = this;");
        sb.AppendLine("        }");

        sb.AppendLine("    }");

        return sb.ToString();
    }
}
