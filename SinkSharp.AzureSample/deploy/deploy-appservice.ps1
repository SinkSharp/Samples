# ═══════════════════════════════════════════════════════════════════════════════
# Deploy SinkSharp.AzureSample to Azure App Service
# Prerequisites:
#   1. Install Azure CLI: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli
#   2. az login
#   3. Run this script from the SinkSharp.AzureSample folder
# ═══════════════════════════════════════════════════════════════════════════════

# ── Config — change these ─────────────────────────────────────────────────────
$ResourceGroup = "sinksharp-rg"
$AppName       = "sinksharp-azure-sample"   # must be globally unique
$Location      = "eastus"
$Plan          = "sinksharp-plan"

# ── 1. Login (skip if already logged in) ─────────────────────────────────────
az login

# ── 2. Create resource group ──────────────────────────────────────────────────
az group create --name $ResourceGroup --location $Location

# ── 3. Create App Service plan (B1 = ~$13/month, free tier: F1) ───────────────
az appservice plan create `
    --name $Plan `
    --resource-group $ResourceGroup `
    --sku B1 `
    --is-linux

# ── 4. Create Web App ─────────────────────────────────────────────────────────
az webapp create `
    --name $AppName `
    --resource-group $ResourceGroup `
    --plan $Plan `
    --runtime "DOTNETCORE:9.0"

# ── 5. Publish and deploy ─────────────────────────────────────────────────────
dotnet publish -c Release -o ./publish

Compress-Archive -Path ./publish/* -DestinationPath ./publish.zip -Force

az webapp deploy `
    --name $AppName `
    --resource-group $ResourceGroup `
    --src-path ./publish.zip `
    --type zip

# ── 6. Open URLs ──────────────────────────────────────────────────────────────
$BaseUrl = "https://$AppName.azurewebsites.net"

Write-Host ""
Write-Host "✅ Deployed! Open these URLs:" -ForegroundColor Green
Write-Host "   App:       $BaseUrl"
Write-Host "   Dashboard: $BaseUrl/sinksharp/dashboard"
Write-Host "   Health:    $BaseUrl/health"
Write-Host "   Simulate:  $BaseUrl/simulate"
Write-Host ""
Write-Host "Azure context in every log event (set automatically by App Service):"
Write-Host "   AzureSiteName      = $AppName"
Write-Host "   AzureRegion        = East US"
Write-Host "   AzureResourceGroup = $ResourceGroup"
Write-Host ""
Write-Host "Note: Dashboard shows logs from THIS instance only."
Write-Host "      For multi-instance: add Application Insights or check logs/"

# ── 7. Clean up publish folder ────────────────────────────────────────────────
Remove-Item -Recurse -Force ./publish
Remove-Item ./publish.zip
