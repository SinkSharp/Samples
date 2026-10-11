using Microsoft.Extensions.Logging;
using SinkSharp.Core;
using SinkSharp.Core.Abstractions;

// ── Custom Sink Implementations ───────────────────────────────────────────────
// All sinks run on the background drainer thread — never block the caller.

// InMemorySink: capture events for unit/integration test assertions.
public sealed class InMemorySink : ISink
{
    private readonly List<LogEvent> _events = [];
    private readonly Lock _lock = new();

    public string Name => "InMemory";

    public IReadOnlyList<LogEvent> Events
    {
        get { lock (_lock) return _events.AsReadOnly(); }
    }

    public ValueTask EmitAsync(LogEvent e, CancellationToken ct = default)
    {
        lock (_lock) _events.Add(e);
        return ValueTask.CompletedTask;
    }

    public void Clear() { lock (_lock) _events.Clear(); }
    public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
}

// AlertSink: fires only on Error/Critical — Slack/Teams/PagerDuty webhook pattern.
public sealed class AlertSink : ISink
{
    public string Name => "Alert";

    public async ValueTask EmitAsync(LogEvent e, CancellationToken ct = default)
    {
        if (e.Level < LogLevel.Error) return;

        var icon   = e.Level == LogLevel.Critical ? "🔴" : "🟠";
        var corrId = e.Properties.TryGetValue("CorrelationId", out var cid) ? $"[{cid}] " : "";

        // In production: await _httpClient.PostAsync(webhookUrl, payload, ct)
        await Task.Run(() =>
            Console.WriteLine($"\n{icon} ALERT {corrId}{e.Category}: {e.RenderedMessage}\n"),
            ct);
    }

    public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
}

// ConditionalSink: wraps another sink, only forwards events matching a predicate.
public sealed class ConditionalSink(ISink inner, Func<LogEvent, bool> predicate) : ISink
{
    public string Name => $"Conditional({inner.Name})";

    public ValueTask EmitAsync(LogEvent e, CancellationToken ct = default)
        => predicate(e) ? inner.EmitAsync(e, ct) : ValueTask.CompletedTask;

    public ValueTask FlushAsync(CancellationToken ct = default)
        => inner.FlushAsync(ct);
}

// BufferedSink: collects events into batches — database bulk insert pattern.
public sealed class BufferedSink(int batchSize = 10) : ISink, IAsyncDisposable
{
    private readonly List<LogEvent> _buffer = [];
    private readonly Lock _lock = new();
    private int _totalFlushed;

    public string Name => "Buffered";

    public ValueTask EmitAsync(LogEvent e, CancellationToken ct = default)
    {
        bool shouldFlush;
        lock (_lock)
        {
            _buffer.Add(e);
            shouldFlush = _buffer.Count >= batchSize;
        }
        return shouldFlush ? FlushAsync(ct) : ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken ct = default)
    {
        List<LogEvent> batch;
        lock (_lock)
        {
            if (_buffer.Count == 0) return ValueTask.CompletedTask;
            batch = [.._buffer];
            _buffer.Clear();
        }
        _totalFlushed += batch.Count;
        // In production: await _db.BulkInsertAsync(batch, ct)
        Console.WriteLine($"  [BufferedSink] Flushed {batch.Count} events (total: {_totalFlushed})");
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await FlushAsync();
}
