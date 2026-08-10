namespace MultiTenantSaaS.Application.Abstractions;

/// <summary>Hashing și verificare de parole.</summary>
public interface IPasswordHasher
{
    /// <summary>Produce un hash care include salt-ul și parametrii, ca să poată fi verificat singur.</summary>
    string Hash(string password);

    /// <summary>Verifică o parolă în clar față de un hash stocat. Nu aruncă pentru hash-uri malformate.</summary>
    bool Verify(string password, string hash);
}
