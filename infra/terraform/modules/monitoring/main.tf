# Log Analytics workspace with a daily ingestion cap, workspace-based Application Insights
# (its data is billed as Log Analytics ingestion, so the cap covers both) and an optional
# e-mail action group for alerts.
#
# Prices (Retail Prices API, swedencentral, 2026-11-08): Analytics Logs ingestion $0 for the first 5 GB
# per month (billing account), then $2.99/GB; 31 days retention included. Worst case with the
# 0.1 GB/day cap: 0.1 * 2.99 / 24 = $0.0125/h. Action group e-mails: first 1,000 per month free.

resource "azurerm_log_analytics_workspace" "this" {
  name                = "log-${var.name}-${var.location}"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  daily_quota_gb      = var.daily_quota_gb
  tags                = merge(var.tags, { est_usd_per_hour = format("%.4f", var.daily_quota_gb * 2.99 / 24) })
}

resource "azurerm_application_insights" "this" {
  name                = "appi-${var.name}-${var.location}"
  location            = var.location
  resource_group_name = var.resource_group_name
  workspace_id        = azurerm_log_analytics_workspace.this.id
  application_type    = "web"
  tags                = merge(var.tags, { est_usd_per_hour = "0" })
}

resource "azurerm_monitor_action_group" "email" {
  count               = var.alert_email == "" ? 0 : 1
  name                = "ag-${var.name}-email"
  resource_group_name = var.resource_group_name
  location            = "global"
  short_name          = "tdlab"
  tags                = merge(var.tags, { est_usd_per_hour = "0" })

  email_receiver {
    name                    = "owner"
    email_address           = var.alert_email
    use_common_alert_schema = true
  }
}
