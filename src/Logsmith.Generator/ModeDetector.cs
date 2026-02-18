using Microsoft.CodeAnalysis;

namespace Logsmith.Generator;

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
}
