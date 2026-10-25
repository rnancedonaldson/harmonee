// Spoke virtual network for one environment: a delegated subnet for the Container Apps environment and, when private
// networking is on, a subnet for private endpoints plus the private DNS zones they register in.

@description('Name of the virtual network.')
param name string

param location string
param tags object

@description('Address space for the virtual network, e.g. 10.40.0.0/16. The first /23 hosts Container Apps; the third /24 hosts private endpoints.')
param addressPrefix string

@description('Create the private endpoint subnet and private DNS zones.')
param privateNetworking bool

var containerAppsSubnetName = 'snet-cae'
var privateEndpointSubnetName = 'snet-pe'

var privateDnsZoneNames = {
  sql: 'privatelink${environment().suffixes.sqlServerHostname}'
  keyVault: 'privatelink.vaultcore.azure.net'
  blob: 'privatelink.blob.${environment().suffixes.storage}'
  appConfig: 'privatelink.azconfig.io'
  redis: 'privatelink.redis.azure.net'
}

module vnet 'br/public:avm/res/network/virtual-network:0.10.2' = {
  name: 'vnet'
  params: {
    name: name
    location: location
    tags: tags
    addressPrefixes: [addressPrefix]
    subnets: concat(
      [
        {
          // Workload profiles environments need at least a /27; a /23 leaves room to grow.
          name: containerAppsSubnetName
          addressPrefix: cidrSubnet(addressPrefix, 23, 0)
          delegation: 'Microsoft.App/environments'
        }
      ],
      privateNetworking
        ? [
            {
              name: privateEndpointSubnetName
              addressPrefix: cidrSubnet(addressPrefix, 24, 2)
            }
          ]
        : []
    )
  }
}

module zones 'br/public:avm/res/network/private-dns-zone:0.8.1' = [
  for zone in items(privateDnsZoneNames): if (privateNetworking) {
    name: 'pdns-${zone.key}'
    params: {
      name: zone.value
      location: 'global'
      tags: tags
      virtualNetworkLinks: [
        {
          virtualNetworkResourceId: vnet.outputs.resourceId
          registrationEnabled: false
        }
      ]
    }
  }
]

output resourceId string = vnet.outputs.resourceId
output containerAppsSubnetResourceId string = vnet.outputs.subnetResourceIds[0]
output privateEndpointSubnetResourceId string = privateNetworking ? vnet.outputs.subnetResourceIds[1] : ''

// items() sorts by key, so look zones up by name rather than by index.
output privateDnsZoneResourceIds object = privateNetworking
  ? {
      appConfig: resourceId('Microsoft.Network/privateDnsZones', privateDnsZoneNames.appConfig)
      blob: resourceId('Microsoft.Network/privateDnsZones', privateDnsZoneNames.blob)
      keyVault: resourceId('Microsoft.Network/privateDnsZones', privateDnsZoneNames.keyVault)
      redis: resourceId('Microsoft.Network/privateDnsZones', privateDnsZoneNames.redis)
      sql: resourceId('Microsoft.Network/privateDnsZones', privateDnsZoneNames.sql)
    }
  : {}
