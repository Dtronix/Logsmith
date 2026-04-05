using System.Collections.Generic;

namespace Logsmith.Generator.Interception;

/// <summary>
/// Represents a complete chain of ILogger calls to be intercepted together.
/// For direct calls, Steps contains a single DirectTerminal entry.
/// For chains, Steps contains ChainStart + optional intermediates + ChainTerminal.
/// </summary>
internal sealed class InterceptorChain
{
    /// <summary>
    /// All call sites in this chain, ordered from root (When/first method) to terminal (Debug/etc.).
    /// For direct calls, this contains a single entry.
    /// </summary>
    public IReadOnlyList<InterceptorCallSite> Steps { get; }

    /// <summary>
    /// Identifies the carrier shape. Null for direct calls (no carrier needed).
    /// Built from the sequence of chain method names, e.g., "When_Tagged" or "When_Sampled_Tagged".
    /// </summary>
    public string? CarrierShapeId { get; }

    /// <summary>
    /// The terminal log level for the chain (used for early-exit in chain start).
    /// </summary>
    public int TerminalLogLevel { get; }

    public InterceptorChain(
        IReadOnlyList<InterceptorCallSite> steps,
        string? carrierShapeId,
        int terminalLogLevel)
    {
        Steps = steps;
        CarrierShapeId = carrierShapeId;
        TerminalLogLevel = terminalLogLevel;
    }
}
