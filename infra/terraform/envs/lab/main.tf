# TicketDesk lab: one root, stage flags decide what exists. All Azure resources live in
# rg-ticketdesk-lab with the tags project/owner/ttl/est_usd_per_hour; the Entra app lives in the
# tenant and survives stage changes.

data "azurerm_client_config" "current" {}

data "azurerm_subscription" "current" {}

data "azuread_client_config" "current" {}

data "azuread_user" "me" {
  object_id = data.azuread_client_config.current.object_id
}

locals {
  me_object_id = data.azuread_client_config.current.object_id
  # Deterministic suffix for globally unique names (Key Vault, SQL server).
  suffix = substr(sha1("${var.subscription_id}/${var.resource_group_name}"), 0, 6)
  tags = {
    project = "ticketdesk"
    owner   = var.owner
    ttl     = var.ttl_utc
  }
  rg_name = var.enable_foundation ? azurerm_resource_group.lab[0].name : null
  # Re-platform (VM + Azure SQL): the VM carries the Azure SQL deploy identity, which is the server's Entra admin.
  sql_on_vm = var.enable_vm && var.enable_sql
  # AKS round: the deploy identity exists whenever something inside the VNet administers Azure SQL: the VM (Run Command
  # steps) and/or the cluster's td-db migrate job (workload identity). It stays the Entra admin when the VM is deleted.
  sql_deploy = var.enable_sql && (var.enable_vm || var.enable_aks)
  sql_on_aks = var.enable_sql && var.enable_aks
  # The legacy attachments account outlives the VM: the new app on AKS keeps reading and writing the same container.
  files_on      = var.enable_vm || var.enable_aks
  aks_namespace = "ticketdesk"
}

check "subscription_is_students" {
  assert {
    condition     = data.azurerm_subscription.current.display_name == "Azure for Students"
    error_message = "This configuration is meant for the Azure for Students subscription only."
  }
}

resource "azurerm_resource_group" "lab" {
  count    = var.enable_foundation ? 1 : 0
  name     = var.resource_group_name
  location = var.location
  tags     = merge(local.tags, { est_usd_per_hour = "0" })
}

module "network" {
  count                    = var.enable_foundation ? 1 : 0
  source                   = "../../modules/network"
  name                     = var.name
  location                 = var.location
  resource_group_name      = local.rg_name
  admin_source_cidrs       = var.admin_source_cidrs
  aks_ingress_source_cidrs = var.enable_aks ? distinct(concat(var.admin_source_cidrs, var.aks_ingress_source_cidrs)) : []
  vm_https_from_aks        = var.enable_vm && var.enable_aks
  tags                     = local.tags
}

module "keyvault" {
  count                   = var.enable_foundation ? 1 : 0
  source                  = "../../modules/keyvault"
  name                    = "kv-${var.name}-${local.suffix}"
  location                = var.location
  resource_group_name     = local.rg_name
  tenant_id               = var.tenant_id
  admin_object_id         = local.me_object_id
  admin_source_cidrs      = var.admin_source_cidrs
  allowed_subnet_ids      = var.enable_aks ? [module.network[0].subnet_ids.vm, module.network[0].subnet_ids.aks] : [module.network[0].subnet_ids.vm]
  enable_private_endpoint = var.kv_private_endpoint
  vnet_id                 = module.network[0].vnet_id
  pe_subnet_id            = module.network[0].subnet_ids.pe
  tags                    = local.tags
}

module "monitoring" {
  count               = var.enable_foundation ? 1 : 0
  source              = "../../modules/monitoring"
  name                = var.name
  location            = var.location
  resource_group_name = local.rg_name
  daily_quota_gb      = var.log_daily_quota_gb
  alert_email         = var.alert_email
  tags                = local.tags
}

