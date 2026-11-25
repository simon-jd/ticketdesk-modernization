// Added for the ASP.NET Core migration (System.Web adapters). Distributed under the Ms-PL like the rest of TicketDesk.

using System.Web;

namespace TicketDesk.Web.Client
{
    /// <summary>
    /// Drops X-Forwarded-For before the System.Web adapters' proxy module sees it, so REMOTE_ADDR stays the real peer.
    /// </summary>
    /// <remarks>
    /// The adapters' proxy module copies X-Forwarded-For into REMOTE_ADDR without checking who sent it. ASP.NET's
    /// Request.IsLocal compares REMOTE_ADDR with 127.0.0.1/::1, and customErrors mode="RemoteOnly" (the lab and Azure
    /// deploy, inventory finding S9) shows full error pages with stack traces to "local" requests. A gateway on the same
    /// host as its client forwards "127.0.0.1", and any client that reaches this app directly could send the header
    /// itself. TicketDesk never uses the client address otherwise, so the header is simply removed.
    /// Set appSettings ticketdesk:RemoteAppTrustForwardedFor=true to turn this off.
    /// </remarks>
    internal sealed class ForwardedForGuardModule : IHttpModule
    {
        private const string Header = "X-Forwarded-For";

        public void Init(HttpApplication context)
        {
            context.BeginRequest += (sender, e) =>
            {
                var headers = ((HttpApplication)sender).Context.Request.Headers;
                if (headers[Header] != null)
                {
                    headers.Remove(Header);
                }
            };
        }

        public void Dispose()
        {
        }
    }
}
