using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TicketDesk.Data.Configurations;

internal static class MappingConventions
{
    public const string Schema = "dbo";

    /// <summary>
    /// EF6 <c>[DatabaseGenerated(Computed)]</c>: the column default fills the value, EF never writes it (not on insert,
    /// not on update) and reads it back after saving. This is not an EF Core computed column (there is none in the schema).
    /// </summary>
    public static PropertyBuilder<T> DatabaseDefaultNeverWritten<T>(this PropertyBuilder<T> property, string defaultSql)
    {
        property.HasDefaultValueSql(defaultSql).ValueGeneratedOnAddOrUpdate();
        property.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        property.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        return property;
    }

    /// <summary>Legacy <c>datetime</c> columns that hold UTC (Identity's <c>LockoutEndDateUtc</c>).</summary>
    public static readonly ValueConverter<DateTime, DateTime> UtcDateTime =
        new(v => SqlUtcDateTime.ToStore(v), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}

public static class SqlUtcDateTime
{
    /// <summary>Largest value a SQL Server <c>datetime</c> column holds (1/300 s precision).</summary>
    public static readonly DateTime MaxValue = new(9999, 12, 31, 23, 59, 59, 997, DateTimeKind.Utc);

    /// <summary>
    /// Converts to UTC and clamps to <see cref="MaxValue"/>: <see cref="DateTime.MaxValue"/> (the legacy "disabled user"
    /// marker) would otherwise round up past the end of the <c>datetime</c> range and fail with an overflow.
    /// </summary>
    public static DateTime ToStore(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return utc > MaxValue ? MaxValue : utc;
    }
}
