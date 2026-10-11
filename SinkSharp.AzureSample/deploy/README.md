# SinkSharp Azure Deployment Guide

## How it works across all Azure services

```
Your app starts
      ↓
AzureContextEnricher reads env vars ONCE
      ↓
Values cached in memory (zero cost after startup)
      ↓
Every log event gets Azure properties stamped automatically
      ↓
Dashboard shows which pod / slot / region each log came from
```

## Why this matters — multi-pod debugging

Without Azure context:
```
23:42:01  ERR  Order processing failed
23:42:02  ERR  Order processing failed
23:42:01  ERR  Order processing failed
```
Which pod failed? Unknown.

With SinkSharp Azure context:
```
23:42:01  ERR  Order processing failed  [pod: order-api-6d4b9f-xk2pq] [region: East US]
23:42:02  ERR  Order processing failed  [pod: order-api-6d4b9f-ab3mn] [region: East US]
23:42:01  ERR  Order processing failed  [pod: order-api-6d4b9f-zr9kl] [region: East US]
```
Filter dashboard by pod name → isolate the problem instantly.

---

## Azure Kubernetes Service (AKS)

**File:** `aks-deployment.yaml`

AKS supports the Kubernetes Downward API — pod name and namespace are
injected automatically from pod metadata. No hardcoding needed.

```yaml
env:
  - name: POD_NAME
    valueFrom:
      fieldRef:
        fieldPath: metadata.name        # ← Kubernetes fills this
  - name: POD_NAMESPACE
    valueFrom:
      fieldRef:
        fieldPath: metadata.namespace   # ← Kubernetes fills this
```

**What you get in logs:**
| Property | Example value |
|---|---|
| `K8sPodName` | `order-api-6d4b9f-xk2pq` |
| `K8sNamespace` | `production` |
| `AzureRegion` | `East US` (if you set REGION_NAME) |

**Access dashboard per-pod:**
```bash
kubectl port-forward pod/order-api-6d4b9f-xk2pq 8080:8080 -n production
# Open: http://localhost:8080/sinksharp/dashboard
```

---

## Azure App Service

**File:** `appservice.bicep`

App Service sets all env vars automatically — nothing to configure.

| Env Var (set by Azure) | SinkSharp property |
|---|---|
| `WEBSITE_SITE_NAME` | `AzureSiteName` |
| `WEBSITE_SLOT_NAME` | `AzureSlot` |
| `REGION_NAME` | `AzureRegion` |
| `WEBSITE_RESOURCE_GROUP` | `AzureResourceGroup` |

**Deployment slots** — filter dashboard by slot:
- Production logs: `AzureSlot = production`
- Staging logs:    `AzureSlot = staging`

This lets you verify staging behaviour before swapping to production.

---

## Azure Container Apps

**File:** `container-apps.yaml`

Container Apps runs on Kubernetes internally but doesn't support the
Downward API directly. Use the built-in env var instead:

```yaml
env:
  - name: POD_NAME
    value: $(CONTAINER_APP_REPLICA_NAME)   # set automatically by Container Apps
  - name: POD_NAMESPACE
    value: $(CONTAINER_APP_NAME)
```

---

## Azure Functions

No yaml or bicep needed — Functions inherits App Service env vars:
- `WEBSITE_SITE_NAME` → `AzureSiteName`
- `REGION_NAME` → `AzureRegion`
- `WEBSITE_RESOURCE_GROUP` → `AzureResourceGroup`

```csharp
// In your Function App startup (isolated worker model):
builder.Services.AddSinkSharp(options => options
    .WithEnvironment(appName, env)
    .WithAzureContext()     // ← reads App Service vars automatically
    .WriteTo(new ColoredConsoleSink()));
```

---

## Local simulation (PowerShell)

```powershell
# Simulate App Service
$env:WEBSITE_SITE_NAME      = "order-api"
$env:REGION_NAME            = "East US"
$env:WEBSITE_SLOT_NAME      = "staging"
$env:WEBSITE_RESOURCE_GROUP = "order-api-rg"

# Simulate AKS pod
$env:POD_NAME      = "order-api-6d4b9f-xk2pq"
$env:POD_NAMESPACE = "production"

dotnet run --project ../
```

---

## Program.cs — same code for all environments

```csharp
builder.Services.AddSinkSharp(options => options
    .MinimumLevel(LogLevel.Information)
    .WithCorrelationId()
    .WithEnvironment(appName, env)
    .WithAzureContext()        // ← reads whatever env vars are present
    .WriteTo(new ColoredConsoleSink())
    .WriteTo(new RollingFileSink("logs", appName)));
```

`WithAzureContext()` reads whatever env vars are present and ignores missing ones.
The same binary runs locally, on App Service, AKS, Container Apps, and Functions
without any code change.