# App Insights connection string for the new app (read later through the CSI driver / config).
resource "azurerm_key_vault_secret" "appinsights" {
  count        = var.enable_foundation ? 1 : 0
  name         = "appinsights-connection-string"
  value        = module.monitoring[0].app_insights_connection_string
  key_vault_id = module.keyvault[0].id
  content_type = "text/plain"
  tags         = merge(local.tags, { est_usd_per_hour = "0" })
}

module "sql" {
  count               = var.enable_sql ? 1 : 0
  source              = "../../modules/sql"
  name                = var.name
  server_name         = "sql-${var.name}-${local.suffix}"
  location            = var.location
  resource_group_name = local.rg_name
  # Without the VM the signed-in user is the Entra admin. With the VM (replatform) the deploy identity is: the
  # server is reachable only through the private endpoint, so administration (BACPAC import, contained users,
  # td-db migrate, reconcile) runs on the VM, inside the VNet.
  admin_login     = local.sql_deploy ? azurerm_user_assigned_identity.sql_deploy[0].name : data.azuread_user.me.user_principal_name
  admin_object_id = local.sql_deploy ? azurerm_user_assigned_identity.sql_deploy[0].principal_id : local.me_object_id
  vnet_id         = module.network[0].vnet_id
  pe_subnet_id    = module.network[0].subnet_ids.pe
  tags            = local.tags
}

resource "azurerm_key_vault_secret" "sql_connection_string" {
  count        = var.enable_sql ? 1 : 0
  name         = "sql-connection-string"
  value        = module.sql[0].connection_string
  key_vault_id = module.keyvault[0].id
  content_type = "text/plain" # Entra authentication: contains no password
  tags         = merge(local.tags, { est_usd_per_hour = "0" })
}

module "vm" {
  count               = var.enable_vm ? 1 : 0
  source              = "../../modules/vm-windows"
  name                = "${var.name}-legacy"
  computer_name       = "tdlab-legacy"
  location            = var.location
  resource_group_name = local.rg_name
  subnet_id           = module.network[0].subnet_ids.vm
  key_vault_id        = module.keyvault[0].id
  size                = var.vm_size
  domain_name_label   = "${var.name}-legacy-${local.suffix}"
  # replatform: the Azure SQL deploy identity sits next to the system-assigned (app) identity.
  user_assigned_identity_ids = local.sql_on_vm ? [azurerm_user_assigned_identity.sql_deploy[0].id] : []
  tags                       = local.tags
}

# ---- Azure SQL re-platform (VM + SQL): deploy identity ------------------------------------------
# User-assigned identity attached to the VM and set as the logical server's Entra admin. The provisioning
# steps ask IMDS for its token by client id (BACPAC import with SqlPackage, CREATE USER for the app, td-db
# migrate and reconcile); the legacy app asks for the system-assigned identity's token and is only a
# contained database user (db_datareader + db_datawriter). Any process on the VM could ask IMDS for this
# identity too: in production the admin steps would run from a separate deployment agent in the VNet
# (docs/sql/azure-sql-move.md). The app user is created WITH SID (= the identity's client id) because a
# service-principal admin cannot resolve names in Entra without Directory Readers on the server identity.
resource "azurerm_user_assigned_identity" "sql_deploy" {
  count               = local.sql_deploy ? 1 : 0
  name                = "id-${var.name}-sqldeploy"
  location            = var.location
  resource_group_name = local.rg_name
  tags                = merge(local.tags, { est_usd_per_hour = "0" })
}

# ---- lift & shift: artifacts/backups storage, secrets and the IIS certificate (lift stage) ------
# Provisioning (ops/azure/lift.sh) runs ops/windows/*.ps1 on the VM through Run Command; the VM's
# managed identity reads the scripts bundle and the publish package from `artifacts`, the secrets
# and the certificate from Key Vault, and writes backups and transcripts to `backups` and `logs`.

module "storage" {
  count               = var.enable_vm ? 1 : 0
  source              = "../../modules/storage"
  name                = "st${var.name}${local.suffix}"
  location            = var.location
  resource_group_name = local.rg_name
  operator_object_id  = local.me_object_id
  admin_source_cidrs  = var.admin_source_cidrs
  allowed_subnet_ids  = [module.network[0].subnet_ids.vm]
  tags                = local.tags
}

