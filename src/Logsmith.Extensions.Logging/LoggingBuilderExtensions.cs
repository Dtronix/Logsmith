using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Logsmith.Extensions.Logging;

public static class LoggingBuilderExtensions
{
    public static ILoggingBuilder AddLogsmith(this ILoggingBuilder builder)
    {
        builder.Services.AddSingleton<ILoggerProvider, LogsmithLoggerProvider>();
        return builder;
    }
}
