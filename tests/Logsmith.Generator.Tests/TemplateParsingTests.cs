using Microsoft.CodeAnalysis;

namespace Logsmith.Generator.Tests;

[TestFixture]
public class TemplateParsingTests
{
    [Test]
    public void ExplicitTemplate_ExtractsPlaceholders()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "User {name} logged in from {ip}")]
                static partial void UserLogin(string name, string ip);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        // Should contain writes for both "name" and "ip" parameters
        Assert.That(generated, Does.Contain("name"));
        Assert.That(generated, Does.Contain("ip"));
        // Should contain the literal text segments
        Assert.That(generated, Does.Contain("User "));
        Assert.That(generated, Does.Contain(" logged in from "));
    }

    [Test]
    public void TemplateFree_GeneratesFromMethodNameAndParams()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information)]
                static partial void UserLogin(string name, string ip);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        // Template-free should include method name as prefix
        Assert.That(generated, Does.Contain("UserLogin"));
    }

    [Test]
    public void EmptyTemplate_UsesTemplateFreeMode()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "")]
                static partial void DoSomething(int count);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("DoSomething"));
    }

    [Test]
    public void Parse_FormatSpecifier_ExtractedFromPlaceholder()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Value={value:F2}")]
                static partial void LogValue(double value);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("WriteFormatted"));
        Assert.That(generated, Does.Contain("\"F2\""));
    }

    [Test]
    public void Parse_JsonSpecifier_Recognized()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Data={data:json}")]
                static partial void LogData(object data);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("SerializeToUtf8Bytes"));
    }

    [Test]
    public void Parse_NoSpecifier_FormatIsNull()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Name={name}")]
                static partial void LogName(string name);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        // Should use WriteString without any format specifier
        Assert.That(generated, Does.Contain("WriteString(name)"));
        Assert.That(generated, Does.Not.Contain("SerializeToUtf8Bytes"));
    }

    [Test]
    public void Parse_ColonInLiteral_NotTreatedAsSpecifier()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "time: {val}")]
                static partial void LogTime(int val);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        // The colon in "time: " is in a literal, not inside braces
        Assert.That(generated, Does.Contain("time: "));
        Assert.That(generated, Does.Contain("WriteFormatted(val)"));
    }

    private static string GetGeneratedSource(GeneratorRunResult result, string hintPrefix)
    {
        var source = result.GeneratedSources
            .FirstOrDefault(s => s.HintName.StartsWith(hintPrefix));
        Assert.That(source.SourceText, Is.Not.Null, $"No generated source found with prefix '{hintPrefix}'");
        return source.SourceText!.ToString();
    }
}
