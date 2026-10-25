// Harmonee on Azure: one resource group per environment.
// Container Apps (web: public, API: internal) + a Container Apps Job for migrations, Azure SQL (Entra-only),
// Key Vault, App Configuration, Storage (Data Protection keys), Container Registry, Log Analytics + Application
// Insights, and optionally Azure Managed Redis (token cache) and private endpoints. See docs/deployment.md.
//
// Started from `azd infra gen` on the Aspire AppHost, then rewritten with Azure Verified Modules (ADR 0008).

targetScope = 'resourceGroup'

// ---------------------------------------------------------------------------
// Environment and naming
// ---------------------------------------------------------------------------

@allowed(['dev', 'test', 'prod'])
param environmentName string

param location string = resourceGroup().location

@description('CAF region abbreviation used in names, e.g. cus for centralus.')
@maxLength(4)
param locationAbbreviation string

@description('Instance suffix for globally unique names (Key Vault, storage, registry, SQL, App Configuration, Redis). Change it if a name is taken or still soft-deleted.')
@maxLength(2)
param instance string = '01'

param owner string
param costCenter string

@allowed(['public', 'internal', 'confidential', 'restricted'])
param dataClassification string = 'confidential'

var workload = 'harmonee'
var suffix = '${workload}-${environmentName}-${locationAbbreviation}'
var compactSuffix = '${workload}${environmentName}${locationAbbreviation}${instance}'

var names = {
  logAnalytics: 'log-${suffix}'
  appInsights: 'appi-${suffix}'
  vnet: 'vnet-${suffix}'
  containerAppsEnvironment: 'cae-${suffix}'
  containerAppsInfrastructure: 'rg-${suffix}-cae'
  registry: 'cr${compactSuffix}'
  keyVault: 'kv-${suffix}-${instance}'
  storage: 'st${compactSuffix}'
  appConfig: 'appcs-${suffix}-${instance}'
  sqlServer: 'sql-${suffix}-${instance}'
  sqlDatabase: 'sqldb-${workload}'
  redis: 'amr-${suffix}-${instance}'
  apiIdentity: 'id-${workload}-api-${environmentName}-${locationAbbreviation}'
  webIdentity: 'id-${workload}-web-${environmentName}-${locationAbbreviation}'
  migrationsIdentity: 'id-${workload}-migrations-${environmentName}-${locationAbbreviation}'
  apiApp: 'ca-${workload}-api-${environmentName}-${locationAbbreviation}'
  webApp: 'ca-${workload}-web-${environmentName}-${locationAbbreviation}'
  migrationsJob: 'caj-${workload}-migrate-${environmentName}-${locationAbbreviation}'
}

var tags = {
  workload: workload
  environment: environmentName
  owner: owner
  costCenter: costCenter
  dataClassification: dataClassification
  createdBy: 'bicep'
}

// ---------------------------------------------------------------------------
// Sizing and resilience (set per environment in main.<env>.bicepparam)
// ---------------------------------------------------------------------------

@description('Private endpoints for SQL, Key Vault, Storage, App Configuration and Redis, with public access disabled.')
param privateNetworking bool

@description('Zone redundancy for the Container Apps environment, SQL and storage.')
param zoneRedundant bool

param vnetAddressPrefix string = '10.40.0.0/16'

param logRetentionDays int = 30

@description('Log Analytics daily ingestion cap in GB; -1 for none.')
param logDailyQuotaGb int = -1

@allowed(['Basic', 'Standard', 'Premium'])
param registrySku string = 'Basic'

@allowed(['Standard_LRS', 'Standard_ZRS', 'Standard_GZRS'])
param storageSku string = 'Standard_LRS'

@allowed(['Developer', 'Standard', 'Premium'])
param appConfigSku string = 'Developer'

param keyVaultPurgeProtection bool = true

@description('Azure SQL serverless General Purpose vCores (max).')
param sqlMaxVCores int = 1

@description('Minimum vCores while running, e.g. 0.5.')
param sqlMinVCores string = '0.5'

@description('Minutes idle before auto-pause; -1 disables auto-pause.')
param sqlAutoPauseDelay int = 60

