using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TicketDesk.Data.Entities;

namespace TicketDesk.Tickets.TicketCenter;

/// <summary>
/// The per-user ticket-center list settings stored as JSON in <c>dbo.UserSettings.ListSettingsJson</c> and shared with the
/// legacy app while both serve the ticket center (legacy <c>UserTicketListSettingsCollection</c>, written by Newtonsoft.Json).
/// Property names, order, enum-as-number and the .NET Framework type names in <c>ColumnValueType</c> are kept exactly, so
/// the legacy app reads what the new app writes and the other way round.
/// </summary>
public sealed class ListSetting
{
    [JsonPropertyOrder(0)] public string ListName { get; set; } = "";
    [JsonPropertyOrder(1)] public string? ListDisplayName { get; set; }
    [JsonPropertyOrder(2)] public int ListMenuDisplayOrder { get; set; }
    [JsonPropertyOrder(3)] public int ItemsPerPage { get; set; }
    [JsonPropertyOrder(4)] public List<SortColumn> SortColumns { get; set; } = [];
    [JsonPropertyOrder(5)] public List<FilterColumn> FilterColumns { get; set; } = [];
    [JsonPropertyOrder(6)] public List<string> DisabledFilterColumnNames { get; set; } = [];

    public FilterColumn? Filter(string columnName) => FilterColumns.SingleOrDefault(f => f.ColumnName == columnName);

    /// <summary>Legacy <c>ModifyFilterSettings</c>: page size, then each filter unless the list disables it.</summary>
    public void ModifyFilterSettings(int pageSize, string? ticketStatus, string? owner, string? assignedTo)
    {
        ItemsPerPage = pageSize;
        if (!DisabledFilterColumnNames.Contains("TicketStatus")) ChangeTicketStatusFilter(ticketStatus);
        if (!DisabledFilterColumnNames.Contains("Owner")) ChangeUserFilter("Owner", owner, allowUnassigned: false);
        if (!DisabledFilterColumnNames.Contains("AssignedTo")) ChangeUserFilter("AssignedTo", assignedTo, allowUnassigned: true);
    }

    private void ChangeTicketStatusFilter(string? statusValue)
    {
        if (string.IsNullOrEmpty(statusValue)) return;
        var column = Filter("TicketStatus");
        if (statusValue == "Any")
        {
            if (column != null) FilterColumns.Remove(column);
            return;
        }
        if (column == null)
        {
            column = new FilterColumn { ColumnName = "TicketStatus", ColumnValueType = FilterColumn.TicketStatusType };
            FilterColumns.Add(column);
        }
        column.UseEqualityComparison = statusValue != "Open";
        // the legacy app stores the name as a string here (its defaults store the enum as a number)
        column.ColumnValue = JsonValue.Create(statusValue == "Open" ? "Closed" : statusValue);
    }

    private void ChangeUserFilter(string columnName, string? value, bool allowUnassigned)
    {
        if (string.IsNullOrEmpty(value)) return;
        var column = Filter(columnName);
        if (value == "anyone")
        {
            if (column != null) FilterColumns.Remove(column);
            return;
        }
        if (column == null)
        {
            column = new FilterColumn { ColumnName = columnName, ColumnValueType = FilterColumn.StringType };
            FilterColumns.Add(column);
        }
        if (allowUnassigned && value == "unassigned")
        {
            column.UseEqualityComparison = null;
            column.ColumnValue = null;
        }
        else
        {
            column.UseEqualityComparison = true;
            column.ColumnValue = JsonValue.Create(value);
        }
    }

    /// <summary>
    /// Legacy <c>SortList</c>: plain click flips a sorted column or replaces the sort; shift-click (multi) adds a column or
    /// removes it (never the last one). The column name must be whitelisted (S5) before it gets here.
    /// </summary>
    public void ChangeSort(string columnName, bool isMultiSort)
    {
        var column = SortColumns.SingleOrDefault(s => s.ColumnName == columnName);
        if (isMultiSort)
        {
            if (column != null)
            {
                if (SortColumns.Count > 1) SortColumns.Remove(column);
            }
            else SortColumns.Add(new SortColumn { ColumnName = columnName, SortDirection = SortDirection.Ascending });
        }
        else if (column != null)
        {
            column.SortDirection = column.SortDirection == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
        }
        else
        {
            SortColumns.Clear();
            SortColumns.Add(new SortColumn { ColumnName = columnName, SortDirection = SortDirection.Ascending });
        }
    }
}

public enum SortDirection
{
    Ascending = 0,
    Descending = 1,
}

public sealed class SortColumn
{
    [JsonPropertyOrder(0)] public SortDirection SortDirection { get; set; }
    [JsonPropertyOrder(1)] public string ColumnName { get; set; } = "";

    /// <summary>Written for the legacy reader (it recomputes it): "LastUpdateDate" → "Last Update Date".</summary>
    [JsonPropertyOrder(2)]
    public string ColumnFriendlyName
    {
        get => string.Concat(ColumnName.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
        set { }
    }
}

public sealed class FilterColumn
{
    public const string TicketStatusType = "TicketDesk.Domain.Model.TicketStatus, TicketDesk.Domain, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
    public const string StringType = "System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";

