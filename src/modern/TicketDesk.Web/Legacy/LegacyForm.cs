using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ValidationStrings = TicketDesk.Localization.Validation;

namespace TicketDesk.Web.Legacy;

/// <summary>
/// Validation metadata of one legacy view-model property (the DataAnnotations of TicketDesk.Web.Client/Models), enough to
/// render what MVC 5 rendered for it: the label, the unobtrusive-validation attributes and the server-side messages.
/// Messages come from the legacy resource strings (Validation.resx), so they follow the request culture like before.
/// </summary>
public sealed record LegacyField(string Name, string Label)
{
    public bool Required { get; init; }
    public int? MaxLength { get; init; }
    /// <summary>[StringLength(max, MinimumLength = n)]; the legacy models use FieldMinimumLength as the message then.</summary>
    public int? MinLength { get; init; }
    public bool Email { get; init; }
    /// <summary>[Compare(other)]: the other property's name and the (already localized) message.</summary>
    public (string Other, string Message)? EqualTo { get; init; }
    /// <summary>int properties (MVC 5 adds data-val-number).</summary>
    public bool Number { get; init; }

    /// <summary>The element id MVC 5 derived from the name ("User.Email" → "User_Email").</summary>
    public string Id => Name.Replace('.', '_');

    public string RequiredMessage => Format(ValidationStrings.FieldRequired, Label);
    public string LengthMessage => MinLength is { } min
        ? Format(ValidationStrings.FieldMinimumLength, Label, MaxLength, min)
        : Format(ValidationStrings.FieldMaximumLength, Label, MaxLength);
    public string EmailMessage => Format(ValidationStrings.InvalidEmail, Label);

    private static string Format(string format, params object?[] args) => string.Format(CultureInfo.CurrentCulture, format, args);

