provider "azurerm" {
  subscription_id = var.subscription_id

  # Register only what this lab uses (Microsoft.Sql was the one not yet registered on
  # 2026-11-07), instead of the provider's default set.
  resource_provider_registrations = "none"
  resource_providers_to_register = [
    "Microsoft.AlertsManagement", # AKS round: alert instances (fired/resolved times of the drill)
    "Microsoft.Compute",
    "Microsoft.ContainerRegistry",
    "Microsoft.ContainerService",
    "Microsoft.Insights",
    "Microsoft.KeyVault",
    "Microsoft.ManagedIdentity",
    "Microsoft.Network",
    "Microsoft.OperationalInsights",
    "Microsoft.ServiceBus", # AKS round: the notification queue
    "Microsoft.Sql",
    "Microsoft.Storage",
  ]

  # The lab storage account has shared keys disabled: Entra auth only, and no data-plane calls from
  # the provider (containers are managed through Resource Manager).
  storage_use_azuread = true

  features {
    resource_group {
      # Never delete a resource group that still holds resources Terraform does not know about.
      prevent_deletion_if_contains_resources = true
    }
    key_vault {
      purge_soft_delete_on_destroy    = true # no purge protection in the lab: name reusable at once
      recover_soft_deleted_key_vaults = true # after `make azure-down` (RG delete) the vault is soft-deleted
    }
    log_analytics_workspace {
      permanently_delete_on_destroy = true
    }
    storage {
      data_plane_available = false
    }
    application_insights {
      # false: the provider does not wait for Azure's generated "Failure Anomalies" smart detector rule. With true it
      # waited 9.5 min on 2026-11-11 for a rule Azure did not generate (Microsoft.AlertsManagement registered), and
      # 17+ min on 2026-11-08 (unregistered). If Azure does generate one, it stays in the group, has no meter and goes
      # with the group (make azure-down; Terraform's own group delete would refuse a group with an unknown resource).
      disable_generated_rule = false
    }
  }
}

provider "azuread" {
  tenant_id = var.tenant_id
}

provider "azapi" {
  subscription_id = var.subscription_id
  tenant_id       = var.tenant_id
}
