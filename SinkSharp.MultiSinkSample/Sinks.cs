using SinkSharp.Core;
using SinkSharp.Core.Abstractions;
using SinkSharp.Sinks.File;

// Audit Sink — dedicated file for business-critical events.
// Only writes events whose Category contains "Audit".
public sealed class AuditFileSink : ISink
{
    private readonly RollingFileSink _inner = new("logs", "audit");

    public string Name => "AuditFile";

    public ValueTask EmitAsync(LogEvent e, CancellationToken ct = default)
    {
        if (!e.Category.Contains("Audit", StringComparison.OrdinalIgnoreCase))
            return ValueTask.CompletedTask;
        return _inner.EmitAsync(e, ct);
    }

    public ValueTask FlushAsync(CancellationToken ct = default)
        => _inner.FlushAsync(ct);
}
