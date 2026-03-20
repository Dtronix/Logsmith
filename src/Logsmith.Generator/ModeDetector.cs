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
    /// Parses the LogsmithMode MSBuild property string to a GeneratorMode enum.
    /// Null/empty defaults to Shared. Unrecognized values default to Shared.
    /// </summary>
    internal static GeneratorMode ParseMode(string? msbuildValue)
    {
        if (string.IsNullOrEmpty(msbuildValue))
            return GeneratorMode.Shared;

        if (string.Equals(msbuildValue, "Shared", System.StringComparison.OrdinalIgnoreCase))
            return GeneratorMode.Shared;

        if (string.Equals(msbuildValue, "Standalone", System.StringComparison.OrdinalIgnoreCase))
            return GeneratorMode.Standalone;

        if (string.Equals(msbuildValue, "Abstraction", System.StringComparison.OrdinalIgnoreCase))
            return GeneratorMode.Abstraction;

        return GeneratorMode.Shared;
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
