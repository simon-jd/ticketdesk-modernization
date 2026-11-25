output "vm_id" {
  value = azurerm_windows_virtual_machine.this.id
}

output "principal_id" {
  value = azurerm_windows_virtual_machine.this.identity[0].principal_id
}

output "public_ip" {
  value = azurerm_public_ip.this.ip_address
}

output "fqdn" {
  value = azurerm_public_ip.this.fqdn
}

output "name" {
  value = azurerm_windows_virtual_machine.this.name
}

output "private_ip" {
  value = azurerm_network_interface.this.private_ip_address
}

output "admin_password_secret_id" {
  value = azurerm_key_vault_secret.admin_password.versionless_id
}

output "est_usd_per_hour" {
  value = local.vm_usd_per_hour + local.disk_usd_per_hour + local.pip_usd_per_hour
}
