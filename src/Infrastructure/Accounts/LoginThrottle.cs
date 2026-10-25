using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

namespace Shop.Infrastructure.Accounts;

/// <summary>
/// Sign-in attempts per account per minute (<c>Auth:AccountAttemptsPerMinute</c>, default 5), in memory (single instance).
/// The key is whatever e-mail string was typed, existing account or not.
/// </summary>
internal sealed class LoginThrottle(IConfiguration configuration, TimeProvider clock)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _attempts = new();

    public bool TryAcquire(string email)
    {
        var limit = configuration.GetValue("Auth:AccountAttemptsPerMinute", 5);
        var now = clock.GetUtcNow();
        var queue = _attempts.GetOrAdd(email.Trim().ToUpperInvariant(), _ => new Queue<DateTimeOffset>());
        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() > Window)
            {
                queue.Dequeue();
            }

            if (queue.Count >= limit)
            {
                return false;
            }

            queue.Enqueue(now);
            return true;
        }
    }
}
