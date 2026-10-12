// ═══════════════════════════════════════════════════════════════════════════════
// SinkSharp — Multi-Sink Sample
// ═══════════════════════════════════════════════════════════════════════════════
// Demonstrates:
//   ✅ Routing events to multiple sinks simultaneously
//   ✅ Console + File + Dashboard all active at once
//   ✅ Per-sink behaviour: File flushes errors immediately, Console colours by level
//   ✅ Fan-out pipeline: all sinks run concurrently on the drainer thread
//   ✅ Per-category routing: audit category goes to a separate log file
//   ✅ Audit sink pattern: critical business events to a separate file
//   ✅ Queue stats: see all events processed across all sinks
//
// Run:  dotnet run
// Open: https://localhost:5001/sinksharp/dashboard
//
// Log files created:
//   logs/multisink-YYYY-MM-DD.jsonl    ← main log
//   logs/audit-YYYY-MM-DD.jsonl        ← audit events only
// ═══════════════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.Logging;
using SinkSharp.Core;
using SinkSharp.Core.Enrichers;
using SinkSharp.Sinks.Console;
using SinkSharp.Sinks.Dashboard;
using SinkSharp.Sinks.File;

// ── Top-level program statements ──────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);

DashboardSink dashSink = null!;

builder.Services.AddSinkSharpWithHealth(options => options
    .MinimumLevel(LogLevel.Debug)
    .MinimumLevelFor("Microsoft", LogLevel.Warning)
    .MinimumLevelFor("System.Net", LogLevel.Error)
    .WithCorrelationId()
    .WithEnvironment(builder.Environment.ApplicationName, builder.Environment.EnvironmentName)
    .WithPiiMasking()
    .WithDashboard(out dashSink)
    // All three sinks active simultaneously — pipeline fans out concurrently
    .WriteTo(new ColoredConsoleSink(showFullCategory: true))
    .WriteTo(new RollingFileSink("logs", "multisink"))
    .WriteTo(new AuditFileSink()));

builder.Services.AddSinkSharpDashboard(dashSink);
    // ── Multi-pod option — uncomment when running 2+ Kubernetes replicas ─────────
    // Requires: dotnet add package Microsoft.AspNetCore.SignalR.StackExchangeRedis
    // builder.Services.AddSinkSharpDashboardRedis(dashSink, signalR =>
    //     signalR.AddStackExchangeRedis(
    //         builder.Configuration["Redis:ConnectionString"]!,
    //         options => options.Configuration.ChannelPrefix =
    //             StackExchange.Redis.RedisChannel.Literal("MyApp.SinkSharp")));

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    var id = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()
               ?? Guid.NewGuid().ToString("N")[..12];
    using var _ = CorrelationContext.Use(id);
    ctx.Response.Headers["X-Correlation-Id"] = id;
    await next();
});

app.UseSinkSharpDashboard("/sinksharp/dashboard");
app.MapHealthChecks("/health");

app.MapGet("/", () => new
{
    sample = "Multi-Sink Sample",
    sinks  = new[] { "ColoredConsoleSink", "RollingFileSink (main)", "AuditFileSink" },
    files  = new[] { "logs/multisink-YYYY-MM-DD.jsonl", "logs/audit-YYYY-MM-DD.jsonl" },
});

// Regular endpoint — logs go to Console + File + Dashboard
app.MapGet("/order/{id:int}", (int id, ILogger<Program> log) =>
{
    log.LogInformation("Order {OrderId} viewed", id);
    return Results.Ok(new { Id = id, Status = "Shipped" });
});

// Audit endpoint — "Audit" category routes to AuditFileSink AND main sinks
app.MapPost("/order/{id:int}/approve", (int id, ILoggerFactory loggerFactory) =>
{
    var audit = loggerFactory.CreateLogger("Audit.Orders");
    audit.LogInformation("Order {OrderId} approved — compliance event recorded", id);
    return Results.Ok(new { Id = id, Status = "Approved", Audited = true });
});

// Generates all log levels for dashboard demonstration
app.MapGet("/demo", async (ILogger<Program> log) =>
{
    log.LogDebug("Debug message — visible in dashboard Debug filter");
    log.LogInformation("Info message — appears in All and Info filters");
    log.LogWarning("Warning — stock threshold approaching {Threshold}", 10);
    try { throw new TimeoutException("External service timeout"); }
    catch (Exception ex)
    {
        log.LogError(ex, "Error on demo endpoint — {Service}", "InventoryService");
    }
    await Task.Delay(100);
    return Results.Ok(new { message = "Demo complete — check dashboard and logs/" });
});

app.Run();
