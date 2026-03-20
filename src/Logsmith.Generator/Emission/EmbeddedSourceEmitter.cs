using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Logsmith.Generator.Emission;

internal static class EmbeddedSourceEmitter
{
    /// <summary>
    /// Reads all embedded .cs resources from the generator assembly,
    /// performs visibility replacement, and adds to context.
    /// Standalone mode: all types become internal.
    /// </summary>
    internal static void EmitEmbeddedSources(SourceProductionContext context)
    {
        var assembly = typeof(EmbeddedSourceEmitter).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        foreach (var name in resourceNames)
        {
            if (!name.EndsWith(".cs"))
                continue;

            // Skip abstraction sources in standalone mode
            if (name.Contains("AbstractionSources"))
                continue;

            using var stream = assembly.GetManifestResourceStream(name);
            if (stream == null)
                continue;

            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd();
            var transformed = ReplaceVisibility(source);

            var hintName = name.Replace('\\', '.').Replace('/', '.');
            context.AddSource(hintName, transformed);
        }
    }

    /// <summary>
    /// Abstraction mode: emits all sources with namespace rewriting.
    /// Public types (LogLevel, LogEntry, etc.) keep public visibility.
    /// Internal types (LogManager, sinks, etc.) become internal.
    /// All types share the same target namespace so cross-references work.
    /// </summary>
    internal static void EmitEmbeddedSources(SourceProductionContext context, GeneratorMode mode, string abstractionNamespace)
    {
        if (mode != GeneratorMode.Abstraction)
        {
            EmitEmbeddedSources(context);
            return;
        }

        var assembly = typeof(EmbeddedSourceEmitter).Assembly;
        var resourceNames = assembly.GetManifestResourceNames();

        foreach (var name in resourceNames)
        {
            if (!name.EndsWith(".cs"))
                continue;

            using var stream = assembly.GetManifestResourceStream(name);
            if (stream == null)
                continue;

            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd();

            bool isAbstractionSource = name.Contains("AbstractionSources");
            bool shouldBePublic = isAbstractionSource || IsPublicAbstractionFile(source);

            string transformed;
            if (shouldBePublic)
            {
                // Keep public visibility, rewrite namespace
                transformed = RewriteAllNamespaces(source, abstractionNamespace);
            }
            else
            {
                // Make internal, rewrite namespace
                transformed = ReplaceVisibility(source);
                transformed = RewriteAllNamespaces(transformed, abstractionNamespace);
            }

            var hintName = name.Replace('\\', '.').Replace('/', '.');
            context.AddSource(hintName, transformed);
        }
    }

    /// <summary>
    /// Type declaration patterns where "public" should be replaced with "internal".
    /// Member-level "public" is kept because interface implementations must remain public.
    /// </summary>
    private static readonly string[] TypeDeclarationPatterns = new[]
    {
        "public enum ",
        "public interface ",
        "public readonly record struct ",
        "public readonly struct ",
        "public ref struct ",
        "public record struct ",
        "public record class ",
        "public record ",
        "public struct ",
        "public sealed class ",
        "public abstract class ",
        "public static class ",
        "public class ",
        "public delegate ",
    };

    /// <summary>
    /// Replaces public type declarations with internal and protected members
    /// with private protected, keeping public member implementations intact
    /// (required for interface implementation).
    /// </summary>
    internal static string ReplaceVisibility(string source)
    {
        var result = source;

        // Replace type declarations: public → internal
        foreach (var pattern in TypeDeclarationPatterns)
        {
            var replacement = "internal " + pattern.Substring("public ".Length);
            result = result.Replace(pattern, replacement);
        }

        // Replace "protected internal" → "internal"
        result = result.Replace("protected internal ", "internal ");

        // Replace "protected" → "private protected" on members of internal types
        // to fix inconsistent accessibility (CS0053/CS0051)
        result = result.Replace("protected ", "private protected ");

        return result;
    }

    /// <summary>
    /// Types that should remain public in abstraction mode.
    /// </summary>
    private static readonly HashSet<string> AbstractionPublicTypeNames = new HashSet<string>
    {
        "LogLevel",
        "LogEntry",
        "LogScope",
        "ScopeEnumerator",
        "WriteProperties",
        "ILogSink",
        "IStructuredLogSink",
        "ILogStructurable",
        "IFlushableLogSink",
    };

