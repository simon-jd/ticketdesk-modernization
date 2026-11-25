// Port of the legacy e-mail views Views/Emails/Ticket.cshtml, Ticket.Html.cshtml and Ticket.Text.cshtml (TicketDesk 2.5,
// Ms-PL; attribution: Stephen Redd, https://github.com/stephenredd), rendered by Postal in the legacy app.
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using TicketDesk.Data.Entities;
using TicketDesk.Tickets.Rendering;
using D = TicketDesk.Localization.Domain.Strings;
using S = TicketDesk.Localization.Views.Emails.Strings;

namespace TicketDesk.Worker;

/// <summary>What the legacy <c>TicketEmail</c> model held: the ticket (project and events loaded) and the user names.</summary>
public sealed record TicketEmailModel(Ticket Ticket, IReadOnlyDictionary<string, string> Names, string SiteRootUrl, bool IsMultiProject, bool ForNewTicket)
{
    public string Name(string? id) => id != null && Names.TryGetValue(id, out var n) ? n : "";
}

public sealed record RenderedEmail(string Subject, string Html, string Text);

/// <summary>
/// The ticket e-mail: subject "[New] Ticket #id : title" (new-ticket broadcast) or "Ticket #id : title", an HTML part and a
/// text part. Kept from the legacy templates on purpose, quirks included: event times print the date's midnight
/// (<c>EventDate.Date.ToShortTimeString()</c>), the text part's project line is never printed (it was in a code block), and
/// the details excerpt is cut at 100 characters. User names are HTML-encoded here (the legacy HTML view pasted the
/// assignee's name raw, as S11 on the ticket page).
/// </summary>
public sealed partial class TicketEmailRenderer(LegacyTextRenderer text)
{
    public RenderedEmail Render(TicketEmailModel m)
    {
        var t = m.Ticket;
        var subject = (m.ForNewTicket ? $"[{S.New}] " : "") + string.Format(CultureInfo.CurrentCulture, S.Subject, t.TicketId, t.Title);
        return new RenderedEmail(subject, Html(m), Text(m));
    }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
    private static string Status(TicketStatus s) => D.ResourceManager.GetString("TicketStatus" + s, CultureInfo.CurrentUICulture) ?? s.ToString();
    private static string ShortDate(DateTimeOffset d) => d.DateTime.ToString("d", CultureInfo.CurrentCulture);
    private static string ShortTime(DateTime d) => d.ToString("t", CultureInfo.CurrentCulture).Replace(' ', ' ');
    private static string PriorityLine(Ticket t) =>
        $"{(string.IsNullOrEmpty(t.Priority) ? "" : string.Format(CultureInfo.CurrentCulture, S.Priority, t.Priority))} {t.Category} {t.TicketType}";

