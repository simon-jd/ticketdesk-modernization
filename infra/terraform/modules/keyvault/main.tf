# Key Vault in RBAC mode (no access policies). Soft delete is always on in Azure (7 days here);
# purge protection is off so a lab vault can be purged on destroy and its name reused.
# Cost: Standard operations $0.03 per 10K (Retail Prices API, swedencentral) - effectively 0 per hour.
#
# The data plane is public but firewalled to the admin source IPs (Terraform and az run from
# lab-host) and to allowed subnets (service endpoint). Trusted Azure services bypass the firewall. A private endpoint can be switched
# on later (enable_private_endpoint) when AKS reads secrets through the CSI driver.

resource "azurerm_key_vault" "this" {
  name                          = var.name
  location                      = var.location
  resource_group_name           = var.resource_group_name
  tenant_id                     = var.tenant_id
  sku_name                      = "standard"
  rbac_authorization_enabled    = true
  soft_delete_retention_days    = 7
  purge_protection_enabled      = false
  public_network_access_enabled = true
  tags                          = merge(var.tags, { est_usd_per_hour = "0" })

  network_acls {
    default_action = "Deny"
    bypass         = "AzureServices"
    # Key Vault stores single addresses without the /32 suffix; strip it to avoid a perpetual diff.
    ip_rules = [for c in var.admin_source_cidrs : trimsuffix(c, "/32")]
    # Subnets with a Microsoft.KeyVault service endpoint (the VM subnet: its managed identity reads
    # the lift secrets and the IIS certificate).
    virtual_network_subnet_ids = var.allowed_subnet_ids
  }
}

# The signed-in user administers the vault's data plane (Owner on the subscription does not
# include data-plane rights in RBAC mode).
resource "azurerm_role_assignment" "admin" {
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = var.admin_object_id
}

# Data-plane role assignments take a while to reach Key Vault; anything that writes secrets
# depends on the module's `id` output, which waits for this.
resource "time_sleep" "rbac_propagation" {
  depends_on      = [azurerm_role_assignment.admin]
  create_duration = var.rbac_propagation_wait
}

# ---- optional private endpoint (off in the foundation stage) ---------------------------------

resource "azurerm_private_dns_zone" "kv" {
  count               = var.enable_private_endpoint ? 1 : 0
  name                = "privatelink.vaultcore.azure.net"
  resource_group_name = var.resource_group_name
  tags                = merge(var.tags, { est_usd_per_hour = "0.0007" })
}

resource "azurerm_private_dns_zone_virtual_network_link" "kv" {
  count               = var.enable_private_endpoint ? 1 : 0
  name                = "pdnslink-${var.name}"
  private_dns_zone_id = azurerm_private_dns_zone.kv[0].id
  virtual_network_id  = var.vnet_id
  tags                = merge(var.tags, { est_usd_per_hour = "0" })
}

resource "azurerm_private_endpoint" "kv" {
  count                         = var.enable_private_endpoint ? 1 : 0
  name                          = "pe-${var.name}"
  location                      = var.location
  resource_group_name           = var.resource_group_name
  subnet_id                     = var.pe_subnet_id
  custom_network_interface_name = "nic-pe-${var.name}"
  tags                          = merge(var.tags, { est_usd_per_hour = "0.01" })

  private_service_connection {
    name                           = "psc-kv"
    private_connection_resource_id = azurerm_key_vault.this.id
    subresource_names              = ["vault"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [azurerm_private_dns_zone.kv[0].id]
  }
}
