using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Logsmith.Generator;

internal enum GeneratorMode
{
    Shared,
    Standalone,
    Abstraction
}

internal static class ModeDetector
{
    /// <summary>
    /// Returns true if Logsmith runtime types are present in the compilation
    /// from the Logsmith assembly (shared mode). False = standalone mode.
    /// </summary>
    internal static bool IsSharedMode(Compilation compilation)
    {
        // Building the Logsmith project itself — types are defined in source
        if (compilation.AssemblyName == "Logsmith")
            return true;

        // Check if Logsmith assembly is referenced (shared mode via ProjectReference or NuGet)
        foreach (var identity in compilation.ReferencedAssemblyNames)
        {
            if (identity.Name == "Logsmith")
                return true;
        }

        return false;
    }

    /// <summary>
    /// Detects the generator mode from MSBuild properties and compilation references.
    /// Priority: Abstraction (explicit) > Shared (Logsmith referenced) > Standalone (default).
    /// </summary>
    internal static GeneratorMode DetectMode(Compilation compilation, AnalyzerConfigOptions globalOptions)
    {
        if (globalOptions.TryGetValue("build_property.LogsmithAbstraction", out var abstractionValue)
            && string.Equals(abstractionValue, "true", System.StringComparison.OrdinalIgnoreCase))
        {
            return GeneratorMode.Abstraction;
        }

        return IsSharedMode(compilation) ? GeneratorMode.Shared : GeneratorMode.Standalone;
    }

    /// <summary>
    /// Resolves the namespace for abstraction mode public types.
    /// Uses LogsmithNamespace if set, otherwise {RootNamespace}.Logging.
    /// </summary>
    internal static string ResolveAbstractionNamespace(AnalyzerConfigOptions globalOptions)
    {
        if (globalOptions.TryGetValue("build_property.LogsmithNamespace", out var explicitNs)
            && !string.IsNullOrEmpty(explicitNs))
        {
            return explicitNs;
        }

        if (globalOptions.TryGetValue("build_property.RootNamespace", out var rootNs)
            && !string.IsNullOrEmpty(rootNs))
        {
            return rootNs + ".Logging";
        }

        return "Logging";
    }
}
