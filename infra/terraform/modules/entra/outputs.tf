output "client_id" {
  value = azuread_application.web.client_id
}

output "application_object_id" {
  value = azuread_application.web.object_id
}

output "service_principal_object_id" {
  value = azuread_service_principal.web.object_id
}

output "app_role_ids" {
  value = { for k, v in local.roles : k => v.id }
}

output "client_secret" {
  value     = try(azuread_application_password.web[0].value, null)
  sensitive = true
}
