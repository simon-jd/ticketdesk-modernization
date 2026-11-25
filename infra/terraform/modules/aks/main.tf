# AKS, free control-plane tier, one x64 system node pool in the VNet's AKS subnet, plus the Basic container registry the
# cluster pulls from. Planned in Round 1, applied in Round 4 (one unattended session, ops/azure/aks.sh).
#
# - One node (Standard_B2as_v2 by default). The session retries with the next size of its fallback list
#   (B2as_v2 -> B2als_v2 -> B2s_v2) when Azure answers AllocationFailed; next to the 2-vCPU Windows VM that is 4 of the
#   6 vCPU regional quota, so an upgrade surge node (+2) still fits.
# - Managed OS disk, 30 GB: AKS gives every size that supports premium storage a Premium SSD OS disk (P4 here); AKS has no
#   setting for a Standard HDD OS disk, and the Basv2/Bsv2 sizes have no temporary disk for an ephemeral one
#   (docs/dev/azure-aks.md, "OS disk").
# - Azure CNI overlay with Cilium dataplane and network policy (pods get overlay IPs, the subnet only holds node IPs).
# - Entra ID authentication with Azure RBAC, local accounts disabled; the signed-in user gets Azure Kubernetes Service
#   RBAC Cluster Admin. API server reachable only from the admin CIDRs.
# - OIDC issuer + workload identity: the app's and the migration job's user-assigned identities federate with their
#   Kubernetes service accounts (root module).
# - Key Vault Secrets Store CSI driver with rotation (poll interval secret_rotation_interval): its identity gets Key Vault
#   Secrets User; the web pods mount the TLS certificate and the few secrets through it.
# - No automatic upgrades (no upgrade channel, node OS channel None) while a session measures; it lives one session.
# - Container Insights off by default: its agents would take ~0.2 vCPU of the single node and fill the workspace's daily
#   cap; the app's request telemetry (Application Insights) carries the alerts and SLOs.
# - The node resource group (`node_resource_group`) is created and owned by AKS; it holds the node VMSS, its disk, the
#   Standard load balancer with the outbound public IP and the app's service IP, and disappears with the cluster.

locals {
  # Retail Prices API, swedencentral, 2026-11-11 (docs/evidence/aks-20261111/prices/)
  node_usd_per_hour = lookup(var.linux_usd_per_hour, var.node_vm_size, 0.2)
  disk_usd_per_hour = (var.node_os_disk_gb <= 32 ? 5.8072 : 11.2272) / 730 # P4 / P6 LRS Premium SSD
  est_usd_per_hour = (
    (local.node_usd_per_hour + local.disk_usd_per_hour) * var.node_count
    + 0.025                            # Standard LB, first 5 rules
    + 0.005 * 2                        # Standard static public IPs: outbound + the app's service
    + var.free_tier_infra_usd_per_hour # meter "FreeTierInfrastructureCost Uptime SLA", see ledger
  )
  acr_usd_per_hour = 0.1666 / 24 # Basic registry unit $0.1666/day (the ledger counts a whole day)
}

resource "azurerm_kubernetes_cluster" "this" {
  name                      = "aks-${var.name}-${var.location}"
  location                  = var.location
  resource_group_name       = var.resource_group_name
  node_resource_group       = var.node_resource_group
  dns_prefix                = "aks-${var.name}"
  sku_tier                  = "Free"
  oidc_issuer_enabled       = true
  workload_identity_enabled = true
  local_account_disabled    = true
  node_os_upgrade_channel   = "None"
  tags                      = merge(var.tags, { est_usd_per_hour = format("%.4f", local.est_usd_per_hour) })

  default_node_pool {
    name                        = "system"
    vm_size                     = var.node_vm_size
    node_count                  = var.node_count
    vnet_subnet_id              = var.subnet_id
    os_sku                      = "AzureLinux"
    os_disk_type                = "Managed"
    os_disk_size_gb             = var.node_os_disk_gb
    temporary_name_for_rotation = "systemtmp"
    tags                        = merge(var.tags, { est_usd_per_hour = format("%.4f", local.node_usd_per_hour + local.disk_usd_per_hour) })

    upgrade_settings {
      max_surge = "1"
    }
  }

  identity {
    type = "SystemAssigned"
  }

  # Fixed node pool, no node auto-provisioning (Karpenter) - the quota leaves no room for it.
  node_provisioning_profile {
    mode = "Manual"
  }

  network_profile {
    network_plugin      = "azure"
    network_plugin_mode = "overlay"
    network_data_plane  = "cilium"
    network_policy      = "cilium"
    load_balancer_sku   = "standard"
    outbound_type       = "loadBalancer"
    pod_cidr            = "192.168.0.0/16"
    service_cidr        = "172.16.0.0/16"
    dns_service_ip      = "172.16.0.10"
  }

  api_server_access_profile {
    authorized_ip_ranges = var.admin_source_cidrs
  }

  azure_active_directory_role_based_access_control {
    tenant_id          = var.tenant_id
    azure_rbac_enabled = true
  }

  key_vault_secrets_provider {
    secret_rotation_enabled  = true
    secret_rotation_interval = var.secret_rotation_interval
  }

  dynamic "oms_agent" {
    for_each = var.enable_container_insights ? [1] : []
    content {
      log_analytics_workspace_id      = var.log_analytics_workspace_id
      msi_auth_for_monitoring_enabled = true
    }
  }
}