@description('Use the Azure SQL free offer (one database per subscription). It pauses when the monthly free allowance runs out.')
param sqlUseFreeLimit bool = false

@allowed(['Local', 'Zone', 'Geo', 'GeoZone'])
param sqlBackupRedundancy string = 'Local'

@description('Deploy Azure Managed Redis for the web app\'s token cache. Without it, tokens are cached in memory per replica.')
param deployRedis bool = false

param redisSku string = 'Balanced_B0'

param apiMinReplicas int = 0
param apiMaxReplicas int = 3
param webMinReplicas int = 0

@description('Keep at 1 without Redis: the in-memory token cache is per replica.')
param webMaxReplicas int = 1

// ---------------------------------------------------------------------------
// Application (supplied by the deploy workflow)
// ---------------------------------------------------------------------------

@description('API image; empty skips the API container app (first deployment, before any image exists).')
param apiImage string = ''

@description('Web image; empty skips the web container app.')
param webImage string = ''

@description('Migration service image; empty skips the migrations job.')
param migrationsImage string = ''

@description('Entra External ID authority host, e.g. https://harmonee.ciamlogin.com/.')
@minLength(1)
param entraInstance string

@minLength(1)
param entraTenantId string

@description('Application (client) ID of the "Harmonee API" app registration.')
@minLength(1)
param apiClientId string

@description('Application (client) ID of the "Harmonee Web" app registration.')
@minLength(1)
param webClientId string

@description('How the web app proves its identity to Entra External ID. ManagedIdentity: federated credential (requires the app registration in the same tenant as the managed identity). KeyVaultCertificate: a certificate in this Key Vault.')
@allowed(['ManagedIdentity', 'KeyVaultCertificate'])
param webClientCredential string = 'ManagedIdentity'

@description('Key Vault certificate name, when webClientCredential is KeyVaultCertificate.')
param webCertificateName string = 'harmonee-web-entra'

// Built-in role definition IDs (https://learn.microsoft.com/azure/role-based-access-control/built-in-roles)
var roles = {
  acrPull: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  appConfigDataReader: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '516239f1-63e1-4d78-a4de-a74fb236a071')
  keyVaultSecretsUser: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
  keyVaultCertificateUser: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'db79e9a7-68ee-4b58-9aeb-b90e7c24fcba')
  keyVaultCryptoServiceEncryptionUser: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'e147488a-f6f5-4113-8e2d-b22465e65bf6')
  storageBlobDataContributor: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
}

var publicNetworkAccess = privateNetworking ? 'Disabled' : 'Enabled'
var dataProtectionContainer = 'dataprotection'
var dataProtectionKey = 'dataprotection'
var appInsightsSecretName = 'appinsights-connection-string'

// ---------------------------------------------------------------------------
// Identities: one per workload, so each gets only the access it needs
// ---------------------------------------------------------------------------

module apiIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.6.0' = {
  name: 'id-api'
  params: { name: names.apiIdentity, location: location, tags: tags }
}

module webIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.6.0' = {
  name: 'id-web'
  params: { name: names.webIdentity, location: location, tags: tags }
}

// Entra administrator of the SQL server: applies migrations and creates the API's database user.
module migrationsIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.6.0' = {
  name: 'id-migrations'
  params: { name: names.migrationsIdentity, location: location, tags: tags }
}

var servicePrincipals = [apiIdentity.outputs.principalId, webIdentity.outputs.principalId, migrationsIdentity.outputs.principalId]

func grant(principalIds string[], roleDefinitionId string) object[] =>
  map(principalIds, principalId => {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionIdOrName: roleDefinitionId
  })

// ---------------------------------------------------------------------------
// Monitoring
// ---------------------------------------------------------------------------

module logAnalytics 'br/public:avm/res/operational-insights/workspace:0.16.1' = {
  name: 'log'
  params: {
    name: names.logAnalytics
    location: location
    tags: tags
    dataRetention: logRetentionDays
    dailyQuotaGb: string(logDailyQuotaGb)
  }
}

