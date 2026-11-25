variable "name" {
  description = "Short name; resources become vm-<name>, nic-<name>, pip-<name>, osdisk-<name>."
  type        = string
}

variable "computer_name" {
  description = "Windows computer name (max 15 characters)."
  type        = string
  validation {
    condition     = length(var.computer_name) <= 15
    error_message = "Windows computer names are at most 15 characters."
  }
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "subnet_id" {
  type = string
}

variable "key_vault_id" {
  type = string
}

variable "size" {
  description = "VM size. Standard_B2as_v2 (2 vCPU, 8 GiB, x64, Basv2 family) matches the on-prem VM's 8 GB."
  type        = string
  default     = "Standard_B2as_v2"
}

variable "windows_usd_per_hour" {
  description = "Windows PAYG hourly prices in swedencentral from the Retail Prices API (2026-11-08), for the est_usd_per_hour tag."
  type        = map(number)
  default = {
    Standard_B2as_v2  = 0.087
    Standard_B2als_v2 = 0.0481
    Standard_B2s_v2   = 0.0956
    Standard_B4as_v2  = 0.174
    Standard_D2as_v5  = 0.184
  }
}

variable "image_sku" {
  description = "Smallest-disk Windows Server 2025 image; Server Core like the on-prem VM."
  type        = string
  default     = "2025-datacenter-core-smalldisk-g2"
}

variable "os_disk_size_gb" {
  type    = number
  default = 32
  validation {
    condition     = var.os_disk_size_gb >= 30 && var.os_disk_size_gb <= 32
    error_message = "Lab rule: OS disks are 30-32 GB."
  }
}

variable "admin_username" {
  type    = string
  default = "tdadmin"
}

variable "domain_name_label" {
  description = "DNS label of the public IP (FQDN <label>.<region>.cloudapp.azure.com)."
  type        = string
}

variable "user_assigned_identity_ids" {
  description = "User-assigned identities attached next to the system-assigned one (the Azure SQL deploy identity)."
  type        = list(string)
  default     = []
}

variable "tags" {
  type = map(string)
}
