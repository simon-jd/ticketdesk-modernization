# VNet with one subnet per workload (VM, AKS nodes, private endpoints) and one NSG per subnet.
# Inbound is deny-by-default: the only allow rules from outside the VNet are HTTPS to the VM subnet
# from the admin source CIDRs (home public IP) and, later, the AKS ingress sources the cutover stage sets, plus the
# load balancer's health probes on the AKS subnet (AllowAzureLoadBalancerInbound).
# VNets, subnets and NSGs carry no charge (Retail Prices API has no meter for them).

locals {
  vm_cidr  = cidrsubnet(var.address_space, 8, 1) # 10.42.1.0/24
  pe_cidr  = cidrsubnet(var.address_space, 8, 2) # 10.42.2.0/24
  aks_cidr = cidrsubnet(var.address_space, 6, 4) # 10.42.16.0/22 (Azure CNI overlay: nodes only)
  tags     = merge(var.tags, { est_usd_per_hour = "0" })

  deny_internet = {
    name                       = "DenyInternetInbound"
    priority                   = 4000
    direction                  = "Inbound"
    access                     = "Deny"
    protocol                   = "*"
    source_address_prefix      = "Internet"
    source_port_range          = "*"
    destination_address_prefix = "*"
    destination_port_ranges    = ["0-65535"]
  }
}

resource "azurerm_virtual_network" "this" {
  name                = "vnet-${var.name}-${var.location}"
  location            = var.location
  resource_group_name = var.resource_group_name
  address_space       = [var.address_space]
  tags                = local.tags
}

# ---- NSGs ------------------------------------------------------------------------------------

resource "azurerm_network_security_group" "vm" {
  name                = "nsg-${var.name}-vm"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = local.tags

  # Only the IIS site (443) from the admin source CIDRs (home public IP). No RDP, WinRM or SSH from
  # anywhere: administration goes through VM Run Command (control plane, no inbound port).
  dynamic "security_rule" {
    for_each = length(var.admin_source_cidrs) > 0 && var.vm_https_from_home ? [1] : []
    content {
      name                       = "AllowHttpsFromHome"
      priority                   = 100
      direction                  = "Inbound"
      access                     = "Allow"
      protocol                   = "Tcp"
      source_address_prefixes    = var.admin_source_cidrs
      source_port_range          = "*"
      destination_address_prefix = "*"
      destination_port_range     = "443"
    }
  }

  # AKS round: the new app's gateway (YARP fallback) reaches IIS over the VNet from the AKS node subnet (Azure CNI
  # overlay SNATs pod traffic to the node IP). The legacy app trusts X-Forwarded-Host/-Proto, so after the cutover the
  # home rule above is dropped (vm_https_from_home = false) and only the gateway can reach it.
  dynamic "security_rule" {
    for_each = var.vm_https_from_aks ? [1] : []
    content {
      name                       = "AllowHttpsFromAks"
      priority                   = 110
      direction                  = "Inbound"
      access                     = "Allow"
      protocol                   = "Tcp"
      source_address_prefix      = local.aks_cidr
      source_port_range          = "*"
      destination_address_prefix = "*"
      destination_port_range     = "443"
    }
  }

  security_rule {
    name                       = local.deny_internet.name
    priority                   = local.deny_internet.priority
    direction                  = local.deny_internet.direction
    access                     = local.deny_internet.access
    protocol                   = local.deny_internet.protocol
    source_address_prefix      = local.deny_internet.source_address_prefix
    source_port_range          = local.deny_internet.source_port_range
    destination_address_prefix = local.deny_internet.destination_address_prefix
    destination_port_ranges    = local.deny_internet.destination_port_ranges
  }
}

resource "azurerm_network_security_group" "aks" {
  name                = "nsg-${var.name}-aks"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = local.tags

  # Empty until an AKS stage opens HTTPS to the app's load balancer service for chosen sources (the home IP). The LB rules
  # use floating IP, so the destination port seen here is the service port (443), not a node port.
  # Load balancer health probes: see AllowAzureLoadBalancerInbound below.
  dynamic "security_rule" {
    for_each = length(var.aks_ingress_source_cidrs) > 0 ? [1] : []
    content {
      name                       = "AllowIngressHttp"
      priority                   = 100
      direction                  = "Inbound"
      access                     = "Allow"
      protocol                   = "Tcp"
      source_address_prefixes    = var.aks_ingress_source_cidrs
      source_port_range          = "*"
      destination_address_prefix = "*"
      destination_port_range     = "443"
    }
  }

  # The load balancer's health probes come from 168.63.129.16, which DenyInternetInbound (source Internet) matches: the
  # default AllowAzureLoadBalancerInBound (65001) is never reached and the probe of the app's service fails (session of
  # 2026-11-12: DipAvailability 0, the service unreachable while the pod answered). Allowed explicitly, before the deny.
  security_rule {
    name                       = "AllowAzureLoadBalancerInbound"
    priority                   = 3900
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "*"
    source_address_prefix      = "AzureLoadBalancer"
    source_port_range          = "*"
    destination_address_prefix = "*"
    destination_port_range     = "*"
  }

  security_rule {
    name                       = local.deny_internet.name
    priority                   = local.deny_internet.priority
    direction                  = local.deny_internet.direction
    access                     = local.deny_internet.access
    protocol                   = local.deny_internet.protocol
    source_address_prefix      = local.deny_internet.source_address_prefix
    source_port_range          = local.deny_internet.source_port_range
    destination_address_prefix = local.deny_internet.destination_address_prefix
    destination_port_ranges    = local.deny_internet.destination_port_ranges
  }
}

