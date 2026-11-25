output "id" {
  description = "Account ID; available only after the operator role assignment has had time to propagate."
  value       = azurerm_storage_account.this.id
  depends_on  = [time_sleep.rbac_propagation]
}

output "name" {
  value = azurerm_storage_account.this.name
}

output "blob_endpoint" {
  value = azurerm_storage_account.this.primary_blob_endpoint
}

output "container_ids" {
  description = "Resource Manager IDs of the containers (scopes for container-level role assignments)."
  value       = { for k, c in azurerm_storage_container.this : k => c.id }
}

output "primary_connection_string" {
  description = "Only meaningful with shared_access_key_enabled (input for an account SAS; never printed)."
  value       = azurerm_storage_account.this.primary_connection_string
  sensitive   = true
}

output "est_usd_per_hour" {
  value = 0
}
