output "vnet_id" {
  value = azurerm_virtual_network.this.id
}

output "vnet_name" {
  value = azurerm_virtual_network.this.name
}

output "aks_subnet_cidr" {
  value = local.aks_cidr
}

output "subnet_ids" {
  value = {
    vm  = azurerm_subnet.vm.id
    aks = azurerm_subnet.aks.id
    pe  = azurerm_subnet.pe.id
  }
  # Consumers (endpoints, VM NICs, AKS nodes) land in subnets that already have their NSG.
  depends_on = [
    azurerm_subnet_network_security_group_association.vm,
    azurerm_subnet_network_security_group_association.aks,
    azurerm_subnet_network_security_group_association.pe,
  ]
}

output "est_usd_per_hour" {
  value = 0
}