    [JsonPropertyOrder(0)] public bool? UseEqualityComparison { get; set; }
    [JsonPropertyOrder(1)] public string ColumnName { get; set; } = "";
    /// <summary>A number (the legacy enum default), a string (a status name or a user id) or null (unassigned).</summary>
    [JsonPropertyOrder(2)] public JsonNode? ColumnValue { get; set; }
    [JsonPropertyOrder(3)] public string? ColumnValueType { get; set; }

    /// <summary>
    /// The value as the legacy query sees it. For the status the legacy code parses the stored value (number or name) with
    /// <c>Enum.Parse</c>, which also makes the filter bar show the name.
    /// </summary>
    public TicketStatus? StatusValue()
    {
        if (ColumnValue is not JsonValue v) return null;
        if (v.TryGetValue<int>(out var n)) return Enum.IsDefined(typeof(TicketStatus), n) ? (TicketStatus)n : null;
        if (v.TryGetValue<string>(out var s))
        {
            if (int.TryParse(s, out n) && Enum.IsDefined(typeof(TicketStatus), n)) return (TicketStatus)n;
            return Enum.TryParse<TicketStatus>(s, ignoreCase: false, out var status) && Enum.IsDefined(status) ? status : null;
        }
        return null;
    }

    public string? StringValue() => ColumnValue is JsonValue v && v.TryGetValue<string>(out var s) ? s : ColumnValue?.ToJsonString();
}

/// <summary>Defaults, JSON and the "required lists" check of the legacy list settings.</summary>
public static class ListSettings
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // Newtonsoft writes non-ASCII as is; both apps read either form
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static readonly string[] HelpDeskOnlyLists = ["unassigned", "assignedToMe"];

    public static List<ListSetting>? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<List<ListSetting>>(json, Json); }
        catch (JsonException) { return null; }
    }

    public static string Serialize(IEnumerable<ListSetting> settings) => JsonSerializer.Serialize(settings, Json);

    /// <summary>Legacy <c>HasRequiredDefaultListSettings</c>: the lists of the user's role must all be there.</summary>
    public static bool HasRequiredLists(IReadOnlyCollection<ListSetting> lists, bool isHelpDeskOrAdmin) =>
        (!isHelpDeskOrAdmin || HelpDeskOnlyLists.All(n => lists.Any(l => l.ListName == n))) &&
        new[] { "mytickets", "opentickets", "historytickets" }.All(n => lists.Any(l => l.ListName == n));

    /// <summary>Legacy <c>UserTicketListSetting.GetDefaultListSettings</c>.</summary>
    public static List<ListSetting> Defaults(string userId, bool isHelpDeskOrAdmin, Func<string, string> displayName)
    {
        static SortColumn Asc(string c) => new() { ColumnName = c, SortDirection = SortDirection.Ascending };
        static SortColumn Desc(string c) => new() { ColumnName = c, SortDirection = SortDirection.Descending };
        static FilterColumn NotClosed() => new() { UseEqualityComparison = false, ColumnName = "TicketStatus", ColumnValue = JsonValue.Create((int)TicketStatus.Closed), ColumnValueType = FilterColumn.TicketStatusType };
        static FilterColumn User(string column, bool? eq, string? id) => new() { UseEqualityComparison = eq, ColumnName = column, ColumnValue = id == null ? null : JsonValue.Create(id), ColumnValueType = FilterColumn.StringType };
        ListSetting List(string name, int order, List<SortColumn> sorts, List<FilterColumn> filters, string disabled) => new()
        {
            ListName = name, ListDisplayName = displayName(name), ListMenuDisplayOrder = order, ItemsPerPage = 20,
            SortColumns = sorts, FilterColumns = filters, DisabledFilterColumnNames = [disabled],
        };
        var lists = new List<ListSetting>();
        var order = 0;
        if (isHelpDeskOrAdmin)
        {
            lists.Add(List("unassigned", order++, [Asc("TargetDate"), Asc("DueDate"), Desc("LastUpdateDate")], [NotClosed(), User("AssignedTo", null, null)], "AssignedTo"));
            lists.Add(List("assignedToMe", order++, [Asc("TicketStatus"), Asc("TargetDate"), Asc("DueDate"), Desc("LastUpdateDate")], [NotClosed(), User("AssignedTo", true, userId)], "AssignedTo"));
        }
        lists.Add(List("mytickets", order++, [Asc("TargetDate"), Asc("DueDate"), Desc("LastUpdateDate")], [NotClosed(), User("Owner", true, userId)], "Owner"));
        lists.Add(List("opentickets", order++, [Asc("TicketStatus"), Asc("TargetDate"), Asc("DueDate"), Desc("LastUpdateDate")], [NotClosed()], "TicketStatus"));
        var closed = new FilterColumn { UseEqualityComparison = true, ColumnName = "TicketStatus", ColumnValue = JsonValue.Create((int)TicketStatus.Closed), ColumnValueType = FilterColumn.TicketStatusType };
        lists.Add(List("historytickets", order, [Asc("TicketStatus"), Desc("LastUpdateDate")], [closed], "TicketStatus"));
        return lists;
    }
}
