# Windows Server 2025 Datacenter Server Core (smalldisk Gen2 image, 32 GB Standard_LRS OS disk)
# for the lift & shift of the legacy app (lift stage).
#
# - Admin password: random, stored in Key Vault (never printed); Terraform state holds it too,
#   which is why state lives outside the repo with 0600 permissions. Nobody signs in with it:
#   there is no inbound RDP/WinRM/SSH, administration goes through VM Run Command.
# - System-assigned managed identity with Key Vault Secrets User on the vault, so the provisioning
#   scripts read secrets and the IIS certificate without passwords on any command line. Storage
#   roles for the same identity are granted in the root module (container scopes). With Azure SQL
#   (replatform) it is also the legacy app's contained database user, and the Azure SQL deploy
#   identity (user-assigned, the server's Entra admin) is attached next to it.
# - Provisioning is not part of this module: ops/azure/lift.sh runs the same ops/windows/*.ps1 as
#   the local VM through Run Command (control plane, no inbound port) after `terraform apply`.
# - Public IP (Standard, static, DNS label) for outbound (the subnet has no default outbound access)
#   and for HTTPS from the admin source CIDRs; the subnet NSG allows 443 only, from those CIDRs.
# - Updates: patch_mode Manual and automatic updates off, so Windows Update never reboots the VM in a
#   measurement window (plan/DECISIONS.md 2026-11-08). The VM lives a few hours and is built from the
#   newest image version, which carries the latest monthly cumulative update. Defender Antivirus stays
#   on (image default; lift.sh records Get-MpComputerStatus).

locals {
  # Retail Prices API, swedencentral, 2026-11-08: see docs/dev/azure-spend.md.
  vm_usd_per_hour   = lookup(var.windows_usd_per_hour, var.size, 0.25)
  disk_usd_per_hour = 1.536 / 730 # S4 LRS (32 GiB Standard HDD) $1.536/month
  pip_usd_per_hour  = 0.005       # Standard IPv4 static public IP
}

resource "random_password" "admin" {
  length           = 24
  special          = true
  override_special = "!#%*-_=+"
  min_upper        = 2
  min_lower        = 2
  min_numeric      = 2
  min_special      = 2
}

resource "azurerm_key_vault_secret" "admin_password" {
  name         = "vm-${var.name}-admin-password"
  value        = random_password.admin.result
  key_vault_id = var.key_vault_id
  content_type = "password"
  tags         = merge(var.tags, { est_usd_per_hour = "0" })
}

resource "azurerm_public_ip" "this" {
  name                = "pip-${var.name}"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "Standard"
  allocation_method   = "Static"
  domain_name_label   = var.domain_name_label # <label>.<region>.cloudapp.azure.com, the IIS certificate's name
  tags                = merge(var.tags, { est_usd_per_hour = tostring(local.pip_usd_per_hour) })
}

resource "azurerm_network_interface" "this" {
  name                = "nic-${var.name}"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = merge(var.tags, { est_usd_per_hour = "0" })

  ip_configuration {
    name                          = "ipconfig1"
    subnet_id                     = var.subnet_id
    private_ip_address_allocation = "Dynamic"
    public_ip_address_id          = azurerm_public_ip.this.id
  }
}

resource "azurerm_windows_virtual_machine" "this" {
  name                      = "vm-${var.name}"
  computer_name             = var.computer_name
  location                  = var.location
  resource_group_name       = var.resource_group_name
  size                      = var.size
  admin_username            = var.admin_username
  admin_password            = random_password.admin.result
  network_interface_ids     = [azurerm_network_interface.this.id]
  secure_boot_enabled       = true
  vtpm_enabled              = true
  provision_vm_agent        = true
  patch_mode                = "Manual"
  automatic_updates_enabled = false
  tags = merge(var.tags, {
    est_usd_per_hour = format("%.4f", local.vm_usd_per_hour + local.disk_usd_per_hour)
  })

  os_disk {
    name                 = "osdisk-${var.name}"
    caching              = "ReadWrite"
    storage_account_type = "Standard_LRS"
    disk_size_gb         = var.os_disk_size_gb
  }

  source_image_reference {
    publisher = "MicrosoftWindowsServer"
    offer     = "WindowsServer"
    sku       = var.image_sku
    version   = "latest"
  }

  identity {
    # The system-assigned identity is the app's (Key Vault, storage, Azure SQL contained user); a user-assigned one
    # (replatform: the Azure SQL deploy identity) can sit next to it. IMDS returns the system-assigned token unless a
    # client_id is asked for.
    type         = length(var.user_assigned_identity_ids) > 0 ? "SystemAssigned, UserAssigned" : "SystemAssigned"
    identity_ids = length(var.user_assigned_identity_ids) > 0 ? var.user_assigned_identity_ids : null
  }

  boot_diagnostics {} # managed storage account
}

resource "azurerm_role_assignment" "kv_secrets_user" {
  scope                = var.key_vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_windows_virtual_machine.this.identity[0].principal_id
}
