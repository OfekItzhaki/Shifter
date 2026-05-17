namespace Jobuler.Application.Common;

/// <summary>
/// Abstraction for distributed caching (Redis-backed in production).
/// Used to cache frequently-read, rarely-written data like schedules and live status.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPatternAsync(string pattern, CancellationToken ct = default);
}
