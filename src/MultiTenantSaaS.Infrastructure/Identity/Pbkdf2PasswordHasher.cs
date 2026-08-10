using System.Globalization;
using System.Security.Cryptography;
using MultiTenantSaaS.Application.Abstractions;

namespace MultiTenantSaaS.Infrastructure.Identity;

/// <summary>
/// Hashing de parole cu PBKDF2-HMAC-SHA512.
/// </summary>
/// <remarks>
/// <para>
/// Am ales PBKDF2 pentru că e în biblioteca standard .NET, fără dependințe externe, și e
/// acceptat de OWASP, NIST și FIPS. Argon2id ar fi mai rezistent la atacuri cu GPU, dar cere
/// un pachet terț; BCrypt e solid, însă trunchiază parolele la 72 de bytes.
/// </para>
/// <para>
/// Numărul de iterații e stocat <b>în interiorul hash-ului</b>, nu într-o constantă de cod.
/// Astfel îl putem crește peste doi ani, iar parolele vechi rămân verificabile: se validează
/// cu parametrii lor, apoi se pot re-hash-ui la următorul login reușit.
/// </para>
/// </remarks>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    // Recomandarea OWASP pentru PBKDF2-HMAC-SHA512.
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
            // Hash corupt în baza de date: refuzăm autentificarea, nu aruncăm 500.
            return false;
        }

        var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expectedKey.Length);

        // Comparație în timp constant: o comparație normală s-ar opri la primul byte diferit
        // și ar scurge informație despre hash prin durata răspunsului.
        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}