resource "azurerm_role_assignment" "vm_artifacts_reader" {
  count                = var.enable_vm ? 1 : 0
  scope                = module.storage[0].container_ids["artifacts"]
  role_definition_name = "Storage Blob Data Reader"
  principal_id         = module.vm[0].principal_id
}

resource "azurerm_role_assignment" "vm_blob_writer" {
  for_each             = var.enable_vm ? toset(["backups", "logs"]) : toset([])
  scope                = module.storage[0].container_ids[each.value]
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = module.vm[0].principal_id
}

# ---- legacy attachments (current legacy build: TicketDesk.IO blob wrapper) ----------------------
# Own small account with shared keys allowed (the legacy Blob SDK knows only keys and SAS); the app gets an
# account SAS limited to the blob service, container + object level, read/add/create/write/delete/list, valid
# 8 days and re-issued weekly (time_rotating). Key Vault holds it as td-attachments-connection; the deploy step
# reads it through the VM's managed identity and writes it into Web.config (never logged). The ops account
# (artifacts/backups/logs) stays Entra-only.
module "files" {
  count                     = local.files_on ? 1 : 0
  source                    = "../../modules/storage"
  name                      = "st${var.name}files${local.suffix}"
  location                  = var.location
  resource_group_name       = local.rg_name
  containers                = ["ticketdesk-attachments"]
  shared_access_key_enabled = true
  operator_object_id        = local.me_object_id
  admin_source_cidrs        = var.admin_source_cidrs
  allowed_subnet_ids        = var.enable_aks ? [module.network[0].subnet_ids.vm, module.network[0].subnet_ids.aks] : [module.network[0].subnet_ids.vm]
  tags                      = local.tags
}

resource "time_rotating" "attachments_sas" {
  count         = local.files_on ? 1 : 0
  rotation_days = 7
}

data "azurerm_storage_account_sas" "attachments" {
  count             = local.files_on ? 1 : 0
  connection_string = module.files[0].primary_connection_string
  https_only        = true
  start             = time_rotating.attachments_sas[0].rfc3339
  expiry            = timeadd(time_rotating.attachments_sas[0].rotation_rfc3339, "24h")

  resource_types {
    service   = false
    container = true
    object    = true
  }

  services {
    blob  = true
    queue = false
    table = false
    file  = false
  }

  permissions {
    read    = true
    write   = true
    delete  = true
    list    = true
    add     = true
    create  = true
    update  = false
    process = false
    tag     = false
    filter  = false
  }
}

resource "azurerm_key_vault_secret" "attachments" {
  count           = local.files_on ? 1 : 0
  name            = "td-attachments-connection"
  value           = "BlobEndpoint=${module.files[0].blob_endpoint};SharedAccessSignature=${trimprefix(data.azurerm_storage_account_sas.attachments[0].sas, "?")}"
  key_vault_id    = module.keyvault[0].id
  content_type    = "connection string with an account SAS"
  expiration_date = timeadd(time_rotating.attachments_sas[0].rotation_rfc3339, "24h")
  tags            = merge(local.tags, { est_usd_per_hour = "0" })
}

# Shared key of the System.Web adapters' remote authentication endpoint (the new app's gateway sends it).
resource "random_uuid" "remoteapp_key" {
  count = var.enable_vm ? 1 : 0
}

resource "azurerm_key_vault_secret" "remoteapp_key" {
  count        = var.enable_vm ? 1 : 0
  name         = "td-remoteapp-api-key"
  value        = random_uuid.remoteapp_key[0].result
  key_vault_id = module.keyvault[0].id
  content_type = "guid"
  tags         = merge(local.tags, { est_usd_per_hour = "0" })
}

