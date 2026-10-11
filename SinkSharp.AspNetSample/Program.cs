using Microsoft.Extensions.Logging;
using SinkSharp.Core;
using SinkSharp.Core.Enrichers;
using SinkSharp.Sinks.Console;
using SinkSharp.Sinks.File;

var builder = WebApplication.CreateBuilder(args);
var appName = builder.Environment.ApplicationName;
var env     = builder.Environment.EnvironmentName;

// ── Register SinkSharp with health check ─────────────────────────────────────
builder.Services.AddSinkSharpWithHealth(options => options
    .MinimumLevel(LogLevel.Information)
    .MinimumLevelFor("Microsoft",     LogLevel.Warning)  // silence ASP.NET noise
    .MinimumLevelFor("System.Net",    LogLevel.Error)
    .WithCorrelationId()
    .WithEnvironment(appName, env)
    .WithPiiMasking()
    .WriteTo(new ColoredConsoleSink())
    .WriteTo(new RollingFileSink("logs", appName)));

builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Correlation ID middleware ──────────────────────────────────────────────────
app.Use(async (ctx, next) =>
{
    var id = ctx.Request.Headers["X-Correlation-Id"]
                        .FirstOrDefault()
               ?? Guid.NewGuid().ToString("N")[..12];

    using var _ = CorrelationContext.Use(id);
    ctx.Response.Headers["X-Correlation-Id"] = id;
    await next();
});

app.MapGet("/", (ILogger<Program> logger) =>
{
    logger.LogInformation("Hello endpoint called");
    return "SinkSharp is running!";
});

app.MapGet("/error", (ILogger<Program> logger) =>
{
    logger.LogError("Simulated error for demonstration");
    return Results.Problem("Something went wrong");
});

// GET /health → Healthy / Degraded / Unhealthy
app.MapHealthChecks("/health");

app.Run();
