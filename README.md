# SinkSharp Samples

Complete working samples covering every use case. Each sample is self-contained and runnable with `dotnet run`.

## Sample index

| Sample | What it shows | Run → Open |
|---|---|---|
| **SinkSharp.Sample** | Console app — no DI, standalone dashboard | `dotnet run` → http://localhost:5341 |
| **SinkSharp.WebApiSample** | Web API with controllers + dashboard on same port | `dotnet run` → https://localhost:5001/sinksharp/dashboard |
| **SinkSharp.WorkerSample** | Worker Service — 2 background workers + dashboard | `dotnet run` → http://localhost:5341 |
| **SinkSharp.MinimalApiSample** | Minimal API — per-category levels, PII, health check | `dotnet run` → https://localhost:5001/sinksharp/dashboard |
| **SinkSharp.CustomSinkSample** | 4 custom sink patterns: InMemory, Alert, Conditional, Buffered | `dotnet run` |
| **SinkSharp.CustomEnricherSample** | 3 custom enrichers: Tenant, BuildInfo, Request + dashboard | `dotnet run` → https://localhost:5001/sinksharp/dashboard |
| **SinkSharp.MultiSinkSample** | Console + File + Dashboard + AuditFile simultaneously | `dotnet run` → https://localhost:5001/sinksharp/dashboard |
| **SinkSharp.HealthCheckSample** | Health check states, K8s probes, multi-check aggregation | `dotnet run` → https://localhost:5001/health/detail |
| **SinkSharp.ScopedLoggingSample** | Correlation through 3 services, parallel tasks, nested scopes | `dotnet run` → https://localhost:5001/sinksharp/dashboard |
| **SinkSharp.AzureSample** | Azure enricher — App Service, AKS, Container Apps | `dotnet run` → https://localhost:5001 |

---

## Quick start

```bash
# Run any sample
cd SinkSharp.WebApiSample
dotnet run

# Open the dashboard
# https://localhost:5001/sinksharp/dashboard
```

---

## What each sample teaches

### SinkSharp.Sample — Console App
- `SinkSharpBuilder` without DI
- `DashboardServer` on a standalone port
- `CorrelationContext.Use()` per batch
- PII masking in practice (`ops@example.com` → `ops@***.***`)
- `builder.GetQueue()` for stats

### SinkSharp.WebApiSample — Web API
- `AddSinkSharpWithHealth()` in DI
- Correlation middleware reading `X-Correlation-Id` header
- `app.UseSinkSharpDashboard("/sinksharp/dashboard")`
- `app.MapHealthChecks("/health")`
- All log levels demonstrated across endpoints

### SinkSharp.WorkerSample — Worker Service
- `AddSinkSharp()` without health check (optional)
- Two `BackgroundService` workers running concurrently
- `DashboardServer` with queue wired from host DI
- Per-worker correlation IDs

### SinkSharp.MinimalApiSample — Minimal API
- Per-category level overrides (silence Microsoft/System noise)
- PII masking on email and credit card in same log event
- Error logging with exception details
- `AddSinkSharpWithHealth()` + `app.MapHealthChecks()`

### SinkSharp.CustomSinkSample — Custom Sinks
- `InMemorySink` — capture events for unit test assertions
- `AlertSink` — fire only on Error/Critical (Slack/Teams/PagerDuty pattern)
- `ConditionalSink` — route events by category (wrapper pattern)
- `BufferedSink` — batch events before writing (database bulk insert pattern)
- How `ISink.EmitAsync()` always runs on the drainer thread

### SinkSharp.CustomEnricherSample — Custom Enrichers
- `TenantEnricher` — multi-tenant SaaS: `ILogger.BeginScope()` captures TenantId on caller thread
- `BuildInfoEnricher` — version/commit SHA stamped at startup (zero runtime cost)
- `RequestEnricher` — ASP.NET: RequestPath, Method, UserId via `IHttpContextAccessor`
- Nested scope override pattern

### SinkSharp.MultiSinkSample — Multiple Sinks
- Console + File + Dashboard + custom AuditFile all active simultaneously
- Pipeline fan-out: all sinks receive every event concurrently
- Audit category routing: `ILoggerFactory.CreateLogger("Audit.Orders")`
- Generating all log levels for dashboard demonstration

### SinkSharp.HealthCheckSample — Health Checks
- Three health states: Healthy / Degraded / Unhealthy
- `/health` — K8s readiness probe (200 or 503)
- `/health/live` — K8s liveness probe (always 200)
- `/health/detail` — full JSON report with all checks and queue stats
- Combining SinkSharp health check with DB and downstream service checks

### SinkSharp.ScopedLoggingSample — Correlation & Scopes
- Correlation ID flows through `OrderService → InventoryService → PaymentService`
- Zero passing — `ILogger<T>` in each service gets the ID automatically via AsyncLocal
- Parallel tasks — each gets its own correlation context
- Nested scopes — inner overrides outer, restores on Dispose
- How to use dashboard correlation filter to trace one request across services

### SinkSharp.AzureSample — Azure Deployment
- `WithAzureContext()` reads env vars once at startup
- App Service: AzureSiteName, AzureSlot, AzureRegion, AzureResourceGroup
- AKS / Container Apps: K8sPodName, K8sNamespace
- Local simulation with PowerShell env vars
- Deploy yaml and bicep files in `deploy/`

---

## Install

```bash
dotnet add package SinkSharp.Core                          # 1.0.2
dotnet add package SinkSharp.Sinks.Console                  # 1.0.1
dotnet add package SinkSharp.Sinks.File                     # 1.0.0
dotnet add package SinkSharp.Sinks.Dashboard --prerelease    # 1.0.1-preview
dotnet add package SinkSharp.Enrichers.Azure                # 1.0.0
```

---

## Links

- 🌐 [https://www.sinksharp.com](https://www.sinksharp.com)
- 📦 [NuGet](https://www.nuget.org/packages/SinkSharp.Core)
- 💻 [GitHub](https://github.com/SinkSharp/SinkSharp-Logging-Samples)
- 📧 [support@sinksharp.com](mailto:support@sinksharp.com)