# sa password of the VM's SQL Express instance: generated here, read on the VM through the managed
# identity (Install-TdSqlExpress.ps1 via lift.sh), never printed. The cutover restores a database
# backup, which carries no server logins, so the Azure host can have its own sa password.
resource "random_password" "sql_sa" {
  count            = var.enable_vm ? 1 : 0
  length           = 24
  special          = true
  override_special = "!#%*-_=+"
  min_upper        = 2
  min_lower        = 2
  min_numeric      = 2
  min_special      = 2
}

resource "azurerm_key_vault_secret" "sql_sa" {
  count        = var.enable_vm ? 1 : 0
  name         = "td-sql-sa-password"
  value        = random_password.sql_sa[0].result
  key_vault_id = module.keyvault[0].id
  content_type = "password"
  tags         = merge(local.tags, { est_usd_per_hour = "0" })
}

# TicketDesk admin password (the account lives in the restored database; the VM's post-provision
# check signs in with it). The Makefile passes it from $TD_W/.secrets/legacy-lab.env as TF_VAR_*.
resource "azurerm_key_vault_secret" "app_admin" {
  count        = var.enable_vm ? 1 : 0
  name         = "td-app-admin-password"
  value        = var.app_admin_password
  key_vault_id = module.keyvault[0].id
  content_type = "password"
  tags         = merge(local.tags, { est_usd_per_hour = "0" })

  lifecycle {
    precondition {
      condition     = var.app_admin_password != ""
      error_message = "app_admin_password is empty: run through make (it reads $TD_W/.secrets/legacy-lab.env)."
    }
  }
}

# IIS certificate: issued by the lab CA (the same root the local VM's certificate chains to, so
# clients switching from the local VM to Azure need no new trust), imported once per apply from the
# PFX that `make azure-lift-cert` writes into $TD_W/.secrets. The VM reads it back through its managed
# identity as the certificate's secret (a passwordless PFX with the chain). Rotation:
# docs/dev/azure-lift.md.
resource "azurerm_key_vault_certificate" "iis" {
  count        = var.enable_vm ? 1 : 0
  name         = "td-iis-legacy"
  key_vault_id = module.keyvault[0].id
  tags         = merge(local.tags, { est_usd_per_hour = "0" })

  certificate {
    contents = filebase64(var.lift_cert_pfx_path)
    password = var.lift_cert_pfx_password
  }

  lifecycle {
    precondition {
      condition     = var.lift_cert_pfx_path != "" && var.lift_cert_pfx_password != ""
      error_message = "lift_cert_pfx_path/lift_cert_pfx_password missing: run `make azure-lift-cert` and apply through make."
    }
  }
}

module "aks" {
  count                      = var.enable_aks ? 1 : 0
  source                     = "../../modules/aks"
  name                       = var.name
  location                   = var.location
  resource_group_name        = local.rg_name
  node_resource_group        = "${var.resource_group_name}-aksnodes"
  tenant_id                  = var.tenant_id
  admin_object_id            = local.me_object_id
  admin_source_cidrs         = var.admin_source_cidrs
  subnet_id                  = module.network[0].subnet_ids.aks
  key_vault_id               = module.keyvault[0].id
  log_analytics_workspace_id = module.monitoring[0].workspace_id
  node_vm_size               = var.aks_node_vm_size
  node_count                 = var.aks_node_count
  acr_name                   = "acr${var.name}${local.suffix}"
  tags                       = local.tags
}

# ---- AKS round: the new app's Azure access, all through workload identity (no keys, no passwords) ----------------
# One user-assigned identity for the web and worker pods (service accounts td-web, td-worker in namespace ticketdesk):
# Azure SQL contained user in td_modern_app_role (created by the VM's aks-sql-user step, WITH SID = its client id),
# Key Vault Crypto User on the Data Protection key only, Service Bus Data Sender + Receiver on the queue only.
# The td-db migrate job (service account td-migrator) federates with the Azure SQL deploy identity, the server's admin.

