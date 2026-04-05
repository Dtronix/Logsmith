using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Logsmith.Extensions.Logging;

/// <summary>
/// Extension methods for registering Logsmith's native ILogger into an IServiceCollection.
/// </summary>
public static class LogsmithServiceCollectionExtensions
{
    /// <summary>
    /// Initializes LogManager and registers <see cref="Logsmith.ILogger"/> and
    /// <see cref="Logsmith.ILogger{T}"/> in the service collection.
    /// </summary>
    public static IServiceCollection AddLogsmith(
        this IServiceCollection services,
        Action<LogConfigBuilder> configure)
    {
        LogManager.Initialize(configure);
        services.TryAddSingleton<Logsmith.ILogger>(
            _ => LogManager.GetLogger("Application"));
        services.TryAdd(ServiceDescriptor.Singleton(
            typeof(Logsmith.ILogger<>),
            typeof(LoggerOfT<>)));
        return services;
    }
}

/// <summary>
/// Open generic implementation of ILogger{T} for DI resolution.
/// Each T resolves to a LogManager-managed logger with category = typeof(T).Name.
/// </summary>
internal sealed class LoggerOfT<T> : Logsmith.ILogger<T>
{
    public LoggerContext Context { get; }

    public LoggerOfT()
    {
        Context = LogManager.GetLogger<T>().Context;
    }
}
