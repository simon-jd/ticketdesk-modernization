variable "display_name" {
  type    = string
  default = "ticketdesk-lab-web"
  validation {
    condition     = startswith(var.display_name, "ticketdesk-lab-")
    error_message = "Lab rule: app registrations are named ticketdesk-lab-*."
  }
}

variable "owner_object_id" {
  description = "Signed-in user: app/service principal owner and TdAdministrators assignee."
  type        = string
}

variable "owner_tag" {
  type = string
}

variable "redirect_uris" {
  description = "OIDC redirect URIs (web platform). Localhost (the gateway https://localhost:5443) now; the AKS host is added in Round 4."
  type        = list(string)
  default = [
    "https://localhost:5001/signin-oidc",
    "https://localhost:5443/signin-oidc",
    "https://localhost:7001/signin-oidc",
  ]
}

variable "create_client_secret" {
  description = "Client secret for the lab OIDC sign-in (code flow, confidential web client)."
  type        = bool
  default     = true
}

variable "client_secret_end_date" {
  description = "Expiry of the lab client secret (RFC 3339). The lab ends before it; delete the app at the end."
  type        = string
  default     = "2026-12-31T00:00:00Z"
}

variable "logout_url" {
  type    = string
  default = "https://localhost:5001/signout-oidc"
}
