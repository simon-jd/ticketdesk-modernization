# Azure SQL logical server (Entra-only authentication, public network access off) with one
# database on the Azure SQL Database free offer, reachable only through a private endpoint whose
# name resolves via the privatelink.database.windows.net private DNS zone linked to the VNet.
#
# azurerm 5.x has no arguments for the free offer (useFreeLimit / freeLimitExhaustionBehavior),
# so the database is created with azapi against the ARM API directly.

resource "azurerm_mssql_server" "this" {
  name                          = var.server_name
  location                      = var.location
  resource_group_name           = var.resource_group_name
  version                       = "12.0"
  minimum_tls_version           = "1.2"
  public_network_access_enabled = false
  tags                          = merge(var.tags, { est_usd_per_hour = "0" })

  azuread_administrator {
    login_username              = var.admin_login
    object_id                   = var.admin_object_id
    azuread_authentication_only = true
  }
}

resource "azapi_resource" "database" {
  type      = "Microsoft.Sql/servers/databases@${var.database_api_version}"
  name      = var.database_name
  parent_id = azurerm_mssql_server.this.id
  location  = var.location
  tags      = merge(var.tags, { est_usd_per_hour = "0" })

  body = {
    sku = {
      name     = "GP_S_Gen5"
      tier     = "GeneralPurpose"
      family   = "Gen5"
      capacity = 2
    }
    properties = {
      useFreeLimit                     = true
      freeLimitExhaustionBehavior      = "AutoPause" # never bill: pause until next month
      autoPauseDelay                   = 60
      minCapacity                      = 0.5
      maxSizeBytes                     = 34359738368 # 32 GB, the free-offer data limit
      collation                        = "SQL_Latin1_General_CP1_CI_AS"
      requestedBackupStorageRedundancy = "Local"
      zoneRedundant                    = false
    }
  }

  response_export_values = {
    status                      = "properties.status"
    use_free_limit              = "properties.useFreeLimit"
    free_limit_exhaustion       = "properties.freeLimitExhaustionBehavior"
    current_service_objective   = "properties.currentServiceObjectiveName"
    requested_backup_redundancy = "properties.requestedBackupStorageRedundancy"
  }
}

# ---- private endpoint ------------------------------------------------------------------------
# Private Endpoint $0.01/h + $0.01/GB processed; private DNS zone $0.50 per zone-month
# (Retail Prices API, swedencentral, 2026-11-08). The endpoint's NIC is created and deleted by Azure with the
# endpoint and carries no tags of its own.

resource "azurerm_private_dns_zone" "sql" {
  name                = "privatelink.database.windows.net"
  resource_group_name = var.resource_group_name
  tags                = merge(var.tags, { est_usd_per_hour = "0.0007" })
}

resource "azurerm_private_dns_zone_virtual_network_link" "sql" {
  name                 = "pdnslink-sql-${var.name}"
  private_dns_zone_id  = azurerm_private_dns_zone.sql.id
  virtual_network_id   = var.vnet_id
  registration_enabled = false
  tags                 = merge(var.tags, { est_usd_per_hour = "0" })
}

resource "azurerm_private_endpoint" "sql" {
  name                          = "pe-sql-${var.name}"
  location                      = var.location
  resource_group_name           = var.resource_group_name
  subnet_id                     = var.pe_subnet_id
  custom_network_interface_name = "nic-pe-sql-${var.name}"
  tags                          = merge(var.tags, { est_usd_per_hour = "0.01" })

  private_service_connection {
    name                           = "psc-sql"
    private_connection_resource_id = azurerm_mssql_server.this.id
    subresource_names              = ["sqlServer"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "default"
    private_dns_zone_ids = [azurerm_private_dns_zone.sql.id]
  }
}
