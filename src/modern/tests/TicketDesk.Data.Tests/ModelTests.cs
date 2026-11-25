using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using TicketDesk.Data.Entities;

namespace TicketDesk.Data.Tests;

/// <summary>Model-level pins that need no database (see docs/migration/ef6-to-efcore.md).</summary>
public sealed class ModelTests
{
    private static readonly IModel Model = Db.ModelOnly().Model;

    private static IProperty Prop<T>(string name) => Model.FindEntityType(typeof(T))!.FindProperty(name)!;

    [Theory]
    [InlineData(typeof(Ticket), nameof(Ticket.CreatedDate))]
    [InlineData(typeof(TicketEvent), nameof(TicketEvent.EventDate))]
    public void Ef6_computed_dates_are_never_written_and_read_back(Type entity, string property)
    {
        var p = Model.FindEntityType(entity)!.FindProperty(property)!;
        Assert.Equal(ValueGenerated.OnAddOrUpdate, p.ValueGenerated);
        Assert.Equal(PropertySaveBehavior.Ignore, p.GetBeforeSaveBehavior());
        Assert.Equal(PropertySaveBehavior.Ignore, p.GetAfterSaveBehavior());
        Assert.Equal("SYSDATETIMEOFFSET()", p.GetDefaultValueSql());
    }

    [Theory]
    [InlineData(typeof(Project))]
    [InlineData(typeof(Ticket))]
    [InlineData(typeof(TicketEvent))]
    [InlineData(typeof(TicketEventNotification))]
    public void Version_columns_are_rowversion_concurrency_tokens(Type entity)
    {
        var p = Model.FindEntityType(entity)!.FindProperty("Version")!;
        Assert.True(p.IsConcurrencyToken);
        Assert.Equal("rowversion", p.GetColumnType());
        Assert.Equal(ValueGenerated.OnAddOrUpdate, p.ValueGenerated);
    }

    [Fact]
    public void Concurrency_tokens_exist_only_where_the_legacy_schema_has_rowversion()
    {
        var withTokens = Model.GetEntityTypes().Where(e => e.GetProperties().Any(p => p.IsConcurrencyToken)).Select(e => e.ClrType.Name).Order();
        Assert.Equal([nameof(Project), nameof(Ticket), nameof(TicketEvent), nameof(TicketEventNotification)], withTokens);
    }

    [Fact]
    public void Delete_behaviours_follow_the_database_not_ef_conventions()
    {
        DeleteBehavior Of<TDependent, TPrincipal>() => Model.FindEntityType(typeof(TDependent))!.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(TPrincipal)).DeleteBehavior;

        // EF Core's convention would cascade these required relationships; the legacy FKs do not.
        Assert.Equal(DeleteBehavior.Restrict, Of<Ticket, Project>());
        Assert.Equal(DeleteBehavior.Restrict, Of<TicketEventNotification, TicketSubscriber>());
        Assert.Equal(DeleteBehavior.Cascade, Of<TicketEvent, Ticket>());
        Assert.Equal(DeleteBehavior.Cascade, Of<TicketEventNotification, TicketEvent>());
        Assert.Equal(DeleteBehavior.Cascade, Of<TicketSubscriber, Ticket>());
        Assert.Equal(DeleteBehavior.Cascade, Of<TicketTag, Ticket>());
    }

    [Fact]
    public void Columns_with_legacy_defaults_that_ef6_always_wrote_have_no_ef_default()
    {
        // Tickets.ProjectId DEFAULT ((1)) and TicketEvents.ForActivity DEFAULT ((16384)): with HasDefaultValue EF Core
        // would skip writing the CLR default (0) and the database default would silently apply.
        foreach (var p in new[] { Prop<Ticket>(nameof(Ticket.ProjectId)), Prop<TicketEvent>(nameof(TicketEvent.ForActivity)) })
        {
            Assert.Null(p.FindAnnotation(RelationalAnnotationNames.DefaultValue));
            Assert.Null(p.GetDefaultValueSql());
            Assert.Equal(ValueGenerated.Never, p.ValueGenerated);
        }
    }

    [Fact]
    public void EventId_is_an_identity_column_inside_the_composite_key()
    {
        var p = Prop<TicketEvent>(nameof(TicketEvent.EventId));
        Assert.Equal(SqlServerValueGenerationStrategy.IdentityColumn, p.GetValueGenerationStrategy());
        Assert.Equal(ValueGenerated.OnAdd, p.ValueGenerated);
        Assert.Equal(ValueGenerated.Never, Prop<TicketEvent>(nameof(TicketEvent.TicketId)).ValueGenerated);
    }

    [Fact]
    public void No_migrations_and_no_migration_history_tables_in_the_model()
    {
        using var db = Db.ModelOnly();
        Assert.Empty(db.Database.GetMigrations());
        Assert.DoesNotContain(Model.GetEntityTypes(), e => e.GetTableName() is "__MigrationHistory" or "__EFMigrationsHistory");
    }
}
