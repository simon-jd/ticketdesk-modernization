# Small Standard_LRS storage account. Used twice by the root module (stages with the VM):
#   ops account (Entra-only)   artifacts (publish package + ops/windows bundle, SHA-256 checked on the VM),
#                              backups (Backup-TdSite.ps1 folders), logs (provisioning transcripts)
#   files account              ticketdesk-attachments: the legacy app's attachments. Its Blob SDK
#                              (WindowsAzure.Storage 8.2.0 + BlobFileSystem.Azure) knows only account keys and
#                              SAS and touches container-level APIs on every path, so this account allows shared
#                              keys (shared_access_key_enabled = true) and the app gets an account SAS (blob
#                              service only, container + object, no account-level operations) from Key Vault.
#                              Kept apart so that SAS can never reach the backups.
#
# By default access is Entra-only: shared keys (and so SAS tokens) are disabled; the operator gets Storage
# Blob Data Contributor on the account, the VM's managed identity gets container-scoped roles (root module).
# The firewall denies everything except the admin source IPs (lab-host, for az uploads) and the VM subnet
# (service endpoint). Containers are private.
#
# Cost (Retail Prices API, swedencentral, 2026-11-08, docs/dev/azure-spend.md): Hot LRS data stored per
# GB-month plus per-10K operations; a few hundred MB for a few hours rounds to $0.00/h.

resource "azurerm_storage_account" "this" {
  name                             = var.name
  location                         = var.location
  resource_group_name              = var.resource_group_name
  account_kind                     = "StorageV2"
  account_tier                     = "Standard"
  account_replication_type         = "LRS"
  access_tier                      = "Hot"
  min_tls_version                  = "TLS1_2"
  https_traffic_only_enabled       = true
  shared_access_key_enabled        = var.shared_access_key_enabled
  default_to_oauth_authentication  = true
  allow_nested_items_to_be_public  = false
  cross_tenant_replication_enabled = false
  local_user_enabled               = false
  public_network_access            = "Enabled"
  tags                             = merge(var.tags, { est_usd_per_hour = "0" })

  network_rules {
    default_action = "Deny"
    bypass         = ["AzureServices"]
    # Storage firewall rules take single addresses for /32 (a /32 CIDR is rejected).
    ip_rules                   = [for c in var.admin_source_cidrs : trimsuffix(c, "/32")]
    virtual_network_subnet_ids = var.allowed_subnet_ids
  }
}

resource "azurerm_storage_container" "this" {
  for_each              = toset(var.containers)
  name                  = each.value
  storage_account_id    = azurerm_storage_account.this.id
  container_access_type = "private"
}

# The signed-in user uploads artifacts and downloads backups/logs with `az storage ... --auth-mode login`.
resource "azurerm_role_assignment" "operator" {
  scope                = azurerm_storage_account.this.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = var.operator_object_id
}

# Data-plane role assignments take a while to apply; the `id` output waits for this.
resource "time_sleep" "rbac_propagation" {
  depends_on      = [azurerm_role_assignment.operator]
  create_duration = var.rbac_propagation_wait
}
