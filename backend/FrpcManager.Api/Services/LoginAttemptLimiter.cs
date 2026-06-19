using System.Collections.Concurrent;

namespace FrpcManager.Api.Services;

public class LoginAttemptLimiter
{
    private readonly ConcurrentDictionary<string, List<DateTime>> _attempts = new();
    private readonly int _permitLimit;
    private readonly TimeSpan _window;

    public LoginAttemptLimiter(IConfiguration configuration)
    {
        _permitLimit = configuration.GetValue("LoginSecurity:IpUsernamePermitLimit", 5);
        _window = TimeSpan.FromMinutes(configuration.GetValue("LoginSecurity:IpUsernameWindowMinutes", 1));
    }

    public bool IsLimited(string ipAddress, string username, DateTime now)
    {
        var normalizedUsername = username.Trim().ToLowerInvariant();
        var key = $"{ipAddress}|{normalizedUsername}";
        var attempts = _attempts.GetOrAdd(key, _ => []);

        lock (attempts)
        {
            attempts.RemoveAll(t => now - t > _window);
            if (attempts.Count >= _permitLimit)
                return true;

            attempts.Add(now);
            return false;
        }
    }
}