resource "azurerm_role_assignment" "cluster_admin" {
  scope                = azurerm_kubernetes_cluster.this.id
  role_definition_name = "Azure Kubernetes Service RBAC Cluster Admin"
  principal_id         = var.admin_object_id
}

resource "azurerm_role_assignment" "csi_kv_secrets_user" {
  scope                = var.key_vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_kubernetes_cluster.this.key_vault_secrets_provider[0].secret_identity[0].object_id
}

# The cluster's identity joins nodes to the subnet in our resource group.
resource "azurerm_role_assignment" "subnet_network_contributor" {
  scope                = var.subnet_id
  role_definition_name = "Network Contributor"
  principal_id         = azurerm_kubernetes_cluster.this.identity[0].principal_id
}

# ---- registry: Basic, this round only (images are pushed from lab-host, GHCR is out: nothing is pushed publicly) ----

resource "azurerm_container_registry" "this" {
  name                          = var.acr_name
  location                      = var.location
  resource_group_name           = var.resource_group_name
  sku                           = "Basic"
  admin_enabled                 = false # Entra only: kubelet identity pulls, the signed-in user pushes
  anonymous_pull_enabled        = false
  public_network_access_enabled = true # Basic has no firewall or private endpoint
  tags                          = merge(var.tags, { est_usd_per_hour = format("%.4f", local.acr_usd_per_hour) })
}

resource "azurerm_role_assignment" "kubelet_acr_pull" {
  scope                            = azurerm_container_registry.this.id
  role_definition_name             = "AcrPull"
  principal_id                     = azurerm_kubernetes_cluster.this.kubelet_identity[0].object_id
  skip_service_principal_aad_check = true
}

resource "azurerm_role_assignment" "admin_acr_push" {
  scope                = azurerm_container_registry.this.id
  role_definition_name = "AcrPush"
  principal_id         = var.admin_object_id
}

# ---- Container Insights (optional; managed identity + data collection rule) ------------------

resource "azurerm_monitor_data_collection_rule" "container_insights" {
  count               = var.enable_container_insights ? 1 : 0
  name                = "dcr-${var.name}-ci"
  location            = var.location
  resource_group_name = var.resource_group_name
  kind                = "Linux"
  tags                = merge(var.tags, { est_usd_per_hour = "0" })

  destinations {
    log_analytics {
      name                  = "law"
      workspace_resource_id = var.log_analytics_workspace_id
    }
  }

  data_flow {
    streams      = ["Microsoft-ContainerInsights-Group-Default"]
    destinations = ["law"]
  }

  data_sources {
    extension {
      name           = "ContainerInsightsExtension"
      extension_name = "ContainerInsights"
      streams        = ["Microsoft-ContainerInsights-Group-Default"]
      extension_json = jsonencode({
        dataCollectionSettings = {
          interval               = "5m" # lab: fewer samples, less ingestion
          namespaceFilteringMode = "Exclude"
          namespaces             = ["kube-system", "gatekeeper-system", "azure-arc"]
          enableContainerLogV2   = true
        }
      })
    }
  }
}

resource "azurerm_monitor_data_collection_rule_association" "container_insights" {
  count                   = var.enable_container_insights ? 1 : 0
  name                    = "ContainerInsightsExtension"
  target_resource_id      = azurerm_kubernetes_cluster.this.id
  data_collection_rule_id = azurerm_monitor_data_collection_rule.container_insights[0].id
}
