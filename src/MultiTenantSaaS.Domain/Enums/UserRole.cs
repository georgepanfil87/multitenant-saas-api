namespace MultiTenantSaaS.Domain.Enums;

/// <summary>Nivelele de acces din platformă.</summary>
/// <remarks>
/// Valorile sunt numerotate explicit pentru că se persistă ca <c>Users.RoleId</c>.
/// Nu reordona și nu reutiliza o valoare existentă: ar schimba în tăcere rolul userilor existenți.
/// </remarks>
public enum UserRole
{
    /// <summary>Administrator de platformă. Singurul rol care poate trece granița de tenant.</summary>
    GlobalAdmin = 1,

    /// <summary>Administrator al organizației client. Putere deplină doar în interiorul tenantului său.</summary>
    TenantAdmin = 2,

    /// <summary>Utilizator obișnuit al organizației.</summary>
    Member = 3
}
