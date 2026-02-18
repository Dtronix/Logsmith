using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Logsmith.Generator.Emission;

internal static class EmbeddedSourceEmitter
{
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
}
