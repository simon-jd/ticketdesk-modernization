variable "name" {
  description = "Short name used in resource names (for example tdlab)."
  type        = string
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "address_space" {
  description = "VNet address space; subnets are carved out of it with cidrsubnet()."
  type        = string
  default     = "10.42.0.0/16"
}

variable "admin_source_cidrs" {
  description = "Public CIDRs allowed to reach HTTPS (443 only) on the VM subnet (home IP as /32)."
  type        = list(string)
  default     = []
}

variable "vm_https_from_home" {
  description = "VM subnet: HTTPS from admin_source_cidrs (off after the AKS cutover: only the gateway reaches the legacy app)."
  type        = bool
  default     = true
}

variable "vm_https_from_aks" {
  description = "VM subnet: HTTPS from the AKS node subnet (the new app's YARP fallback to the legacy app)."
  type        = bool
  default     = false
}

variable "aks_ingress_source_cidrs" {
  description = "Public CIDRs allowed to reach HTTP/HTTPS on the AKS subnet (empty until cutover)."
  type        = list(string)
  default     = []
}

variable "tags" {
  description = "project/owner/ttl tags; est_usd_per_hour is added per resource."
  type        = map(string)
}
