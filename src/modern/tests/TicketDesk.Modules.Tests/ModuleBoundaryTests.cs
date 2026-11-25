using System.Reflection;

namespace TicketDesk.Modules.Tests;

/// <summary>Modular monolith: modules depend on Data/Identity and on a few published module contracts, never on each other freely.</summary>
public sealed class ModuleBoundaryTests
{
    public static TheoryData<string, string[]> Allowed => new()
    {
        // Tickets publishes ticket changes to the Notifications seam (ITicketChangeNotifier) and writes event texts from the legacy resources
        { "TicketDesk.Tickets", ["TicketDesk.Data", "TicketDesk.Identity", "TicketDesk.Users", "TicketDesk.Attachments", "TicketDesk.Notifications", "TicketDesk.Localization"] },
        { "TicketDesk.Projects", ["TicketDesk.Data", "TicketDesk.Identity"] },
        { "TicketDesk.Reports", ["TicketDesk.Data", "TicketDesk.Identity", "TicketDesk.Users"] },
        { "TicketDesk.Users", ["TicketDesk.Data"] },
        { "TicketDesk.Attachments", [] },
        { "TicketDesk.Search", ["TicketDesk.Data", "TicketDesk.Identity"] },
        { "TicketDesk.Notifications", [] },
        { "TicketDesk.Settings", ["TicketDesk.Data"] },
    };

    [Theory]
    [MemberData(nameof(Allowed))]
    public void Module_references_only_what_it_may(string module, string[] allowed)
    {
        var references = Assembly.Load(module).GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("TicketDesk.", StringComparison.Ordinal))
            .ToArray();
        Assert.All(references, r => Assert.Contains(r, allowed));
    }
}
