using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Logsmith.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class LogsmithGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Phase 1: skeleton — pipeline stages will be wired in subsequent phases.
    }
}
