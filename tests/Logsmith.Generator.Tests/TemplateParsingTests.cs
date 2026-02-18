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

    private static string GetGeneratedSource(GeneratorRunResult result, string hintPrefix)
    {
        var source = result.GeneratedSources
            .FirstOrDefault(s => s.HintName.StartsWith(hintPrefix));
        Assert.That(source.SourceText, Is.Not.Null, $"No generated source found with prefix '{hintPrefix}'");
        return source.SourceText!.ToString();
    }
}