module appInsights 'br/public:avm/res/insights/component:0.8.0' = {
  name: 'appi'
  params: {
    name: names.appInsights
    location: location
    tags: tags
    workspaceResourceId: logAnalytics.outputs.resourceId
    applicationType: 'web'
    kind: 'web'
    retentionInDays: logRetentionDays
  }
}

var diagnostics = [{ workspaceResourceId: logAnalytics.outputs.resourceId }]

// ---------------------------------------------------------------------------
// Network
// ---------------------------------------------------------------------------

module network 'modules/network.bicep' = {
  name: 'network'
  params: {
    name: names.vnet
    location: location
    tags: tags
    addressPrefix: vnetAddressPrefix
    privateNetworking: privateNetworking
  }
}

func privateEndpoint(service string, subnetResourceId string, zoneResourceId string) object => {
  service: service
  subnetResourceId: subnetResourceId
  privateDnsZoneGroup: { privateDnsZoneGroupConfigs: [{ privateDnsZoneResourceId: zoneResourceId }] }
}

// ---------------------------------------------------------------------------
// Container registry: the pipeline pushes, the apps pull with their identities
// ---------------------------------------------------------------------------

module registry 'br/public:avm/res/container-registry/registry:0.13.1' = {
  name: 'cr'
  params: {
    name: names.registry
    location: location
    tags: tags
    acrSku: registrySku
    acrAdminUserEnabled: false
    // GitHub-hosted runners push over the internet; pulls use managed identities.
    publicNetworkAccess: 'Enabled'
    zoneRedundancy: zoneRedundant ? 'Enabled' : 'Disabled'
    diagnosticSettings: diagnostics
    roleAssignments: grant(servicePrincipals, roles.acrPull)
  }
}

// ---------------------------------------------------------------------------
// Key Vault: Data Protection key-wrapping key, and secrets the apps read by reference
// ---------------------------------------------------------------------------

module keyVault 'br/public:avm/res/key-vault/vault:0.14.2' = {
  name: 'kv'
  params: {
    name: names.keyVault
    location: location
    tags: tags
    sku: 'standard'
    enableRbacAuthorization: true
    enablePurgeProtection: keyVaultPurgeProtection
    softDeleteRetentionInDays: keyVaultPurgeProtection ? 90 : 7
    publicNetworkAccess: publicNetworkAccess
    networkAcls: { bypass: 'AzureServices', defaultAction: privateNetworking ? 'Deny' : 'Allow' }
    privateEndpoints: privateNetworking
      ? [privateEndpoint('vault', network.outputs.privateEndpointSubnetResourceId, network.outputs.privateDnsZoneResourceIds.keyVault)]
      : []
    diagnosticSettings: diagnostics
    keys: [
      {
        name: dataProtectionKey
        kty: 'RSA'
        keySize: 2048
        roleAssignments: [
          {
            principalId: webIdentity.outputs.principalId
            principalType: 'ServicePrincipal'
            roleDefinitionIdOrName: roles.keyVaultCryptoServiceEncryptionUser
          }
        ]
      }
    ]
    secrets: [
      {
        name: appInsightsSecretName
        value: appInsights.outputs.connectionString
        roleAssignments: grant(servicePrincipals, roles.keyVaultSecretsUser)
      }
    ]
    // The certificate itself is created outside IaC (docs/deployment.md); the web app reads it with its identity.
    roleAssignments: webClientCredential == 'KeyVaultCertificate'
      ? [
          {
            principalId: webIdentity.outputs.principalId
            principalType: 'ServicePrincipal'
            roleDefinitionIdOrName: roles.keyVaultCertificateUser
          }
          {
            principalId: webIdentity.outputs.principalId
            principalType: 'ServicePrincipal'
            roleDefinitionIdOrName: roles.keyVaultSecretsUser
          }
        ]
      : []
  }
}

// ---------------------------------------------------------------------------
// Storage: Data Protection key ring (keys wrapped with the Key Vault key)
// ---------------------------------------------------------------------------

