#!/usr/bin/env bash
# Check with az (not Terraform) that the lab resources exist with the intended settings.
#   scripts/azure_verify.sh    -> prints one line per check, "OK"/"FAIL"; exit 1 if any FAIL
# Prints names and settings only: no secret values, no connection strings.
set -uo pipefail
RG=rg-ticketdesk-lab
REGION=${REGION:-swedencentral}   # lab region (westus until 2026-11-08)
fails=0
chk() { # chk <label> <actual> <expected-regex>
  if [[ "$2" =~ $3 ]]; then printf 'OK    %-46s %s\n' "$1" "$2"; else printf 'FAIL  %-46s got [%s], want /%s/\n' "$1" "$2" "$3"; fails=$((fails+1)); fi
}
q() { az "$@" -o tsv 2>/dev/null | tr '\t\n' '  ' | sed 's/ *$//'; }

echo "# azure_verify $(date -u +%FT%TZ)"
chk "resource group exists" "$(az group exists -n $RG)" '^true$'
chk "RG location / tags" "$(q group show -n $RG --query '[location, tags.project, tags.owner]')" "^$REGION ticketdesk agent-"
none() { local v; v=$(cat | tr '\n' ' ' | sed 's/ *$//'); echo "${v:-none}"; }
# <server>/master is the SQL system database Azure creates with every logical server (free, cannot be tagged)
chk "every resource has the 4 tags (SQL master exempt)" "$(az resource list -g $RG --query "[?tags.ttl==null || tags.project!='ticketdesk' || tags.owner==null || tags.est_usd_per_hour==null].name" -o tsv | grep -v '/master$' | none)" '^none$'
chk "all resources in $REGION (DNS zones global)" "$(az resource list -g $RG --query "[?location!='$REGION' && location!='global'].name" -o tsv | none)" '^none$'

vnet=$(q network vnet list -g $RG --query '[0].name')
chk "VNet" "$(q network vnet show -g $RG -n "$vnet" --query '[name, addressSpace.addressPrefixes[0]]')" "^vnet-tdlab-$REGION 10\\.42\\.0\\.0/16\$"
for sn in snet-vm snet-pe snet-aks; do
  chk "subnet $sn: prefix, NSG, no default outbound" \
    "$(q network vnet subnet show -g $RG --vnet-name "$vnet" -n $sn --query '[addressPrefixes[0] || addressPrefix, networkSecurityGroup.id, defaultOutboundAccess]' | sed 's|/subscriptions/.*/networkSecurityGroups/||')" \
    '^10\.42\.[0-9]+\.0/2[0-9] nsg-tdlab-[a-z]+ false$'
done
for n in vm pe aks; do
  chk "nsg-tdlab-$n DenyInternetInbound" "$(q network nsg rule show -g $RG --nsg-name nsg-tdlab-$n -n DenyInternetInbound --query '[access, priority, sourceAddressPrefix]')" '^Deny 4000 Internet$'
done
# the home IP itself is not printed
chk "nsg-tdlab-vm: only 443, from one /32" "$(q network nsg rule show -g $RG --nsg-name nsg-tdlab-vm -n AllowHttpsFromHome --query '[destinationPortRange, length(destinationPortRanges), sourceAddressPrefixes[0]]' | sed -E 's#[0-9.]+/32$#<home-ip>/32#')" '^443 0 <home-ip>/32$'
chk "nsg-tdlab-vm: no other inbound Allow" "$(az network nsg show -g $RG -n nsg-tdlab-vm --query "securityRules[?direction=='Inbound' && access=='Allow' && name!='AllowHttpsFromHome'].name" -o tsv | none)" '^none$'
chk "snet-vm service endpoints" "$(q network vnet subnet show -g $RG --vnet-name "$vnet" -n snet-vm --query 'sort(serviceEndpoints[].service)')" '^Microsoft\.KeyVault Microsoft\.Storage$'
chk "no inbound Allow from Internet/* anywhere" "$(az network nsg list -g $RG --query "[].securityRules[?direction=='Inbound' && access=='Allow' && (sourceAddressPrefix=='*' || sourceAddressPrefix=='Internet' || sourceAddressPrefix=='0.0.0.0/0')].name[]" -o tsv | none)" '^none$'

