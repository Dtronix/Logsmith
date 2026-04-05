# Workflow: feature-ilogger-rework

## Config
platform: github
remote: https://github.com/Dtronix/Logsmith.git
base-branch: master

## State
phase: REVIEW
status: active
issue: discussion
pr:
session: 3
phases-total: 7
phases-complete: 7

## Problem Statement
Logsmith v2 adds a new ILogger API alongside the existing [LogMessage] attribute pattern — it does NOT replace it. Both APIs share the same underlying sink/dispatch system. The new ILogger API provides an ergonomic alternative using C# interpolated strings with compile-time optimizations via source generator interceptors.

Key features of the new API:
- InterpolatedStringHandler with dual-buffer (UTF-8 text + JSON structured output)
- Three logging tiers: Direct (~3-5ns disabled), Chained (~8-15ns disabled), Static (0ns via [Conditional] stripping)
- Mutable hierarchical paths (PathNode/PathSegment pattern from NexNet)
- Scoped logging (struct-based, no AsyncLocal)
- Tags for orthogonal event classification
- Timed operations with correlation IDs
- DI integration alongside static LogManager factory
- Generator-optimized carrier pattern for fluent chains
- Two-stage source generator (Stage 1: types, Stage 2: interceptors)

Design is finalized in prototype/summary.md with all decisions resolved. Prototypes validated in prototype/ directory.

### Baseline Test Results
- Logsmith.Tests: 152 passed
- Logsmith.Generator.Tests: 106 passed
- Logsmith.Extensions.Logging.Tests: 8 passed
- Total: 266 passed, 0 failed, 0 skipped
- No pre-existing failures.

## Decisions
- 2026-04-04: ILogger API supplements (not replaces) existing [LogMessage] pattern. Both share the same sink system.
- 2026-04-04: Continue on existing feature/ilogger-rework branch with prototype history.
- 2026-04-04: **Dispatch consolidation** — LoggerContext becomes THE central dispatch hub. LogManager becomes factory + config holder. Both [LogMessage] and new ILogger API dispatch through LoggerContext.
- 2026-04-04: **Structured logging consolidation** — Sinks receive pre-built UTF-8 JSON bytes. No more WriteProperties callback pattern. Both APIs pre-build JSON.
- 2026-04-04: **Sink interface unification** — ILogSink and IStructuredLogSink merge into single `ILogSink.Write(in DispatchInfo)`. DispatchInfo carries text, json, level, category, path, tag, exception, caller info.
- 2026-04-04: **Replace LogEntry with DispatchInfo** — DispatchInfo (ref struct) becomes the single data carrier through the dispatch path.
- 2026-04-04: **Remove LogScope** — AsyncLocal-based ambient scoping removed entirely. Scoping is explicit via ILogger.Scoped() returning struct with path segment.
- 2026-04-04: **[LogMessage] adaptation** — Optional ILogger parameter + static LoggerContext field on class. [LogMessage] is for high-performance scenarios; ILogger.* is the primary API.
- 2026-04-04: **Caller info via interceptors** — Interceptors embed CallerFilePath/Line/Member at compile time into DispatchInfo.
- 2026-04-04: **Output format** — `[time LVL Category|Path #Tag] message`. Path appended to category with | separator, tag as #-prefixed suffix.
- 2026-04-04: **Phased implementation** — Multiple commit phases on one branch, each independently buildable/testable.

## Suspend State
- Current phase: IMPLEMENT, Phase 6/7 next
- In progress: Nothing — Phase 5 committed cleanly
- Immediate next step: Phase 6 — Generator Stage 2 interceptors + chain carriers
- WIP commit: None (clean tree)
- Test status: 366 passing (252 runtime + 106 generator + 8 MEL bridge), 0 failures
- Unrecorded context:
  - ILogger naming conflict with MEL resolved using `using Log = ...` aliases in samples and `Microsoft.Extensions.Logging.ILogger` explicit qualification in bridge/benchmark code
  - Generator now dispatches through LoggerContext (not LogManager) for standard mode; emits static `__loggerContext` field per class, inline JSON via Utf8JsonWriter
  - Default interface methods on ILogger require calling through interface type (e.g., `((ILogger)scope).Debug(...)` not `scope.Debug(...)` for structs)
  - NullLogger overrides IsEnabled (returns false), CreateChild (returns self), PathSegment (no-op set); chain methods use default interface impls which return `this` (NullLogger)
  - Phase 6 (interceptors) and Phase 7 (DI/MEL/cleanup) remain

## Session Log
| # | Phase Start | Phase End | Summary |
|---|------------|-----------|---------|
| 1 | INTAKE | IMPLEMENT | Bootstrapped workflow. Baseline: 266 tests. Completed DESIGN + PLAN + Phase 1 (core dispatch refactor). 260 tests passing. Suspended for context refresh. |
| 2 | IMPLEMENT | IMPLEMENT | Resumed. Completed Phases 2-5 (LoggerContext, ILogger, Handlers, Static Log + Generator). 366 tests passing. Suspended for context refresh. |
