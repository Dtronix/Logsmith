using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Logsmith.Generator.Models;

internal sealed class LogMethodInfo
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
    public GeneratorMode Mode { get; }
    public bool IsStandaloneMode => Mode == GeneratorMode.Standalone;
    public string ConditionalLevel { get; }
    public Location MethodLocation { get; }
    public string AccessModifier { get; }
    public int SampleRate { get; }
    public int MaxPerSecond { get; }

    /// <summary>
    /// The namespace for abstraction mode public types (e.g., "Acme.Networking.Logging").
    /// Only meaningful when Mode == Abstraction.
    /// </summary>
    public string AbstractionNamespace { get; }

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
        GeneratorMode mode,
        string conditionalLevel,
        Location methodLocation,
        string accessModifier = "",
        int sampleRate = 0,
        int maxPerSecond = 0,
        string abstractionNamespace = "")
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
        Mode = mode;
        ConditionalLevel = conditionalLevel;
        MethodLocation = methodLocation;
        AccessModifier = accessModifier;
        SampleRate = sampleRate;
        MaxPerSecond = maxPerSecond;
        AbstractionNamespace = abstractionNamespace;
    }
}
