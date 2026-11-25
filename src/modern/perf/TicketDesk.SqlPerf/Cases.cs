using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TicketDesk.SqlPerf;

/// <summary>A file of captured scenarios: each scenario is the list of commands one API call sends, in order.</summary>
public sealed class CaseFile
{
    public string Set { get; set; } = "";
    public string Database { get; set; } = "";
    public string CapturedAt { get; set; } = "";
    public string Source { get; set; } = "";
    public Dictionary<string, string> Values { get; set; } = [];
    public List<Scenario> Scenarios { get; set; } = [];

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static CaseFile Load(string path) => JsonSerializer.Deserialize<CaseFile>(File.ReadAllText(path), Json)!;
    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Json) + "\n");
}

public sealed class Scenario
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<CapturedCommand> Commands { get; set; } = [];
}

public sealed class CapturedCommand
{
    public CommandType CommandType { get; set; } = CommandType.Text;
    public string Text { get; set; } = "";
    public List<CapturedParameter> Parameters { get; set; } = [];

    public static CapturedCommand From(DbCommand cmd) => new()
    {
        CommandType = cmd.CommandType,
        Text = cmd.CommandText,
        Parameters = cmd.Parameters.Cast<SqlParameter>().Select(CapturedParameter.From).ToList(),
    };

    public SqlCommand ToSqlCommand(SqlConnection connection, int timeoutSeconds)
    {
        var cmd = new SqlCommand(Text, connection) { CommandType = CommandType, CommandTimeout = timeoutSeconds };
        foreach (var p in Parameters) cmd.Parameters.Add(p.ToSqlParameter());
        return cmd;
    }
}

/// <summary>A parameter exactly as SqlClient sent it (type, size, precision, scale, direction, value).</summary>
public sealed class CapturedParameter
{
    public string Name { get; set; } = "";
    public SqlDbType SqlDbType { get; set; }
    public int Size { get; set; }
    public byte Precision { get; set; }
    public byte Scale { get; set; }
    public ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    public bool IsNullable { get; set; }
    public string? ClrType { get; set; }
    public string? Value { get; set; }

    public static CapturedParameter From(SqlParameter p)
    {
        var v = p.Value is DBNull ? null : p.Value;
        return new CapturedParameter
        {
            Name = p.ParameterName,
            SqlDbType = p.SqlDbType,
            Size = p.Size,
            Precision = p.Precision,
            Scale = p.Scale,
            Direction = p.Direction,
            IsNullable = p.IsNullable,
            ClrType = v?.GetType().FullName,
            Value = v switch
            {
                null => null,
                DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
                DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
                byte[] b => Convert.ToBase64String(b),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => v.ToString(),
            },
        };
    }

    public SqlParameter ToSqlParameter()
    {
        object? v = Value == null ? null : ClrType switch
        {
            "System.Int32" => int.Parse(Value, CultureInfo.InvariantCulture),
            "System.Int64" => long.Parse(Value, CultureInfo.InvariantCulture),
            "System.Int16" => short.Parse(Value, CultureInfo.InvariantCulture),
            "System.Byte" => byte.Parse(Value, CultureInfo.InvariantCulture),
            "System.Boolean" => bool.Parse(Value),
            "System.Decimal" => decimal.Parse(Value, CultureInfo.InvariantCulture),
            "System.Double" => double.Parse(Value, CultureInfo.InvariantCulture),
            "System.DateTimeOffset" => DateTimeOffset.Parse(Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            "System.DateTime" => DateTime.Parse(Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            "System.Guid" => Guid.Parse(Value),
            "System.Byte[]" => Convert.FromBase64String(Value),
            _ => Value,
        };
        return new SqlParameter(Name, SqlDbType)
        {
            Size = Size, Precision = Precision, Scale = Scale, Direction = Direction, IsNullable = IsNullable,
            Value = v ?? DBNull.Value,
        };
    }
}

/// <summary>Records every command EF Core executes while <see cref="Current"/> names a scenario.</summary>
public sealed class CommandRecorder : DbCommandInterceptor
{
    public Scenario? Current { get; set; }

    private void Record(DbCommand command) => Current?.Commands.Add(CapturedCommand.From(command));

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Record(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Record(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Record(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Record(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Record(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Record(command);
        return ValueTask.FromResult(result);
    }
}
