output "id" {
  description = "Vault ID; available only after the admin role assignment has had time to propagate."
  value       = azurerm_key_vault.this.id
  depends_on  = [time_sleep.rbac_propagation]
}

output "name" {
  value = azurerm_key_vault.this.name
}

output "vault_uri" {
  value = azurerm_key_vault.this.vault_uri
}

output "est_usd_per_hour" {
  value = var.enable_private_endpoint ? 0.0107 : 0
}
