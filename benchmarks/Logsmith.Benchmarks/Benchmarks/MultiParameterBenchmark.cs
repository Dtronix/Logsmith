using BenchmarkDotNet.Attributes;
using Logsmith.Benchmarks.LogDefinitions;
using ZLogger;

namespace Logsmith.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class MultiParameterBenchmark : BenchmarkBase
{
    private const string Method = "GET";
    private const string Path = "/api/users/42";
    private const double Elapsed = 12.345;
    private const int StatusCode = 200;

    [Benchmark(Baseline = true)]
    public void Logsmith()
    {
        LogsmithLog.MultiParameter(Method, Path, Elapsed, StatusCode);
    }

    [Benchmark]
    public void MEL()
    {
        MelLog.MultiParameter(MelLogger, Method, Path, Elapsed, StatusCode);
    }

    [Benchmark]
    public void Serilog()
    {
        SerilogLogger.Information("{method} {path} completed in {elapsed}ms with status {statusCode}", Method, Path, Elapsed, StatusCode);
    }

    [Benchmark]
    public void NLog()
    {
        NLogLogger.Info("{method} {path} completed in {elapsed}ms with status {statusCode}", Method, Path, Elapsed, StatusCode);
    }

    [Benchmark]
    public void ZLogger()
    {
        ZLoggerLogger.ZLogInformation($"{Method} {Path} completed in {Elapsed}ms with status {StatusCode}");
    }
}
