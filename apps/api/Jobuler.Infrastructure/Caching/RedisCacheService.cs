using System.Text.Json;
using Jobuler.Application.Common;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Jobuler.Infrastructure.Caching;

/// <summary>
/// Redis-backed implementation of ICacheService.
/// Gracefully degrades (returns cache miss) if Redis is unavailable.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);
            if (value.IsNullOrEmpty)
                return default;

            return JsonSerializer.Deserialize<T>(value!, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GET failed for key {Key} — treating as cache miss", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await db.StringSetAsync(key, json, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET failed for key {Key} — skipping cache write", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis DEL failed for key {Key}", key);
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
    {
        try
        {
            foreach (var endpoint in _redis.GetEndPoints())
            {
                var server = _redis.GetServer(endpoint);
                var keys = server.Keys(pattern: pattern).ToArray();
                if (keys.Length > 0)
                {
                    var db = _redis.GetDatabase();
                    await db.KeyDeleteAsync(keys);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis pattern DEL failed for pattern {Pattern}", pattern);
        }
    }
}
