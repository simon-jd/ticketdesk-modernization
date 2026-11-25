using System.Globalization;
using TicketDesk.Data.Entities;
using D = TicketDesk.Localization.Domain.Strings;

namespace TicketDesk.Web.Views;

/// <summary>Formatting the legacy ticket views did inline (current culture, the stored offset's clock time).</summary>
public static class TicketText
{
    /// <summary><c>TicketStatus.GetDisplayName()</c>.</summary>
    public static string Status(TicketStatus status) => D.ResourceManager.GetString("TicketStatus" + status, CultureInfo.CurrentUICulture) ?? status.ToString();

    /// <summary><c>Date.DateTime.ToShortDateString()</c>.</summary>
    public static string ShortDate(DateTimeOffset d) => d.DateTime.ToString("d", CultureInfo.CurrentCulture);

    /// <summary><c>Date.DateTime.ToShortTimeString()</c> / <c>ToString("t")</c>.</summary>
    public static string ShortTime(DateTimeOffset d) => d.ToString("t", CultureInfo.CurrentCulture).Replace('\u202F', ' '); // ICU: narrow no-break space before AM/PM; NLS wrote a space

    /// <summary><c>EventDate.ToString("D")</c>.</summary>
    public static string LongDate(DateTimeOffset d) => d.ToString("D", CultureInfo.CurrentCulture);
}

/// <summary>Display names of ticket activities (legacy <c>TicketActivity.GetDisplayName()</c>: <c>TicketActivity_*</c> resources).</summary>
public static class ActivityText
{
    private static readonly Dictionary<TicketActivity, string> Keys = new()
    {
        [TicketActivity.AddComment] = "TicketActivity_Comment",
        [TicketActivity.SupplyMoreInfo] = "TicketActivity_ProvideInfo",
        [TicketActivity.CancelMoreInfo] = "TicketActivity_CancelMoreInfo",
        [TicketActivity.RequestMoreInfo] = "TicketActivity_RequestMoreInfo",
        [TicketActivity.TakeOver] = "TicketActivity_TakeOver",
        [TicketActivity.Resolve] = "TicketActivity_Resolve",
        [TicketActivity.Assign] = "TicketActivity_Assign",
        [TicketActivity.ReAssign] = "TicketActivity_ReAssign",
        [TicketActivity.Pass] = "TicketActivity_Pass",
        [TicketActivity.Close] = "TicketActivity_Close",
        [TicketActivity.ReOpen] = "TicketActivity_ReOpen",
        [TicketActivity.GiveUp] = "TicketActivity_GiveUp",
        [TicketActivity.ForceClose] = "TicketActivity_ForceClose",
        [TicketActivity.ModifyAttachments] = "TicketActivity_EditAttachments",
        [TicketActivity.EditTicketInfo] = "TicketActivity_Edit",
        [TicketActivity.Create] = "TicketActivity_Create",
        [TicketActivity.CreateOnBehalfOf] = "TicketActivity_CreateOnBehalfOf",
    };

    public static string Name(TicketActivity activity) =>
        Keys.TryGetValue(activity, out var key) ? D.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? activity.ToString() : activity.ToString();

    /// <summary>Route segment of an activity's POST (the legacy <c>[Route]</c> of each TicketActivityController action).</summary>
    public static string Route(TicketActivity activity) => activity switch
    {
        TicketActivity.AddComment => "add-comment",
        TicketActivity.Assign => "assign",
        TicketActivity.CancelMoreInfo => "cancel-more-info",
        TicketActivity.Close => "close",
        TicketActivity.EditTicketInfo => "edit-ticket-info",
        TicketActivity.ForceClose => "force-close",
        TicketActivity.GiveUp => "give-up",
        TicketActivity.ModifyAttachments => "modify-attachments",
        TicketActivity.Pass => "pass",
        TicketActivity.ReAssign => "reassign",
        TicketActivity.RequestMoreInfo => "request-more-info",
        TicketActivity.ReOpen => "reopen",
        TicketActivity.Resolve => "resolve",
        TicketActivity.SupplyMoreInfo => "supply-more-info",
        TicketActivity.TakeOver => "take-over",
        _ => activity.ToString(),
    };
}
