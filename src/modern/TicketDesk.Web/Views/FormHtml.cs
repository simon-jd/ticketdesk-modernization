using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using D = TicketDesk.Localization.Domain.Strings;
using V = TicketDesk.Localization.Validation;

namespace TicketDesk.Web.Views;

/// <summary>
/// What MVC 5's <c>DropDownListFor</c>/<c>TextBoxFor</c> rendered for the legacy <c>Ticket</c> model: the unobtrusive
/// validation attributes come from the model's data annotations (localized Domain/Validation resources) and MVC 5's own
/// English defaults for implicit rules (number, value-type required).
/// </summary>
public static class FormHtml
{
    private static string E(string? s) => HtmlEncoder.Default.Encode(s ?? "");

    /// <summary>Display name of a ticket property (its <c>[Display(Name = ...)]</c> resource).</summary>
    public static string Display(string resourceName) => D.ResourceManager.GetString(resourceName, CultureInfo.CurrentUICulture) ?? resourceName;

    public static string Required(string resourceName) => string.Format(CultureInfo.CurrentCulture, V.FieldRequired, Display(resourceName));
    public static string Length(string resourceName, int max) => string.Format(CultureInfo.CurrentCulture, V.FieldMaximumLength, Display(resourceName), max);
    /// <summary>MVC 5 ClientDataTypeModelValidatorProvider (not localized in the legacy app).</summary>
    public static string Number(string resourceName) => $"The field {Display(resourceName)} must be a number.";
    /// <summary>MVC 5 implicit [Required] for non-nullable value types (not localized in the legacy app).</summary>
    public static string ImplicitRequired(string resourceName) => $"The {Display(resourceName)} field is required.";

    public sealed record Option(string Value, string Text);

    /// <summary>Unobtrusive validation attributes: (data-val-length + max, data-val-number, data-val-required).</summary>
    public sealed record Rules(string Display, int? MaxLength = null, bool Number = false, bool Required = false, bool ImplicitRequired = false)
    {
        public string Attributes()
        {
            var sb = new StringBuilder();
            if (MaxLength == null && !Number && !Required && !ImplicitRequired) return "";
            sb.Append(" data-val=\"true\"");
            if (MaxLength is { } max) sb.Append($" data-val-length=\"{E(Length(Display, max))}\" data-val-length-max=\"{max}\"");
            if (Number) sb.Append($" data-val-number=\"{E(FormHtml.Number(Display))}\"");
            if (Required) sb.Append($" data-val-required=\"{E(FormHtml.Required(Display))}\"");
            else if (ImplicitRequired) sb.Append($" data-val-required=\"{E(FormHtml.ImplicitRequired(Display))}\"");
            return sb.ToString();
        }
    }

    /// <summary><c>Html.DropDownListFor(m =&gt; m.X, selectList, new { @class, tabindex })</c>.</summary>
    public static IHtmlContent Select(string name, IEnumerable<Option> options, string? selected, Rules rules, string cssClass, int tabIndex)
    {
        var sb = new StringBuilder();
        sb.Append($"<select class=\"{E(cssClass)}\"{rules.Attributes()} id=\"{E(name)}\" name=\"{E(name)}\" tabindex=\"{tabIndex}\">");
        foreach (var o in options)
        {
            sb.Append(o.Value == selected && selected != null ? $"<option selected=\"selected\" value=\"{E(o.Value)}\">" : $"<option value=\"{E(o.Value)}\">");
            sb.Append(E(o.Text)).Append("</option>\r\n");
        }
        sb.Append("</select>");
        return new HtmlString(sb.ToString());
    }

    /// <summary><c>Html.TextBoxFor(m =&gt; m.X, new { @class, tabindex, ... })</c>; <paramref name="extra"/> is raw attribute text.</summary>
    public static IHtmlContent TextBox(string name, string? value, Rules? rules, string cssClass, int tabIndex, string? id = null, string? extra = null) =>
        new HtmlString($"<input class=\"{E(cssClass)}\"{rules?.Attributes()}{extra} id=\"{E(id ?? name)}\" name=\"{E(name)}\" tabindex=\"{tabIndex}\" type=\"text\" value=\"{E(value)}\" />");

    /// <summary><c>Html.ValidationMessageFor(m =&gt; m.X, "", new { @class = "text-danger" })</c> (with the field's first error, if any).</summary>
    public static IHtmlContent ValidationMessage(string name, string? error = null, string? style = null) =>
        new HtmlString(error == null
            ? $"<span class=\"field-validation-valid text-danger\" data-valmsg-for=\"{E(name)}\" data-valmsg-replace=\"true\"{(style != null ? $" style=\"{E(style)}\"" : "")}></span>"
            : $"<span class=\"field-validation-error text-danger\" data-valmsg-for=\"{E(name)}\" data-valmsg-replace=\"true\"{(style != null ? $" style=\"{E(style)}\"" : "")}><span>{E(error)}</span></span>");

    /// <summary><c>Html.LabelFor(m =&gt; m.X, new { @class })</c>.</summary>
    public static IHtmlContent Label(string forName, string resourceName, string cssClass) =>
        new HtmlString($"<label class=\"{E(cssClass)}\" for=\"{E(forName)}\">{E(Display(resourceName))}</label>");

    /// <summary>A select list of strings with the legacy "-- select --" first item (<c>ToSelectList(..., includeDefaultItem: true)</c>).</summary>
    public static IEnumerable<Option> WithDefault(IEnumerable<Option> options) =>
        new[] { new Option("", TicketDesk.Localization.Models.Strings.SelectList_DefaultItem) }.Concat(options);

    public static IEnumerable<Option> Strings(IEnumerable<string> values) => values.Select(v => new Option(v, v));
}
