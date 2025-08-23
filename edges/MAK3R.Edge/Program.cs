using Mak3r.Edge;
using Mak3r.Edge.Services;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<EdgeConfig>(builder.Configuration.GetSection("Edge"));
builder.Services.AddSingleton<NetDiagDb>();
builder.Services.AddSingleton<QueueService>();
builder.Services.AddSingleton<UplinkService>();
builder.Services.AddHostedService<ConfigService>();
builder.Services.AddHostedService<ConnectorManager>();
builder.Services.AddHostedService<NormalizerService>();
builder.Services.AddHostedService<StoreService>();
builder.Services.AddHostedService<HealthService>();
builder.Services.AddHostedService<AdminApi>();
builder.Services.AddHostedService<LoadGenService>();

var app = builder.Build();

// ensure SQLite initialized
var db = app.Services.GetRequiredService<NetDiagDb>();
db.Init();

await app.RunAsync();
