variable "name" {
  description = "Globally unique namespace name."
  type        = string
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "queue_name" {
  description = "Same queue name as the emulator's (ops/dev/servicebus-emulator/Config.json)."
  type        = string
  default     = "ticket-notifications"
}

variable "tags" {
  type = map(string)
}
