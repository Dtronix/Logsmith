using Microsoft.CodeAnalysis;

namespace Logsmith.Generator.Tests;

[TestFixture]
public class DiagnosticTests
{
    [Test]
    public void LSMITH001_PlaceholderNoMatchingParam()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Hello {name} and {missing}")]
                static partial void Greet(string name);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        Assert.That(result.Diagnostics, Has.Some.Matches<Diagnostic>(
            d => d.Id == "LSMITH001"));
    }

    [Test]
    public void LSMITH002_ParamNotInTemplate()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Hello {name}")]
                static partial void Greet(string name, int unused);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        Assert.That(result.Diagnostics, Has.Some.Matches<Diagnostic>(
            d => d.Id == "LSMITH002"));
    }

    [Test]
    public void LSMITH003_NotStaticPartialInPartialClass()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class Log
            {
                [LogMessage(LogLevel.Information, "Hello")]
                public void Greet() { }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        Assert.That(result.Diagnostics, Has.Some.Matches<Diagnostic>(
            d => d.Id == "LSMITH003"));
    }

    [Test]
    public void LSMITH005_CallerParamNameInTemplate()
    {
        var source = """
            using Logsmith;
            using System.Runtime.CompilerServices;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "From {callerFile}")]
                static partial void Greet([CallerFilePath] string callerFile = "");
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        Assert.That(result.Diagnostics, Has.Some.Matches<Diagnostic>(
            d => d.Id == "LSMITH005"));
    }
}
