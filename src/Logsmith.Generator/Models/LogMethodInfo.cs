using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Logsmith.Generator.Models;

public sealed class LogMethodInfo
{
    public string ContainingNamespace { get; }
    public IReadOnlyList<ContainingTypeInfo> ContainingTypeChain { get; }
    public string ContainingClassName => ContainingTypeChain[ContainingTypeChain.Count - 1].Name;
    public string QualifiedClassName => string.Join(".", ContainingTypeChain.Select(c => c.Name));
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
        IReadOnlyList<ContainingTypeInfo> containingTypeChain,
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
        ContainingTypeChain = containingTypeChain;
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
