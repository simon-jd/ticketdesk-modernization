output "server_id" {
  value = azurerm_mssql_server.this.id
}

output "server_name" {
  value = azurerm_mssql_server.this.name
}

output "server_fqdn" {
  value = azurerm_mssql_server.this.fully_qualified_domain_name
}

output "database_id" {
  value = azapi_resource.database.id
}

output "database_name" {
  value = azapi_resource.database.name
}

output "database_properties" {
  value = azapi_resource.database.output
}

output "private_endpoint_ip" {
  value = azurerm_private_endpoint.sql.private_service_connection[0].private_ip_address
}

output "connection_string" {
  description = "No secret inside: Entra authentication (managed identity or developer sign-in)."
  value       = "Server=tcp:${azurerm_mssql_server.this.fully_qualified_domain_name},1433;Database=${azapi_resource.database.name};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connect Timeout=60"
}

output "est_usd_per_hour" {
  description = "Private endpoint + private DNS zone (0.50/month spread over 720 h); the free DB is 0."
  value       = 0.0107
}
