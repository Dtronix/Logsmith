using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Logsmith.Generator;

namespace Logsmith.Generator.Tests;

internal static class GeneratorTestHelper
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    internal static CSharpCompilation CreateCompilation(params string[] sources)
    {
        var syntaxTrees = new List<SyntaxTree>();
        foreach (var source in sources)
        {
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(source, ParseOptions));
        }

        var references = GetMetadataReferences();

        return CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));
    }

    internal static CSharpCompilation CreateCompilationWithoutLogsmith(params string[] sources)
    {
        var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s, ParseOptions)).ToList();

        // Only runtime references, no Logsmith
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location)
                && !a.GetName().Name!.StartsWith("Logsmith"))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        return CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));
    }

    private static List<MetadataReference> GetMetadataReferences()
    {
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        // Ensure Logsmith.dll is included (force load via typeof)
        var logsmithAssembly = typeof(Logsmith.LogLevel).Assembly;
        if (!refs.Any(r => r.Display?.Contains("Logsmith.dll") == true))
            refs.Add(MetadataReference.CreateFromFile(logsmithAssembly.Location));

        // Ensure System.Text.Json is included
        var jsonAssembly = typeof(System.Text.Json.Utf8JsonWriter).Assembly;
        if (!refs.Any(r => r.Display?.Contains("System.Text.Json") == true))
            refs.Add(MetadataReference.CreateFromFile(jsonAssembly.Location));

        return refs;
    }

    internal static GeneratorRunResult RunGenerator(CSharpCompilation compilation)
    {
        var generator = new LogsmithGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out _, out _);
        return driver.GetRunResult().Results[0];
    }

    internal static (GeneratorRunResult Result, ImmutableArray<Diagnostic> Diagnostics) RunGeneratorWithDiagnostics(
        CSharpCompilation compilation)
    {
        var generator = new LogsmithGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out var diagnostics);
        var result = driver.GetRunResult().Results[0];
        return (result, outputCompilation.GetDiagnostics());
    }
}
