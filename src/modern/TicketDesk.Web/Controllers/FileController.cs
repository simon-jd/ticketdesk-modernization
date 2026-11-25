using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using TicketDesk.Attachments;
using TicketDesk.Identity;
using TicketDesk.Tickets;
using TicketDesk.Tickets.TicketCenter;
using TicketDesk.Web.Migration;

namespace TicketDesk.Web.Controllers;

/// <summary>
/// Legacy <c>FileController</c> read side (wave 2) and the ticket's file panel, over the attachment store shared with the
/// legacy app (Blob): both apps see the same blobs. Wave 4 adds the uploads and deletes of pending files (antiforgery
/// header from the page that hosts the upload widget, S3; unsafe file names refused, S8).
/// </summary>
[Authorize(Policy = TicketDeskPolicies.InternalUser)]
public sealed class FileController(IAttachmentStore store, TicketCenterService tickets, IAuthorizationService authorization) : LegacyPageController
{
    private static readonly FileExtensionContentTypeProvider Types = new();

    /// <summary>S8: help desk and admins see every ticket's files, other users the files of tickets they are on.</summary>
    private async Task<bool> MaySeeFilesAsync(int ticketId)
    {
        var access = await tickets.GetAccessAsync(ticketId, HttpContext.RequestAborted);
        return access != null && (await authorization.AuthorizeAsync(User, access, TicketAttachmentsRequirement.Instance)).Succeeded;
    }

    /// <summary>
    /// <c>file/{id}/{fileName}</c>: id is a ticket id (committed file) or a GUID (pending upload, readable by whoever holds the
    /// upload's tempId, as before). Always <c>application/octet-stream</c> so browsers download. A file the user may not see
    /// is a 404, like a missing one (S8: existence is not revealed); legacy failed with a 500 for a missing file.
    /// </summary>
    [HttpGet]
    [Route("file/{id}/{fileName}")]
    [MigratedRoute("file.get")]
    public async Task<IActionResult> GetFile(string id, string fileName)
    {
        var ct = HttpContext.RequestAborted;
        Stream? content = null;
        if (Guid.TryParse(id, out var tempId)) content = await store.OpenPendingAsync(tempId, fileName, ct);
        else if (int.TryParse(id, out var ticketId) && await MaySeeFilesAsync(ticketId)) content = await store.OpenAsync(ticketId, fileName, ct);
        return content == null ? NotFound() : File(content, "application/octet-stream");
    }

    /// <summary>
    /// <c>file/get-attachments-info?id=&amp;tempId=</c>: pending uploads (L1: the legacy action threw because its pending-file route
    /// did not exist; here they are listed with their <c>file/{tempId}/{name}</c> URL) followed by the ticket's files (S8).
    /// </summary>
    [HttpGet]
    [Route("file/get-attachments-info")]
    [MigratedRoute("file.attachments-info")]
    public async Task<IActionResult> GetAttachmentsInfo(int? id, Guid? tempId)
    {
        var ct = HttpContext.RequestAborted;
        var files = new List<object>();
        if (tempId is { } t)
            foreach (var f in await store.ListPendingAsync(t, ct))
                files.Add(new { name = f.Name, size = f.Size, isAttached = false, type = Type(f.Name), url = FileUrl(t.ToString(), f.Name) });
        if (id is { } ticketId && await MaySeeFilesAsync(ticketId))
            foreach (var f in await store.ListAsync(ticketId, ct))
                files.Add(new { name = f.Name, size = f.Size, isAttached = true, type = Type(f.Name), url = FileUrl(ticketId.ToString(), f.Name) });
        return Json(files);
    }

    /// <summary>
    /// Dropzone upload of pending files (<c>file/upload?tempId=</c>): every posted file is stored as
    /// <c>pending/{name}.{tempId}</c>. Answers <c>{"message":""}</c> as the widget expects; an unsafe name is a 400 (S8).
    /// </summary>
    [HttpPost]
    [Route("file/upload")]
    [MigratedRoute("file.upload")]
    public async Task<IActionResult> Upload(Guid tempId)
    {
        if (tempId == Guid.Empty || !Request.HasFormContentType) return BadRequest(new { message = "tempId and a file are required" });
        foreach (var file in Request.Form.Files)
        {
            await using var content = file.OpenReadStream();
            if (!await store.SavePendingAsync(tempId, Path.GetFileName(file.FileName), content, HttpContext.RequestAborted))
                return BadRequest(new { message = "unsafe file name" });
        }
        return Json(new { message = "" });
    }

    /// <summary>Removes a pending upload (<c>file/delete</c>, form id = tempId, fileName). Committed files are only removed by ModifyAttachments.</summary>
    [HttpPost]
    [Route("file/delete")]
    [MigratedRoute("file.delete")]
    public async Task<IActionResult> Delete([FromForm] Guid id, [FromForm] string? fileName)
    {
        if (!string.IsNullOrEmpty(fileName)) await store.DeletePendingAsync(id, fileName, HttpContext.RequestAborted);
        return Json(new { message = "" });
    }

    /// <summary>Legacy <c>TicketController.TicketFiles</c>: the "Files" panel of the ticket page (AJAX refresh).</summary>
    [Route("ticket/ticket-files")]
    [MigratedRoute("ticket.files")]
    public async Task<IActionResult> TicketFiles(int ticketId)
    {
        var files = await MaySeeFilesAsync(ticketId) ? await store.ListAsync(ticketId, HttpContext.RequestAborted) : [];
        ViewData["TicketId"] = ticketId;
        return PartialView("~/Views/Ticket/_TicketFiles.cshtml", files);
    }

    private static string Type(string name) => Types.TryGetContentType(name, out var type) ? type : "application/octet-stream";

    /// <summary>The legacy route URL <c>/file/{id}/{fileName}</c> (route values escaped as MVC 5 did: parentheses kept).</summary>
    public static string FileUrl(string id, string fileName) =>
        $"/file/{Uri.EscapeDataString(id)}/{Uri.EscapeDataString(fileName).Replace("%28", "(").Replace("%29", ")")}";
}
