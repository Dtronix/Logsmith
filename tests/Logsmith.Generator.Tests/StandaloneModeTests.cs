using Microsoft.CodeAnalysis;

namespace Logsmith.Generator.Tests;

[TestFixture]
public class StandaloneModeTests
{
    [Test]
    public void ModeDetection_LogLevelPresent_SharedMode()
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

        // In shared mode, no embedded sources should be emitted
        var embeddedSources = result.GeneratedSources
            .Where(s => s.HintName.Contains("EmbeddedSources"))
            .ToList();

        Assert.That(embeddedSources, Is.Empty);
    }

    [Test]
    public void ModeDetection_LogLevelAbsent_StandaloneMode()
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
        var result = GeneratorTestHelper.RunGenerator(compilation);

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
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var hintNames = result.GeneratedSources.Select(s => s.HintName).ToList();

        Assert.That(hintNames, Has.Some.Contain("LogLevel"));
        Assert.That(hintNames, Has.Some.Contain("LogManager"));
        Assert.That(hintNames, Has.Some.Contain("LogEntry"));
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
        var result = GeneratorTestHelper.RunGenerator(compilation);

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
        var result = GeneratorTestHelper.RunGenerator(compilation);

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
        // Validates that the visibility replacement handles protected members correctly.
        // Uses the full compilation helper since standalone needs many framework refs.
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
        var (result, diagnostics) = GeneratorTestHelper.RunGeneratorWithDiagnostics(compilation);

        // Only check for accessibility inconsistency errors (CS0053, CS0051, CS0737).
        // Other errors (missing references) are expected in the test environment
        // since the test compilation lacks full framework references.
        var accessibilityErrors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error &&
                        (d.Id == "CS0053" || d.Id == "CS0051" || d.Id == "CS0737"))
            .ToList();

        Assert.That(accessibilityErrors, Is.Empty,
            $"Should have no accessibility inconsistency errors. Found: {string.Join(", ", accessibilityErrors.Select(e => e.ToString()))}");
    }
}
