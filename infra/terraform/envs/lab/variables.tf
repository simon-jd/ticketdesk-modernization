# ---- where -----------------------------------------------------------------------------------

variable "subscription_id" {
  description = "Azure for Students subscription (the Makefile passes it from `az account show`)."
  type        = string
}

variable "tenant_id" {
  type = string
}

variable "location" {
  type    = string
  default = "swedencentral"
  validation {
    # Decided 2026-11-08 (plan/DECISIONS.md): the subscription's region policy allows only westus,
    # norwayeast, francecentral, denmarkeast and swedencentral; westus restricts Azure SQL and offers
    # no deployable x64 VM size, so the lab runs in swedencentral.
    condition     = var.location == "swedencentral"
    error_message = "Lab rule: swedencentral only."
  }
}

variable "resource_group_name" {
  type    = string
  default = "rg-ticketdesk-lab"
  validation {
    condition     = var.resource_group_name == "rg-ticketdesk-lab"
    error_message = "Lab rule: every resource lives in rg-ticketdesk-lab."
  }
}

variable "name" {
  description = "Short name inside resource names."
  type        = string
  default     = "tdlab"
}

# ---- tags ------------------------------------------------------------------------------------

variable "owner" {
  description = "owner tag: agent-<name> of whoever applies."
  type        = string
  default     = "agent-azure-foundation"
  validation {
    condition     = startswith(var.owner, "agent-")
    error_message = "owner must be agent-<name>."
  }
}

variable "entra_owner" {
  description = "owner tag of the Entra app (created by azure-foundation, kept across stages and agents)."
  type        = string
  default     = "agent-azure-foundation"
}

variable "ttl_utc" {
  description = "Expiry written to the ttl tag (UTC, YYYY-MM-DDThh:mm:ssZ); the reaper deletes after it."
  type        = string
  validation {
    condition     = can(regex("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$", var.ttl_utc))
    error_message = "ttl_utc must look like 2026-11-07T23:00:00Z."
  }
}

# ---- stage flags (stages/*.tfvars set them; each file describes the whole desired state) ----

variable "enable_foundation" {
  description = "Resource group, network, Key Vault, monitoring."
  type        = bool
  default     = false
}

variable "enable_sql" {
  description = "Azure SQL logical server + free-offer DB + private endpoint."
  type        = bool
  default     = false
  validation {
    condition     = !var.enable_sql || var.enable_foundation
    error_message = "enable_sql needs enable_foundation."
  }
}

variable "enable_vm" {
  description = "Windows Server 2025 VM for the lift & shift."
  type        = bool
  default     = false
  validation {
    condition     = !var.enable_vm || var.enable_foundation
    error_message = "enable_vm needs enable_foundation."
  }
}

variable "enable_aks" {
  description = "AKS free tier cluster."
  type        = bool
  default     = false
  validation {
    condition     = !var.enable_aks || var.enable_foundation
    error_message = "enable_aks needs enable_foundation."
  }
}

variable "enable_entra" {
  description = "Entra app registration ticketdesk-lab-web (kept across stages)."
  type        = bool
  default     = true
}

# ---- knobs -----------------------------------------------------------------------------------

variable "admin_source_cidrs" {
  description = "Home public IP as /32 (from $TD_W/.secrets/azure-lab.tfvars, not committed)."
  type        = list(string)
  default     = []
  validation {
    condition     = alltrue([for c in var.admin_source_cidrs : can(cidrhost(c, 0)) && !contains(["0.0.0.0/0", "*"], c)])
    error_message = "admin_source_cidrs must be specific CIDRs, never 0.0.0.0/0."
  }
}

variable "aks_ingress_source_cidrs" {
  type    = list(string)
  default = []
}

variable "alert_email" {
  description = "Action group receiver (from the secrets tfvars); empty = no action group."
  type        = string
  default     = ""
}

variable "log_daily_quota_gb" {
  type    = number
  default = 0.1
}

variable "kv_private_endpoint" {
  description = "Private endpoint + privatelink.vaultcore.azure.net for Key Vault."
  type        = bool
  default     = false
}

variable "vm_size" {
  description = "Windows VM size (x64, Basv2 family): 2 vCPU / 8 GiB like the on-prem VM."
  type        = string
  default     = "Standard_B2as_v2"
}

variable "app_admin_password" {
  description = "Lift stage: TicketDesk admin password for Key Vault (TF_VAR_app_admin_password, set by the Makefile)."
  type        = string
  default     = ""
  sensitive   = true
}

variable "lift_cert_pfx_path" {
  description = "Lift stage: PFX of the IIS certificate issued by the lab CA (make azure-lift-cert)."
  type        = string
  default     = ""
}

variable "lift_cert_pfx_password" {
  description = "Lift stage: password of that PFX (TF_VAR_lift_cert_pfx_password, set by the Makefile)."
  type        = string
  default     = ""
  sensitive   = true
}

variable "aks_node_vm_size" {
  description = "AKS node size (x64, so one linux/amd64 image serves local docker and AKS); on AllocationFailed the session retries with Standard_B2als_v2, then Standard_B2s_v2."
  type        = string
  default     = "Standard_B2as_v2"
}

variable "aks_node_count" {
  description = "AKS round: 1 node (2 vCPU) next to the 2 vCPU VM; a surge node still fits the 6 vCPU regional cap."
  type        = number
  default     = 1
}

variable "aks_cert_pfx_path" {
  description = "AKS stages: PFX of the app endpoint's TLS certificate from the lab CA (ops/azure/aks.sh cert; same PFX password as the lift certificate)."
  type        = string
  default     = ""
}

variable "failed_requests_alert_threshold" {
  description = "Failed requests per minute above which the alert fires (the drill's fault: every request fails, and the kubelet's readiness probe alone fails 6 times a minute)."
  type        = number
  default     = 2
}

variable "entra_redirect_uris" {
  type = list(string)
  default = [
    "https://localhost:5001/signin-oidc",
    "https://localhost:5443/signin-oidc",
    "https://localhost:7001/signin-oidc",
  ]
}