module storage 'br/public:avm/res/storage/storage-account:0.33.1' = {
  name: 'st'
  params: {
    name: names.storage
    location: location
    tags: tags
    kind: 'StorageV2'
    skuName: storageSku
    allowSharedKeyAccess: false
    allowBlobPublicAccess: false
    defaultToOAuthAuthentication: true
    publicNetworkAccess: publicNetworkAccess
    networkAcls: { bypass: 'AzureServices', defaultAction: privateNetworking ? 'Deny' : 'Allow' }
    privateEndpoints: privateNetworking
      ? [privateEndpoint('blob', network.outputs.privateEndpointSubnetResourceId, network.outputs.privateDnsZoneResourceIds.blob)]
      : []
    blobServices: {
      deleteRetentionPolicyEnabled: true
      deleteRetentionPolicyDays: 7
      containers: [
        {
          name: dataProtectionContainer
          publicAccess: 'None'
          roleAssignments: [
            {
              principalId: webIdentity.outputs.principalId
              principalType: 'ServicePrincipal'
              roleDefinitionIdOrName: roles.storageBlobDataContributor
            }
          ]
        }
      ]
    }
  }
}

// ---------------------------------------------------------------------------
// App Configuration: non-secret settings and feature flags (local auth off; apps read with their identities)
// ---------------------------------------------------------------------------

module appConfig 'br/public:avm/res/app-configuration/configuration-store:0.10.1' = {
  name: 'appcs'
  params: {
    name: names.appConfig
    location: location
    tags: tags
    sku: appConfigSku
    disableLocalAuth: true
    publicNetworkAccess: publicNetworkAccess
    privateEndpoints: privateNetworking
      ? [privateEndpoint('configurationStores', network.outputs.privateEndpointSubnetResourceId, network.outputs.privateDnsZoneResourceIds.appConfig)]
      : []
    diagnosticSettings: diagnostics
    roleAssignments: grant([apiIdentity.outputs.principalId, webIdentity.outputs.principalId], roles.appConfigDataReader)
  }
}

// ---------------------------------------------------------------------------
// Azure SQL: Entra-only authentication; one database, a schema per module
// ---------------------------------------------------------------------------

module sql 'br/public:avm/res/sql/server:0.22.1' = {
  name: 'sql'
  params: {
    name: names.sqlServer
    location: location
    tags: tags
    minimalTlsVersion: '1.2'
    publicNetworkAccess: publicNetworkAccess
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: names.migrationsIdentity
      sid: migrationsIdentity.outputs.principalId
      principalType: 'Application'
      tenantId: tenant().tenantId
    }
    // Without private endpoints, allow Azure services only (the Container Apps environment). Entra-only auth still applies.
    firewallRules: privateNetworking
      ? []
      : [
          {
            name: 'AllowAllWindowsAzureIps'
            startIpAddress: '0.0.0.0'
            endIpAddress: '0.0.0.0'
          }
        ]
    privateEndpoints: privateNetworking
      ? [privateEndpoint('sqlServer', network.outputs.privateEndpointSubnetResourceId, network.outputs.privateDnsZoneResourceIds.sql)]
      : []
    databases: [
      {
        name: names.sqlDatabase
        availabilityZone: -1
        sku: {
          name: 'GP_S_Gen5'
          tier: 'GeneralPurpose'
          family: 'Gen5'
          capacity: sqlMaxVCores
        }
        minCapacity: sqlMinVCores
        autoPauseDelay: sqlAutoPauseDelay
        zoneRedundant: zoneRedundant
        useFreeLimit: sqlUseFreeLimit
        freeLimitExhaustionBehavior: sqlUseFreeLimit ? 'AutoPause' : null
        requestedBackupStorageRedundancy: sqlBackupRedundancy
        maxSizeBytes: 34359738368
        diagnosticSettings: diagnostics
      }
    ]
  }
}

// ---------------------------------------------------------------------------
// Azure Managed Redis (optional): distributed token cache for the web app, Entra auth only
// ---------------------------------------------------------------------------

