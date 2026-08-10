using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MultiTenantSaaS.Infrastructure.Persistence.Converters;

/// <summary>
/// Ensures every <see cref="DateTime"/> reaches PostgreSQL as UTC and comes back with
/// <see cref="DateTimeKind.Utc"/>. Npgsql rejects Unspecified values for timestamptz columns,
/// and JSON deserialization produces exactly that. Unspecified is treated as UTC rather than
/// converted from local time, so results do not depend on the server's time zone.
/// </summary>
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