resource "azurerm_network_security_group" "pe" {
  name                = "nsg-${var.name}-pe"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = local.tags

  security_rule {
    name                       = "AllowSqlFromVnet"
    priority                   = 100
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_address_prefix      = "VirtualNetwork"
    source_port_range          = "*"
    destination_address_prefix = "*"
    destination_port_range     = "1433"
  }

  security_rule {
    name                       = "DenyOtherVnetInbound"
    priority                   = 3900
    direction                  = "Inbound"
    access                     = "Deny"
    protocol                   = "*"
    source_address_prefix      = "VirtualNetwork"
    source_port_range          = "*"
    destination_address_prefix = "*"
    destination_port_range     = "*"
  }

  security_rule {
    name                       = local.deny_internet.name
    priority                   = local.deny_internet.priority
    direction                  = local.deny_internet.direction
    access                     = local.deny_internet.access
    protocol                   = local.deny_internet.protocol
    source_address_prefix      = local.deny_internet.source_address_prefix
    source_port_range          = local.deny_internet.source_port_range
    destination_address_prefix = local.deny_internet.destination_address_prefix
    destination_port_ranges    = local.deny_internet.destination_port_ranges
  }
}

# ---- Subnets ---------------------------------------------------------------------------------
# default_outbound_access_enabled = false: no implicit outbound SNAT. The VM gets outbound through
# its own Standard public IP, AKS through its Standard load balancer (NAT Gateway is not used).

# Service endpoints (free): the VM reaches Key Vault and the lab storage account over the Azure backbone
# with its subnet identity, so both keep their firewalls closed to the internet (default deny + home IP)
# and allow this subnet instead. A private endpoint would cost $0.01/h each.
resource "azurerm_subnet" "vm" {
  name                            = "snet-vm"
  resource_group_name             = var.resource_group_name
  virtual_network_name            = azurerm_virtual_network.this.name
  address_prefixes                = [local.vm_cidr]
  default_outbound_access_enabled = false

  service_endpoint {
    service = "Microsoft.KeyVault"
  }
  service_endpoint {
    service = "Microsoft.Storage"
  }
}

# AKS round: the same free service endpoints for the nodes (pods are SNATed to the node IP): the Secrets Store CSI driver
# and the app's key-wrap calls reach Key Vault, the app reaches the attachments account, both firewalled to subnets.
resource "azurerm_subnet" "aks" {
  name                            = "snet-aks"
  resource_group_name             = var.resource_group_name
  virtual_network_name            = azurerm_virtual_network.this.name
  address_prefixes                = [local.aks_cidr]
  default_outbound_access_enabled = false

  service_endpoint {
    service = "Microsoft.KeyVault"
  }
  service_endpoint {
    service = "Microsoft.Storage"
  }
}

resource "azurerm_subnet" "pe" {
  name                              = "snet-pe"
  resource_group_name               = var.resource_group_name
  virtual_network_name              = azurerm_virtual_network.this.name
  address_prefixes                  = [local.pe_cidr]
  default_outbound_access_enabled   = false
  private_endpoint_network_policies = "Enabled" # NSG rules apply to private endpoint traffic
}

# NSG on each subnet (azurerm 5.x: the subnet's network_security_group_id is read-only).
resource "azurerm_subnet_network_security_group_association" "vm" {
  subnet_id                 = azurerm_subnet.vm.id
  network_security_group_id = azurerm_network_security_group.vm.id
}

resource "azurerm_subnet_network_security_group_association" "aks" {
  subnet_id                 = azurerm_subnet.aks.id
  network_security_group_id = azurerm_network_security_group.aks.id
}

resource "azurerm_subnet_network_security_group_association" "pe" {
  subnet_id                 = azurerm_subnet.pe.id
  network_security_group_id = azurerm_network_security_group.pe.id
}
