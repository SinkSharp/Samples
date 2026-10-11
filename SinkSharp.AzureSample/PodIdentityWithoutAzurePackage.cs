// ═══════════════════════════════════════════════════════════════════════════════
// HOW TO GET POD IDENTITY WITHOUT SinkSharp.Enrichers.Azure
//
// If you only have SinkSharp.Core, you can still stamp pod name on every event
// using a custom enricher. Three approaches — pick the one that fits:
// ═══════════════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.Logging;
using SinkSharp.Core;
using SinkSharp.Core.Abstractions;
using SinkSharp.Core.Enrichers;
using SinkSharp.Sinks.Console;
using SinkSharp.Sinks.File;

// ── APPROACH 1: WithEnvironment() — simplest, no extra enricher ───────────────
//
// WithEnvironment(appName, env) already stamps MachineName on every event.
// In Kubernetes, MachineName = pod name automatically (it's the hostname).
// No yaml changes needed. No extra package needed.
//
// builder.Services.AddSinkSharp(options => options
//     .WithEnvironment("OrderApi", "Production")  // ← MachineName = pod name in K8s
//     .WriteTo(new ColoredConsoleSink()));
//
// Result: every log event gets:
//   AppName:     OrderApi
//   Environment: Production
//   MachineName: order-api-6d4b9f-xk2pq   ← pod name, free!
//
// This is the easiest option. Works on any Kubernetes cluster.
// The downside: it's called "MachineName" not "PodName" in the dashboard.

// ── APPROACH 2: Custom enricher — explicit pod name ───────────────────────────
//
// If you want a property explicitly called "PodName" instead of "MachineName":

public sealed class KubernetesPodEnricher : IEnricher
{
    // Read at startup — zero cost per log event
    private readonly string? _podName      = Environment.GetEnvironmentVariable("POD_NAME")
                                          ?? Environment.MachineName;
    private readonly string? _podNamespace = Environment.GetEnvironmentVariable("POD_NAMESPACE");
    private readonly string? _nodeName     = Environment.GetEnvironmentVariable("NODE_NAME");
    private readonly string? _region       = Environment.GetEnvironmentVariable("REGION_NAME");

    public void Enrich(LogEvent logEvent, IPropertyBag bag)
    {
        if (_podName      is not null) bag.AddOrUpdate("PodName",      _podName);
        if (_podNamespace is not null) bag.AddOrUpdate("PodNamespace", _podNamespace);
        if (_nodeName     is not null) bag.AddOrUpdate("NodeName",     _nodeName);
        if (_region       is not null) bag.AddOrUpdate("Region",       _region);
    }
}

// Usage:
// builder.Services.AddSinkSharp(options => options
//     .WithCorrelationId()
//     .WithEnvironment("OrderApi", "Production")
//     .WithEnricher(new KubernetesPodEnricher())   // ← custom enricher
//     .WriteTo(new ColoredConsoleSink()));
//
// Add to your Deployment.yaml:
//   env:
//     - name: POD_NAME
//       valueFrom:
//         fieldRef:
//           fieldPath: metadata.name
//     - name: POD_NAMESPACE
//       valueFrom:
//         fieldRef:
//           fieldPath: metadata.namespace
//     - name: NODE_NAME
//       valueFrom:
//         fieldRef:
//           fieldPath: spec.nodeName    ← bonus: which node the pod is on

// ── APPROACH 3: MinimumLevelFor per pod — advanced ────────────────────────────
//
// You can also use pod identity to adjust log levels per deployment:
//
// var podName = Environment.GetEnvironmentVariable("POD_NAME") ?? "local";
// bool isCanary = podName.Contains("canary");
//
// builder.Services.AddSinkSharp(options =>
// {
//     options.MinimumLevel(LogLevel.Information);
//     if (isCanary)
//         options.MinimumLevel(LogLevel.Debug);   // ← verbose logs on canary pods only
//     options
//         .WithEnvironment("OrderApi", "Production")
//         .WithEnricher(new KubernetesPodEnricher())
//         .WriteTo(new ColoredConsoleSink());
// });

// ── COMPARISON ────────────────────────────────────────────────────────────────
//
// Approach       Package needed          Property name    Dashboard filter
// ─────────────────────────────────────────────────────────────────────────────
// WithEnvironment  SinkSharp.Core        MachineName      ✅ works
// Custom enricher  SinkSharp.Core        PodName          ✅ works, explicit name
// WithAzureContext SinkSharp.Enrichers.Azure PodName + Region + Slot + RG  ✅ everything

// See Program.cs for the runnable sample.
