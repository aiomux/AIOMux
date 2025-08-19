using System;
using System.Collections.Concurrent;

namespace AIOMux.Core.Configuration;

/// <summary>
/// A simple rate limiter for request throttling.
/// </summary>
public class RateLimiter
{
    private readonly int _maxRequestsPerMinute;
    private readonly ConcurrentQueue<DateTime> _requestTimestamps = new();

    public RateLimiter(int maxRequestsPerMinute)
    {
        _maxRequestsPerMinute = maxRequestsPerMinute;
    }

    /// <summary>
    /// Attempts to make a request, checking against the rate limit.
    /// </summary>
    /// <returns>True if the request is allowed; otherwise, false.</returns>
    public bool TryRequest()
    {
        var now = DateTime.UtcNow;
        // Remove timestamps older than 1 minute
        while (_requestTimestamps.TryPeek(out var oldest) && (now - oldest).TotalSeconds > 60)
            _requestTimestamps.TryDequeue(out _);
        if (_requestTimestamps.Count >= _maxRequestsPerMinute)
            return false;
        _requestTimestamps.Enqueue(now);
        return true;
    }
}
