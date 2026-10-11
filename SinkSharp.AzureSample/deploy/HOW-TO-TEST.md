# How to Deploy and Test SinkSharp.AzureSample

## What this sample shows
Every log event automatically gets Azure properties stamped:
- App Service → `AzureSiteName`, `AzureSlot`, `AzureRegion`, `AzureResourceGroup`
- AKS / Container Apps → `K8sPodName`, `K8sNamespace`
- All visible in the dashboard at `/sinksharp/dashboard`

---

## Option 1 — Run Locally (no Azure account needed)

```powershell
cd samples\SinkSharp.AzureSample
dotnet run
```

Open `https://localhost:{port}/sinksharp/dashboard`
Hit endpoints — logs show with empty Azure properties.

### Simulate Azure locally

```powershell
# Simulate App Service
$env:WEBSITE_SITE_NAME      = "sinksharp-api"
$env:REGION_NAME            = "East US"
$env:WEBSITE_SLOT_NAME      = "staging"
$env:WEBSITE_RESOURCE_GROUP = "sinksharp-rg"
dotnet run

# Simulate AKS pod
$env:POD_NAME      = "sinksharp-api-6d4b9f-xk2pq"
$env:POD_NAMESPACE = "production"
dotnet run
```

Hit `https://localhost:{port}/` to see detected Azure context.
Hit `https://localhost:{port}/simulate` to generate logs with PII masking.
Open dashboard — every event shows full Azure context in the properties pane.

---

## Option 2 — Azure App Service (easiest Azure option)

### Prerequisites
```powershell
# Install Azure CLI
winget install Microsoft.AzureCLI
az login
```

### Deploy
```powershell
cd samples\SinkSharp.AzureSample
.\deploy\deploy-appservice.ps1
```

### What you get
```
https://sinksharp-azure-sample.azurewebsites.net/                    ← index
https://sinksharp-azure-sample.azurewebsites.net/sinksharp/dashboard ← dashboard
https://sinksharp-azure-sample.azurewebsites.net/health              ← health
https://sinksharp-azure-sample.azurewebsites.net/simulate            ← generate logs
https://sinksharp-azure-sample.azurewebsites.net/orders              ← list orders
https://sinksharp-azure-sample.azurewebsites.net/error               ← trigger error log
```

### What you see in dashboard
Every log event has:
```
AzureSiteName      = sinksharp-azure-sample    ← set by App Service automatically
AzureRegion        = East US                    ← set by App Service automatically
AzureResourceGroup = sinksharp-rg              ← set by App Service automatically
CorrelationId      = a1b2c3d4e5f6              ← generated per request
Environment        = Production
```

### Clean up (avoid charges)
```powershell
az group delete --name sinksharp-rg --yes
```

---

## Option 3 — Azure Container Apps

### Prerequisites
```powershell
az extension add --name containerapp --upgrade
# Install Docker Desktop: https://www.docker.com/products/docker-desktop
```

### Deploy
```powershell
cd samples\SinkSharp.AzureSample
.\deploy\deploy-containerapp.ps1
```

### Scale to multiple replicas and check dashboard
```powershell
az containerapp update `
    --name sinksharp-azure-sample `
    --resource-group sinksharp-rg `
    --min-replicas 3

# Now hit the app multiple times
# Each request may go to a different replica
# In the dashboard, K8sPodName shows WHICH replica handled each request
# Filter by K8sPodName to isolate one replica's logs
```

---

## Option 4 — AKS (most advanced)

```powershell
# Apply the deployment yaml (from deploy/ folder)
kubectl apply -f deploy\aks-deployment.yaml

# Port forward to access dashboard from local browser
kubectl port-forward `
    deployment/sinksharp-azure-sample `
    8080:8080 `
    -n production

# Open: http://localhost:8080/sinksharp/dashboard
# Each pod shows its own K8sPodName in every log event
```

---

## What to check in the dashboard after deploying

1. Open `/sinksharp/dashboard`
2. Hit `/simulate` a few times
3. Click any log event → Properties pane shows:
   - `AzureSiteName` — which app
   - `AzureRegion` — East US / West Europe etc.
   - `AzureSlot` — production / staging
   - `K8sPodName` — which exact pod (AKS/Container Apps)
4. Hit `/error` — see error with stack trace in dashboard
5. Check PII masking — hit `/simulate`, find "Payment confirmed" log,
   email shows as `customer@***` not `customer@example.com`
6. Correlation ID — click the ID link to filter all logs for that request
