using Microsoft.CodeAnalysis;

namespace Logsmith.Generator.Tests;

[TestFixture]
public class ParameterClassificationTests
{
    [Test]
    public void FirstParam_ILogSink_ClassifiedAsSink()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Hello")]
                static partial void Greet(ILogSink sink);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("sink.IsEnabled"));
    }

    [Test]
    public void ExceptionParam_ClassifiedAsException()
    {
        var source = """
            using Logsmith;
            using System;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Error, "Error occurred")]
                static partial void LogError(Exception ex);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("exception: ex"));
    }

    [Test]
    public void CallerFilePath_ClassifiedAsCallerFile()
    {
        var source = """
            using Logsmith;
            using System.Runtime.CompilerServices;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Hello")]
                static partial void Greet([CallerFilePath] string file = "");
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("callerFile: file"));
    }

    [Test]
    public void CallerLineNumber_ClassifiedAsCallerLine()
    {
        var source = """
            using Logsmith;
            using System.Runtime.CompilerServices;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Hello")]
                static partial void Greet([CallerLineNumber] int line = 0);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("callerLine: line"));
    }

    [Test]
    public void CallerMemberName_ClassifiedAsCallerMember()
    {
        var source = """
            using Logsmith;
            using System.Runtime.CompilerServices;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Hello")]
                static partial void Greet([CallerMemberName] string member = "");
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("callerMember: member"));
    }

    [Test]
    public void RegularParam_ClassifiedAsMessageParam()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Value={count}")]
                static partial void LogCount(int count);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("WriteFormatted(count)"));
    }

    [Test]
    public void MixedParams_AllClassifiedCorrectly()
    {
        var source = """
            using Logsmith;
            using System;
            using System.Runtime.CompilerServices;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Error, "Error: {msg}")]
                static partial void LogError(
                    string msg,
                    Exception ex,
                    [CallerFilePath] string file = "",
                    [CallerLineNumber] int line = 0,
                    [CallerMemberName] string member = "");
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("WriteString(msg)"));
        Assert.That(generated, Does.Contain("exception: ex"));
        Assert.That(generated, Does.Contain("callerFile: file"));
        Assert.That(generated, Does.Contain("callerLine: line"));
        Assert.That(generated, Does.Contain("callerMember: member"));
    }

    private static string GetGeneratedSource(GeneratorRunResult result, string hintPrefix)
    {
        var source = result.GeneratedSources
            .FirstOrDefault(s => s.HintName.StartsWith(hintPrefix));
        Assert.That(source.SourceText, Is.Not.Null, $"No generated source found with prefix '{hintPrefix}'");
        return source.SourceText!.ToString();
    }
}