resource "azurerm_user_assigned_identity" "app" {
  count               = var.enable_aks ? 1 : 0
  name                = "id-${var.name}-app"
  location            = var.location
  resource_group_name = local.rg_name
  tags                = merge(local.tags, { est_usd_per_hour = "0" })
}

resource "azurerm_federated_identity_credential" "app" {
  for_each                  = var.enable_aks ? toset(["td-web", "td-worker"]) : toset([])
  name                      = "fic-${each.value}"
  user_assigned_identity_id = azurerm_user_assigned_identity.app[0].id
  audience                  = ["api://AzureADTokenExchange"]
  issuer                    = module.aks[0].oidc_issuer_url
  subject                   = "system:serviceaccount:${local.aks_namespace}:${each.value}"
}

resource "azurerm_federated_identity_credential" "migrator" {
  count                     = local.sql_on_aks ? 1 : 0
  name                      = "fic-td-migrator"
  user_assigned_identity_id = azurerm_user_assigned_identity.sql_deploy[0].id
  audience                  = ["api://AzureADTokenExchange"]
  issuer                    = module.aks[0].oidc_issuer_url
  subject                   = "system:serviceaccount:${local.aks_namespace}:td-migrator"
}

# Data Protection key ring wrapping key (both apps wrap and unwrap with it; it never leaves the vault).
resource "azurerm_key_vault_key" "dataprotection" {
  count        = var.enable_aks ? 1 : 0
  name         = "td-dataprotection"
  key_vault_id = module.keyvault[0].id
  key_type     = "RSA"
  key_size     = 2048
  key_opts     = ["wrapKey", "unwrapKey"]
  tags         = merge(local.tags, { est_usd_per_hour = "0" })
}

resource "azurerm_role_assignment" "app_dataprotection_key" {
  count                = var.enable_aks ? 1 : 0
  scope                = azurerm_key_vault_key.dataprotection[0].resource_versionless_id
  role_definition_name = "Key Vault Crypto User"
  principal_id         = azurerm_user_assigned_identity.app[0].principal_id
}

resource "azurerm_role_assignment" "vm_dataprotection_key" {
  count                = var.enable_vm && var.enable_aks ? 1 : 0
  scope                = azurerm_key_vault_key.dataprotection[0].resource_versionless_id
  role_definition_name = "Key Vault Crypto User"
  principal_id         = module.vm[0].principal_id
}

# TLS certificate of the app's public endpoint (Kestrel behind the load balancer), issued by the lab CA for the service
# IP's DNS name; the CSI driver mounts it and re-reads it when a new version is imported (rotation: ops/azure/aks.sh).
# Versions imported by a rotation are not Terraform's: ignore_changes keeps a later apply from importing the old one.
resource "azurerm_key_vault_certificate" "aks_web" {
  count        = var.enable_aks ? 1 : 0
  name         = "td-aks-web"
  key_vault_id = module.keyvault[0].id
  tags         = merge(local.tags, { est_usd_per_hour = "0" })

  certificate {
    contents = filebase64(var.aks_cert_pfx_path)
    password = var.lift_cert_pfx_password
  }

  lifecycle {
    ignore_changes = [certificate]
    precondition {
      condition     = var.aks_cert_pfx_path != "" && var.lift_cert_pfx_password != ""
      error_message = "aks_cert_pfx_path missing: run `ops/azure/aks.sh cert` and apply through make."
    }
  }
}

module "servicebus" {
  count               = var.enable_aks ? 1 : 0
  source              = "../../modules/servicebus"
  name                = "sb-${var.name}-${local.suffix}"
  location            = var.location
  resource_group_name = local.rg_name
  tags                = local.tags
}

resource "azurerm_role_assignment" "app_servicebus" {
  for_each             = var.enable_aks ? toset(["Azure Service Bus Data Sender", "Azure Service Bus Data Receiver"]) : toset([])
  scope                = module.servicebus[0].queue_id
  role_definition_name = each.value
  principal_id         = azurerm_user_assigned_identity.app[0].principal_id
}

