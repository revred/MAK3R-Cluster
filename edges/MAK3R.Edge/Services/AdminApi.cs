using Mak3r.Edge.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Mak3r.Edge.Services;

public class AdminApi : BackgroundService
{
    private readonly EdgeConfig _cfg;
    private readonly QueueService _queue;
    private readonly NetDiagDb _db;
    private readonly ILogger<AdminApi> _log;
    private IHost? _web;

    public AdminApi(IOptions<EdgeConfig> cfg, QueueService queue, NetDiagDb db, ILogger<AdminApi> log)
    { _cfg = cfg.Value; _queue = queue; _db = db; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(_cfg.AdminApi.Listen);
        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok(new { ok = true, ts = DateTime.UtcNow }));
        app.MapGet("/metrics", () => Results.Ok(new { queueDepth = _queue.ApproxDepth }));
        app.MapGet("/config", () => Results.Ok(_cfg));
        // lightweight netdiag endpoints would query SQLite; here we just confirm API is live

        _web = app;
        _log.LogInformation("Admin API listening on {url}", _cfg.AdminApi.Listen);
        await app.RunAsync(stoppingToken);
    }
}
