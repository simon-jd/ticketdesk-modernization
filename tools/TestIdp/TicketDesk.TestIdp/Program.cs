namespace TicketDesk.TestIdp;

/// <summary>
/// td-test-idp: the test identity provider as a process (lab runs of the Entra ID sign-in without the real tenant).
///   td-test-idp --urls http://127.0.0.1:5590 --tenant &lt;guid&gt; --client-id &lt;id&gt; [--client-secret &lt;s&gt;] [--accounts accounts.json]
/// accounts.json: [{"Oid": "...", "PreferredUsername": "...", "Name": "...", "Email": "...", "Roles": ["TdHelpDeskUsers"]}]
/// (no top-level Program: the test project also references TicketDesk.Web, whose Program WebApplicationFactory uses)
/// </summary>
internal static class TestIdpMain
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var tenant = builder.Configuration["tenant"] ?? "00000000-0000-0000-0000-000000000001";
        var clientId = builder.Configuration["client-id"] ?? "ticketdesk-test-client";
        var accountsFile = builder.Configuration["accounts"];
        var accounts = accountsFile != null
            ? System.Text.Json.JsonSerializer.Deserialize<List<TestAccount>>(File.ReadAllText(accountsFile)) ?? []
            : [new TestAccount("11111111-1111-1111-1111-111111111111", "entra.helpdesk@lab.test", "Entra Helpdesk", "entra.helpdesk@lab.test", ["TdHelpDeskUsers"])];
        var idp = new TestIdentityProvider(tenant, clientId, builder.Configuration["client-secret"], accounts);
        var app = builder.Build();
        idp.Map(app);
        app.Run();
    }
}
