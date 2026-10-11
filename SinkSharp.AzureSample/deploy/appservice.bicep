// ═══════════════════════════════════════════════════════════════════════════════
// Azure App Service Deployment — SinkSharp with Azure context enricher
// ═══════════════════════════════════════════════════════════════════════════════
//
// App Service sets WEBSITE_SITE_NAME, REGION_NAME, WEBSITE_RESOURCE_GROUP
// automatically — you get them for free, no yaml needed.
//
// WEBSITE_SLOT_NAME is set automatically when you use deployment slots:
//   production slot → "production"
//   staging slot    → "staging"
//
// This lets you filter dashboard logs by slot:
//   "show me only errors from staging" → filter by AzureSlot = staging
//
// az deployment group create \
//   --resource-group order-api-rg \
//   --template-file appservice.bicep \
//   --parameters appName=order-api
// ═══════════════════════════════════════════════════════════════════════════════

param appName string
param location string = resourceGroup().location
param sku string = 'B2'
param dockerImage string = 'yourregistry.azurecr.io/order-api:1.0.0'

// ── App Service Plan ──────────────────────────────────────────────────────────
resource plan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${appName}-plan'
  location: location
  kind: 'linux'
  sku: {
    name: sku
  }
  properties: {
    reserved: true    // required for Linux
  }
}

// ── Web App ───────────────────────────────────────────────────────────────────
resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: appName
  location: location
  properties: {
    serverFarmId: plan.id
    siteConfig: {
      linuxFxVersion: 'DOCKER|${dockerImage}'
      appSettings: [
        // ── SinkSharp doesn't need any special env vars on App Service ────────
        // Azure sets these automatically:
        //   WEBSITE_SITE_NAME      → AzureSiteName
        //   REGION_NAME            → AzureRegion
        //   WEBSITE_RESOURCE_GROUP → AzureResourceGroup
        //   WEBSITE_SLOT_NAME      → AzureSlot (set per-slot, see below)

        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        // Add your own app config here (connection strings, API keys, etc.)
        // SinkSharp Azure context is fully automatic — nothing to add.
      ]
    }
    httpsOnly: true
  }
}

// ── Deployment Slots ──────────────────────────────────────────────────────────
// Staging slot: WEBSITE_SLOT_NAME = "staging" (set automatically by Azure)
// In the dashboard you can filter AzureSlot = "staging" to see only staging logs
resource stagingSlot 'Microsoft.Web/sites/slots@2023-01-01' = {
  parent: webApp
  name: 'staging'
  location: location
  properties: {
    serverFarmId: plan.id
    siteConfig: {
      linuxFxVersion: 'DOCKER|${dockerImage}'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Staging'
        }
      ]
    }
  }
}

output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output dashboardUrl string = 'https://${webApp.properties.defaultHostName}/sinksharp/dashboard'
output stagingUrl string = 'https://${stagingSlot.properties.defaultHostName}'
