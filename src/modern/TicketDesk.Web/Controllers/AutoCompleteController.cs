using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketDesk.Identity;
using TicketDesk.Tickets.TicketCenter;
using TicketDesk.Web.Migration;

namespace TicketDesk.Web.Controllers;

/// <summary>Legacy <c>AutoCompleteController</c> (wave 2): select2 tag suggestions as camelCase JSON <c>[{id, text}]</c>.</summary>
[Authorize(Policy = TicketDeskPolicies.InternalUser)]
public sealed class AutoCompleteController(TicketCenterService center) : LegacyPageController
{
    [HttpGet]
    [Route("auto-complete/tag-list")]
    [MigratedRoute("autocomplete.tag-list")]
    public async Task<IActionResult> TagList(string? term)
    {
        var tags = await center.TagSuggestionsAsync(term, HttpContext.RequestAborted);
        return Json(tags.Select(t => new { id = t, text = t }));
    }
}
