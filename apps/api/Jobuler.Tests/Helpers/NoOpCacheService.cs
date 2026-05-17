using Jobuler.Application.Common;

namespace Jobuler.Tests.Helpers;

/// <summary>
/// No-op cache service for unit tests — always returns cache miss, never stores.
/// </summary>
public class NoOpCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => Task.FromResult<T?>(default);
    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) => Task.CompletedTask;
    public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    public Task RemoveByPatternAsync(string pattern, CancellationToken ct = default) => Task.CompletedTask;
}
