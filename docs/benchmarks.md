# Benchmarks

All benchmarks compare Logsmith against popular .NET logging libraries using BenchmarkDotNet.
Each library writes to a dev-null sink that forces full message rendering to ensure fair comparison.

- **Logsmith**: Source-generated, writes UTF-8 to a no-op `ILogSink`
- **MEL**: Source-generated `[LoggerMessage]`, writes to a no-op `ILoggerProvider`
- **Serilog**: String template API, writes to a no-op `ILogEventSink` (calls `RenderMessage()`)
- **NLog**: String template API, writes to a no-op `Target` (reads `FormattedMessage`)
- **ZLogger**: Interpolated string API backed by MEL, writes to `Stream.Null` via async channel

## DisabledLevelBenchmark

Measures the cost of logging a Trace message when minimum level is Warning.
Tests how efficiently each library's guard check short-circuits.

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.7840)
Intel Core i7-10700 CPU 2.90GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 10.0.3 (10.0.326.7603), X64 RyuJIT AVX2
  Job-LWSLAR : .NET 10.0.3 (10.0.326.7603), X64 RyuJIT AVX2

MaxIterationCount=20  MaxWarmupIterationCount=10

| Method   | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------- |----------:|----------:|----------:|----------:|------:|--------:|----------:|------------:|
| Logsmith | 0.0001 ns | 0.0002 ns | 0.0002 ns | 0.0000 ns |     ? |       ? |         - |           ? |
| MEL      | 1.5935 ns | 0.0090 ns | 0.0084 ns | 1.5924 ns |     ? |       ? |         - |           ? |
| Serilog  | 2.1142 ns | 0.0055 ns | 0.0052 ns | 2.1126 ns |     ? |       ? |         - |           ? |
| NLog     | 0.0019 ns | 0.0020 ns | 0.0018 ns | 0.0012 ns |     ? |       ? |         - |           ? |
| ZLogger  | 3.7108 ns | 0.0199 ns | 0.0186 ns | 3.7122 ns |     ? |       ? |         - |           ? |
```

## SimpleMessageBenchmark

Logs a static message with no parameters.

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.7840)
Intel Core i7-10700 CPU 2.90GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 10.0.3 (10.0.326.7603), X64 RyuJIT AVX2
  Job-LWSLAR : .NET 10.0.3 (10.0.326.7603), X64 RyuJIT AVX2

MaxIterationCount=20  MaxWarmupIterationCount=10

| Method   | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------- |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| Logsmith |  30.07 ns | 0.076 ns | 0.071 ns |  1.00 |    0.00 |      - |         - |          NA |
| MEL      |  19.97 ns | 0.093 ns | 0.082 ns |  0.66 |    0.00 |      - |         - |          NA |
| Serilog  | 135.76 ns | 2.394 ns | 2.122 ns |  4.51 |    0.07 | 0.0267 |     224 B |          NA |
| NLog     |  41.07 ns | 0.806 ns | 0.791 ns |  1.37 |    0.03 | 0.0143 |     120 B |          NA |
| ZLogger  | 299.78 ns | 2.017 ns | 1.886 ns |  9.97 |    0.06 |      - |         - |          NA |
```

## TemplatedMessageBenchmark

Logs a message with two parameters: `string` and `int`.

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.7840)
Intel Core i7-10700 CPU 2.90GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 10.0.3 (10.0.326.7603), X64 RyuJIT AVX2
  Job-LWSLAR : .NET 10.0.3 (10.0.326.7603), X64 RyuJIT AVX2

MaxIterationCount=20  MaxWarmupIterationCount=10

