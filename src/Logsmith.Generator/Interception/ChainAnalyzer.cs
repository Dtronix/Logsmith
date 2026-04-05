using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

#pragma warning disable RSEXPERIMENTAL002

namespace Logsmith.Generator.Interception;

/// <summary>
/// Analyzes ILogger call chains by walking backwards from terminal calls through
/// the MemberAccessExpression syntax tree.
/// </summary>
internal static class ChainAnalyzer
{
    private static readonly string[] TerminalMethods =
    {
        "Trace", "Debug", "Information", "Warning", "Error", "Critical"
    };

    private static readonly string[] ChainMethods =
    {
        "When", "Sampled", "RateLimited", "Tagged"
    };

    private static readonly string[] LogLevelNames =
    {
        "Trace", "Debug", "Information", "Warning", "Error", "Critical"
    };

    /// <summary>
    /// Quick syntactic check: is this an InvocationExpression calling a chain method
    /// (When, Sampled, RateLimited, Tagged) that might be stored in a variable?
    /// </summary>
    internal static bool IsChainBreakCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax inv)
            return false;

        if (inv.Expression is not MemberAccessExpressionSyntax member)
            return false;

        string name = member.Name.Identifier.Text;
        for (int i = 0; i < ChainMethods.Length; i++)
        {
            if (ChainMethods[i] == name)
            {
                // Check if the result is NOT used fluently (i.e., parent is not a MemberAccess)
                var parent = inv.Parent;
                if (parent is MemberAccessExpressionSyntax)
                    return false; // Part of a fluent chain — fine

                return true; // Stored in variable, passed as argument, etc.
            }
        }

        return false;
    }

    /// <summary>
    /// Semantic check: confirms the broken chain call is actually on ILogger and returns
    /// the diagnostic location + method name. Returns null if not an ILogger method.
    /// </summary>
    internal static (Location Location, string MethodName)? DetectBrokenChain(
        GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var inv = (InvocationExpressionSyntax)ctx.Node;
        var member = (MemberAccessExpressionSyntax)inv.Expression;
        string methodName = member.Name.Identifier.Text;

        var symbolInfo = ctx.SemanticModel.GetSymbolInfo(inv, ct);
        var methodSymbol = symbolInfo.Symbol as IMethodSymbol;
        if (methodSymbol == null || !IsILoggerMethod(methodSymbol))
            return null;

        return (inv.GetLocation(), methodName);
    }

    /// <summary>
    /// Quick syntactic check: is this an InvocationExpression calling a terminal method
    /// on what could be an ILogger?
    /// </summary>
    internal static bool IsTerminalCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax inv)
            return false;

        if (inv.Expression is not MemberAccessExpressionSyntax member)
            return false;

        string name = member.Name.Identifier.Text;
        for (int i = 0; i < TerminalMethods.Length; i++)
        {
            if (TerminalMethods[i] == name)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Analyzes a terminal call site and builds a complete InterceptorChain.
    /// Returns null if the receiver is not ILogger or the location is not interceptable.
    /// </summary>
    internal static InterceptorChain? AnalyzeTerminalCall(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        var inv = (InvocationExpressionSyntax)ctx.Node;
        var member = (MemberAccessExpressionSyntax)inv.Expression;
        string terminalName = member.Name.Identifier.Text;

        // Resolve the method symbol to confirm it's on ILogger
        var symbolInfo = ctx.SemanticModel.GetSymbolInfo(inv, ct);
        var methodSymbol = symbolInfo.Symbol as IMethodSymbol;
        if (methodSymbol == null)
            return null;

        if (!IsILoggerMethod(methodSymbol))
            return null;

        // Determine terminal level
        int terminalLevel = GetLogLevel(terminalName);
        if (terminalLevel < 0) return null;

        // Determine overload shape
        bool hasExceptionParam = false;
        bool hasHandlerParam = false;
        foreach (var param in methodSymbol.Parameters)
        {
            if (param.Type.Name == "Exception")
                hasExceptionParam = true;
            if (param.RefKind == RefKind.Ref && param.Type.Name.StartsWith("Log") && param.Type.Name.EndsWith("Handler"))
                hasHandlerParam = true;
        }

        // Extract caller info from the terminal call site
        var callerInfo = ExtractCallerInfo(inv);

        // Compute event ID from interpolated string template literals
        int eventId = 0;
        if (hasHandlerParam)
        {
            eventId = ComputeEventIdFromArgs(inv);
        }

        // Walk backwards to detect chain
        var chainSteps = new List<ChainStepInfo>();
        WalkChainBackwards(member.Expression, ctx.SemanticModel, ct, chainSteps);

        // Get the interceptable location for the terminal call
        var terminalLocation = ctx.SemanticModel.GetInterceptableLocation(inv, ct);
        if (terminalLocation is null)
            return null;

        if (chainSteps.Count == 0)
        {
            // Direct terminal call — no chain
            var directSite = new InterceptorCallSite(
                terminalLocation,
                terminalName,
                InterceptorCallKind.DirectTerminal,
                terminalLevel,
                eventId,
                hasExceptionParam,
                hasHandlerParam,
                callerInfo.FilePath,
                callerInfo.Line,
                callerInfo.Member);

            return new InterceptorChain(
                new[] { directSite },
                carrierShapeId: null,
                terminalLogLevel: terminalLevel);
        }
        else
        {
            // Chain detected — build all steps
            var steps = new List<InterceptorCallSite>();

            // Reverse so chain is root-first
            chainSteps.Reverse();

            // Build shape ID from chain method names
            string shapeId = string.Join("_", chainSteps.Select(s => s.MethodName));

            for (int i = 0; i < chainSteps.Count; i++)
            {
                var step = chainSteps[i];
                var kind = i == 0 ? InterceptorCallKind.ChainStart : InterceptorCallKind.ChainIntermediate;

                steps.Add(new InterceptorCallSite(
                    step.Location,
                    step.MethodName,
                    kind,
                    logLevel: terminalLevel,
                    eventId: 0,
                    hasExceptionParam: false,
                    hasHandlerParam: false,
                    callerFilePath: callerInfo.FilePath,
                    callerLine: callerInfo.Line,
                    callerMember: callerInfo.Member,
                    chainPosition: i));
            }

            // Add terminal
            steps.Add(new InterceptorCallSite(
                terminalLocation,
                terminalName,
                InterceptorCallKind.ChainTerminal,
                terminalLevel,
                eventId,
                hasExceptionParam,
                hasHandlerParam,
                callerInfo.FilePath,
                callerInfo.Line,
                callerInfo.Member,
                chainPosition: chainSteps.Count));

            return new InterceptorChain(steps, shapeId, terminalLevel);
        }
    }

    /// <summary>
    /// Walks backwards from a terminal call's receiver expression to detect chain calls.
    /// </summary>
    private static void WalkChainBackwards(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken ct,
        List<ChainStepInfo> steps)
    {
        // The expression is what appears before .Debug(...)
        // If it's another invocation like .Tagged("SQL"), walk further
        if (expression is not InvocationExpressionSyntax chainInv)
            return;

        if (chainInv.Expression is not MemberAccessExpressionSyntax chainMember)
            return;

        string methodName = chainMember.Name.Identifier.Text;

        // Check if this is a known chain method
        bool isChainMethod = false;
        for (int i = 0; i < ChainMethods.Length; i++)
        {
            if (ChainMethods[i] == methodName)
            {
                isChainMethod = true;
                break;
            }
        }

        if (!isChainMethod)
            return;

        // Verify it's actually an ILogger method
        var symbolInfo = semanticModel.GetSymbolInfo(chainInv, ct);
        var methodSymbol = symbolInfo.Symbol as IMethodSymbol;
        if (methodSymbol == null || !IsILoggerMethod(methodSymbol))
            return;

        // Get interceptable location for this chain step
        var location = semanticModel.GetInterceptableLocation(chainInv, ct);
        if (location is null)
            return;

        steps.Add(new ChainStepInfo(methodName, location));

        // Continue walking backwards
        WalkChainBackwards(chainMember.Expression, semanticModel, ct, steps);
    }

    /// <summary>
    /// Checks if a method symbol belongs to the Logsmith ILogger interface.
    /// </summary>
    private static bool IsILoggerMethod(IMethodSymbol method)
    {
        // Check containing type
        var containingType = method.ContainingType;
        if (containingType == null)
            return false;

        // Direct check: method is defined on ILogger
        if (containingType.Name == "ILogger" &&
            containingType.ContainingNamespace?.ToDisplayString() == "Logsmith")
            return true;

        // Check if the method is an explicit or implicit implementation of ILogger
        if (containingType.TypeKind == TypeKind.Interface)
            return false;

        // For concrete types, check if they implement ILogger
        foreach (var iface in containingType.AllInterfaces)
        {
            if (iface.Name == "ILogger" &&
                iface.ContainingNamespace?.ToDisplayString() == "Logsmith")
                return true;
        }

        return false;
    }

    /// <summary>
    /// Extracts caller file path, line number, and enclosing member name from a syntax node.
    /// </summary>
    private static (string FilePath, int Line, string Member) ExtractCallerInfo(
        InvocationExpressionSyntax invocation)
    {
        var location = invocation.GetLocation();
        var lineSpan = location.GetMappedLineSpan();
        string filePath = lineSpan.Path ?? "";
        int line = lineSpan.StartLinePosition.Line + 1; // 1-based

        // Find enclosing method/property
        string member = "";
        SyntaxNode? current = invocation.Parent;
        while (current != null)
        {
            if (current is MethodDeclarationSyntax methodDecl)
            {
                member = methodDecl.Identifier.Text;
                break;
            }
            if (current is PropertyDeclarationSyntax propDecl)
            {
                member = propDecl.Identifier.Text;
                break;
            }
            if (current is ConstructorDeclarationSyntax ctorDecl)
            {
                member = ".ctor";
                break;
            }
            if (current is LocalFunctionStatementSyntax localFunc)
            {
                member = localFunc.Identifier.Text;
                break;
            }
            current = current.Parent;
        }

        return (filePath, line, member);
    }

    /// <summary>
    /// Computes a stable event ID from the interpolated string template literals.
    /// Uses FNV-1a hash of the literal parts concatenated.
    /// </summary>
    private static int ComputeEventIdFromArgs(InvocationExpressionSyntax invocation)
    {
        // Find the interpolated string argument
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is InterpolatedStringExpressionSyntax interpolated)
            {
                return ComputeEventIdFromInterpolated(interpolated);
            }
        }

        return 0;
    }

    internal static int ComputeEventIdFromInterpolated(InterpolatedStringExpressionSyntax interpolated)
    {
        // Extract literal parts and hash them
        var literals = new List<string>();
        foreach (var content in interpolated.Contents)
        {
            if (content is InterpolatedStringTextSyntax text)
                literals.Add(text.TextToken.ValueText);
        }

        if (literals.Count == 0)
            return 0;

        return Fnv1aHash(string.Join("\0", literals));
    }

    private static int GetLogLevel(string methodName)
    {
        for (int i = 0; i < LogLevelNames.Length; i++)
        {
            if (LogLevelNames[i] == methodName)
                return i;
        }
        return -1;
    }

    private static int Fnv1aHash(string input)
    {
        unchecked
        {
            const uint offsetBasis = 2166136261u;
            const uint prime = 16777619u;

            uint hash = offsetBasis;
            for (int i = 0; i < input.Length; i++)
            {
                hash ^= input[i];
                hash *= prime;
            }
            return (int)hash;
        }
    }

    private sealed class ChainStepInfo
    {
        public string MethodName { get; }
        public InterceptableLocation Location { get; }

        public ChainStepInfo(string methodName, InterceptableLocation location)
        {
            MethodName = methodName;
            Location = location;
        }
    }
}

#pragma warning restore RSEXPERIMENTAL002
