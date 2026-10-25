// Dev: smallest SKUs, scale to zero, public endpoints (Entra-only auth), no Redis.
// Identity values come from the GitHub "dev" environment variables (see docs/deployment.md).
using './main.bicep'

param environmentName = 'dev'
param location = 'centralus'
param locationAbbreviation = 'cus'
param owner = 'harmonee-maintainers'
param costCenter = 'harmonee'

param privateNetworking = false
param zoneRedundant = false
param vnetAddressPrefix = '10.40.0.0/16'

param logRetentionDays = 30
param logDailyQuotaGb = 1
param registrySku = 'Basic'
param storageSku = 'Standard_LRS'
param appConfigSku = 'Developer'
param keyVaultPurgeProtection = false

param sqlMaxVCores = 2
param sqlMinVCores = '0.5'
param sqlAutoPauseDelay = 60
param sqlUseFreeLimit = true
param sqlBackupRedundancy = 'Local'

param deployRedis = false

param apiMinReplicas = 0
param apiMaxReplicas = 2
param webMinReplicas = 0
param webMaxReplicas = 1

param apiImage = readEnvironmentVariable('HARMONEE_API_IMAGE', '')
param webImage = readEnvironmentVariable('HARMONEE_WEB_IMAGE', '')
param migrationsImage = readEnvironmentVariable('HARMONEE_MIGRATIONS_IMAGE', '')

param entraInstance = readEnvironmentVariable('ENTRA_INSTANCE', '')
param entraTenantId = readEnvironmentVariable('ENTRA_TENANT_ID', '')
param apiClientId = readEnvironmentVariable('ENTRA_API_CLIENT_ID', '')
param webClientId = readEnvironmentVariable('ENTRA_WEB_CLIENT_ID', '')
param webClientCredential = readEnvironmentVariable('WEB_CLIENT_CREDENTIAL', 'ManagedIdentity')
