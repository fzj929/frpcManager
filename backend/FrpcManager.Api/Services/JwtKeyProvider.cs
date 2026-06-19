using System.Security.Cryptography;

namespace FrpcManager.Api.Services;

public class JwtKeyProvider
{
    private readonly string _key;

    public JwtKeyProvider(IConfiguration configuration)
    {
        var configuredKey = configuration["Jwt:Key"];
        if (!string.IsNullOrWhiteSpace(configuredKey))
        {
            _key = configuredKey;
            return;
        }

        var keyFile = configuration["Jwt:KeyFile"];
        if (string.IsNullOrWhiteSpace(keyFile))
            keyFile = "data/jwt.key";

        if (!Path.IsPathRooted(keyFile))
            keyFile = Path.Combine(AppContext.BaseDirectory, keyFile);

        var keyDirectory = Path.GetDirectoryName(keyFile);
        if (!string.IsNullOrEmpty(keyDirectory))
            Directory.CreateDirectory(keyDirectory);

        if (File.Exists(keyFile))
        {
            _key = File.ReadAllText(keyFile).Trim();
            if (!string.IsNullOrWhiteSpace(_key))
                return;
        }

        _key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        File.WriteAllText(keyFile, _key);
    }

    public string Key => _key;
}
