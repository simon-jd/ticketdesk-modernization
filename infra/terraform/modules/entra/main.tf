# Entra ID app registration for the new app's OIDC sign-in, with app roles that mirror the
# legacy ASP.NET Identity roles. App registrations cost nothing; this one stays across stages
# and is deleted at the end of the project (lab rule: at most 2 apps, named ticketdesk-lab-*).
#
# Round 2b (identity): one client secret for the lab sign-in (code flow + PKCE on the gateway host
# https://localhost:5443). It expires on var.client_secret_end_date; the value goes to $TD_W/.secrets
# (ops/dev/entra-secret.sh), never into the repository. AKS replaces it with a federated credential.

locals {
  # Fixed role IDs so tokens keep the same role identifiers if the app is ever recreated.
  roles = {
    TdAdministrators = {
      id          = "5b0f7a1e-2c4d-4e6f-8a9b-0c1d2e3f4a01"
      display     = "TicketDesk administrators"
      description = "Full administration of TicketDesk (legacy role TdAdministrators)."
    }
    TdHelpDeskUsers = {
      id          = "5b0f7a1e-2c4d-4e6f-8a9b-0c1d2e3f4a02"
      display     = "TicketDesk help desk staff"
      description = "Help desk staff who work tickets (legacy role TdHelpDeskUsers)."
    }
    TdInternalUsers = {
      id          = "5b0f7a1e-2c4d-4e6f-8a9b-0c1d2e3f4a03"
      display     = "TicketDesk internal users"
      description = "Users who submit and follow tickets (legacy role TdInternalUsers)."
    }
    TdPendingUsers = {
      id          = "5b0f7a1e-2c4d-4e6f-8a9b-0c1d2e3f4a04"
      display     = "TicketDesk pending users"
      description = "Registered users awaiting approval (legacy role TdPendingUsers)."
    }
  }
}

resource "azuread_application" "web" {
  display_name            = var.display_name
  sign_in_audience        = "AzureADMyOrg"
  owners                  = [var.owner_object_id]
  prevent_duplicate_names = true
  notes                   = "TicketDesk modernization lab (OIDC sign-in for the new app). Created by Terraform, infra/terraform/envs/lab; delete at project end."
  tags                    = ["project=ticketdesk", "owner=${var.owner_tag}", "ttl=project-end"]

  api {
    requested_access_token_version = 2
  }

  dynamic "app_role" {
    for_each = local.roles
    content {
      id                   = app_role.value.id
      value                = app_role.key
      display_name         = app_role.value.display
      description          = app_role.value.description
      allowed_member_types = ["User"]
      enabled              = true
    }
  }

  web {
    redirect_uris = var.redirect_uris
    logout_url    = var.logout_url

    implicit_grant {
      access_token_issuance_enabled = false
      id_token_issuance_enabled     = false
    }
  }
}

resource "azuread_application_password" "web" {
  count          = var.create_client_secret ? 1 : 0
  application_id = azuread_application.web.id
  display_name   = "ticketdesk-lab-web local sign-in (identity round)"
  end_date       = var.client_secret_end_date
}

resource "azuread_service_principal" "web" {
  client_id = azuread_application.web.client_id
  owners    = [var.owner_object_id]
  # Only users with a role assignment can sign in (the tenant is a whole university).
  app_role_assignment_required = true
  notes                        = "TicketDesk modernization lab; delete at project end."
  tags                         = ["project=ticketdesk", "owner=${var.owner_tag}", "ttl=project-end"]
}

resource "azuread_app_role_assignment" "admin" {
  app_role_id         = local.roles.TdAdministrators.id
  principal_object_id = var.owner_object_id
  resource_object_id  = azuread_service_principal.web.object_id
}
