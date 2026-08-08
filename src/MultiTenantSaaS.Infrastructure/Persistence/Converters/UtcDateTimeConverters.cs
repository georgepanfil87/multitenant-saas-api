using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MultiTenantSaaS.Infrastructure.Persistence.Converters;

/// <summary>
/// Garantează că orice <see cref="DateTime"/> ajunge în PostgreSQL ca UTC și se întoarce
/// cu <see cref="DateTimeKind.Utc"/>.
/// </summary>
/// <remarks>
/// Npgsql aruncă excepție dacă primește un <c>DateTime</c> cu <c>Kind.Unspecified</c> pentru
/// o coloană <c>timestamptz</c> - iar un DateTime deserializat din JSON este exact
/// Unspecified. Fără convertorul acesta, orice câmp de dată trimis de client crapă la salvare.
/// Unspecified e tratat ca UTC, nu convertit din ora locală, ca rezultatul să nu depindă
/// de fusul orar al serverului.
/// </remarks>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

/// <inheritdoc cref="UtcDateTimeConverter" />
public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter()
        : base(
            v => v.HasValue
                ? (v.Value.Kind == DateTimeKind.Local
                    ? v.Value.ToUniversalTime()
                    : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc))
                : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
    {
    }
}
