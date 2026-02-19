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

        var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        Assert.That(warnings, Is.Empty,
            $"Full sample compilation should have no warnings. Warnings: {string.Join("\n", warnings.Select(w => w.ToString()))}");
    }

    [Test]
    public void CustomStructParam_DifferentNamespace_CompilesSuccessfully()
    {
        var structSource = """
            using System;
            using System.Buffers;
            namespace Acme.Models;
            public struct SensorReading : IUtf8SpanFormattable
            {
                public double Temperature;
                public double Humidity;
                public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                {
                    bytesWritten = 0;
                    return true;
                }
                public string ToString(string? format, IFormatProvider? provider) => $"T={Temperature}";
            }
            """;

        var logSource = """
            using Logsmith;
            using Acme.Models;
            namespace App.Logging;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Sensor reported {reading}")]
                public static partial void SensorData(SensorReading reading);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(structSource, logSource);
        var (result, diagnostics) = GeneratorTestHelper.RunGeneratorWithDiagnostics(compilation);

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.That(errors, Is.Empty,
            $"Custom struct from different namespace should compile. Errors: {string.Join("\n", errors.Select(e => e.ToString()))}");

        var generated = GetGeneratedSource(result, "App.Logging.Log");
        // Type should be fully qualified with global:: prefix
        Assert.That(generated, Does.Contain("global::Acme.Models.SensorReading"));
    }

    [Test]
    public void CallerInfo_NoDefaultValuesOnImplementation()
    {
        // CS1066: default values on partial method implementations have no effect
        var source = """
            using Logsmith;
            using System.Runtime.CompilerServices;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Trace, "Checkpoint")]
                static partial void Check(
                    [CallerFilePath] string file = "",
                    [CallerLineNumber] int line = 0,
                    [CallerMemberName] string member = "");
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var (result, diagnostics) = GeneratorTestHelper.RunGeneratorWithDiagnostics(compilation);

        var cs1066 = diagnostics.Where(d => d.Id == "CS1066").ToList();
        Assert.That(cs1066, Is.Empty,
            "Generated implementation should not emit default values on caller info params");
    }

    [Test]
    public void NullableReferenceType_PreservedOnImplementation()
    {
        // CS8611: nullability mismatch between definition and implementation
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "User {name}")]
                static partial void LogUser(string? name);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var (result, diagnostics) = GeneratorTestHelper.RunGeneratorWithDiagnostics(compilation);

        var cs8611 = diagnostics.Where(d => d.Id == "CS8611").ToList();
        Assert.That(cs8611, Is.Empty,
            "Generated implementation should preserve nullable reference type annotation");

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("string? name"));
    }

    [Test]
    public void NullableReferenceType_PreservedOnStateStruct()
    {
        // CS8604: possible null reference argument when passing nullable to non-nullable state ctor
        var source = """
            using Logsmith;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "User {name}")]
                static partial void LogUser(string? name);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        // State struct field and constructor parameter should be nullable
        Assert.That(generated, Does.Contain("readonly string? name;"));
    }

    [Test]
    public void InParameter_PreservedOnImplementation()
    {
        var structSource = """
            using System;
            using System.Buffers;
            namespace Acme.Models;
            public struct SensorReading : IUtf8SpanFormattable
            {
                public double Temperature;
                public double Humidity;
                public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                {
                    bytesWritten = 0;
                    return true;
                }
                public string ToString(string? format, IFormatProvider? provider) => $"T={Temperature}";
            }
            """;

        var logSource = """
            using Logsmith;
            using Acme.Models;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Sensor reported {reading}")]
                public static partial void SensorData(in SensorReading reading);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(structSource, logSource);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        // Method signature should have 'in' modifier
        Assert.That(generated, Does.Contain("in global::Acme.Models.SensorReading reading"));
        // State struct constructor should have 'in' modifier
        Assert.That(generated, Does.Contain("internal SensorDataState(in global::Acme.Models.SensorReading reading)"));
        // State construction should pass 'in'
        Assert.That(generated, Does.Contain("new SensorDataState(in reading)"));
    }

    [Test]
    public void InParameter_CompilationSucceeds()
    {
        var structSource = """
            using System;
            using System.Buffers;
            namespace Acme.Models;
            public struct SensorReading : IUtf8SpanFormattable
            {
                public double Temperature;
                public double Humidity;
                public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                {
                    bytesWritten = 0;
                    return true;
                }
                public string ToString(string? format, IFormatProvider? provider) => $"T={Temperature}";
            }
            """;

        var logSource = """
            using Logsmith;
            using Acme.Models;
            namespace TestNs;
            public static partial class Log
            {
                [LogMessage(LogLevel.Information, "Sensor reported {reading}")]
                public static partial void SensorData(in SensorReading reading);
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(structSource, logSource);
        var (result, diagnostics) = GeneratorTestHelper.RunGeneratorWithDiagnostics(compilation);

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.That(errors, Is.Empty,
            $"in parameter compilation should succeed. Errors: {string.Join("\n", errors.Select(e => e.ToString()))}");

        var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        Assert.That(warnings, Is.Empty,
            $"in parameter compilation should have no warnings. Warnings: {string.Join("\n", warnings.Select(w => w.ToString()))}");
    }

    [Test]
    public void ClassModifier_Public_EmittedOnPartialClass()
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
        Assert.That(generated, Does.Contain("public static partial class Log"));
    }

    [Test]
    public void ClassModifier_Internal_EmittedOnPartialClass()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            internal static partial class Log
            {
                [LogMessage(LogLevel.Information, "Hello")]
                static partial void Greet();
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Log");
        Assert.That(generated, Does.Contain("internal static partial class Log"));
    }

    [Test]
    public void ClassModifier_NoExplicitAccess_EmittedWithoutModifier()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public partial class Outer
            {
                static partial class Log
                {
                    [LogMessage(LogLevel.Information, "Hello")]
                    static partial void Greet();
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Outer.Log");
        Assert.That(generated, Does.Contain("static partial class Log"));
        Assert.That(generated, Does.Not.Contain("public static partial class Log"));
        Assert.That(generated, Does.Not.Contain("internal static partial class Log"));
    }

    [Test]
    public void ClassModifier_Protected_EmittedOnNestedPartialClass()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public partial class Outer
            {
                protected static partial class Log
                {
                    [LogMessage(LogLevel.Information, "Hello")]
                    static partial void Greet();
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Outer.Log");
        Assert.That(generated, Does.Contain("protected static partial class Log"));
    }

    [Test]
    public void ClassModifier_Private_EmittedOnNestedPartialClass()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public partial class Outer
            {
                private static partial class Log
                {
                    [LogMessage(LogLevel.Information, "Hello")]
                    static partial void Greet();
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Outer.Log");
        Assert.That(generated, Does.Contain("private static partial class Log"));
    }

    [Test]
    public void ClassModifier_ProtectedInternal_EmittedOnNestedPartialClass()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public partial class Outer
            {
                protected internal static partial class Log
                {
                    [LogMessage(LogLevel.Information, "Hello")]
                    static partial void Greet();
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Outer.Log");
        Assert.That(generated, Does.Contain("protected internal static partial class Log"));
    }

    [Test]
    public void ClassModifier_PrivateProtected_EmittedOnNestedPartialClass()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public partial class Outer
            {
                private protected static partial class Log
                {
                    [LogMessage(LogLevel.Information, "Hello")]
                    static partial void Greet();
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Outer.Log");
        Assert.That(generated, Does.Contain("private protected static partial class Log"));
    }

    [Test]
    public void ClassModifier_Sealed_EmittedOnPartialClass()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public partial class Outer
            {
                sealed partial class Log
                {
                    [LogMessage(LogLevel.Information, "Hello")]
                    static partial void Greet();
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Outer.Log");
        Assert.That(generated, Does.Contain("sealed partial class Log"));
    }

    [Test]
    public void ClassModifier_AllVisibilities_CompileSuccessfully()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public partial class Outer
            {
                public static partial class PubLog
                {
                    [LogMessage(LogLevel.Information, "pub")]
                    public static partial void Pub();
                }
                internal static partial class IntLog
                {
                    [LogMessage(LogLevel.Information, "int")]
                    static partial void Int();
                }
                protected static partial class ProtLog
                {
                    [LogMessage(LogLevel.Information, "prot")]
                    static partial void Prot();
                }
                private static partial class PrivLog
                {
                    [LogMessage(LogLevel.Information, "priv")]
                    static partial void Priv();
                }
                protected internal static partial class ProtIntLog
                {
                    [LogMessage(LogLevel.Information, "protint")]
                    static partial void ProtInt();
                }
                private protected static partial class PrivProtLog
                {
                    [LogMessage(LogLevel.Information, "privprot")]
                    static partial void PrivProt();
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var (result, diagnostics) = GeneratorTestHelper.RunGeneratorWithDiagnostics(compilation);

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.That(errors, Is.Empty,
            $"All visibility modifiers should compile. Errors: {string.Join("\n", errors.Select(e => e.ToString()))}");

        var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        Assert.That(warnings, Is.Empty,
            $"All visibility modifiers should have no warnings. Warnings: {string.Join("\n", warnings.Select(w => w.ToString()))}");
    }

    [Test]
    public void NestedClass_SingleLevel_EmitsNestingWrapper()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public partial class Outer
            {
                public static partial class Log
                {
                    [LogMessage(LogLevel.Information, "Hello")]
                    static partial void Greet();
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Outer.Log");
        Assert.That(generated, Does.Contain("public partial class Outer"));
        Assert.That(generated, Does.Contain("public static partial class Log"));
    }

    [Test]
    public void NestedClass_TwoLevels_EmitsFullChain()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public partial class A
            {
                internal partial class B
                {
                    static partial class Log
                    {
                        [LogMessage(LogLevel.Information, "Hello")]
                        static partial void Greet();
                    }
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.A.B.Log");
        Assert.That(generated, Does.Contain("public partial class A"));
        Assert.That(generated, Does.Contain("internal partial class B"));
        Assert.That(generated, Does.Contain("static partial class Log"));
    }

    [Test]
    public void NestedClass_CompilationSucceeds()
    {
        var source = """
            using Logsmith;
            using System;
            namespace TestNs;
            public partial class Outer
            {
                [LogCategory("Nested")]
                public static partial class Log
                {
                    [LogMessage(LogLevel.Information, "Started with {count} args")]
                    public static partial void AppStarted(int count);

                    [LogMessage(LogLevel.Error, "Failed: {op}")]
                    public static partial void OpFailed(string op, Exception ex);
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var (result, diagnostics) = GeneratorTestHelper.RunGeneratorWithDiagnostics(compilation);

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.That(errors, Is.Empty,
            $"Nested class compilation should succeed. Errors: {string.Join("\n", errors.Select(e => e.ToString()))}");

        var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        Assert.That(warnings, Is.Empty,
            $"Nested class compilation should have no warnings. Warnings: {string.Join("\n", warnings.Select(w => w.ToString()))}");
    }

    [Test]
    public void NestedClass_NonPartialAncestor_ReportsDiagnostic()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public class Outer
            {
                public static partial class Log
                {
                    [LogMessage(LogLevel.Information, "Hello")]
                    static partial void Greet();
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var diag = result.Diagnostics.FirstOrDefault(d => d.Id == "LSMITH003");
        Assert.That(diag, Is.Not.Null, "Should report LSMITH003 for non-partial ancestor");
    }

    [Test]
    public void NestedStruct_EmitsStructKeyword()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public partial struct Outer
            {
                public static partial class Log
                {
                    [LogMessage(LogLevel.Information, "Hello")]
                    static partial void Greet();
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Outer.Log");
        Assert.That(generated, Does.Contain("public partial struct Outer"));
        Assert.That(generated, Does.Contain("public static partial class Log"));
    }

    [Test]
    public void NestedStruct_CompilationSucceeds()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public partial struct Outer
            {
                public static partial class Log
                {
                    [LogMessage(LogLevel.Information, "Hello {name}")]
                    public static partial void Greet(string name);
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var (result, diagnostics) = GeneratorTestHelper.RunGeneratorWithDiagnostics(compilation);

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.That(errors, Is.Empty,
            $"Struct nesting compilation should succeed. Errors: {string.Join("\n", errors.Select(e => e.ToString()))}");
    }

    [Test]
    public void NestedClass_CategoryFromInnerClass()
    {
        var source = """
            using Logsmith;
            namespace TestNs;
            public partial class Outer
            {
                [LogCategory("InnerCat")]
                public static partial class Log
                {
                    [LogMessage(LogLevel.Information, "Hello")]
                    static partial void Greet();
                }
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var result = GeneratorTestHelper.RunGenerator(compilation);

        var generated = GetGeneratedSource(result, "TestNs.Outer.Log");
        Assert.That(generated, Does.Contain("\"InnerCat\""));
    }

    private static string GetGeneratedSource(GeneratorRunResult result, string hintPrefix)
    {
        var source = result.GeneratedSources
            .FirstOrDefault(s => s.HintName.StartsWith(hintPrefix));
        Assert.That(source.SourceText, Is.Not.Null, $"No generated source found with prefix '{hintPrefix}'");
        return source.SourceText!.ToString();
    }
}
