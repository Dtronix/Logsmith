using Microsoft.CodeAnalysis;

namespace Logsmith.Generator.Diagnostics;

internal static class DiagnosticDescriptors
{
    internal static readonly DiagnosticDescriptor LSMITH001 = new(
        id: "LSMITH001",
        title: "Placeholder has no matching parameter",
        messageFormat: "Template placeholder '{0}' has no matching MessageParam parameter",
        category: "Logsmith",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor LSMITH002 = new(
        id: "LSMITH002",
        title: "Parameter unreferenced in template",
        messageFormat: "Parameter '{0}' is not referenced by any placeholder in the message template",
        category: "Logsmith",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor LSMITH003 = new(
        id: "LSMITH003",
        title: "Method must be static partial in partial class",
        messageFormat: "Log method '{0}' must be a static partial method in a partial class",
        category: "Logsmith",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor LSMITH004 = new(
        id: "LSMITH004",
        title: "No supported formatting path",
        messageFormat: "Parameter '{0}' of type '{1}' does not implement any supported formatting interface (IUtf8SpanFormattable, ISpanFormattable, IFormattable, or ToString override)",
        category: "Logsmith",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor LSMITH005 = new(
        id: "LSMITH005",
        title: "Caller info parameter name in template",
        messageFormat: "Caller info parameter '{0}' also appears as a placeholder in the template; it will be treated as a caller info parameter, not a message parameter",
        category: "Logsmith",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
