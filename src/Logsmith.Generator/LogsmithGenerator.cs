using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Logsmith.Generator.Diagnostics;
using Logsmith.Generator.Emission;
using Logsmith.Generator.Models;
using Logsmith.Generator.Parsing;

namespace Logsmith.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class LogsmithGenerator : IIncrementalGenerator
{
    private const string LogMessageAttributeFullName = "Logsmith.LogMessageAttribute";
    private const string LogCategoryAttributeFullName = "Logsmith.LogCategoryAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all methods decorated with [LogMessage]
        var methodProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            LogMessageAttributeFullName,
            predicate: static (node, _) => node is MethodDeclarationSyntax,
            transform: static (ctx, ct) => ExtractMethodInfo(ctx, ct))
            .Where(static m => m != null)!;

        // Get conditional level from MSBuild property
        var conditionalLevelProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
            {
                provider.GlobalOptions.TryGetValue(
                    "build_property.LogsmithConditionalLevel", out var value);
                return value;
            });

        // Combine method info with conditional level
        var combined = methodProvider.Combine(conditionalLevelProvider);

        // Register source output
        context.RegisterSourceOutput(combined.Collect(), static (ctx, methodsAndLevel) =>
        {
            var grouped = new Dictionary<string, List<LogMethodInfo>>();

            foreach (var (methodResult, conditionalLevel) in methodsAndLevel)
            {
                if (methodResult == null)
                    continue;

                var (method, diagnostics) = methodResult.Value;

                // Report diagnostics
                foreach (var diag in diagnostics)
                    ctx.ReportDiagnostic(diag);

                if (method == null)
                    continue;

                // Group by containing class
                string key = $"{method.ContainingNamespace}.{method.ContainingClassName}";
                if (!grouped.TryGetValue(key, out var list))
                {
                    list = new List<LogMethodInfo>();
                    grouped[key] = list;
                }
                list.Add(method);
            }

            // Emit per-class files
            foreach (var kvp in grouped)
            {
                var first = kvp.Value[0];
                var source = MethodEmitter.EmitClassFile(
                    first.ContainingNamespace,
                    first.ContainingClassName,
                    kvp.Value);

                string hintName = $"{kvp.Key}.g.cs";
                ctx.AddSource(hintName, source);
            }
        });

        // Standalone mode: emit embedded sources
        var modeProvider = context.CompilationProvider
            .Select(static (compilation, _) => ModeDetector.IsSharedMode(compilation));

        context.RegisterSourceOutput(modeProvider, static (ctx, isSharedMode) =>
        {
            if (!isSharedMode)
            {
                EmbeddedSourceEmitter.EmitEmbeddedSources(ctx);
            }
        });
    }

    private static (LogMethodInfo? Method, IReadOnlyList<Diagnostic> Diagnostics)? ExtractMethodInfo(
        GeneratorAttributeSyntaxContext ctx,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var methodSymbol = (IMethodSymbol)ctx.TargetSymbol;
        var methodSyntax = (MethodDeclarationSyntax)ctx.TargetNode;
        var compilation = ctx.SemanticModel.Compilation;
        var diagnostics = new List<Diagnostic>();

        // LSMITH003: validate static partial in partial class
        if (!methodSymbol.IsStatic || !methodSymbol.IsPartialDefinition)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.LSMITH003,
                methodSyntax.GetLocation(),
                methodSymbol.Name));
            return (null, diagnostics);
        }

        // Check containing type is partial
        var containingType = methodSymbol.ContainingType;
        bool isContainingTypePartial = false;
        foreach (var syntaxRef in containingType.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax(ct) is TypeDeclarationSyntax typeDecl &&
                typeDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                isContainingTypePartial = true;
                break;
            }
        }

        if (!isContainingTypePartial)
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticDescriptors.LSMITH003,
                methodSyntax.GetLocation(),
                methodSymbol.Name));
            return (null, diagnostics);
        }

        // Extract attribute data
        var attrData = ctx.Attributes.First();
        int level = 0;
        string message = "";
        int eventId = 0;
        bool alwaysEmit = false;

        if (attrData.ConstructorArguments.Length > 0)
            level = (int)(byte)attrData.ConstructorArguments[0].Value!;
        if (attrData.ConstructorArguments.Length > 1)
            message = (string)attrData.ConstructorArguments[1].Value! ?? "";

        foreach (var named in attrData.NamedArguments)
        {
            if (named.Key == "EventId")
                eventId = (int)named.Value.Value!;
            else if (named.Key == "AlwaysEmit")
                alwaysEmit = (bool)named.Value.Value!;
        }

        // Classify parameters
        var parameters = ParameterClassifier.Classify(methodSymbol, compilation);

        // Parse/bind template
        IReadOnlyList<TemplatePart> templateParts;
        string? templateString;
        bool isTemplateFree = string.IsNullOrEmpty(message);

        if (isTemplateFree)
        {
            templateString = null;
            var messageParams = parameters.Where(p => p.Kind == ParameterKind.MessageParam).ToList();
            templateParts = TemplateParser.GenerateTemplateFree(methodSymbol.Name, messageParams);
        }
        else
        {
            templateString = message;
            templateParts = TemplateParser.Parse(message);
            var bindDiags = TemplateParser.Bind(templateParts, parameters, methodSyntax.GetLocation());
            diagnostics.AddRange(bindDiags);
        }

        // EventId
        int resolvedEventId = EventIdGenerator.Generate(
            containingType.Name, methodSymbol.Name, eventId);

        // Mode detection
        bool isStandaloneMode = !ModeDetector.IsSharedMode(compilation);

        // Has explicit sink?
        bool hasExplicitSink = parameters.Count > 0 && parameters[0].Kind == ParameterKind.Sink;

        // Category
        string category = containingType.Name;
        foreach (var attr in containingType.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() == LogCategoryAttributeFullName)
            {
                if (attr.ConstructorArguments.Length > 0)
                    category = (string)attr.ConstructorArguments[0].Value! ?? category;
                break;
            }
        }

        // Namespace
        string containingNamespace = containingType.ContainingNamespace?.IsGlobalNamespace == true
            ? ""
            : containingType.ContainingNamespace?.ToDisplayString() ?? "";

        // Read conditional level from MSBuild (will be combined later)
        string conditionalLevel = "";

        var methodInfo = new LogMethodInfo(
            containingNamespace: containingNamespace,
            containingClassName: containingType.Name,
            methodName: methodSymbol.Name,
            category: category,
            level: level,
            eventId: resolvedEventId,
            alwaysEmit: alwaysEmit,
            templateString: templateString,
            parameters: parameters,
            templateParts: templateParts,
            hasExplicitSink: hasExplicitSink,
            isStandaloneMode: isStandaloneMode,
            conditionalLevel: conditionalLevel,
            methodLocation: methodSyntax.GetLocation());

        return (methodInfo, diagnostics);
    }
}
