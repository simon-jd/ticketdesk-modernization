variable "name" {
  description = "Globally unique vault name (3-24 chars)."
  type        = string
  validation {
    condition     = can(regex("^[a-zA-Z][a-zA-Z0-9-]{1,22}[a-zA-Z0-9]$", var.name))
    error_message = "Key Vault names are 3-24 characters: letters, digits and hyphens, starting with a letter."
  }
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "tenant_id" {
  type = string
}

variable "admin_object_id" {
  description = "Object ID that gets Key Vault Administrator on the vault (the signed-in user)."
  type        = string
}

variable "admin_source_cidrs" {
  description = "Public CIDRs allowed through the vault firewall."
  type        = list(string)
  default     = []
}

variable "allowed_subnet_ids" {
  description = "Subnets (with a Microsoft.KeyVault service endpoint) allowed through the vault firewall."
  type        = list(string)
  default     = []
}

variable "rbac_propagation_wait" {
  description = "Wait after the role assignment before anything writes secrets."
  type        = string
  default     = "120s"
}

variable "enable_private_endpoint" {
  type    = bool
  default = false
}

variable "vnet_id" {
  description = "Needed only with enable_private_endpoint."
  type        = string
  default     = null
}

variable "pe_subnet_id" {
  description = "Needed only with enable_private_endpoint."
  type        = string
  default     = null
}

variable "tags" {
  type = map(string)
}
