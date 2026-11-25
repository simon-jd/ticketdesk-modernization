// TicketDesk - Attribution notice
// Contributor(s):
//
//      Stephen Redd (https://github.com/stephenredd)
//
// This file is distributed under the terms of the Microsoft Public 
// License (Ms-PL). See http://opensource.org/licenses/MS-PL
// for the complete terms of use. 
//
// For any distribution that contains code from this file, this notice of 
// attribution must remain intact, and a copy of the license must be 
// provided to the recipient.

using System.Data.Entity;
using Microsoft.AspNet.Identity.EntityFramework;
using TicketDesk.Web.Identity.Model;

namespace TicketDesk.Web.Identity
{
    public class TdIdentityContext : IdentityDbContext<TicketDeskUser>
    {
        // throwIfV1Schema: false. Identity 2's ASP.NET Identity 1.0 schema check opens its own SqlConnection, outside EF6
        // interception, so on Azure SQL it has no managed-identity token ("Login failed for user ''"); and it looks for
        // AspNetUsers, which this context never uses (it maps to IdentityUsers), so on SQL Server it always found nothing.
        public TdIdentityContext()
            : base("TicketDesk", false)
        {
        }


        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            //we have to call the base modelbuilder first, or the new names get overridden by the base
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TicketDeskUser>().ToTable("IdentityUsers");
            modelBuilder.Entity<TicketDeskRole>().ToTable("IdentityRoles");
            modelBuilder.Entity<IdentityRole>().ToTable("IdentityRoles");
            modelBuilder.Entity<IdentityUserRole>().ToTable("IdentityUserRoles");
            modelBuilder.Entity<IdentityUserLogin>().ToTable("IdentityUserLogins");
            modelBuilder.Entity<IdentityUserClaim>().ToTable("IdentityUserClaims");

        }

       


    }
}
