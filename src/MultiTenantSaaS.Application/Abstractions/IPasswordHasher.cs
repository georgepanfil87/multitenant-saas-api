namespace MultiTenantSaaS.Application.Abstractions;

public interface IPasswordHasher
{
    /// <summary>Produces a self-describing hash that embeds the salt and parameters.</summary>
    string Hash(string password);

    /// <summary>Verifies a password. Returns false for malformed hashes rather than throwing.</summary>
    bool Verify(string password, string hash);
}
