output "resource_group" {
  value = try(azurerm_resource_group.lab[0].name, null)
}

output "est_usd_per_hour" {
  description = "Hourly estimate of what this configuration keeps running (unit prices in docs/dev/azure-spend.md)."
  value = format("%.4f", sum([
    try(module.network[0].est_usd_per_hour, 0),
    try(module.keyvault[0].est_usd_per_hour, 0),
    try(module.monitoring[0].est_usd_per_hour, 0),
    try(module.sql[0].est_usd_per_hour, 0),
    try(module.vm[0].est_usd_per_hour, 0),
    try(module.storage[0].est_usd_per_hour, 0),
    try(module.files[0].est_usd_per_hour, 0),
    try(module.aks[0].est_usd_per_hour, 0),
    try(module.servicebus[0].est_usd_per_hour, 0),
    var.enable_aks ? 0.10 / 730 : 0, # the failed-requests metric alert ($0.10 per monitored metric-month)
  ]))
}

output "vnet_id" {
  value = try(module.network[0].vnet_id, null)
}

output "key_vault_name" {
  value = try(module.keyvault[0].name, null)
}

output "key_vault_uri" {
  value = try(module.keyvault[0].vault_uri, null)
}

output "log_analytics_workspace" {
  value = try(module.monitoring[0].workspace_name, null)
}

output "sql_server_fqdn" {
  value = try(module.sql[0].server_fqdn, null)
}

output "sql_database" {
  value = try(module.sql[0].database_name, null)
}

output "sql_database_properties" {
  value = try(module.sql[0].database_properties, null)
}

output "sql_private_endpoint_ip" {
  value = try(module.sql[0].private_endpoint_ip, null)
}

output "vm_public_ip" {
  value = try(module.vm[0].public_ip, null)
}

output "vm_name" {
  value = try(module.vm[0].name, null)
}

output "vm_fqdn" {
  value = try(module.vm[0].fqdn, null)
}

output "storage_account" {
  value = try(module.storage[0].name, null)
}

output "storage_blob_endpoint" {
  value = try(module.storage[0].blob_endpoint, null)
}

output "files_account" {
  value = try(module.files[0].name, null)
}

output "sql_admin" {
  description = "Entra admin of the logical server (the deploy identity with the VM or AKS, else the signed-in user)."
  value       = local.sql_deploy ? azurerm_user_assigned_identity.sql_deploy[0].name : try(data.azuread_user.me.user_principal_name, null)
}

output "sql_deploy_identity_client_id" {
  value = try(azurerm_user_assigned_identity.sql_deploy[0].client_id, null)
}

output "aks_cluster" {
  value = try(module.aks[0].cluster_name, null)
}

# ---- AKS round (ops/azure/aks.sh reads these; none is a secret) ----

output "aks_node_resource_group" {
  value = try(module.aks[0].node_resource_group, null)
}

output "aks_node_vm_size" {
  value = try(module.aks[0].node_vm_size, null)
}

output "aks_oidc_issuer_url" {
  value = try(module.aks[0].oidc_issuer_url, null)
}

output "aks_csi_identity_client_id" {
  value = try(module.aks[0].csi_identity_client_id, null)
}

output "acr_login_server" {
  value = try(module.aks[0].acr_login_server, null)
}

output "acr_name" {
  value = try(module.aks[0].acr_name, null)
}

output "app_identity_name" {
  value = try(azurerm_user_assigned_identity.app[0].name, null)
}

output "app_identity_client_id" {
  value = try(azurerm_user_assigned_identity.app[0].client_id, null)
}

output "dataprotection_key_id" {
  description = "Versionless URI of the Data Protection wrapping key (DataProtection:KeyVaultKeyId / ticketdesk:DataProtectionKeyVaultKeyId)."
  value       = try(azurerm_key_vault_key.dataprotection[0].versionless_id, null)
}

output "aks_web_certificate_name" {
  value = try(azurerm_key_vault_certificate.aks_web[0].name, null)
}

output "servicebus_namespace" {
  value = try(module.servicebus[0].fully_qualified_namespace, null)
}

output "servicebus_queue" {
  value = try(module.servicebus[0].queue_name, null)
}

output "files_blob_endpoint" {
  value = try(module.files[0].blob_endpoint, null)
}

output "vm_private_ip" {
  value = try(module.vm[0].private_ip, null)
}

output "aks_subnet_cidr" {
  value = try(module.network[0].aks_subnet_cidr, null)
}

output "app_insights_id" {
  value = try(module.monitoring[0].app_insights_id, null)
}

output "failed_requests_alert_id" {
  value = try(azurerm_monitor_metric_alert.failed_requests[0].id, null)
}

output "entra_client_id" {
  value = try(module.entra[0].client_id, null)
}

output "entra_app_role_ids" {
  value = try(module.entra[0].app_role_ids, null)
}

output "tenant_id" {
  value = var.tenant_id
}

output "entra_client_secret" {
  description = "Lab OIDC client secret (sensitive; ops/dev/entra-secret.sh writes it to $TD_W/.secrets/entra-web.env)."
  value       = try(module.entra[0].client_secret, null)
  sensitive   = true
}
