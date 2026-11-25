variable "name" {
  type = string
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "node_resource_group" {
  description = "Name of the AKS-managed node resource group."
  type        = string
}

variable "tenant_id" {
  type = string
}

variable "admin_object_id" {
  description = "Gets Azure Kubernetes Service RBAC Cluster Admin (the signed-in user)."
  type        = string
}

variable "admin_source_cidrs" {
  description = "API server authorized IP ranges."
  type        = list(string)
}

variable "subnet_id" {
  type = string
}

variable "key_vault_id" {
  type = string
}

variable "log_analytics_workspace_id" {
  type = string
}

variable "node_vm_size" {
  description = "Standard_B2as_v2: 2 vCPU / 8 GiB x64 (Basv2 family, quota 10); x64 keeps one linux/amd64 image. AllocationFailed fallbacks: Standard_B2als_v2 (2/4), Standard_B2s_v2 (2/8)."
  type        = string
  default     = "Standard_B2as_v2"
  validation {
    condition     = contains(["Standard_B2as_v2", "Standard_B2als_v2", "Standard_B2s_v2"], var.node_vm_size)
    error_message = "Lab rule: 2-vCPU x64 B sizes only (Standard_B2as_v2, then Standard_B2als_v2, then Standard_B2s_v2)."
  }
}

variable "node_count" {
  description = "1 node (2 vCPU) next to the VM (2 vCPU): 4 of the 6 vCPU regional cap, so a surge node fits."
  type        = number
  default     = 1
  validation {
    condition     = var.node_count >= 1 && var.node_count <= 2
    error_message = "The 6 vCPU regional quota allows at most 2 two-vCPU nodes next to the VM."
  }
}

variable "acr_name" {
  description = "Globally unique registry name (5-50 lowercase letters and digits)."
  type        = string
  validation {
    condition     = can(regex("^[a-z0-9]{5,50}$", var.acr_name))
    error_message = "Registry names are 5-50 lowercase letters and digits."
  }
}

variable "secret_rotation_interval" {
  description = "How often the Secrets Store CSI driver polls Key Vault for new versions (the TLS certificate rotation)."
  type        = string
  default     = "2m"
}

variable "enable_container_insights" {
  description = "Container Insights agents + data collection rule (off: CPU of the single node, workspace daily cap)."
  type        = bool
  default     = false
}

variable "node_os_disk_gb" {
  type    = number
  default = 30
}

variable "linux_usd_per_hour" {
  description = "Linux PAYG hourly prices in swedencentral from the Retail Prices API (re-fetched 2026-11-11, unchanged)."
  type        = map(number)
  default = {
    Standard_B2as_v2  = 0.0778
    Standard_B2als_v2 = 0.0389
    Standard_B2s_v2   = 0.0864
  }
}

variable "free_tier_infra_usd_per_hour" {
  description = "Retail Prices API lists 'FreeTierInfrastructureCost Uptime SLA' at $0.05/h from 2026-10-01; counted until shown not to apply."
  type        = number
  default     = 0.05
}

variable "tags" {
  type = map(string)
}
