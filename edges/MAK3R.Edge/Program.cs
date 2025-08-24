using Mak3r.Edge;
using Mak3r.Edge.Services;
using Mak3r.Edge.Tools;
using Microsoft.Extensions.Options;
using System.CommandLine;

// Check if running config validation command
if (args.Length > 0 && args[0] == "validate-config")
{
    var rootCommand = new RootCommand("MAK3R Edge Runtime");
    rootCommand.AddCommand(ConfigValidator.CreateValidateCommand());
    return await rootCommand.InvokeAsync(args[1..]);
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<EdgeConfig>(builder.Configuration.GetSection("Edge"));
builder.Services.AddSingleton<NetDiagDb>();
builder.Services.AddSingleton<QueueService>();
builder.Services.AddSingleton<UplinkService>();
builder.Services.AddSingleton<ConfigValidationService>();
builder.Services.AddSingleton<ConnectorDiscoveryService>();
builder.Services.AddHostedService<ConfigService>();
builder.Services.AddHostedService<ConnectorManager>();
builder.Services.AddHostedService<NormalizerService>();
builder.Services.AddHostedService<StoreService>();
builder.Services.AddHostedService<HealthService>();
builder.Services.AddHostedService<AdminApi>();
builder.Services.AddHostedService<LoadGenService>();

var app = builder.Build();

// Validate configuration on startup
var validator = app.Services.GetRequiredService<ConfigValidationService>();
var config = app.Services.GetRequiredService<IOptions<EdgeConfig>>().Value;
var validationResult = validator.ValidateEdgeConfig(config);

if (!validationResult.IsValid)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError("Configuration validation failed:");
    foreach (var error in validationResult.Errors)
    {
        logger.LogError("  - {Error}", error);
    }
    Environment.Exit(1);
}

foreach (var warning in validationResult.Warnings)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning("Configuration warning: {Warning}", warning);
}

// ensure SQLite initialized
var db = app.Services.GetRequiredService<NetDiagDb>();
db.Init();

await app.RunAsync();
