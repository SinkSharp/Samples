using Microsoft.AspNetCore.Http;
using SinkSharp.Core;
using SinkSharp.Core.Abstractions;
using SinkSharp.Core.Enrichers;

// ── Custom Enricher Implementations ──────────────────────────────────────────

// TenantEnricher: multi-tenant SaaS — reads TenantId from AsyncLocal context.
// Flows through async call chains automatically. Zero cost when no tenant set.
public sealed class TenantEnricher : IEnricher
{
    private static readonly AsyncLocal<string?> _tenantId = new();

    public static IDisposable Use(string tenantId)
    {
        var previous = _tenantId.Value;
        _tenantId.Value = tenantId;
        return new Scope(() => _tenantId.Value = previous);
    }

    public void Enrich(LogEvent e, IPropertyBag bag)
    {
        if (_tenantId.Value is { } id)
            bag.AddOrUpdate("TenantId", id);
    }

    private sealed class Scope(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}

// BuildInfoEnricher: stamps version/commit SHA — read ONCE at startup, zero runtime cost.
public sealed class BuildInfoEnricher : IEnricher
{
    private readonly string _version  = typeof(BuildInfoEnricher).Assembly
                                            .GetName().Version?.ToString() ?? "0.0.0";
    private readonly string _buildEnv = Environment.GetEnvironmentVariable("BUILD_ENV") ?? "local";
    private readonly string _commit   = Environment.GetEnvironmentVariable("GIT_COMMIT")  ?? "dev";

    public void Enrich(LogEvent e, IPropertyBag bag)
    {
        bag.AddOrUpdate("AppVersion", _version);
        bag.AddOrUpdate("BuildEnv",   _buildEnv);
        bag.AddOrUpdate("CommitSha",  _commit);
    }
}

// RequestEnricher: stamps RequestPath, Method, ClientIp, UserId via IHttpContextAccessor.
public sealed class RequestEnricher(IHttpContextAccessor http) : IEnricher
{
    public void Enrich(LogEvent e, IPropertyBag bag)
    {
        var ctx = http.HttpContext;
        if (ctx is null) return;

        bag.AddOrUpdate("RequestPath",   ctx.Request.Path.Value ?? "");
        bag.AddOrUpdate("RequestMethod", ctx.Request.Method);
        bag.AddOrUpdate("ClientIp",      ctx.Connection.RemoteIpAddress?.ToString() ?? "");

        var userId = ctx.User?.FindFirst("sub")?.Value
                  ?? ctx.User?.FindFirst("userId")?.Value;
        if (userId is not null)
            bag.AddOrUpdate("UserId", userId);
    }
}
