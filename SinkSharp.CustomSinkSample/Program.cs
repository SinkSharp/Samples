// ═══════════════════════════════════════════════════════════════════════════════
// SinkSharp — Custom Sink Sample
// ═══════════════════════════════════════════════════════════════════════════════
// Demonstrates:
//   ✅ InMemorySink    — capture events for unit/integration test assertions
//   ✅ AlertSink       — fires only on Error/Critical (Slack/Teams/PagerDuty)
//   ✅ ConditionalSink — wraps another sink, filters by predicate
//   ✅ BufferedSink    — batches events before writing (DB bulk insert pattern)
//   ✅ ISink runs on background drainer thread — never blocks the caller
//
// Sink implementations are in Sinks.cs
// Run: dotnet run
// ═══════════════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.Logging;
using SinkSharp.Core;
using SinkSharp.Core.Enrichers;
using SinkSharp.Sinks.Console;

var memory   = new InMemorySink();
var alert    = new AlertSink();
var buffered = new BufferedSink(batchSize: 5);

// ConditionalSink: only forward Payment errors to the alert sink
var paymentAlert = new ConditionalSink(alert,
    e => e.Category.Contains("Payment", StringComparison.OrdinalIgnoreCase));

var builder = SinkSharpBuilder
    .Create()
    .MinimumLevel(LogLevel.Debug)
    .WithCorrelationId()
    .WithEnvironment("CustomSinkSample", "Development")
    .WithPiiMasking()
    .WriteTo(new ColoredConsoleSink())
    .WriteTo(memory)
    .WriteTo(buffered)
    .WriteTo(paymentAlert);

var logger = builder.BuildLogger("CustomSinkSample");
var queue  = builder.GetQueue();

Console.WriteLine("SinkSharp Custom Sink Sample");
Console.WriteLine("════════════════════════════\n");

// 1. InMemorySink — capture and assert
Console.WriteLine("1️⃣  InMemorySink — capture events for assertions");
using (CorrelationContext.Use("demo-001"))
{
    logger.Log(LogLevel.Information, "Order {OrderId} created", 1001);
    logger.Log(LogLevel.Debug,       "Stock reserved for {Sku}", "SKU-99");
    logger.Log(LogLevel.Warning,     "Stock low — {Remaining} units left", 3);
}
await Task.Delay(200);

Console.WriteLine($"   Captured: {memory.Events.Count} events");
foreach (var e in memory.Events)
    Console.WriteLine($"   [{e.Level}] {e.RenderedMessage}");

// 2. AlertSink via ConditionalSink — only Payment errors trigger alert
Console.WriteLine("\n2️⃣  AlertSink — fires only for Payment category errors");
using (CorrelationContext.Use("demo-002"))
{
    logger.Log(LogLevel.Information, "Order submitted");
    logger.Log(LogLevel.Error, "Payment gateway timeout for {Email}", "user@example.com");
    // PII masked → user@***.***
}

// 3. BufferedSink — flushes every 5 events
Console.WriteLine("\n3️⃣  BufferedSink — flushes every 5 events");
for (var i = 1; i <= 12; i++)
{
    using var _ = CorrelationContext.Use($"batch-{i:D3}");
    logger.Log(LogLevel.Information, "Processing item {Item}", i);
    await Task.Delay(30);
}

await Task.Delay(300);
await buffered.FlushAsync();

Console.WriteLine($"\n✅ InMemory events:   {memory.Events.Count}");
Console.WriteLine($"✅ Errors captured:    {memory.Events.Count(e => e.Level >= LogLevel.Error)}");
Console.WriteLine($"✅ Queue — Processed:  {queue.TotalProcessed}  Dropped: {queue.TotalDropped}");
