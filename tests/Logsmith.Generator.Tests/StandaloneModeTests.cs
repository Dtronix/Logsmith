using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Logsmith.Generator.Tests;

[TestFixture]
public class StandaloneModeTests
{
    /// <summary>
    /// Creates a compilation with LogsmithMode MSBuild property set via options provider.
    /// </summary>
    private static GeneratorRunResult RunWithMode(
        CSharpCompilation compilation,
        string mode)
    {
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.LogsmithMode"] = mode,
        });

        var generator = new LogsmithGenerator();
        var driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            optionsProvider: optionsProvider);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out _, out _);
        return driver.GetRunResult().Results[0];
    }

    private static (GeneratorRunResult Result, ImmutableArray<Diagnostic> Diagnostics) RunWithModeAndDiagnostics(
        CSharpCompilation compilation,
        string mode)
    {
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(new Dictionary<string, string>
        {
            ["build_property.LogsmithMode"] = mode,
        });

        var generator = new LogsmithGenerator();
        var driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            optionsProvider: optionsProvider);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out _);
        var result = driver.GetRunResult().Results[0];
        return (result, outputCompilation.GetDiagnostics());
    }

    [Test]
    public void ModeDetection_SharedMode_NoEmbeddedSources()
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
        var result = RunWithMode(compilation, "Shared");

        // In shared mode, no embedded sources should be emitted
        var embeddedSources = result.GeneratedSources
            .Where(s => s.HintName.Contains("EmbeddedSources"))
            .ToList();

        Assert.That(embeddedSources, Is.Empty);
    }

    [Test]
    public void ModeDetection_StandaloneMode_EmitsEmbeddedSources()
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

        var compilation = GeneratorTestHelper.CreateCompilationWithoutLogsmith(source);
        var result = RunWithMode(compilation, "Standalone");

        // In standalone mode, embedded sources should be emitted
        var embeddedSources = result.GeneratedSources
            .Where(s => s.HintName.Contains("EmbeddedSources"))
            .ToList();

        Assert.That(embeddedSources, Is.Not.Empty,
            "Standalone mode should emit embedded sources");
    }

    [Test]
    public void StandaloneMode_EmbeddedSources_ContainCoreTypes()
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

        var compilation = GeneratorTestHelper.CreateCompilationWithoutLogsmith(source);
        var result = RunWithMode(compilation, "Standalone");

        var hintNames = result.GeneratedSources.Select(s => s.HintName).ToList();

        Assert.That(hintNames, Has.Some.Contain("LogLevel"));
        Assert.That(hintNames, Has.Some.Contain("LogManager"));
        Assert.That(hintNames, Has.Some.Contain("DispatchInfo"));
        Assert.That(hintNames, Has.Some.Contain("Utf8LogWriter"));
        Assert.That(hintNames, Has.Some.Contain("ILogSink"));
    }

    [Test]
    public void StandaloneMode_EmbeddedSources_TypesMadeInternal()
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

        var compilation = GeneratorTestHelper.CreateCompilationWithoutLogsmith(source);
        var result = RunWithMode(compilation, "Standalone");

        var logLevelSource = result.GeneratedSources
            .FirstOrDefault(s => s.HintName.Contains("LogLevel"));

        Assert.That(logLevelSource.SourceText, Is.Not.Null);
        var text = logLevelSource.SourceText!.ToString();
        Assert.That(text, Does.Contain("internal enum LogLevel"));
        Assert.That(text, Does.Not.Contain("public enum LogLevel"));
    }

    [Test]
    public void StandaloneMode_GeneratesMethodImplementation()
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

        var compilation = GeneratorTestHelper.CreateCompilationWithoutLogsmith(source);
        var result = RunWithMode(compilation, "Standalone");

        var methodSource = result.GeneratedSources
            .FirstOrDefault(s => s.HintName.StartsWith("TestNs.Log"));

        Assert.That(methodSource.SourceText, Is.Not.Null,
            "Standalone mode should generate method implementations via syntax-based discovery");
        var text = methodSource.SourceText!.ToString();
        Assert.That(text, Does.Contain("static partial void Greet"));
        Assert.That(text, Does.Contain("Utf8LogWriter"));
    }

    [Test]
    public void StandaloneMode_ProtectedMembers_NoAccessibilityErrors()
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

        var compilation = GeneratorTestHelper.CreateCompilationWithoutLogsmith(source);
        var (result, diagnostics) = RunWithModeAndDiagnostics(compilation, "Standalone");

        // Only check for accessibility inconsistency errors (CS0053, CS0051, CS0737).
        var accessibilityErrors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error &&
                        (d.Id == "CS0053" || d.Id == "CS0051" || d.Id == "CS0737"))
            .ToList();

        Assert.That(accessibilityErrors, Is.Empty,
            $"Should have no accessibility inconsistency errors. Found: {string.Join(", ", accessibilityErrors.Select(e => e.ToString()))}");
    }

    [Test]
    public void ModeDetection_UnrecognizedMode_DefaultsToShared()
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
        var result = RunWithMode(compilation, "InvalidMode");

        // Unrecognized mode defaults to Shared — no embedded sources
        var embeddedSources = result.GeneratedSources
            .Where(s => s.HintName.Contains("EmbeddedSources"))
            .ToList();

        Assert.That(embeddedSources, Is.Empty,
            "Unrecognized mode should default to Shared (no embedded sources)");
    }

    [Test]
    public void ModeDetection_ExplicitSharedOverride_NoEmbeddedSources()
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

        // Even with no Logsmith assembly reference, explicit Shared mode should not emit embedded sources
        var compilation = GeneratorTestHelper.CreateCompilationWithoutLogsmith(source);
        var result = RunWithMode(compilation, "Shared");

        var embeddedSources = result.GeneratedSources
            .Where(s => s.HintName.Contains("EmbeddedSources"))
            .ToList();

        Assert.That(embeddedSources, Is.Empty,
            "Explicit Shared mode should not emit embedded sources regardless of assembly references");
    }

    /// <summary>
    /// Custom AnalyzerConfigOptionsProvider for tests that supplies MSBuild properties.
    /// </summary>
    private class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly TestGlobalOptions _globalOptions;

        public TestAnalyzerConfigOptionsProvider(Dictionary<string, string> properties)
        {
            _globalOptions = new TestGlobalOptions(properties);
        }

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
            => TestGlobalOptions.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
            => TestGlobalOptions.Empty;

        private class TestGlobalOptions : AnalyzerConfigOptions
        {
            public static readonly TestGlobalOptions Empty = new(new Dictionary<string, string>());

            private readonly Dictionary<string, string> _properties;

            public TestGlobalOptions(Dictionary<string, string> properties)
            {
                _properties = properties;
            }

            public override bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
                => _properties.TryGetValue(key, out value);
        }
    }
}