| Method   | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------- |------------:|----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| Logsmith |    47.47 ns |  0.173 ns |  0.162 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| MEL      |    60.67 ns |  1.611 ns |  1.724 ns |  1.28 |    0.04 | 0.0095 |      - |      80 B |          NA |
| Serilog  |   223.16 ns |  2.010 ns |  1.880 ns |  4.70 |    0.04 | 0.0525 |      - |     440 B |          NA |
| NLog     |   168.96 ns |  5.498 ns |  6.111 ns |  3.56 |    0.13 | 0.0439 |      - |     368 B |          NA |
| ZLogger  | 1,505.71 ns | 52.064 ns | 59.957 ns | 31.72 |    1.24 | 0.1717 | 0.1106 |    1682 B |          NA |
```

## MultiParameterBenchmark

Logs a message with four parameters: `string`, `string`, `double`, and `int`.

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.7840)
Intel Core i7-10700 CPU 2.90GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 10.0.3 (10.0.326.7603), X64 RyuJIT AVX2
  Job-LWSLAR : .NET 10.0.3 (10.0.326.7603), X64 RyuJIT AVX2

MaxIterationCount=20  MaxWarmupIterationCount=10

| Method   | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------- |---------:|----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| Logsmith | 146.7 ns |   0.77 ns |   0.72 ns |  1.00 |    0.01 |      - |      - |         - |          NA |
| MEL      | 267.7 ns |   2.92 ns |   2.59 ns |  1.83 |    0.02 | 0.0286 |      - |     240 B |          NA |
| Serilog  | 493.9 ns |   5.01 ns |   4.44 ns |  3.37 |    0.03 | 0.1049 |      - |     880 B |          NA |
| NLog     | 427.5 ns |   7.32 ns |   7.19 ns |  2.91 |    0.05 | 0.0849 |      - |     712 B |          NA |
| ZLogger  | 554.8 ns | 154.46 ns | 177.88 ns |  3.78 |    1.18 | 0.0095 | 0.0086 |      97 B |          NA |
```

## ExceptionBenchmark

Logs an error message with an exception and one `string` parameter.

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.7840)
Intel Core i7-10700 CPU 2.90GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 10.0.3 (10.0.326.7603), X64 RyuJIT AVX2
  Job-LWSLAR : .NET 10.0.3 (10.0.326.7603), X64 RyuJIT AVX2

MaxIterationCount=20  MaxWarmupIterationCount=10

| Method   | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------- |----------:|----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| Logsmith |  86.40 ns |  0.250 ns |  0.222 ns |  1.00 |    0.00 |      - |      - |         - |          NA |
| MEL      |  71.76 ns |  0.328 ns |  0.274 ns |  0.83 |    0.00 | 0.0105 |      - |      88 B |          NA |
| Serilog  | 316.09 ns |  3.526 ns |  2.944 ns |  3.66 |    0.03 | 0.0639 |      - |     536 B |          NA |
| NLog     | 213.91 ns |  1.976 ns |  1.650 ns |  2.48 |    0.02 | 0.0563 |      - |     472 B |          NA |
| ZLogger  | 346.86 ns | 11.807 ns | 11.596 ns |  4.01 |    0.13 | 0.0033 | 0.0029 |      28 B |          NA |
```

## ScopedContextBenchmark

Logs a templated message (two parameters) with one ambient scope property active per library.
Tests the overhead of scoped context (e.g. request IDs, correlation IDs) during log dispatch.

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.26100.7840)
Intel Core i7-10700 CPU 2.90GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 10.0.3 (10.0.326.7603), X64 RyuJIT AVX2
  Job-LWSLAR : .NET 10.0.3 (10.0.326.7603), X64 RyuJIT AVX2

MaxIterationCount=20  MaxWarmupIterationCount=10

| Method   | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------- |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| Logsmith |  48.34 ns | 0.074 ns | 0.070 ns |  1.00 |    0.00 |      - |         - |          NA |
| MEL      |  70.09 ns | 0.442 ns | 0.413 ns |  1.45 |    0.01 | 0.0105 |      88 B |          NA |
| Serilog  | 270.32 ns | 2.446 ns | 2.168 ns |  5.59 |    0.04 | 0.0610 |     512 B |          NA |
| NLog     | 209.90 ns | 2.030 ns | 1.695 ns |  4.34 |    0.03 | 0.0563 |     472 B |          NA |
| ZLogger  | 349.45 ns | 7.004 ns | 7.193 ns |  7.23 |    0.14 |      - |         - |          NA |
```
