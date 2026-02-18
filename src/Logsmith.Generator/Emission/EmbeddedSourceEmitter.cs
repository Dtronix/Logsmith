using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Logsmith.Generator.Emission;

internal static class EmbeddedSourceEmitter
{
    private static readonly string[] VisibilityReplacements = new[]
    {
        "public enum ",
        "public interface ",
        "public readonly struct ",
        "public ref struct ",
        "public struct ",
        "public sealed class ",
        "public static class ",
        "public class ",
        "public delegate ",
    };

    /// <summary>
    /// Reads all embedded .cs resources from the generator assembly,
    /// performs visibility replacement, and adds to context.
    /// </summary>
    internal static void EmitEmbeddedSources(SourceProductionContext context)
    {
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
            var transformed = ReplaceVisibility(source);

            // Use the resource name as the hint name
            var hintName = name.Replace('\\', '.').Replace('/', '.');
            context.AddSource(hintName, transformed);
        }
    }

    /// <summary>
    /// Replaces public type declarations with internal.
    /// </summary>
    internal static string ReplaceVisibility(string source)
    {
        var result = source;
        foreach (var pattern in VisibilityReplacements)
        {
            var replacement = "internal " + pattern.Substring("public ".Length);
            result = result.Replace(pattern, replacement);
        }
        return result;
    }
}
