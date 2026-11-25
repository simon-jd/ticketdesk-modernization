variable "name" {
  type = string
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "daily_quota_gb" {
  description = "Log Analytics daily ingestion cap in GB (covers App Insights too)."
  type        = number
  default     = 0.1
  validation {
    condition     = var.daily_quota_gb > 0 && var.daily_quota_gb <= 1
    error_message = "Keep the daily cap between 0 and 1 GB for the lab."
  }
}

variable "alert_email" {
  description = "Receiver of the action group; empty means no action group."
  type        = string
  default     = ""
}

variable "tags" {
  type = map(string)
}