module redis 'br/public:avm/res/cache/redis-enterprise:0.5.1' = if (deployRedis) {
  name: 'amr'
  params: {
    name: names.redis
    location: location
    tags: tags
    skuName: redisSku
    highAvailability: zoneRedundant ? 'Enabled' : 'Disabled'
    publicNetworkAccess: publicNetworkAccess
    privateEndpoints: privateNetworking
      ? [privateEndpoint('redisEnterprise', network.outputs.privateEndpointSubnetResourceId, network.outputs.privateDnsZoneResourceIds.redis)]
      : []
    database: {
      accessKeysAuthentication: 'Disabled'
      clusteringPolicy: 'OSSCluster'
      accessPolicyAssignments: [
        {
          userObjectId: webIdentity.outputs.principalId
          accessPolicyName: 'default'
        }
      ]
    }
  }
}

// ---------------------------------------------------------------------------
// Container Apps environment
// ---------------------------------------------------------------------------

module containerAppsEnvironment 'br/public:avm/res/app/managed-environment:0.16.0' = {
  name: 'cae'
  params: {
    name: names.containerAppsEnvironment
    location: location
    tags: tags
    infrastructureSubnetResourceId: network.outputs.containerAppsSubnetResourceId
    infrastructureResourceGroupName: names.containerAppsInfrastructure
    internal: false
    // The web app is public. Put Front Door + WAF in front before go-live (docs/roadmap.md).
    publicNetworkAccess: 'Enabled'
    zoneRedundant: zoneRedundant
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsWorkspaceResourceId: logAnalytics.outputs.resourceId
    }
  }
}

// ---------------------------------------------------------------------------
// Workloads
// ---------------------------------------------------------------------------

var sqlConnectionPrefix = 'Server=tcp:${sql.outputs.fullyQualifiedDomainName},1433;Database=${names.sqlDatabase};Encrypt=True;TrustServerCertificate=False;Authentication=Active Directory Managed Identity'
var appInsightsSecret = {
  name: appInsightsSecretName
  envName: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
  keyVaultUrl: '${keyVault.outputs.uri}secrets/${appInsightsSecretName}'
}
var entraEnv = [
  { name: 'Authentication__Mode', value: 'EntraExternalId' }
  { name: 'AzureAd__Instance', value: entraInstance }
  { name: 'AzureAd__TenantId', value: entraTenantId }
]
var apiBaseAddress = 'https://${names.apiApp}.internal.${containerAppsEnvironment.outputs.defaultDomain}'

var webCredentialEnv = webClientCredential == 'ManagedIdentity'
  ? [
      { name: 'AzureAd__ClientCredentials__0__SourceType', value: 'SignedAssertionFromManagedIdentity' }
      { name: 'AzureAd__ClientCredentials__0__ManagedIdentityClientId', value: webIdentity.outputs.clientId }
    ]
  : [
      { name: 'AzureAd__ClientCredentials__0__SourceType', value: 'KeyVault' }
      { name: 'AzureAd__ClientCredentials__0__KeyVaultUrl', value: keyVault.outputs.uri }
      { name: 'AzureAd__ClientCredentials__0__KeyVaultCertificateName', value: webCertificateName }
    ]

var redisEnv = deployRedis
  ? [{ name: 'ConnectionStrings__cache', value: '${redis.?outputs.hostName ?? ''}:${redis.?outputs.port ?? 10000},ssl=true,abortConnect=false' }]
  : []

