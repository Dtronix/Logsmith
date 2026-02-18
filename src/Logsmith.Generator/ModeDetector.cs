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
        var logLevelType = compilation.GetTypeByMetadataName("Logsmith.LogLevel");
        if (logLevelType == null)
            return false;

        return logLevelType.ContainingAssembly.Name == "Logsmith";
    }
}
