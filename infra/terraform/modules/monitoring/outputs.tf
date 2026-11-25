output "workspace_id" {
  value = azurerm_log_analytics_workspace.this.id
}

output "workspace_name" {
  value = azurerm_log_analytics_workspace.this.name
}

output "app_insights_id" {
  value = azurerm_application_insights.this.id
}

output "app_insights_connection_string" {
  value     = azurerm_application_insights.this.connection_string
  sensitive = true
}

output "action_group_id" {
  value = try(azurerm_monitor_action_group.email[0].id, null)
}

output "est_usd_per_hour" {
  description = "Worst case: the daily cap fully used beyond the monthly free 5 GB."
  value       = var.daily_quota_gb * 2.99 / 24
}
