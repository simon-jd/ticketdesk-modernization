variable "name" {
  description = "Short name used in child resource names (for example tdlab)."
  type        = string
}

variable "server_name" {
  description = "Globally unique logical server name."
  type        = string
}

variable "database_name" {
  type    = string
  default = "sqldb-ticketdesk"
}

variable "database_api_version" {
  description = "ARM API version for Microsoft.Sql/servers/databases (must know useFreeLimit)."
  type        = string
  default     = "2025-01-01"
}

variable "location" {
  description = "Region of the server, database and private endpoint (same as the VNet)."
  type        = string
}

variable "resource_group_name" {
  type = string
}

variable "admin_login" {
  description = "Display name / UPN of the Entra administrator (the signed-in user)."
  type        = string
}

variable "admin_object_id" {
  type = string
}

variable "vnet_id" {
  type = string
}

variable "pe_subnet_id" {
  type = string
}

variable "tags" {
  type = map(string)
}
