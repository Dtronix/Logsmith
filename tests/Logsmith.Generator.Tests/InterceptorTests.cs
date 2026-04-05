using Microsoft.CodeAnalysis;
using Logsmith.Generator.Interception;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Logsmith.Generator.Tests;

[TestFixture]
public class InterceptorTests
{
    [Test]
    public void DirectTerminalCall_EmitsInterceptor()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger)
                {
                    logger.Debug($"Processing {42} items");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null, "Interceptor source should be generated");
        Assert.That(generated, Does.Contain("InterceptsLocation"));
        Assert.That(generated, Does.Contain("__Debug_"));
        Assert.That(generated, Does.Contain("CallerFile"));
        Assert.That(generated, Does.Contain("CallerLine"));
        Assert.That(generated, Does.Contain("CallerMember"));
    }

    [Test]
    public void DirectTerminalCall_EmbedsCaller()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger)
                {
                    logger.Information($"Hello {123}");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null);
        Assert.That(generated, Does.Contain("CallerMember = \"DoWork\""));
    }

    [Test]
    public void DirectTerminalCall_ComputesEventId()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger)
                {
                    logger.Debug($"Processing {42} items");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null);
        // Event ID should be non-zero (FNV-1a of template literals)
        Assert.That(generated, Does.Contain("EventId ="));
        Assert.That(generated, Does.Not.Contain("EventId = 0,"));
    }

    [Test]
    public void DirectTerminalCall_DispatchesThroughContext()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger)
                {
                    logger.Debug($"Processing {42} items");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null);
        Assert.That(generated, Does.Contain("@this.Context.Dispatch(in __info)"));
    }

    [Test]
    public void DirectTerminalCall_HandlesExceptionOverload()
    {
        var source = """
            using Logsmith;
            using System;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger, Exception ex)
                {
                    logger.Error(ex, $"Failed {42}");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null);
        Assert.That(generated, Does.Contain("__Error_"));
        Assert.That(generated, Does.Contain("global::System.Exception? exception"));
    }

    [Test]
    public void DirectTerminalCall_StringOverload()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger)
                {
                    logger.Debug("simple message");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null);
        Assert.That(generated, Does.Contain("__Debug_"));
        Assert.That(generated, Does.Contain("string message"));
        Assert.That(generated, Does.Contain("Encoding.UTF8.GetBytes(message)"));
    }

    [Test]
    public void MultipleTerminalCalls_AllIntercepted()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger)
                {
                    logger.Debug($"Step 1: {1}");
                    logger.Information($"Step 2: {2}");
                    logger.Warning($"Step 3: {3}");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null);
        Assert.That(generated, Does.Contain("__Debug_"));
        Assert.That(generated, Does.Contain("__Information_"));
        Assert.That(generated, Does.Contain("__Warning_"));
    }

    [Test]
    public void AllLogLevels_Intercepted()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger)
                {
                    logger.Trace($"t {1}");
                    logger.Debug($"d {2}");
                    logger.Information($"i {3}");
                    logger.Warning($"w {4}");
                    logger.Error($"e {5}");
                    logger.Critical($"c {6}");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null);
        Assert.That(generated, Does.Contain("LogLevel.Trace"));
        Assert.That(generated, Does.Contain("LogLevel.Debug"));
        Assert.That(generated, Does.Contain("LogLevel.Information"));
        Assert.That(generated, Does.Contain("LogLevel.Warning"));
        Assert.That(generated, Does.Contain("LogLevel.Error"));
        Assert.That(generated, Does.Contain("LogLevel.Critical"));
    }

    [Test]
    public void ChainCall_WhenDebug_EmitsCarrier()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger, bool verbose)
                {
                    logger.When(verbose).Debug($"Verbose: {42}");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null);
        // Carrier type should be generated
        Assert.That(generated, Does.Contain("LogCarrier_When"));
        // Chain start should check condition and level
        Assert.That(generated, Does.Contain("__When_"));
        Assert.That(generated, Does.Contain("NullLogger.Instance"));
        // Terminal should extract from carrier
        Assert.That(generated, Does.Contain("__carrier.Return()"));
    }

    [Test]
    public void ChainCall_WhenTaggedDebug_EmitsCarrierWithTag()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger, bool verbose)
                {
                    logger.When(verbose).Tagged("SQL").Debug($"Query: {42}");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null);
        // Carrier with tag support
        Assert.That(generated, Does.Contain("LogCarrier_When_Tagged"));
        Assert.That(generated, Does.Contain("internal string? __tag"));
        // Tagged intermediate stores tag on carrier
        Assert.That(generated, Does.Contain("__Tagged_"));
        Assert.That(generated, Does.Contain("__c.__tag = tag"));
        // Terminal reads tag from carrier
        Assert.That(generated, Does.Contain("Tag = __tag"));
    }

    [Test]
    public void ChainCall_SampledDebug_EmitsSamplingCounter()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger)
                {
                    logger.Sampled(100).Debug($"Sampled: {42}");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null);
        Assert.That(generated, Does.Contain("__Sampled_"));
        Assert.That(generated, Does.Contain("__sampleCounter_"));
        Assert.That(generated, Does.Contain("Interlocked.Increment"));
    }

    [Test]
    public void ChainCall_TaggedDebug_EmitsCarrierAndTagged()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger)
                {
                    logger.Tagged("HTTP").Debug($"Request: {42}");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null);
        Assert.That(generated, Does.Contain("LogCarrier_Tagged"));
        Assert.That(generated, Does.Contain("__tag"));
    }

    [Test]
    public void InterceptorEmitsAttribute()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger)
                {
                    logger.Debug($"Test {1}");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null);
        // InterceptsLocationAttribute should be emitted as a file-scoped class
        Assert.That(generated, Does.Contain("sealed file class InterceptsLocationAttribute"));
        Assert.That(generated, Does.Contain("namespace System.Runtime.CompilerServices"));
    }

    [Test]
    public void NoILoggerCalls_NoInterceptorsGenerated()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Hello {name}")]
                static partial void Greet(string name);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var interceptorSource = result.GeneratedSources
            .FirstOrDefault(s => s.HintName == "LogInterceptors.g.cs");
        // No ILogger terminal calls → no interceptor source
        Assert.That(interceptorSource.SourceText, Is.Null);
    }

    [Test]
    public void EventId_FnvHash_StableForSameTemplate()
    {
        // Compute expected FNV-1a hash for the template "Processing " + "\0" + " items"
        var text = "Processing \0 items";
        var expectedHash = Fnv1aHash(text);

        var source = """
            using Logsmith;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger)
                {
                    logger.Debug($"Processing {42} items");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null);
        Assert.That(generated, Does.Contain($"EventId = {expectedHash}"));
    }

    [Test]
    public void HandlerOutput_ReadsTextAndJson()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger)
                {
                    logger.Debug($"Value: {42}");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null);
        Assert.That(generated, Does.Contain("handler.GetTextWritten()"));
        Assert.That(generated, Does.Contain("handler.GetJsonWritten()"));
    }

    [Test]
    public void CarrierType_HasThreadStaticPool()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger, bool v)
                {
                    logger.When(v).Debug($"Test {1}");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null);
        Assert.That(generated, Does.Contain("[global::System.ThreadStatic]"));
        Assert.That(generated, Does.Contain("Rent("));
        Assert.That(generated, Does.Contain("Return()"));
    }

    [Test]
    public void CarrierType_ImplementsILogger()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger, bool v)
                {
                    logger.When(v).Debug($"Test {1}");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null);
        Assert.That(generated, Does.Contain(": global::Logsmith.ILogger"));
        Assert.That(generated, Does.Contain("global::Logsmith.LoggerContext global::Logsmith.ILogger.Context"));
    }

    [Test]
    public void ChainStart_When_ChecksConditionAndLevel()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class MyService
            {
                public void DoWork(ILogger logger, bool v)
                {
                    logger.When(v).Debug($"Test {1}");
                }
            }
            """;

        var generated = RunAndGetInterceptorSource(source);
        Assert.That(generated, Is.Not.Null);
        // When should check both condition and level
        Assert.That(generated, Does.Contain("!condition"));
        Assert.That(generated, Does.Contain("IsEnabled(global::Logsmith.LogLevel.Debug)"));
    }

    // ── Helpers ─────────────────────────────────────────────────

    private static string? RunAndGetInterceptorSource(string source)
    {
        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var interceptorSource = result.GeneratedSources
            .FirstOrDefault(s => s.HintName == "LogInterceptors.g.cs");
        return interceptorSource.SourceText?.ToString();
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
}
