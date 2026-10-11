// ═══════════════════════════════════════════════════════════════════════════════
// SinkSharp — Health Check Sample
// ═══════════════════════════════════════════════════════════════════════════════
// Demonstrates:
//   ✅ AddSinkSharpWithHealth() — registers logging + health check in one call
//   ✅ Health states: Healthy / Degraded / Unhealthy and what triggers each
//   ✅ /health endpoint output — compatible with Kubernetes liveness/readiness probes
//   ✅ /health/detail — detailed report with SinkSharp queue stats
//   ✅ Simulating queue pressure to see Degraded state
//   ✅ How dropped events trigger Unhealthy
//   ✅ Combining with other health checks (DB, downstream service)
//
// Run:  dotnet run
// Test: curl https://localhost:5001/health
//       curl https://localhost:5001/health/detail
//       curl https://localhost:5001/simulate-pressure
//
// Kubernetes probe config (in your Deployment yaml):
//   readinessProbe:
//     httpGet:
//       path: /health
//       port: 8080
//     initialDelaySeconds: 5
//     periodSeconds: 10
//   livenessProbe:
//     httpGet:
//       path: /health/live
//       port: 8080
//     initialDelaySeconds: 15
//     periodSeconds: 20
// ═══════════════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using SinkSharp.Core;
using SinkSharp.Core.Enrichers;
using SinkSharp.Sinks.Console;
using SinkSharp.Sinks.File;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ── AddSinkSharpWithHealth — one call registers both ─────────────────────────
// Health states:
//   Healthy   — queue normal, zero drops
//   Degraded  — queue > 10,000 pending (drainer falling behind)
//   Unhealthy — events have been dropped (bounded channel full)
builder.Services.AddSinkSharpWithHealth(options => options
    .MinimumLevel(LogLevel.Debug)
    .MinimumLevelFor("Microsoft", LogLevel.Warning)
    .WithCorrelationId()
    .WithEnvironment(builder.Environment.ApplicationName, builder.Environment.EnvironmentName)
    .WriteTo(new ColoredConsoleSink())
    .WriteTo(new RollingFileSink("logs", "healthcheck-sample")));

// ── Additional health checks — combine with SinkSharp's ─────────────────────
builder.Services.AddHealthChecks()
    // Simulated DB health check
    .AddCheck("database", () =>
    {
        // In production: check connection pool, query latency
        return HealthCheckResult.Healthy("Database connection pool healthy — 45/100 connections used");
    })
    // Simulated downstream API check
    .AddCheck("payment-gateway", () =>
    {
        // In production: ping /health of downstream service
        return HealthCheckResult.Healthy("Payment gateway responding in 42ms");
    });

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    var id = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()
               ?? Guid.NewGuid().ToString("N")[..12];
    using var _ = CorrelationContext.Use(id);
    ctx.Response.Headers["X-Correlation-Id"] = id;
    await next();
});

// ── Health endpoints ──────────────────────────────────────────────────────────

// Simple — returns 200 OK or 503 Unhealthy (for K8s probes)
app.MapHealthChecks("/health");

// Liveness — only checks if app is running (not downstream deps)
app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false  // no checks = always alive if process runs
});

// Readiness — detailed JSON report with all check results
app.MapHealthChecks("/health/detail", new()
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var response = new
        {
            status    = report.Status.ToString(),
            duration  = report.TotalDuration.TotalMilliseconds,
            checks    = report.Entries.Select(e => new
            {
                name        = e.Key,
                status      = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration_ms = e.Value.Duration.TotalMilliseconds,
                tags        = e.Value.Tags,
            })
        };
        await ctx.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
    }
});

// ── Demo endpoints ────────────────────────────────────────────────────────────
app.MapGet("/", () => new
{
    sample   = "Health Check Sample",
    endpoints = new
    {
        health        = "/health          → 200 Healthy / 503 Unhealthy",
        health_live   = "/health/live     → liveness probe (always 200 if process runs)",
        health_detail = "/health/detail   → full JSON report with all checks",
        simulate      = "/simulate-pressure → generates rapid logs to show queue stats",
    },
    kubernetes = new
    {
        readiness = "/health",
        liveness  = "/health/live",
    }
});

app.MapGet("/work", (ILogger<Program> log) =>
{
    log.LogInformation("Processing work item — queue stays healthy");
    return Results.Ok(new { status = "ok" });
});

// Rapid log generation — watch /health/detail for queue stats changing
app.MapGet("/simulate-pressure", async (ILogger<Program> log) =>
{
    log.LogInformation("Starting pressure simulation — 500 log events");
    for (var i = 0; i < 500; i++)
    {
        log.LogInformation("Pressure event {N} — order {OrderId} processed", i,
            Random.Shared.Next(1000, 9999));
    }
    await Task.Delay(100);
    return Results.Ok(new
    {
        message  = "500 events enqueued — check /health/detail for queue stats",
        nextStep = "curl https://localhost:5001/health/detail"
    });
});

app.Run();
