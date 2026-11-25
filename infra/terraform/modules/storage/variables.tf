variable "name" {
  description = "Globally unique storage account name (3-24 lowercase letters and digits)."
  type        = string
  validation {
    condition     = can(regex("^[a-z0-9]{3,24}$", var.name))
    error_message = "Storage account names are 3-24 lowercase letters and digits."
  }
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "containers" {
  type    = list(string)
  default = ["artifacts", "backups", "logs"]
}

variable "operator_object_id" {
  description = "Object ID of the signed-in user (gets Storage Blob Data Contributor on the account)."
  type        = string
}

variable "admin_source_cidrs" {
  description = "Public CIDRs allowed through the storage firewall (home public IP)."
  type        = list(string)
  default     = []
}

variable "allowed_subnet_ids" {
  description = "Subnets (with a Microsoft.Storage service endpoint) allowed through the storage firewall."
  type        = list(string)
  default     = []
}

variable "shared_access_key_enabled" {
  description = "Allow shared keys (and so account SAS). Off except for the legacy attachments account."
  type        = bool
  default     = false
}

variable "rbac_propagation_wait" {
  type    = string
  default = "60s"
}

variable "tags" {
  type = map(string)
}
