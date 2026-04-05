using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Logsmith.Generator.Tests;

/// <summary>
/// Verifies that [Conditional] stripping works correctly for Log.Trace and Log.Debug
/// static methods. When LOGSMITH_TRACE / LOGSMITH_DEBUG symbols are absent, the C#
/// compiler omits call sites entirely, achieving zero cost.
/// </summary>
[TestFixture]
public class ConditionalStrippingTests
{
    private const string TraceSource = """
        using Logsmith;
        using Logsmith.Handlers;
        namespace TestNs;
        public static class TestClass
        {
            public static void Call(ILogger logger)
            {
                var x = 42;
                Log.Trace(logger, $"trace value is {x}");
            }
        }
        """;

    private const string TraceExceptionSource = """
        using Logsmith;
        using Logsmith.Handlers;
        namespace TestNs;
        public static class TestClass
        {
            public static void Call(ILogger logger)
            {
                var ex = new System.Exception("err");
                var x = 42;
                Log.Trace(logger, ex, $"trace error {x}");
            }
        }
        """;

    private const string DebugSource = """
        using Logsmith;
        using Logsmith.Handlers;
        namespace TestNs;
        public static class TestClass
        {
            public static void Call(ILogger logger)
            {
                var x = 42;
                Log.Debug(logger, $"debug value is {x}");
            }
        }
        """;

    private const string DebugExceptionSource = """
        using Logsmith;
        using Logsmith.Handlers;
        namespace TestNs;
        public static class TestClass
        {
            public static void Call(ILogger logger)
            {
                var ex = new System.Exception("err");
                var x = 42;
                Log.Debug(logger, ex, $"debug error {x}");
            }
        }
        """;

    private const string NonConditionalSource = """
        using Logsmith;
        using Logsmith.Handlers;
        namespace TestNs;
        public static class TestClass
        {
            public static void CallInfo(ILogger logger)
            {
                var x = 42;
                Log.Information(logger, $"info value is {x}");
            }
            public static void CallWarn(ILogger logger)
            {
                var x = 42;
                Log.Warning(logger, $"warn value is {x}");
            }
            public static void CallError(ILogger logger)
            {
                var x = 42;
                Log.Error(logger, $"error value is {x}");
            }
            public static void CallCritical(ILogger logger)
            {
                var x = 42;
                Log.Critical(logger, $"critical value is {x}");
            }
        }
        """;

    // ── Trace stripping ────────────────────────────────────────────────

    [Test]
    public void Trace_StrippedWithoutSymbol()
    {
        var refs = GetLogMemberReferences(TraceSource);
        Assert.That(refs, Is.Empty,
            "Log.Trace call site should be stripped when LOGSMITH_TRACE is not defined");
    }

    [Test]
    public void Trace_PresentWithSymbol()
    {
        var refs = GetLogMemberReferences(TraceSource, "LOGSMITH_TRACE");
        Assert.That(refs, Does.Contain("Trace"),
            "Log.Trace call site should be present when LOGSMITH_TRACE is defined");
    }

    [Test]
    public void TraceExceptionOverload_StrippedWithoutSymbol()
    {
        var refs = GetLogMemberReferences(TraceExceptionSource);
        Assert.That(refs, Is.Empty,
            "Log.Trace exception overload should be stripped when LOGSMITH_TRACE is not defined");
    }

    [Test]
    public void TraceExceptionOverload_PresentWithSymbol()
    {
        var refs = GetLogMemberReferences(TraceExceptionSource, "LOGSMITH_TRACE");
        Assert.That(refs, Does.Contain("Trace"),
            "Log.Trace exception overload should be present when LOGSMITH_TRACE is defined");
    }

    // ── Debug stripping ────────────────────────────────────────────────

    [Test]
    public void Debug_StrippedWithoutSymbol()
    {
        var refs = GetLogMemberReferences(DebugSource);
        Assert.That(refs, Is.Empty,
            "Log.Debug call site should be stripped when LOGSMITH_DEBUG is not defined");
    }

    [Test]
    public void Debug_PresentWithSymbol()
    {
        var refs = GetLogMemberReferences(DebugSource, "LOGSMITH_DEBUG");
        Assert.That(refs, Does.Contain("Debug"),
            "Log.Debug call site should be present when LOGSMITH_DEBUG is defined");
    }

    [Test]
    public void DebugExceptionOverload_StrippedWithoutSymbol()
    {
        var refs = GetLogMemberReferences(DebugExceptionSource);
        Assert.That(refs, Is.Empty,
            "Log.Debug exception overload should be stripped when LOGSMITH_DEBUG is not defined");
    }

    [Test]
    public void DebugExceptionOverload_PresentWithSymbol()
    {
        var refs = GetLogMemberReferences(DebugExceptionSource, "LOGSMITH_DEBUG");
        Assert.That(refs, Does.Contain("Debug"),
            "Log.Debug exception overload should be present when LOGSMITH_DEBUG is defined");
    }

    // ── Non-conditional levels always present ──────────────────────────

    [Test]
    public void Information_AlwaysPresent()
    {
        var refs = GetLogMemberReferences(NonConditionalSource);
        Assert.That(refs, Does.Contain("Information"),
            "Log.Information should always be present regardless of symbols");
    }

    [Test]
    public void Warning_AlwaysPresent()
    {
        var refs = GetLogMemberReferences(NonConditionalSource);
        Assert.That(refs, Does.Contain("Warning"),
            "Log.Warning should always be present regardless of symbols");
    }

    [Test]
    public void Error_AlwaysPresent()
    {
        var refs = GetLogMemberReferences(NonConditionalSource);
        Assert.That(refs, Does.Contain("Error"),
            "Log.Error should always be present regardless of symbols");
    }

    [Test]
    public void Critical_AlwaysPresent()
    {
        var refs = GetLogMemberReferences(NonConditionalSource);
        Assert.That(refs, Does.Contain("Critical"),
            "Log.Critical should always be present regardless of symbols");
    }

    /// <summary>
    /// Compiles the source with optional preprocessor symbols, emits to IL,
    /// and returns the set of method names referenced on Logsmith.Log.
    /// </summary>
    private static HashSet<string> GetLogMemberReferences(string source, params string[] symbols)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest,
            preprocessorSymbols: symbols);
        var compilation = GeneratorTestHelper.CreateCompilation(parseOptions, source);

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);
        Assert.That(emitResult.Success, Is.True,
            () => string.Join("\n", emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString())));

        stream.Position = 0;
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();

        var logMethodNames = new HashSet<string>();
        foreach (var handle in reader.MemberReferences)
        {
            var memberRef = reader.GetMemberReference(handle);
            var name = reader.GetString(memberRef.Name);

            if (memberRef.Parent.Kind == HandleKind.TypeReference)
            {
                var typeRef = reader.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
                var typeName = reader.GetString(typeRef.Name);
                var ns = reader.GetString(typeRef.Namespace);
                if (typeName == "Log" && ns == "Logsmith")
                    logMethodNames.Add(name);
            }
        }

        return logMethodNames;
    }
}
