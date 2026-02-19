using Microsoft.CodeAnalysis;

namespace Logsmith.Generator.Tests;

[TestFixture]
public class SamplingEmissionTests
{
    [Test]
    public void SampleRate_EmitsCounterAndModuloGuard()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Tick", SampleRate = 10)]
                static partial void Tick();
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("__sampleCounter_Tick"));
        Assert.That(generated, Does.Contain("% 10"));
    }

    [Test]
    public void MaxPerSecond_EmitsRateWindowAndCountGuard()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Request", MaxPerSecond = 100)]
                static partial void Request();
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("__rateWindow_Request"));
        Assert.That(generated, Does.Contain("__rateCount_Request"));
        Assert.That(generated, Does.Contain("> 100"));
    }

    [Test]
    public void SampleRate_One_DoesNotEmitGuard()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Tick", SampleRate = 1)]
                static partial void Tick();
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Not.Contain("__sampleCounter"));
    }

    [Test]
    public void BothSet_EmitsBothGuards()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Tick", SampleRate = 5, MaxPerSecond = 50)]
                static partial void Tick();
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("__sampleCounter_Tick"));
        Assert.That(generated, Does.Contain("__rateWindow_Tick"));
    }

    [Test]
    public void BothSet_ReportsLSMITH007Warning()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Tick", SampleRate = 5, MaxPerSecond = 50)]
                static partial void Tick();
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var diag = result.Diagnostics.FirstOrDefault(d => d.Id == "LSMITH007");
        Assert.That(diag, Is.Not.Null);
        Assert.That(diag!.Severity, Is.EqualTo(DiagnosticSeverity.Warning));
    }

    [Test]
    public void NoSamplingOrRateLimit_NoGuards()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Hello")]
                static partial void Greet();
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Not.Contain("__sampleCounter"));
        Assert.That(generated, Does.Not.Contain("__rateWindow"));
    }

    private static string GetGeneratedSource(GeneratorRunResult result, string hintPrefix)
    {
        var source = result.GeneratedSources
            .FirstOrDefault(s => s.HintName.StartsWith(hintPrefix));
        Assert.That(source.SourceText, Is.Not.Null, $"No generated source found with prefix '{hintPrefix}'");
        return source.SourceText!.ToString();
    }
}
