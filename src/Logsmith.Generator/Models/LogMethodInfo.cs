using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Logsmith.Generator.Models;

public sealed class LogMethodInfo
{
    public string ContainingNamespace { get; }
    public string ContainingClassName { get; }
    public string MethodName { get; }
    public string Category { get; }
    public int Level { get; }
    public int EventId { get; }
    public bool AlwaysEmit { get; }
    public string? TemplateString { get; }
    public IReadOnlyList<ParameterInfo> Parameters { get; }
    public IReadOnlyList<TemplatePart> TemplateParts { get; }
    public bool HasExplicitSink { get; }
    public bool IsStandaloneMode { get; }
    public string ConditionalLevel { get; }
    public Location MethodLocation { get; }
    public string AccessModifier { get; }

    public LogMethodInfo(
        string containingNamespace,
        string containingClassName,
        string methodName,
        string category,
        int level,
        int eventId,
        bool alwaysEmit,
        string? templateString,
        IReadOnlyList<ParameterInfo> parameters,
        IReadOnlyList<TemplatePart> templateParts,
        bool hasExplicitSink,
        bool isStandaloneMode,
        string conditionalLevel,
        Location methodLocation,
        string accessModifier = "")
    {
        ContainingNamespace = containingNamespace;
        ContainingClassName = containingClassName;
        MethodName = methodName;
        Category = category;
        Level = level;
        EventId = eventId;
        AlwaysEmit = alwaysEmit;
        TemplateString = templateString;
        Parameters = parameters;
        TemplateParts = templateParts;
        HasExplicitSink = hasExplicitSink;
        IsStandaloneMode = isStandaloneMode;
        ConditionalLevel = conditionalLevel;
        MethodLocation = methodLocation;
        AccessModifier = accessModifier;
    }
}
