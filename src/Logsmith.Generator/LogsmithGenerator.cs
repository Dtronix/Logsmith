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
    private static readonly string[] LogLevelNames =
    {
        "Trace", "Debug", "Information", "Warning", "Error", "Critical", "None"
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all methods decorated with [LogMessage] using syntax-only matching.
        // This avoids requiring the attribute type to be in the compilation at provider
        // evaluation time, which is essential for standalone mode where the attribute
        // is emitted later via RegisterSourceOutput.
        var methodProvider = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => IsLogMessageCandidate(node),
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

    /// <summary>
    /// Syntax-only predicate: checks if a node is a method with a [LogMessage] attribute.
    /// No semantic resolution needed — matches on attribute name syntax only.
    /// </summary>
    private static bool IsLogMessageCandidate(SyntaxNode node)
    {
        if (node is not MethodDeclarationSyntax method)
            return false;

        foreach (var attrList in method.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = GetSimpleAttributeName(attr.Name);
                if (name == "LogMessage" || name == "LogMessageAttribute")
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts the simple name from an attribute name syntax, handling qualified names.
    /// </summary>
    private static string GetSimpleAttributeName(NameSyntax nameSyntax)
    {
        return nameSyntax switch
        {
            SimpleNameSyntax simple => simple.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.Text,
            _ => ""
        };
    }

    private static (LogMethodInfo? Method, IReadOnlyList<Diagnostic> Diagnostics)? ExtractMethodInfo(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var methodSyntax = (MethodDeclarationSyntax)ctx.Node;
        var semanticModel = ctx.SemanticModel;
        var diagnostics = new List<Diagnostic>();

        // Find the LogMessage attribute in the syntax tree
        var logMessageAttr = FindLogMessageAttribute(methodSyntax);
        if (logMessageAttr == null)
            return null;

        // Get method symbol for semantic analysis
        var methodSymbol = semanticModel.GetDeclaredSymbol(methodSyntax, ct);
        if (methodSymbol == null)
            return null;

        var compilation = semanticModel.Compilation;

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

        // Extract attribute data from syntax (works even when the attribute type
        // is not yet in the compilation, i.e. standalone mode)
        ExtractAttributeDataFromSyntax(logMessageAttr,
            out int level, out string message, out int eventId, out bool alwaysEmit);

        // Classify parameters (uses semantic model for BCL types like Exception, CallerInfo)
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

        // Category — extract from syntax (works in standalone mode)
        string category = containingType.Name;
        if (methodSyntax.Parent is TypeDeclarationSyntax classDecl)
        {
            category = ExtractCategoryFromSyntax(classDecl) ?? containingType.Name;
        }

        // Namespace
        string containingNamespace = containingType.ContainingNamespace?.IsGlobalNamespace == true
            ? ""
            : containingType.ContainingNamespace?.ToDisplayString() ?? "";

        // Read conditional level from MSBuild (will be combined later)
        string conditionalLevel = "";

        // Accessibility
        string accessModifier = methodSymbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public ",
            Accessibility.Internal => "internal ",
            Accessibility.Protected => "protected ",
            Accessibility.ProtectedOrInternal => "protected internal ",
            Accessibility.ProtectedAndInternal => "private protected ",
            _ => ""
        };

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
            methodLocation: methodSyntax.GetLocation(),
            accessModifier: accessModifier);

        return (methodInfo, diagnostics);
    }

    /// <summary>
    /// Finds the [LogMessage] attribute syntax on a method declaration.
    /// </summary>
    private static AttributeSyntax? FindLogMessageAttribute(MethodDeclarationSyntax method)
    {
        foreach (var attrList in method.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = GetSimpleAttributeName(attr.Name);
                if (name == "LogMessage" || name == "LogMessageAttribute")
                    return attr;
            }
        }
        return null;
    }

    /// <summary>
    /// Extracts attribute constructor and named arguments purely from syntax.
    /// </summary>
    private static void ExtractAttributeDataFromSyntax(
        AttributeSyntax attr,
        out int level,
        out string message,
        out int eventId,
        out bool alwaysEmit)
    {
        level = 2; // Default: Information
        message = "";
        eventId = 0;
        alwaysEmit = false;

        var args = attr.ArgumentList?.Arguments;
        if (args == null || args.Value.Count == 0)
            return;

        int positionalIndex = 0;
        foreach (var arg in args.Value)
        {
            if (arg.NameEquals != null)
            {
                // Named argument: EventId = 42, AlwaysEmit = true
                string argName = arg.NameEquals.Name.Identifier.Text;
                if (argName == "EventId")
                    eventId = ExtractIntLiteral(arg.Expression);
                else if (argName == "AlwaysEmit")
                    alwaysEmit = ExtractBoolLiteral(arg.Expression);
            }
            else if (arg.NameColon != null)
            {
                // Named positional: level: LogLevel.Information
                string argName = arg.NameColon.Name.Identifier.Text;
                if (argName == "level")
                    level = ExtractLogLevel(arg.Expression);
                else if (argName == "message")
                    message = ExtractStringLiteral(arg.Expression);
            }
            else
            {
                // Positional argument
                if (positionalIndex == 0)
                    level = ExtractLogLevel(arg.Expression);
                else if (positionalIndex == 1)
                    message = ExtractStringLiteral(arg.Expression);
                positionalIndex++;
            }
        }
    }

    /// <summary>
    /// Extracts a LogLevel ordinal from a member access expression like LogLevel.Information.
    /// </summary>
    private static int ExtractLogLevel(ExpressionSyntax expr)
    {
        string memberName;
        if (expr is MemberAccessExpressionSyntax memberAccess)
            memberName = memberAccess.Name.Identifier.Text;
        else if (expr is IdentifierNameSyntax identifier)
            memberName = identifier.Identifier.Text;
        else
            return 2; // Default: Information

        for (int i = 0; i < LogLevelNames.Length; i++)
        {
            if (LogLevelNames[i] == memberName)
                return i;
        }
        return 2;
    }

    private static string ExtractStringLiteral(ExpressionSyntax expr)
    {
        if (expr is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            return literal.Token.ValueText;
        return "";
    }

    private static int ExtractIntLiteral(ExpressionSyntax expr)
    {
        if (expr is LiteralExpressionSyntax literal && literal.Token.Value is int value)
            return value;
        return 0;
    }

    private static bool ExtractBoolLiteral(ExpressionSyntax expr)
    {
        if (expr.IsKind(SyntaxKind.TrueLiteralExpression))
            return true;
        return false;
    }

    /// <summary>
    /// Extracts the category name from a [LogCategory("...")] attribute on a class, using syntax.
    /// Returns null if no LogCategory attribute is found.
    /// </summary>
    private static string? ExtractCategoryFromSyntax(TypeDeclarationSyntax classDecl)
    {
        foreach (var attrList in classDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = GetSimpleAttributeName(attr.Name);
                if (name == "LogCategory" || name == "LogCategoryAttribute")
                {
                    var args = attr.ArgumentList?.Arguments;
                    if (args != null && args.Value.Count > 0)
                        return ExtractStringLiteral(args.Value[0].Expression);
                }
            }
        }
        return null;
    }
}
