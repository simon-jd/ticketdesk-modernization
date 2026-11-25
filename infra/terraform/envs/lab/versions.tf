terraform {
  required_version = ">= 1.17.0, < 2.0.0"

  required_providers {
    azurerm = { source = "hashicorp/azurerm", version = "~> 5.13" }
    azuread = { source = "hashicorp/azuread", version = "~> 3.10" }
    azapi   = { source = "Azure/azapi", version = "~> 2.13" }
    random  = { source = "hashicorp/random", version = "~> 3.9" }
    time    = { source = "hashicorp/time", version = "~> 0.14" }
  }

  # Local state outside the repo: `make azure-init` passes
  # -backend-config=path=$TD_W/.tfstate/lab/terraform.tfstate (directory 0700, files 0600).
  backend "local" {}
}
