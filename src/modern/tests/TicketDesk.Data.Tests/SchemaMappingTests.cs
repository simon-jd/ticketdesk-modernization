using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using TicketDesk.Testing;

namespace TicketDesk.Data.Tests;

/// <summary>
/// The EF Core model must describe the schema exactly (the legacy schema plus the expand migrations of td-db): same tables, same columns with the same store types,
/// nullability and identity, same primary keys (in order) and the same foreign keys with the same delete actions.
/// Runs against a fresh scratch database (EF6-implied schema) and against the seeded database, which can be pointed at
/// the restored legacy database with TD_MODERN_SEEDED_DB=TicketDesk.
/// </summary>
public sealed class SchemaMappingTests(ScratchDatabase scratch, SeededDatabase seeded)
{
    [Fact]
    public async Task Model_matches_scratch_database_schema()
    {
        Assert.SkipUnless(scratch.Available, SqlTestEnvironment.SkipReason);
        await AssertModelMatches(scratch.ConnectionString!);
    }

    [Fact]
    public async Task Model_matches_seeded_database_schema()
    {
        Assert.SkipUnless(seeded.ConnectionString != null, $"{SqlTestEnvironment.SkipReason} and seed {SqlTestEnvironment.SeededDatabaseName}");
        await AssertModelMatches(seeded.ConnectionString!);
    }

    private static async Task AssertModelMatches(string conn)
    {
        await using var db = Db.Create(conn);
        var database = await DatabaseSchema.LoadAsync(conn);
        var model = ModelSchema.From(db.Model);

        // the two migration journals (EF6's and td-db's) are not part of the domain model, nor is the Data Protection key ring
        // shared with the legacy app (V0010, mapped by TicketDesk.Identity's TicketDeskKeyRingContext). notifications.* (push
        // notifications, EF6 automatic migrations) exists in the restored legacy database but not in the scratch schema
        var withNotifications = database.Tables.Keys.Any(t => t.StartsWith(NotificationsPrefix, StringComparison.Ordinal));
        var modelTables = model.Tables.Keys.Where(t => withNotifications || !t.StartsWith(NotificationsPrefix, StringComparison.Ordinal)).ToList();
        Assert.Equal(database.Tables.Keys.Where(t => t is not ("__MigrationHistory" or "__ModernSchemaHistory" or "DataProtectionKeys")).Order(), modelTables.Order());
        foreach (var (table, modelColumns) in model.Tables.Where(t => modelTables.Contains(t.Key)))
        {
            var dbColumns = database.Tables[table];
            Assert.True(dbColumns.Keys.Order().SequenceEqual(modelColumns.Keys.Order()),
                $"{table}: database columns [{string.Join(", ", dbColumns.Keys.Order())}] vs model [{string.Join(", ", modelColumns.Keys.Order())}]");
            foreach (var (column, expected) in dbColumns)
                Assert.True(expected == modelColumns[column], $"{table}.{column}: database {expected} vs model {modelColumns[column]}");
        }
        foreach (var (table, pk) in model.PrimaryKeys.Where(t => modelTables.Contains(t.Key)))
            Assert.True(database.PrimaryKeys[table].SequenceEqual(pk), $"{table} PK: database ({string.Join(", ", database.PrimaryKeys[table])}) vs model ({string.Join(", ", pk)})");
        Assert.Equal(database.ForeignKeys.Order(), model.ForeignKeys.Where(f => withNotifications || !f.StartsWith(NotificationsPrefix, StringComparison.Ordinal)).Order());
    }

    private const string NotificationsPrefix = "notifications.";

    /// <summary>Table key: the name for dbo, schema.name otherwise.</summary>
    private static string TableKey(string? schema, string table) => schema is null or "dbo" ? table : $"{schema}.{table}";

    private const string Schemas = "('dbo', 'notifications')";

    public sealed record Column(string StoreType, bool Nullable, bool Identity)
    {
        public override string ToString() => $"{StoreType}{(Nullable ? " null" : " not null")}{(Identity ? " identity" : "")}";
    }

    private sealed record Schema(
        Dictionary<string, Dictionary<string, Column>> Tables,
        Dictionary<string, List<string>> PrimaryKeys,
        List<string> ForeignKeys);