kv=$(q keyvault list -g $RG --query '[0].name')
chk "Key Vault rbac / soft delete / purge prot." "$(q keyvault show -n "$kv" --query '[properties.enableRbacAuthorization, properties.enableSoftDelete, properties.softDeleteRetentionInDays, properties.enablePurgeProtection]')" '^true true 7 (None|false)$'
chk "Key Vault firewall default action" "$(q keyvault show -n "$kv" --query 'properties.networkAcls.defaultAction')" '^Deny$'
chk "Key Vault secrets present (names only)" "$(q keyvault secret list --vault-name "$kv" --query '[].name')" 'appinsights-connection-string'
if az network private-endpoint show -g $RG -n "pe-$kv" >/dev/null 2>&1; then
  chk "KV private endpoint connection" "$(q network private-endpoint show -g $RG -n "pe-$kv" --query 'privateLinkServiceConnections[0].privateLinkServiceConnectionState.status')" '^Approved$'
  chk "privatelink.vaultcore.azure.net A record" "$(q network private-dns record-set a show -g $RG -z privatelink.vaultcore.azure.net -n "$kv" --query 'aRecords[0].ipv4Address')" '^10\.42\.2\.[0-9]+$'
  chk "KV DNS zone linked to the VNet" "$(q network private-dns link vnet list -g $RG -z privatelink.vaultcore.azure.net --query '[0].virtualNetworkLinkState')" '^Completed$'
fi

