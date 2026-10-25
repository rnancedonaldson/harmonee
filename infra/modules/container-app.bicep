// A Harmonee HTTP service on Container Apps: user-assigned identity (for the registry, Key Vault and data), port 8080,
// health probes on the ServiceDefaults endpoints, and secrets resolved from Key Vault.

param name string
param location string
param tags object

@description('Short component name, used for the container name and the harmonee-component tag the deploy workflow looks up.')
param component string

param environmentResourceId string

@description('Full image reference, e.g. crharmoneedevcus01.azurecr.io/harmonee-api:<git-sha>.')
param image string

param registryServer string

@description('Resource ID and client ID of the app\'s user-assigned managed identity.')
param identity { resourceId: string, clientId: string }

@description('true: reachable from the internet. false: only from inside the Container Apps environment.')
param ingressExternal bool

param minReplicas int
param maxReplicas int
param cpu string = '0.5'
param memory string = '1Gi'

@description('Plain environment variables (no secrets).')
param env { name: string, value: string }[]

@description('Secrets read from Key Vault by the app identity, exposed as environment variables: { name, envName, keyVaultUrl }.')
param keyVaultSecrets { name: string, envName: string, keyVaultUrl: string }[] = []

var port = 8080

module app 'br/public:avm/res/app/container-app:0.23.0' = {
  name: 'ca-${component}'
  params: {
    name: name
    location: location
    tags: union(tags, { 'harmonee-component': component })
    environmentResourceId: environmentResourceId
    workloadProfileName: 'Consumption'
    managedIdentities: { userAssignedResourceIds: [identity.resourceId] }
    registries: [
      {
        server: registryServer
        identity: identity.resourceId
      }
    ]
    activeRevisionsMode: 'Single'
    ingressExternal: ingressExternal
    ingressTargetPort: port
    ingressTransport: 'auto'
    ingressAllowInsecure: false
    secrets: [
      for secret in keyVaultSecrets: {
        name: secret.name
        keyVaultUrl: secret.keyVaultUrl
        identity: identity.resourceId
      }
    ]
    scaleSettings: {
      minReplicas: minReplicas
      maxReplicas: maxReplicas
      rules: [
        {
          name: 'http'
          http: { metadata: { concurrentRequests: '50' } }
        }
      ]
    }
    containers: [
      {
        name: component
        image: image
        resources: { cpu: json(cpu), memory: memory }
        env: concat(
          env,
          [
            { name: 'AZURE_CLIENT_ID', value: identity.clientId }
            { name: 'ASPNETCORE_HTTP_PORTS', value: string(port) }
            // TLS ends at the Container Apps ingress; trust its X-Forwarded-* headers so redirects and cookies use https.
            { name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED', value: 'true' }
          ],
          map(keyVaultSecrets, secret => { name: secret.envName, secretRef: secret.name })
        )
        probes: [
          {
            type: 'Startup'
            httpGet: { path: '/health/live', port: port }
            periodSeconds: 5
            failureThreshold: 24
          }
          {
            type: 'Liveness'
            httpGet: { path: '/health/live', port: port }
            periodSeconds: 30
          }
          {
            type: 'Readiness'
            httpGet: { path: '/health/ready', port: port }
            periodSeconds: 15
            failureThreshold: 3
          }
        ]
      }
    ]
  }
}

output name string = app.outputs.name
output fqdn string = app.outputs.fqdn
