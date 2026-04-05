namespace Logsmith;

/// <summary>
/// Marker interface for a logger whose category is derived from <typeparamref name="T"/>.
/// </summary>
public interface ILogger<T> : ILogger;