    /// <summary>Server-side validation of a posted value, in the order MVC 5's DataAnnotations validators reported errors.</summary>
    public IEnumerable<string> Validate(string? value, Func<string, string?> otherValue)
    {
        if (Required && string.IsNullOrWhiteSpace(value)) { yield return RequiredMessage; yield break; }
        if (string.IsNullOrEmpty(value)) yield break;
        if (MaxLength is { } max && (value.Length > max || (MinLength is { } min && value.Length < min))) yield return LengthMessage;
        if (Email && !System.Text.RegularExpressions.Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+$")) yield return EmailMessage;
        if (EqualTo is { } eq && !string.Equals(value, otherValue(eq.Other), StringComparison.Ordinal)) yield return eq.Message;
    }

    /// <summary>Compare also runs when the field is empty (MVC 5's CompareAttribute compares nulls too).</summary>
    public IEnumerable<string> ValidateAll(string? value, Func<string, string?> otherValue)
    {
        var errors = Validate(value, otherValue).ToList();
        if (string.IsNullOrEmpty(value) && !Required && EqualTo is { } eq && !string.IsNullOrEmpty(otherValue(eq.Other))) errors.Add(eq.Message);
        return errors;
    }
}

/// <summary>
/// MVC 5 HtmlHelper output (TextBoxFor, PasswordFor, CheckBoxFor, LabelFor, ValidationMessageFor, ValidationSummary,
/// HiddenFor) for the migrated forms. MVC 5's TagBuilder wrote attributes in ordinal order and encoded values with
/// HttpUtility.HtmlAttributeEncode; both are reproduced so the pages compare equal to the golden recording.
/// </summary>
public static class LegacyForm
{
    private static string A(string? s) => LegacyRedirectResult.HtmlAttributeEncode(s ?? "");
    private static string T(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string Tag(string name, SortedDictionary<string, string> attributes, bool selfClosing = true)
    {
        var sb = new StringBuilder("<").Append(name);
        foreach (var (k, v) in attributes) sb.Append(' ').Append(k).Append("=\"").Append(A(v)).Append('"');
        return sb.Append(selfClosing ? " />" : ">").ToString();
    }

    private static SortedDictionary<string, string> Attributes(LegacyField f, string type, string? cssClass, bool unobtrusive, ModelStateDictionary? state)
    {
        var a = new SortedDictionary<string, string>(StringComparer.Ordinal) { ["id"] = f.Id, ["name"] = f.Name, ["type"] = type };
        var css = cssClass;
        if (state != null && state.TryGetValue(f.Name, out var entry) && entry.Errors.Count > 0)
            css = string.IsNullOrEmpty(css) ? "input-validation-error" : "input-validation-error " + css;
        if (!string.IsNullOrEmpty(css)) a["class"] = css;
        if (unobtrusive) AddValidation(a, f);
        return a;
    }

    private static void AddValidation(SortedDictionary<string, string> a, LegacyField f)
    {
        var any = false;
        if (f.Required) { a["data-val-required"] = f.RequiredMessage; any = true; }
        if (f.MaxLength is { } max)
        {
            a["data-val-length"] = f.LengthMessage;
            a["data-val-length-max"] = max.ToString(CultureInfo.InvariantCulture);
            if (f.MinLength is { } min) a["data-val-length-min"] = min.ToString(CultureInfo.InvariantCulture);
            any = true;
        }
        if (f.Email) { a["data-val-email"] = f.EmailMessage; any = true; }
        if (f.EqualTo is { } eq) { a["data-val-equalto"] = eq.Message; a["data-val-equalto-other"] = "*." + eq.Other; any = true; }
        if (f.Number) { a["data-val-number"] = string.Format(CultureInfo.CurrentCulture, "The field {0} must be a number.", f.Label); any = true; }
        if (any) a["data-val"] = "true";
    }

    public static IHtmlContent Label(LegacyField f, string cssClass) =>
        new HtmlString($"<label class=\"{A(cssClass)}\" for=\"{A(f.Id)}\">{T(f.Label)}</label>");

    public static IHtmlContent TextBox(LegacyField f, string? value, string? cssClass = "form-control", ModelStateDictionary? state = null, bool unobtrusive = true)
    {
        var a = Attributes(f, "text", cssClass, unobtrusive, state);
        a["value"] = value ?? "";
        return new HtmlString(Tag("input", a));
    }

    /// <summary>PasswordFor never renders the value.</summary>
    public static IHtmlContent Password(LegacyField f, string? cssClass = "form-control", ModelStateDictionary? state = null) =>
        new HtmlString(Tag("input", Attributes(f, "password", cssClass, true, state)));

    public static IHtmlContent Hidden(LegacyField f, string? value)
    {
        var a = Attributes(f, "hidden", null, true, null);
        a["value"] = value ?? "";
        return new HtmlString(Tag("input", a));
    }

    /// <summary>CheckBoxFor/EditorFor(bool): the checkbox and the hidden "false" that MVC 5 appended.</summary>
    public static IHtmlContent CheckBox(LegacyField f, bool isChecked, string? cssClass = "check-box", bool unobtrusive = true, bool disabled = false)
    {
        var a = Attributes(f, "checkbox", cssClass, unobtrusive, null);
        a["value"] = "true";
        if (isChecked) a["checked"] = "checked";
        if (disabled) a["disabled"] = "disabled";
        return new HtmlString(Tag("input", a) + $"<input name=\"{A(f.Name)}\" type=\"hidden\" value=\"false\" />");
    }

    public static IHtmlContent ValidationMessage(LegacyField f, ModelStateDictionary state, string? cssClass = "text-danger")
    {
        var error = state.TryGetValue(f.Name, out var entry) ? entry.Errors.FirstOrDefault()?.ErrorMessage : null;
        var css = (error != null ? "field-validation-error" : "field-validation-valid") + (string.IsNullOrEmpty(cssClass) ? "" : " " + cssClass);
        return new HtmlString($"<span class=\"{css}\" data-valmsg-for=\"{A(f.Name)}\" data-valmsg-replace=\"true\">{T(error)}</span>");
    }

    /// <summary>
    /// <c>Html.ValidationSummary(excludePropertyErrors, "", new { @class = "text-danger" })</c>. With
    /// <paramref name="excludePropertyErrors"/> only model-level errors are listed and nothing is rendered without one.
    /// </summary>
    public static IHtmlContent ValidationSummary(ModelStateDictionary state, bool excludePropertyErrors = false)
    {
        var errors = (excludePropertyErrors
                ? state.TryGetValue("", out var model) ? model.Errors : []
                : state.Values.SelectMany(v => v.Errors))
            .Select(e => e.ErrorMessage).ToList();
        if (excludePropertyErrors)
        {
            if (errors.Count == 0) return HtmlString.Empty;
            return new HtmlString("<div class=\"validation-summary-errors text-danger\"><ul>" + string.Concat(errors.Select(e => $"<li>{T(e)}</li>\r\n")) + "</ul></div>");
        }
        if (errors.Count == 0)
            return new HtmlString("<div class=\"validation-summary-valid text-danger\" data-valmsg-summary=\"true\"><ul><li style=\"display:none\"></li>\r\n</ul></div>");
        return new HtmlString("<div class=\"validation-summary-errors text-danger\" data-valmsg-summary=\"true\"><ul>" + string.Concat(errors.Select(e => $"<li>{T(e)}</li>\r\n")) + "</ul></div>");
    }

    /// <summary>Adds the field's validation errors for the posted value to the model state (key = field name).</summary>
    public static bool Validate(ModelStateDictionary state, IFormCollection form, params LegacyField[] fields)
    {
        foreach (var f in fields)
            foreach (var error in f.ValidateAll(form[f.Name].FirstOrDefault(), other => form[other].FirstOrDefault()))
                state.AddModelError(f.Name, error);
        return state.IsValid;
    }
}
