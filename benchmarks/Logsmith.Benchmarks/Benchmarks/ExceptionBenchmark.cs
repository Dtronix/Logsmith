using BenchmarkDotNet.Attributes;
using Logsmith.Benchmarks.LogDefinitions;
using ZLogger;

namespace Logsmith.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class ExceptionBenchmark : BenchmarkBase
{
    private const string OperationName = "data-sync";
    private Exception _exception = null!;

    public override void Setup()
    {
        // Create exception once — avoid measuring exception construction cost.
        try { throw new InvalidOperationException("Something went wrong"); }
        catch (Exception ex) { _exception = ex; }

        base.Setup();
    }

    [Benchmark(Baseline = true)]
    public void Logsmith()
    {
        LogsmithLog.ExceptionMessage(OperationName, _exception);
    }

    [Benchmark]
    public void MEL()
    {
        MelLog.ExceptionMessage(MelLogger, _exception, OperationName);
    }

    [Benchmark]
    public void Serilog()
    {
        SerilogLogger.Error(_exception, "Operation {operationName} failed", OperationName);
    }

    [Benchmark]
    public void NLog()
    {
        NLogLogger.Error(_exception, "Operation {operationName} failed", OperationName);
    }

    [Benchmark]
    public void ZLogger()
    {
        ZLoggerLogger.ZLogError(_exception, $"Operation {OperationName} failed");
    }
}
