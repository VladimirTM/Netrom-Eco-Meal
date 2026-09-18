using System.Security.Cryptography;

namespace Netrom_Eco_Meal.Services;

// Only the hash is ever persisted (Business.WebhookApiKeyHash) — the plaintext is shown once.
public static class ApiKeyHasher
{
    public static string Generate()
    {
        // "eco_" prefix makes a leaked key recognizable, same convention as GitHub/Stripe tokens.
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "eco_" + Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public static string Hash(string plaintext)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexString(bytes);
    }
}