module migrationsJob 'br/public:avm/res/app/job:0.7.2' = if (!empty(migrationsImage)) {
  name: 'caj-migrations'
  params: {
    name: names.migrationsJob
    location: location
    tags: union(tags, { 'harmonee-component': 'migrations' })
    environmentResourceId: containerAppsEnvironment.outputs.resourceId
    workloadProfileName: 'Consumption'
    triggerType: 'Manual'
    manualTriggerConfig: { parallelism: 1, replicaCompletionCount: 1 }
    replicaTimeout: 1800
    replicaRetryLimit: 1
    managedIdentities: { userAssignedResourceIds: [migrationsIdentity.outputs.resourceId] }
    registries: [{ server: registry.outputs.loginServer, identity: migrationsIdentity.outputs.resourceId }]
    secrets: [
      {
        name: appInsightsSecret.name
        keyVaultUrl: appInsightsSecret.keyVaultUrl
        identity: migrationsIdentity.outputs.resourceId
      }
    ]
    containers: [
      {
        name: 'migrations'
        image: migrationsImage
        resources: { cpu: '0.5', memory: '1Gi' }
        env: [
          { name: 'AZURE_CLIENT_ID', value: migrationsIdentity.outputs.clientId }
          { name: 'OTEL_SERVICE_NAME', value: 'harmonee-migrations' }
          { name: 'ConnectionStrings__harmonee-db', value: '${sqlConnectionPrefix};User Id=${migrationsIdentity.outputs.clientId}' }
          { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', secretRef: appInsightsSecret.name }
          // The API's identity gets read/write data access only; schema changes stay with the migration identity.
          { name: 'Database__Users__0__Name', value: names.apiIdentity }
          { name: 'Database__Users__0__ClientId', value: apiIdentity.outputs.clientId }
          { name: 'Database__Users__0__Roles__0', value: 'db_datareader' }
          { name: 'Database__Users__0__Roles__1', value: 'db_datawriter' }
        ]
      }
    ]
  }
}

module api 'modules/container-app.bicep' = if (!empty(apiImage)) {
  name: 'api'
  params: {
    name: names.apiApp
    location: location
    tags: tags
    component: 'api'
    environmentResourceId: containerAppsEnvironment.outputs.resourceId
    image: apiImage
    registryServer: registry.outputs.loginServer
    identity: { resourceId: apiIdentity.outputs.resourceId, clientId: apiIdentity.outputs.clientId }
    // Only the web app (inside the environment) calls the API today. Expose it through Front Door for mobile clients.
    ingressExternal: false
    minReplicas: apiMinReplicas
    maxReplicas: apiMaxReplicas
    keyVaultSecrets: [appInsightsSecret]
    env: concat(entraEnv, [
      { name: 'OTEL_SERVICE_NAME', value: 'harmonee-api' }
      { name: 'AzureAd__ClientId', value: apiClientId }
      { name: 'ConnectionStrings__harmonee-db', value: '${sqlConnectionPrefix};User Id=${apiIdentity.outputs.clientId}' }
    ])
  }
}

module web 'modules/container-app.bicep' = if (!empty(webImage)) {
  name: 'web'
  params: {
    name: names.webApp
    location: location
    tags: tags
    component: 'web'
    environmentResourceId: containerAppsEnvironment.outputs.resourceId
    image: webImage
    registryServer: registry.outputs.loginServer
    identity: { resourceId: webIdentity.outputs.resourceId, clientId: webIdentity.outputs.clientId }
    ingressExternal: true
    minReplicas: webMinReplicas
    maxReplicas: webMaxReplicas
    keyVaultSecrets: [appInsightsSecret]
    env: concat(entraEnv, webCredentialEnv, redisEnv, [
      { name: 'OTEL_SERVICE_NAME', value: 'harmonee-web' }
      { name: 'AzureAd__ClientId', value: webClientId }
      { name: 'HarmoneeApi__BaseAddress', value: apiBaseAddress }
      { name: 'HarmoneeApi__Scopes__0', value: 'api://${apiClientId}/Harmonee.Access' }
      { name: 'DataProtection__BlobUri', value: '${storage.outputs.primaryBlobEndpoint}${dataProtectionContainer}/keys.xml' }
      { name: 'DataProtection__KeyVaultKeyUri', value: '${keyVault.outputs.uri}keys/${dataProtectionKey}' }
    ])
  }
}

// ---------------------------------------------------------------------------
// Outputs (non-secret): used by the deploy workflow and docs/deployment.md
// ---------------------------------------------------------------------------

output registryName string = registry.outputs.name
output registryLoginServer string = registry.outputs.loginServer
output containerAppsEnvironmentName string = containerAppsEnvironment.outputs.name
output apiAppName string = names.apiApp
output webAppName string = names.webApp
output migrationsJobName string = names.migrationsJob
output webUrl string = 'https://${names.webApp}.${containerAppsEnvironment.outputs.defaultDomain}'
output webIdentityPrincipalId string = webIdentity.outputs.principalId
output webIdentityClientId string = webIdentity.outputs.clientId
output keyVaultName string = keyVault.outputs.name
output sqlServerName string = sql.outputs.name
output appConfigEndpoint string = appConfig.outputs.endpoint
