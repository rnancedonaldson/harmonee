// Prod: zone-redundant, private endpoints for data services, Redis token cache, always-on replicas.
// Identity values come from the GitHub "prod" environment variables (see docs/deployment.md).
using './main.bicep'

param environmentName = 'prod'
param location = 'centralus'
param locationAbbreviation = 'cus'
param owner = 'harmonee-maintainers'
param costCenter = 'harmonee'

param privateNetworking = true
param zoneRedundant = true
param vnetAddressPrefix = '10.41.0.0/16'

param logRetentionDays = 90
param logDailyQuotaGb = -1
param registrySku = 'Standard'
param storageSku = 'Standard_ZRS'
param appConfigSku = 'Standard'
param keyVaultPurgeProtection = true

param sqlMaxVCores = 2
param sqlMinVCores = '0.5'
param sqlAutoPauseDelay = -1
param sqlUseFreeLimit = false
param sqlBackupRedundancy = 'Zone'

param deployRedis = true
param redisSku = 'Balanced_B0'

param apiMinReplicas = 2
param apiMaxReplicas = 6
param webMinReplicas = 2
param webMaxReplicas = 6

param apiImage = readEnvironmentVariable('HARMONEE_API_IMAGE', '')
param webImage = readEnvironmentVariable('HARMONEE_WEB_IMAGE', '')
param migrationsImage = readEnvironmentVariable('HARMONEE_MIGRATIONS_IMAGE', '')

param entraInstance = readEnvironmentVariable('ENTRA_INSTANCE', '')
param entraTenantId = readEnvironmentVariable('ENTRA_TENANT_ID', '')
param apiClientId = readEnvironmentVariable('ENTRA_API_CLIENT_ID', '')
param webClientId = readEnvironmentVariable('ENTRA_WEB_CLIENT_ID', '')
param webClientCredential = readEnvironmentVariable('WEB_CLIENT_CREDENTIAL', 'ManagedIdentity')