    private string Html(TicketEmailModel m)
    {
        var t = m.Ticket;
        var link = $"{m.SiteRootUrl.TrimEnd('/')}/ticket/{t.TicketId}";
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html>\n<html>\n<head>\n    <meta name=\"viewport\" content=\"width=device-width\" />\n")
          .Append("    <title>").Append(E(string.Format(CultureInfo.CurrentCulture, S.Title, t.TicketId))).Append("</title>\n")
          .Append(Css).Append("</head>\n<body>\n    <div style=\"border: 1px solid #dcdcdc;\">\n")
          .Append("        <table style=\"background-color: #B3CBDF;width: 100%;\">\n            <tr>\n")
          .Append($"                <td style=\"width: 55px; font-weight: bold; border-right: 1px solid #dcdcdc; text-align: center;\"><a href=\"{E(link)}\">{E(Status(t.TicketStatus))}</a></td>\n")
          .Append($"                <td style=\"width: 40px; font-weight: bold; border-right: 1px solid #dcdcdc; text-align: center;\"><a href=\"{E(link)}\">#{t.TicketId}</a></td>\n")
          .Append($"                <td style=\"font-weight: bold; text-align: left;\"><a href=\"{E(link)}\">{E(t.Title)}</a></td>\n")
          .Append("            </tr>\n        </table>\n        <table style=\"border-bottom: 1px solid #e7e7e7; width: 100%; background-color: #F7F7F7;\">\n");
        if (!string.IsNullOrEmpty(t.TagList))
            sb.Append("            <tr><td style=\"text-align: right;\">")
              .Append(string.Concat(t.TagList.Split(',').Select(tag => $"<span class=\"tag\">{E(tag)}</span>"))).Append("</td></tr>\n");
        sb.Append($"            <tr><td>{E(PriorityLine(t))}</td></tr>\n");
        if (m.IsMultiProject) sb.Append($"            <tr><td>{E(string.Format(CultureInfo.CurrentCulture, S.Project, t.Project?.ProjectName))}</td></tr>\n");
        var assigned = string.IsNullOrEmpty(t.AssignedTo) ? "<span class=\"tag\">" + E(S.Unassigned) + "</span>" : E(m.Name(t.AssignedTo));
        sb.Append($"            <tr><td>{string.Format(CultureInfo.CurrentCulture, S.AssignedTo, assigned)}</td></tr>\n")
          .Append($"            <tr><td>{E(string.Format(CultureInfo.CurrentCulture, S.Owner, m.Name(t.Owner)))}</td></tr>\n")
          .Append($"            <tr><td>{E(string.Format(CultureInfo.CurrentCulture, S.UpdatedBy, m.Name(t.LastUpdateBy), ShortDate(t.LastUpdateDate), ShortTime(t.LastUpdateDate.DateTime)))}</td></tr>\n")
          .Append("        </table>\n        <table style=\"width: 100%;\"><tr><td><div style=\"overflow-y:scroll; max-height: 250px; min-height: 100px;\">")
          .Append(text.Render(t.Details, t.IsHtml)).Append("</div></td></tr></table>\n")
          .Append($"        <table style=\"background-color: #B3CBDF; width: 100%;\"><tr><td style=\"font-weight: bold;\">{E(S.Activity)}</td></tr></table>\n")
          .Append("        <div style=\"padding: 10px;\">\n");
        foreach (var evt in t.TicketEvents.OrderByDescending(e => e.EventDate))
        {
            sb.Append("            <div style=\"border: 1px solid #dfdfdf; margin-bottom: 10px;\">\n")
              .Append("                <table style=\"background-color: #F7F7F7;width: 100%;\">\n")
              .Append($"                    <tr><td><strong>{E(m.Name(evt.EventBy))}</strong> {E(evt.EventDescription)}</td></tr>\n")
              .Append($"                    <tr><td style=\"text-indent: 1em; font-size: smaller\">{E(evt.EventDate.Date.ToString("D", CultureInfo.CurrentCulture))} {E(ShortTime(evt.EventDate.Date))}</td></tr>\n")
              .Append("                </table>\n");
            var comment = text.Render(evt.Comment, evt.IsHtml);
            if (!string.IsNullOrEmpty(comment))
                sb.Append("                <table style=\"width: 100%;\"><tr><td style=\"font-family: monospace; font-size: 9pt;\">").Append(comment).Append("</td></tr></table>\n");
            sb.Append("            </div>\n");
        }
        sb.Append("        </div>\n    </div>\n</body>\n</html>\n");
        return sb.ToString();
    }

    private string Text(TicketEmailModel m)
    {
        const string rule = "----------------------------------------------------------------";
        var t = m.Ticket;
        var sb = new StringBuilder();
        sb.Append(rule).Append('\n')
          .Append(string.Format(CultureInfo.CurrentCulture, S.Title, t.TicketId)).Append(" - ").Append(PriorityLine(t)).Append('\n')
          .Append("    ").Append(t.Title).Append('\n').Append(rule).Append('\n')
          .Append(string.Format(CultureInfo.CurrentCulture, S.Status, Status(t.TicketStatus))).Append('\n')
          .Append(string.Format(CultureInfo.CurrentCulture, S.AssignedTo, m.Name(t.AssignedTo))).Append('\n')
          .Append(string.Format(CultureInfo.CurrentCulture, S.Owner, m.Name(t.Owner))).Append('\n')
          .Append(string.Format(CultureInfo.CurrentCulture, S.UpdatedBy, m.Name(t.LastUpdateBy), ShortDate(t.LastUpdateDate), ShortTime(t.LastUpdateDate.DateTime))).Append('\n')
          .Append(S.Details).Append("\n\n");
        var details = Tags().Replace(text.Render(t.Details, t.IsHtml), "");
        if (details.Length > 100) details = details[..100] + "\n\n...";
        sb.Append(details).Append('\n').Append(rule).Append('\n').Append(S.Activity).Append('\n').Append(rule).Append("\n\n");
        foreach (var evt in t.TicketEvents.OrderByDescending(e => e.EventDate))
        {
            var cmt = Tags().Replace(text.Render(evt.Comment, evt.IsHtml), "");
            sb.Append("    ").Append(m.Name(evt.EventBy)).Append(' ').Append(evt.EventDescription).Append('\n')
              .Append("        ").Append(evt.EventDate.Date.ToString("D", CultureInfo.CurrentCulture)).Append(' ').Append(ShortTime(evt.EventDate.Date)).Append("\n\n")
              .Append("    ").Append(cmt).Append('\n').Append(rule).Append("\n\n");
        }
        return sb.ToString();
    }

    private const string Css = """
            <style type="text/css">
                * { font-family: Calibri, Tahoma, Verdana, Helvetica, Sans-Serif; font-size: 10pt; }
                p { margin: 0.8ex 0 0.8ex 0; }
                td { padding-left: 3px; padding-right: 3px; }
                .tag { border: 1px solid #dcdcdc; background-color: #fcf8e3; font-size: 9pt; padding: 2px; }
                dl { margin-left: 15px; }
                dd { text-indent: 1em; font-family: monospace; font-size: smaller; }
            </style>

        """;

    [GeneratedRegex(@"<(.|\n)*?>")]
    private static partial Regex Tags();
}