    private static class DatabaseSchema
    {
        public static async Task<Schema> LoadAsync(string conn)
        {
            var tables = new Dictionary<string, Dictionary<string, Column>>();
            foreach (var r in await Sql.RowsAsync(conn, $"""
                SELECT t.name, c.name, ty.name, c.max_length, c.precision, c.scale, c.is_nullable, c.is_identity, SCHEMA_NAME(t.schema_id)
                FROM sys.columns c
                JOIN sys.tables t ON t.object_id = c.object_id
                JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                WHERE SCHEMA_NAME(t.schema_id) IN {Schemas}
                """))
            {
                var type = (string)r[2]!;
                var maxLength = Convert.ToInt32(r[3]);
                var storeType = type switch
                {
                    "nvarchar" or "nchar" => maxLength == -1 ? $"{type}(max)" : $"{type}({maxLength / 2})",
                    "varchar" or "char" or "varbinary" or "binary" => maxLength == -1 ? $"{type}(max)" : $"{type}({maxLength})",
                    "decimal" or "numeric" => $"decimal({r[4]},{r[5]})",
                    "datetimeoffset" or "datetime2" or "time" => $"{type}({r[5]})",
                    "timestamp" => "rowversion",
                    _ => type,
                };
                var table = TableKey((string)r[8]!, (string)r[0]!);
                if (!tables.TryGetValue(table, out var cols)) tables[table] = cols = new Dictionary<string, Column>();
                cols[(string)r[1]!] = new Column(storeType, (bool)r[6]!, (bool)r[7]!);
            }

            var pks = new Dictionary<string, List<string>>();
            foreach (var r in await Sql.RowsAsync(conn, $"""
                SELECT t.name, c.name, SCHEMA_NAME(t.schema_id)
                FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                JOIN sys.tables t ON t.object_id = i.object_id
                WHERE i.is_primary_key = 1 AND SCHEMA_NAME(t.schema_id) IN {Schemas}
                ORDER BY t.name, ic.key_ordinal
                """))
            {
                var table = TableKey((string)r[2]!, (string)r[0]!);
                if (!pks.TryGetValue(table, out var list)) pks[table] = list = [];
                list.Add((string)r[1]!);
            }

            var fks = new Dictionary<string, (string Parent, List<string> ParentCols, string Ref, List<string> RefCols, string Action)>();
            foreach (var r in await Sql.RowsAsync(conn, $"""
                SELECT fk.name, tp.name, cp.name, tr.name, cr.name, fk.delete_referential_action_desc, SCHEMA_NAME(tp.schema_id), SCHEMA_NAME(tr.schema_id)
                FROM sys.foreign_keys fk
                JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
                JOIN sys.tables tp ON tp.object_id = fkc.parent_object_id
                JOIN sys.columns cp ON cp.object_id = fkc.parent_object_id AND cp.column_id = fkc.parent_column_id
                JOIN sys.tables tr ON tr.object_id = fkc.referenced_object_id
                JOIN sys.columns cr ON cr.object_id = fkc.referenced_object_id AND cr.column_id = fkc.referenced_column_id
                WHERE SCHEMA_NAME(tp.schema_id) IN {Schemas}
                ORDER BY fk.name, fkc.constraint_column_id
                """))
            {
                var name = (string)r[0]!;
                if (!fks.TryGetValue(name, out var fk)) fks[name] = fk = (TableKey((string)r[6]!, (string)r[1]!), [], TableKey((string)r[7]!, (string)r[3]!), [], (string)r[5]!);
                fk.ParentCols.Add((string)r[2]!);
                fk.RefCols.Add((string)r[4]!);
            }
            var fkList = fks.Values.Select(f => ModelSchema.FkSignature(f.Parent, f.ParentCols, f.Ref, f.RefCols, f.Action)).ToList();
            return new Schema(tables, pks, fkList);
        }
    }

    private static class ModelSchema
    {
        public static string FkSignature(string parent, IEnumerable<string> parentCols, string referenced, IEnumerable<string> refCols, string action) =>
            $"{parent}({string.Join(",", parentCols)}) -> {referenced}({string.Join(",", refCols)}) ON DELETE {action}";

        public static Schema From(IModel model)
        {
            var tables = new Dictionary<string, Dictionary<string, Column>>();
            var pks = new Dictionary<string, List<string>>();
            var fks = new List<string>();
            foreach (var entity in model.GetEntityTypes())
            {
                var table = TableKey(entity.GetSchema(), entity.GetTableName()!);
                var store = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                var cols = new Dictionary<string, Column>();
                foreach (var p in entity.GetProperties())
                {
                    var storeType = p.GetColumnType(store).ToLowerInvariant() switch
                    {
                        "datetimeoffset" => "datetimeoffset(7)",
                        "datetime2" => "datetime2(7)",
                        var t => t.Replace("decimal(18, 2)", "decimal(18,2)"),
                    };
                    var identity = p.GetValueGenerationStrategy(store) == Microsoft.EntityFrameworkCore.Metadata.SqlServerValueGenerationStrategy.IdentityColumn;
                    cols[p.GetColumnName(store)!] = new Column(storeType, p.IsColumnNullable(store), identity);
                }
                tables[table] = cols;
                pks[table] = entity.FindPrimaryKey()!.Properties.Select(p => p.GetColumnName(store)!).ToList();
                foreach (var fk in entity.GetForeignKeys())
                {
                    var principal = fk.PrincipalEntityType;
                    var pstore = StoreObjectIdentifier.Table(principal.GetTableName()!, principal.GetSchema());
                    var action = fk.DeleteBehavior switch
                    {
                        DeleteBehavior.Cascade => "CASCADE",
                        DeleteBehavior.SetNull => "SET_NULL",
                        _ => "NO_ACTION",
                    };
                    fks.Add(FkSignature(table, fk.Properties.Select(p => p.GetColumnName(store)!), TableKey(principal.GetSchema(), principal.GetTableName()!),
                        fk.PrincipalKey.Properties.Select(p => p.GetColumnName(pstore)!), action));
                }
            }
            return new Schema(tables, pks, fks);
        }
    }
}
