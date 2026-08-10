using System.Globalization;
using System.Security.Cryptography;
using MultiTenantSaaS.Application.Abstractions;

namespace MultiTenantSaaS.Infrastructure.Identity;

/// <summary>
/// PBKDF2-HMAC-SHA512 password hashing. Chosen because it ships with .NET and is accepted by
/// OWASP, NIST and FIPS; Argon2id resists GPU attacks better but needs a third-party package.
/// The iteration count is stored inside the hash, so it can be raised later while old hashes
/// stay verifiable with their original parameters.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    // OWASP recommendation for PBKDF2-HMAC-SHA512.
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const char Separator = '.';

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

        return string.Join(Separator,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        var parts = hash.Split(Separator);
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedKey;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedKey = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            // Corrupt hash in the database: refuse the login instead of returning a 500.
            return false;
        }

        var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expectedKey.Length);

        // Constant-time comparison: a normal one would stop at the first differing byte and leak
        // information about the hash through response timing.
        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}
