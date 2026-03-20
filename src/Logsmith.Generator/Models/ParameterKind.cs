namespace Logsmith.Generator.Models;

internal enum ParameterKind
{
    MessageParam,
    Sink,
    Exception,
    CallerFile,
    CallerLine,
    CallerMember,
    /// <summary>
    /// ILogsmithLogger explicit parameter (abstraction mode equivalent of Sink).
    /// </summary>
    AbstractionLogger
}
