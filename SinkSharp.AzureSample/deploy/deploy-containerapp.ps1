# ═══════════════════════════════════════════════════════════════════════════════
# Deploy SinkSharp.AzureSample to Azure Container Apps
# Prerequisites:
#   1. Install Azure CLI + Container Apps extension
#      az extension add --name containerapp --upgrade
#   2. Install Docker Desktop
#   3. az login
# ═══════════════════════════════════════════════════════════════════════════════

# ── Config — change these ─────────────────────────────────────────────────────
$ResourceGroup  = "sinksharp-rg"
$Location       = "eastus"
$Environment    = "sinksharp-env"
$AppName        = "sinksharp-azure-sample"
$RegistryName   = "sinksharpregistry"    # must be globally unique, lowercase

# ── 1. Login ──────────────────────────────────────────────────────────────────
az login
az extension add --name containerapp --upgrade

# ── 2. Create resource group ──────────────────────────────────────────────────
az group create --name $ResourceGroup --location $Location

# ── 3. Create Azure Container Registry ───────────────────────────────────────
az acr create `
    --name $RegistryName `
    --resource-group $ResourceGroup `
    --sku Basic `
    --admin-enabled true

$RegistryServer = az acr show --name $RegistryName --query loginServer -o tsv

# ── 4. Build and push Docker image ───────────────────────────────────────────
# Build and push directly to ACR (no local Docker needed)
az acr build `
    --registry $RegistryName `
    --image sinksharp-azure-sample:latest `
    --file Dockerfile .

# ── 5. Create Container Apps environment ──────────────────────────────────────
az containerapp env create `
    --name $Environment `
    --resource-group $ResourceGroup `
    --location $Location

# ── 6. Get ACR credentials ────────────────────────────────────────────────────
$AcrUser     = az acr credential show --name $RegistryName --query username -o tsv
$AcrPassword = az acr credential show --name $RegistryName --query "passwords[0].value" -o tsv

# ── 7. Create Container App ───────────────────────────────────────────────────
az containerapp create `
    --name $AppName `
    --resource-group $ResourceGroup `
    --environment $Environment `
    --image "$RegistryServer/sinksharp-azure-sample:latest" `
    --registry-server $RegistryServer `
    --registry-username $AcrUser `
    --registry-password $AcrPassword `
    --target-port 8080 `
    --ingress external `
    --min-replicas 1 `
    --max-replicas 5 `
    --env-vars `
        "ASPNETCORE_URLS=http://+:8080" `
        "ASPNETCORE_ENVIRONMENT=Production" `
        "POD_NAME=secretref:container-app-replica-name" `
        "WEBSITE_RESOURCE_GROUP=$ResourceGroup"

$Fqdn = az containerapp show `
    --name $AppName `
    --resource-group $ResourceGroup `
    --query properties.configuration.ingress.fqdn -o tsv

Write-Host ""
Write-Host "✅ Deployed to Container Apps!" -ForegroundColor Green
Write-Host "   App:       https://$Fqdn"
Write-Host "   Dashboard: https://$Fqdn/sinksharp/dashboard"
Write-Host "   Simulate:  https://$Fqdn/simulate"