# ---- lift stage (only when the VM exists)
vm=$(q vm list -g $RG --query '[0].name')
if [[ -n "$vm" ]]; then
  chk "VM size / image / patch mode" "$(q vm show -g $RG -n "$vm" --query '[hardwareProfile.vmSize, storageProfile.imageReference.sku, osProfile.windowsConfiguration.patchSettings.patchMode, osProfile.windowsConfiguration.enableAutomaticUpdates]')" '^Standard_B2as_v2 2025-datacenter-core-smalldisk-g2 Manual false$'
  chk "VM OS disk Standard_LRS 30-32 GB" "$(q disk show -g $RG -n "osdisk-${vm#vm-}" --query '[sku.name, diskSizeGB]')" '^Standard_LRS 3[0-2]$'
  chk "VM system-assigned identity, Trusted Launch" "$(q vm show -g $RG -n "$vm" --query '[identity.type, securityProfile.securityType, securityProfile.uefiSettings.secureBootEnabled]')" '^SystemAssigned TrustedLaunch true$'
  pid=$(q vm show -g $RG -n "$vm" --query identity.principalId)
  chk "VM identity roles (names, scopes)" "$(az role assignment list --assignee "$pid" --all --query '[].[roleDefinitionName, scope]' -o tsv | sed -E 's#\t/subscriptions/.*/(vaults|containers)/#:\1/#' | sort | tr '\t\n' ': ' | sed 's/ $//')" '^Key Vault Secrets User:vaults/kv-tdlab-[0-9a-f]+ Storage Blob Data Contributor:containers/backups Storage Blob Data Contributor:containers/logs Storage Blob Data Reader:containers/artifacts$'
  chk "public IP DNS label" "$(q network public-ip show -g $RG -n "pip-${vm#vm-}" --query 'dnsSettings.fqdn')" "^tdlab-legacy-[0-9a-f]{6}\.$REGION\.cloudapp\.azure\.com\$"
  chk "VM NIC has no NSG of its own (subnet NSG rules)" "$(q network nic show -g $RG -n "nic-${vm#vm-}" --query 'networkSecurityGroup.id' | none)" '^none$'
  st=$(q storage account list -g $RG --query '[0].name')
  chk "storage: LRS, TLS1.2, shared keys off, no public blobs" "$(q storage account show -g $RG -n "$st" --query '[sku.name, minimumTlsVersion, allowSharedKeyAccess, allowBlobPublicAccess]')" '^Standard_LRS TLS1_2 false false$'
  chk "storage firewall: Deny, home IP + snet-vm" "$(q storage account show -g $RG -n "$st" --query '[networkRuleSet.defaultAction, length(networkRuleSet.ipRules), length(networkRuleSet.virtualNetworkRules)]')" '^Deny 1 1$'
  chk "storage containers private" "$(az storage container list --account-name "$st" --auth-mode login --query '[].[name, properties.publicAccess]' -o tsv 2>/dev/null | sort | tr '\t\n' ': ' | sed 's/ $//')" '^artifacts:(None)? backups:(None)? logs:(None)?$'
  chk "Key Vault allows snet-vm" "$(q keyvault show -n "$kv" --query 'properties.networkAcls.virtualNetworkRules[].id' | sed 's#.*/subnets/##')" '^snet-vm$'
  chk "Key Vault lift secrets + certificate (names only)" "$(q keyvault secret list --vault-name "$kv" --query "sort([?starts_with(name, 'td-')].name)") / $(q keyvault certificate list --vault-name "$kv" --query '[].name')" '^td-app-admin-password td-sql-sa-password / td-iis-legacy$'
fi

chk "Log Analytics daily cap / retention" "$(q monitor log-analytics workspace show -g $RG -n log-tdlab-$REGION --query '[workspaceCapping.dailyQuotaGb, retentionInDays, sku.name]')" '^0\.1 30 PerGB2018$'
chk "App Insights is workspace-based" "$(q monitor app-insights component show -g $RG --app appi-tdlab-$REGION --query 'workspaceResourceId' | sed 's|.*/||')" "^log-tdlab-$REGION\$"
chk "action group has an e-mail receiver" "$(q monitor action-group show -g $RG -n ag-tdlab-email --query 'length(emailReceivers)')" '^1$'

sql=$(q sql server list -g $RG --query '[0].name')
if [[ -n "$sql" ]]; then
  chk "SQL public access / Entra-only" "$(q sql server show -g $RG -n "$sql" --query '[publicNetworkAccess, administrators.azureAdOnlyAuthentication]')" '^Disabled true$'
  chk "SQL DB free offer" "$(q sql db show -g $RG -s "$sql" -n sqldb-ticketdesk --query '[useFreeLimit, freeLimitExhaustionBehavior, currentServiceObjectiveName]')" '^true AutoPause GP_S_Gen5_2$'
  chk "SQL private endpoint connection" "$(q network private-endpoint show -g $RG -n pe-sql-tdlab --query 'privateLinkServiceConnections[0].privateLinkServiceConnectionState.status')" '^Approved$'
  chk "SQL DNS zone linked to the VNet" "$(q network private-dns link vnet list -g $RG -z privatelink.database.windows.net --query '[0].virtualNetworkLinkState')" '^Completed$'
  chk "SQL server minimal TLS" "$(q sql server show -g $RG -n "$sql" --query minimalTlsVersion)" '^1\.2$'
  chk "privatelink.database.windows.net A record" "$(q network private-dns record-set a show -g $RG -z privatelink.database.windows.net -n "$sql" --query 'aRecords[0].ipv4Address')" '^10\.42\.2\.[0-9]+$'
else
  echo "SKIP  SQL server (not in this configuration)"
fi

app=$(q ad app list --display-name ticketdesk-lab-web --query '[0].appId')
chk "Entra app ticketdesk-lab-web" "${app:+exists}" '^exists$'
chk "Entra app roles" "$(az ad app show --id "$app" --query 'appRoles[].value' -o tsv | sort | tr '\n' ' ' | sed 's/ $//')" '^TdAdministrators TdHelpDeskUsers TdInternalUsers TdPendingUsers$'
sp=$(q ad sp show --id "$app" --query id)
chk "service principal assignment required" "$(q ad sp show --id "$app" --query appRoleAssignmentRequired)" '^true$'
me=$(q ad signed-in-user show --query id)
chk "signed-in user has TdAdministrators" "$(az rest --method get --url "https://graph.microsoft.com/v1.0/servicePrincipals/$sp/appRoleAssignedTo" --query "value[?principalId=='$me'].appRoleId" -o tsv)" '^5b0f7a1e-2c4d-4e6f-8a9b-0c1d2e3f4a01$'

echo "# $fails failed"
exit $((fails > 0))
