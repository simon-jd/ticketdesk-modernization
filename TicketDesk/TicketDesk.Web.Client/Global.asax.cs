// Added for the ASP.NET Core migration (System.Web adapters). Distributed under the Ms-PL like the rest of TicketDesk.

using System.Web;

namespace TicketDesk.Web.Client
{
    /// <summary>
    /// The application class. TicketDesk configures itself in the OWIN <see cref="Startup"/>; this class exists only because
    /// the System.Web adapters' host must be registered in Application_Start, before IIS initializes the HTTP modules.
    /// </summary>
    public class TicketDeskApplication : HttpApplication
    {
        protected void Application_Start()
        {
            SystemWebAdaptersConfig.Register();
        }
    }
}
