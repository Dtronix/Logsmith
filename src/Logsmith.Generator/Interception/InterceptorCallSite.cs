using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

#pragma warning disable RSEXPERIMENTAL002

namespace Logsmith.Generator.Interception;

/// <summary>
/// Represents a single ILogger method call that should be intercepted.
/// </summary>
internal sealed class InterceptorCallSite
{
    public InterceptableLocation Location { get; }
    public string MethodName { get; }
    public InterceptorCallKind Kind { get; }

    // Terminal call info
    public int LogLevel { get; }
    public int EventId { get; }
    public bool HasExceptionParam { get; }
    public bool HasHandlerParam { get; }

    // Caller info (baked in at generation time)
    public string CallerFilePath { get; }
    public int CallerLine { get; }
    public string CallerMember { get; }

    // Chain info (only for chain members)
    public int ChainPosition { get; }

    public InterceptorCallSite(
        InterceptableLocation location,
        string methodName,
        InterceptorCallKind kind,
        int logLevel,
        int eventId,
        bool hasExceptionParam,
        bool hasHandlerParam,
        string callerFilePath,
        int callerLine,
        string callerMember,
        int chainPosition = 0)
    {
        Location = location;
        MethodName = methodName;
        Kind = kind;
        LogLevel = logLevel;
        EventId = eventId;
        HasExceptionParam = hasExceptionParam;
        HasHandlerParam = hasHandlerParam;
        CallerFilePath = callerFilePath;
        CallerLine = callerLine;
        CallerMember = callerMember;
        ChainPosition = chainPosition;
    }
}

internal enum InterceptorCallKind
{
    /// <summary>Direct terminal call: logger.Debug($"...")</summary>
    DirectTerminal,
    /// <summary>Chain start: logger.When(cond) — first call in a chain</summary>
    ChainStart,
    /// <summary>Chain intermediate: .Tagged("SQL"), .Sampled(100) in the middle of a chain</summary>
    ChainIntermediate,
    /// <summary>Chain terminal: .Debug($"...") at the end of a chain</summary>
    ChainTerminal,
}

#pragma warning restore RSEXPERIMENTAL002
