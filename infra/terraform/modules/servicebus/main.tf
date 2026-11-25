# Service Bus Basic namespace with the notification queue (AKS round): the web app sends one message per ticket write, the
# worker receives, delivers the mail and schedules retry copies (Basic: queues only, which is all the worker uses).
# Local (SAS) authentication is off: only Entra identities with a data role on the queue (the app's workload identity)
# reach it. Basic has no VNet integration; the endpoint is public, protected by Entra auth and TLS 1.2.
# Cost (Retail Prices API, swedencentral, 2026-11-11): Basic messaging operations $0.05 per million, no base charge.

resource "azurerm_servicebus_namespace" "this" {
  name                          = var.name
  location                      = var.location
  resource_group_name           = var.resource_group_name
  sku                           = "Basic"
  local_auth_enabled            = false
  minimum_tls_version           = "1.2"
  public_network_access_enabled = true
  tags                          = merge(var.tags, { est_usd_per_hour = "0" })
}

resource "azurerm_servicebus_queue" "notifications" {
  name                                 = var.queue_name
  namespace_id                         = azurerm_servicebus_namespace.this.id
  lock_duration                        = "PT1M"
  max_delivery_count                   = 5
  default_message_ttl                  = "PT1H"
  dead_lettering_on_message_expiration = true
  partitioning_enabled                 = false
}
