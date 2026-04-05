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
    /// Public types (LogLevel, DispatchInfo, etc.) keep public visibility.
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
    /// These are types consumers need to implement ILogsmithLogger or
    /// that must be public for interface inheritance consistency.
    /// </summary>
    private static readonly HashSet<string> AbstractionPublicTypeNames = new HashSet<string>
    {
        "LogLevel",
        "DispatchInfo",
        "ILogSink",
        "ILogStructurable",
        // IFlushableLogSink extends ILogSink — must stay public for
        // BufferedLogSink's interface implementation consistency
        "IFlushableLogSink",
    };

    /// <summary>
    /// Checks if a source file contains types that should remain public in abstraction mode.
    /// </summary>
    private static bool IsPublicAbstractionFile(string source)
    {
        foreach (var typeName in AbstractionPublicTypeNames)
        {
            foreach (var pattern in TypeDeclarationPatterns)
            {
                // For delegate patterns, the return type sits between "public delegate "
                // and the type name (e.g., "public delegate void WriteProperties<TState>").
                // All other patterns are immediately followed by the type name.
                if (pattern == "public delegate ")
                {
                    // Match: "public delegate" ... typeName on the same line
                    int idx = 0;
                    while ((idx = source.IndexOf(pattern, idx)) >= 0)
                    {
                        int lineEnd = source.IndexOf('\n', idx);
                        if (lineEnd < 0) lineEnd = source.Length;
                        string line = source.Substring(idx, lineEnd - idx);
                        // Ensure the type name follows the delegate keyword+return type
                        // by checking it appears as a word boundary (space before it)
                        if (line.Contains(" " + typeName))
                            return true;
                        idx += pattern.Length;
                    }
                }
                else
                {
                    if (source.Contains(pattern + typeName))
                        return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Rewrites all Logsmith namespace references to the target namespace.
    /// Uses generic prefix matching so new sub-namespaces are handled automatically.
    /// </summary>
    internal static string RewriteAllNamespaces(string source, string targetNs)
    {
        var result = source;

        // Replace sub-namespace references first (longer prefix) to avoid partial matches.
        // Generic: any "Logsmith." prefix becomes "{targetNs}." — covers all sub-namespaces
        // (Sinks, Formatting, Internal, DynamicLevel, Attributes, and any future additions).

        // Using directives: "using Logsmith.Sinks;" → "using {targetNs}.Sinks;"
        result = result.Replace("using Logsmith.", $"using {targetNs}.");

        // Root using: "using Logsmith;" → "using {targetNs};"
        result = result.Replace("using Logsmith;", $"using {targetNs};");

        // Namespace declarations (file-scoped and block-scoped)
        result = ReplaceNamespaceDeclarations(result, "Logsmith", targetNs);

        return result;
    }

    /// <summary>
    /// Replaces namespace declarations (both file-scoped and block-scoped).
    /// Uses generic prefix matching for sub-namespaces.
    /// </summary>
    private static string ReplaceNamespaceDeclarations(string source, string sourceNs, string targetNs)
    {
        var result = source;

        // Sub-namespaces first (longer match) to avoid partial replacement.
        // "namespace Logsmith.Sinks;" → "namespace {targetNs}.Sinks;" etc.
        result = result.Replace($"namespace {sourceNs}.", $"namespace {targetNs}.");

        // Root namespace (file-scoped): "namespace Logsmith;" → "namespace {targetNs};"
        result = result.Replace($"namespace {sourceNs};", $"namespace {targetNs};");

        // Block-scoped root: "namespace Logsmith\n{" → "namespace {targetNs}\n{"
        result = result.Replace($"namespace {sourceNs}\r\n", $"namespace {targetNs}\r\n");
        result = result.Replace($"namespace {sourceNs}\n", $"namespace {targetNs}\n");

        return result;
    }
}
