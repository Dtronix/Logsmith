using System;

namespace Logsmith.Generator;

internal static class ConditionalCompilation
{
    /// <summary>
    /// Determines if the method should receive [Conditional("DEBUG")].
    /// </summary>
    internal static bool ShouldApplyConditional(
        int methodLevel,
        int thresholdLevel,
        bool alwaysEmit)
    {
        if (alwaysEmit)
            return false;

        // Apply [Conditional("DEBUG")] if method level <= threshold level
        return methodLevel <= thresholdLevel;
    }

    /// <summary>
    /// Parses the MSBuild property string to a LogLevel ordinal.
    /// Returns 1 (Debug) if the value is null, empty, or unrecognized.
    /// </summary>
    internal static int ParseThreshold(string? msbuildValue)
    {
        if (string.IsNullOrEmpty(msbuildValue))
            return 1; // Debug

        switch (msbuildValue)
        {
            case "Trace": return 0;
            case "Debug": return 1;
            case "Information": return 2;
            case "Warning": return 3;
            case "Error": return 4;
            case "Critical": return 5;
            case "None": return 6;
            default: return 1; // Debug
        }
    }
}
