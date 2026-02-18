using Microsoft.CodeAnalysis;

namespace Logsmith.Generator.Tests;

[TestFixture]
public class SyntaxExtractionTests
{
    [Test]
    public void AccessModifier_Public_PreservedInOutput()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Hello")]
                public static partial void Greet();
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("public static partial void Greet"));
    }

    [Test]
    public void AccessModifier_Internal_PreservedInOutput()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Hello")]
                internal static partial void Greet();
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("internal static partial void Greet"));
    }

    [Test]
    public void AccessModifier_Private_OmittedInOutput()
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
        // Should contain "    static partial void Greet" without an access modifier prefix
        Assert.That(generated, Does.Contain("    static partial void Greet"));
        Assert.That(generated, Does.Not.Contain("public static partial void Greet"));
        Assert.That(generated, Does.Not.Contain("internal static partial void Greet"));
    }

    [Test]
    public void ZeroMessageParams_EmitsEmptyStateStruct()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Critical, "System shutdown")]
                static partial void Shutdown();
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("ShutdownState"));
        Assert.That(generated, Does.Contain("LogManager.Dispatch"));
    }

    [Test]
    public void CallerInfoOnly_EmitsEmptyStateStruct()
    {
        var source = """
            using Logsmith;
            using System.Runtime.CompilerServices;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Trace, "Checkpoint")]
                static partial void Checkpoint(
                    [CallerFilePath] string file = "",
                    [CallerLineNumber] int line = 0,
                    [CallerMemberName] string member = "");
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("CheckpointState"));
        Assert.That(generated, Does.Contain("callerFile: file"));
        Assert.That(generated, Does.Contain("callerLine: line"));
        Assert.That(generated, Does.Contain("callerMember: member"));
    }

    [Test]
    public void CallerInfo_NoCallerAttributesOnImplementation()
    {
        var source = """
            using Logsmith;
            using System.Runtime.CompilerServices;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Trace, "Checkpoint")]
                static partial void Checkpoint(
                    [CallerFilePath] string file = "",
                    [CallerLineNumber] int line = 0);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        // The generated implementation should NOT re-emit CallerFilePath/CallerLineNumber
        // to avoid CS0579 (duplicate attribute)
        Assert.That(generated, Does.Not.Contain("CallerFilePath"));
        Assert.That(generated, Does.Not.Contain("CallerLineNumber"));
    }

    [Test]
    public void LogCategory_ExtractedFromSyntax()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            [LogCategory("MyCategory")]
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Hello")]
                static partial void Greet();
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("\"MyCategory\""));
    }

    [Test]
    public void LogCategory_DefaultsToClassName()
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
        Assert.That(generated, Does.Contain("\"Log\""));
    }

    [Test]
    public void TemplateFree_GeneratesMethodNameTemplate()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Debug)]
                static partial void ProcessItem(string name, int count);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("ProcessItem"));
        Assert.That(generated, Does.Contain("WriteString"));
        Assert.That(generated, Does.Contain("WriteFormatted"));
    }

    [Test]
    public void AllLogLevels_ExtractedFromSyntax()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Trace, "t")]
                static partial void T();

                [LogMessage(LogLevel.Debug, "d")]
                static partial void D();

                [LogMessage(LogLevel.Information, "i")]
                static partial void I();

                [LogMessage(LogLevel.Warning, "w")]
                static partial void W();

                [LogMessage(LogLevel.Error, "e")]
                static partial void E();

                [LogMessage(LogLevel.Critical, "c")]
                static partial void C();
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("LogLevel.Trace"));
        Assert.That(generated, Does.Contain("LogLevel.Debug"));
        Assert.That(generated, Does.Contain("LogLevel.Information"));
        Assert.That(generated, Does.Contain("LogLevel.Warning"));
        Assert.That(generated, Does.Contain("LogLevel.Error"));
        Assert.That(generated, Does.Contain("LogLevel.Critical"));
    }

    [Test]
    public void ExceptionParam_ClassifiedAndPassedToLogEntry()
    {
        var source = """
            using Logsmith;
            using System;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Error, "Failed: {operation}")]
                static partial void OpFailed(string operation, Exception ex);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("exception: ex"));
    }

    [Test]
    public void CompilationSucceeds_NoErrors()
    {
        var source = """
            using Logsmith;
            using System;
            using System.Runtime.CompilerServices;
            namespace TestNs;

            [LogCategory("Sample")]
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Started with {count} args")]
                public static partial void AppStarted(int count);

                [LogMessage(LogLevel.Debug)]
                public static partial void ProcessItem(string name, int index);

                [LogMessage(LogLevel.Warning, "Cache miss for {key}, value was {cached}")]
                public static partial void CacheMiss(string key, int? cached);

                [LogMessage(LogLevel.Error, "Failed: {op}")]
                public static partial void OpFailed(string op, Exception ex);

                [LogMessage(LogLevel.Trace, "Checkpoint")]
                public static partial void Checkpoint(
                    [CallerFilePath] string file = "",
                    [CallerLineNumber] int line = 0,
                    [CallerMemberName] string member = "");

                [LogMessage(LogLevel.Critical, "Shutdown")]
                public static partial void Shutdown();

                [LogMessage(LogLevel.Information, "User {id}, name: {name}")]
                public static partial void UserLogin(int id, string? name);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var (result, diagnostics) = GeneratorTestHelper.RunGeneratorWithDiagnostics(compilation);

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.That(errors, Is.Empty,
            $"Full sample compilation should succeed. Errors: {string.Join("\n", errors.Select(e => e.ToString()))}");
    }

    private static string GetGeneratedSource(GeneratorRunResult result, string hintPrefix)
    {
        var source = result.GeneratedSources
            .FirstOrDefault(s => s.HintName.StartsWith(hintPrefix));
        Assert.That(source.SourceText, Is.Not.Null, $"No generated source found with prefix '{hintPrefix}'");
        return source.SourceText!.ToString();
    }
}