# ---- Azure DevOps CD (AKS round, the user's decision 2026-11-11: v2 through the pipeline's aks-lab stage) --------------
# The service connection ticketdesk-lab-arm signs in as the Entra app ticketdesk-lab-ado (pipelines round, workload identity
# federation, no secret). Its roles exist only with the session's resources and go with them:
# - Contributor on the resource group: ACR push (the registry has no admin user), AKS user credentials (kubeconfig), and the
#   read of the VMs that cd-aks.sh checks before it deploys (no v2 while vm-tdlab-legacy exists);
# - one AKS data-plane role through Azure RBAC (local accounts are off; Contributor has no Kubernetes dataActions):
#   "Azure Kubernetes Service RBAC Admin", scoped to the namespace ticketdesk only. "RBAC Writer" would be less, but its
#   dataActions (az role definition list, docs/evidence/aks-20261111/ado-role/) have no secrets-store.csi.x-k8s.io: helm
#   could not read or update the chart's SecretProviderClass td-kv. Admin's are managedClusters/* without namespace or
#   resource-quota writes; the namespace scope keeps it out of kube-system and every other namespace. Everything else the
#   v2 deploy touches is namespaced: Deployments, Service, ServiceAccounts, the pre-upgrade Job (+ its hook ServiceAccount),
#   Pods/ReplicaSets/Events for --wait, and the Secrets that hold the Helm release.
data "azuread_service_principal" "ado" {
  count        = var.enable_aks ? 1 : 0
  display_name = "ticketdesk-lab-ado"
}

resource "azurerm_role_assignment" "ado_rg_contributor" {
  count                = var.enable_aks ? 1 : 0
  scope                = azurerm_resource_group.lab[0].id
  role_definition_name = "Contributor"
  principal_id         = data.azuread_service_principal.ado[0].object_id
  principal_type       = "ServicePrincipal"
}

resource "azurerm_role_assignment" "ado_aks_rbac_admin" {
  count                = var.enable_aks ? 1 : 0
  scope                = "${module.aks[0].cluster_id}/namespaces/${local.aks_namespace}"
  role_definition_name = "Azure Kubernetes Service RBAC Admin"
  principal_id         = data.azuread_service_principal.ado[0].object_id
  principal_type       = "ServicePrincipal"
}

# ---- alerts (AKS round): failed requests of the new app, action group without receivers (fired/resolved times are read
# from the Alerts Management API by the drill) ------------------------------------------------------------------------

resource "azurerm_monitor_action_group" "drill" {
  count               = var.enable_aks ? 1 : 0
  name                = "ag-${var.name}-drill"
  resource_group_name = local.rg_name
  location            = "global"
  short_name          = "tddrill"
  tags                = merge(local.tags, { est_usd_per_hour = "0" })
}

resource "azurerm_monitor_metric_alert" "failed_requests" {
  count               = var.enable_aks ? 1 : 0
  name                = "alert-${var.name}-failed-requests"
  resource_group_name = local.rg_name
  scopes              = [module.monitoring[0].app_insights_id]
  description         = "The new app answers failed requests (Application Insights requests/failed, count per minute above the threshold)."
  severity            = 2
  frequency           = "PT1M"
  window_size         = "PT1M"
  auto_mitigate       = true
  tags                = merge(local.tags, { est_usd_per_hour = format("%.4f", 0.10 / 730) })

  criteria {
    metric_namespace = "microsoft.insights/components"
    metric_name      = "requests/failed"
    aggregation      = "Count"
    operator         = "GreaterThan"
    threshold        = var.failed_requests_alert_threshold
  }

  action {
    action_group_id = azurerm_monitor_action_group.drill[0].id
  }
}

module "entra" {
  count           = var.enable_entra ? 1 : 0
  source          = "../../modules/entra"
  owner_object_id = local.me_object_id
  owner_tag       = var.entra_owner
  redirect_uris   = var.entra_redirect_uris
}
