using Logsmith.Generator;

namespace Logsmith.Generator.Tests;

[TestFixture]
public class ConditionalCompilationTests
{
    [Test]
    public void DefaultThreshold_Debug_TraceAndDebugGetConditional()
    {
        // Default threshold is Debug (ordinal 1)
        int threshold = ConditionalCompilation.ParseThreshold(null);
        Assert.That(threshold, Is.EqualTo(1)); // Debug

        // Trace (0) <= Debug (1) → conditional
        Assert.That(ConditionalCompilation.ShouldApplyConditional(0, threshold, false), Is.True);
        // Debug (1) <= Debug (1) → conditional
        Assert.That(ConditionalCompilation.ShouldApplyConditional(1, threshold, false), Is.True);
        // Information (2) > Debug (1) → not conditional
        Assert.That(ConditionalCompilation.ShouldApplyConditional(2, threshold, false), Is.False);
    }

    [Test]
    public void Threshold_Information_TraceDebugInfoGetConditional()
    {
        int threshold = ConditionalCompilation.ParseThreshold("Information");
        Assert.That(threshold, Is.EqualTo(2));

        Assert.That(ConditionalCompilation.ShouldApplyConditional(0, threshold, false), Is.True);  // Trace
        Assert.That(ConditionalCompilation.ShouldApplyConditional(1, threshold, false), Is.True);  // Debug
        Assert.That(ConditionalCompilation.ShouldApplyConditional(2, threshold, false), Is.True);  // Information
        Assert.That(ConditionalCompilation.ShouldApplyConditional(3, threshold, false), Is.False); // Warning
    }

    [Test]
    public void Threshold_None_NoMethodsGetConditional()
    {
        int threshold = ConditionalCompilation.ParseThreshold("None");
        Assert.That(threshold, Is.EqualTo(6));

        // All levels <= None → all get conditional
        // But None means "strip everything" which is all levels
        Assert.That(ConditionalCompilation.ShouldApplyConditional(0, threshold, false), Is.True);
        Assert.That(ConditionalCompilation.ShouldApplyConditional(5, threshold, false), Is.True);
    }

    [Test]
    public void AlwaysEmit_BypassesConditional()
    {
        int threshold = ConditionalCompilation.ParseThreshold("Information");

        // Trace would normally get conditional, but AlwaysEmit bypasses it
        Assert.That(ConditionalCompilation.ShouldApplyConditional(0, threshold, true), Is.False);
        Assert.That(ConditionalCompilation.ShouldApplyConditional(1, threshold, true), Is.False);
    }

    [Test]
    public void AboveThreshold_NoConditionalAttribute()
    {
        int threshold = ConditionalCompilation.ParseThreshold("Debug");

        // Warning (3) > Debug (1) → no conditional
        Assert.That(ConditionalCompilation.ShouldApplyConditional(3, threshold, false), Is.False);
        // Error (4) > Debug (1) → no conditional
        Assert.That(ConditionalCompilation.ShouldApplyConditional(4, threshold, false), Is.False);
    }
}