    /// <summary>
    /// Checks if a source file contains types that should remain public in abstraction mode.
    /// Handles delegates with return types (e.g., "public delegate void WriteProperties").
    /// </summary>
    private static bool IsPublicAbstractionFile(string source)
    {
        foreach (var typeName in AbstractionPublicTypeNames)
        {
            // Direct match: "public class LogLevel", "public enum LogLevel", etc.
            foreach (var pattern in TypeDeclarationPatterns)
            {
                if (source.Contains(pattern + typeName))
                    return true;
            }

            // Delegate with return type: "public delegate void WriteProperties"
            if (source.Contains("public delegate ") && source.Contains(" " + typeName))
            {
                // More precise check: ensure the type name appears after "public delegate <returntype> "
                int delegateIdx = source.IndexOf("public delegate ");
                while (delegateIdx >= 0)
                {
                    int lineEnd = source.IndexOf('\n', delegateIdx);
                    if (lineEnd < 0) lineEnd = source.Length;
                    string line = source.Substring(delegateIdx, lineEnd - delegateIdx);
                    if (line.Contains(typeName))
                        return true;
                    delegateIdx = source.IndexOf("public delegate ", delegateIdx + 1);
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Rewrites all Logsmith namespace references to the target namespace.
    /// Handles file-scoped declarations, block-scoped declarations, using directives,
    /// and fully-qualified references in generated code.
    /// </summary>
    internal static string RewriteAllNamespaces(string source, string targetNs)
    {
        var result = source;

        // File-scoped namespace: "namespace Logsmith;" or "namespace Logsmith.Sinks;"
        result = ReplaceNamespaceDeclarations(result, "Logsmith", targetNs);

        // Fully-qualified references in source: "Logsmith.LogLevel", "Logsmith.LogManager", etc.
        // Handle using directives: "using Logsmith;" → "using TargetNs;"
        result = result.Replace("using Logsmith.Sinks;", $"using {targetNs}.Sinks;");
        result = result.Replace("using Logsmith.Formatting;", $"using {targetNs}.Formatting;");
        result = result.Replace("using Logsmith.Internal;", $"using {targetNs}.Internal;");
        result = result.Replace("using Logsmith.DynamicLevel;", $"using {targetNs}.DynamicLevel;");
        result = result.Replace("using Logsmith.Attributes;", $"using {targetNs}.Attributes;");
        result = result.Replace("using Logsmith;", $"using {targetNs};");

        return result;
    }

    /// <summary>
    /// Replaces namespace declarations (both file-scoped and block-scoped).
    /// Handles sub-namespaces: "Logsmith.Sinks" → "Target.Sinks"
    /// </summary>
    private static string ReplaceNamespaceDeclarations(string source, string sourceNs, string targetNs)
    {
        var result = source;

        // Must replace sub-namespaces first (longer matches) to avoid partial replacement
        // e.g., "namespace Logsmith.Sinks;" before "namespace Logsmith;"

        // File-scoped sub-namespaces
        result = result.Replace($"namespace {sourceNs}.Sinks;", $"namespace {targetNs}.Sinks;");
        result = result.Replace($"namespace {sourceNs}.Formatting;", $"namespace {targetNs}.Formatting;");
        result = result.Replace($"namespace {sourceNs}.Internal;", $"namespace {targetNs}.Internal;");
        result = result.Replace($"namespace {sourceNs}.DynamicLevel;", $"namespace {targetNs}.DynamicLevel;");
        result = result.Replace($"namespace {sourceNs}.Attributes;", $"namespace {targetNs}.Attributes;");

        // Root namespace (file-scoped)
        result = result.Replace($"namespace {sourceNs};", $"namespace {targetNs};");

        // Block-scoped variants
        result = result.Replace($"namespace {sourceNs}.Sinks\r\n", $"namespace {targetNs}.Sinks\r\n");
        result = result.Replace($"namespace {sourceNs}.Sinks\n", $"namespace {targetNs}.Sinks\n");
        result = result.Replace($"namespace {sourceNs}\r\n", $"namespace {targetNs}\r\n");
        result = result.Replace($"namespace {sourceNs}\n", $"namespace {targetNs}\n");

        return result;
    }
}
