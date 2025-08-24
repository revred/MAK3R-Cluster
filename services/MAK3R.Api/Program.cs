using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Serilog;
using MAK3R.Data;
using MAK3R.DigitalTwin.Services;
using MAK3R.Shared.DTOs;
using MAK3R.Core;
using MAK3R.Connectors;
// Connector-specific using statements removed for MCP architecture
using MAK3R.Api.Endpoints;
using MAK3R.Api.Hubs;
using MAK3R.Core.Hubs;
using MAK3R.Core.Services;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/mak3r-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
builder.Host.UseSerilog();

// Add services
builder.Services.AddDbContext<MAK3RDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=mak3r.db"));

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<MAK3RDbContext>();

// Add JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "MAK3R-Super-Secret-Key-For-Development-Only-2024";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "MAK3R.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "MAK3R.PWA";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorWasm", policy =>
    {
        policy.WithOrigins("https://localhost:7228", "http://localhost:5223", "https://localhost:5173", "http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add domain services
builder.Services.AddScoped<ITwinOrchestrator, TwinOrchestrator>();

// Add connector hub with MCP-like architecture
builder.Services.AddConnectorHub();

// Add SignalR
builder.Services.AddSignalR();

// Add background services
builder.Services.AddHostedService<MachineDataBroadcastService>();

// Add API services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowBlazorWasm");
app.UseAuthentication();
app.UseAuthorization();

// SignalR Hubs
app.MapHub<MachineDataHub>("/hubs/machinedata");
app.MapHub<MachineHub>("/hubs/machines");  // Edge-to-Cluster hub

// Health check endpoint
app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }))
    .WithName("GetHealth")
    .WithOpenApi();

// Onboarding endpoints
app.MapPost("/api/onboard", async (OnboardingWizardDto wizardData, ITwinOrchestrator orchestrator) =>
{
    var result = await orchestrator.CreateDigitalTwinAsync(wizardData);
    return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
})
.WithName("CreateDigitalTwin")
.WithOpenApi();

// Digital Twin endpoints
app.MapGet("/api/twin/{companyId:guid}", async (Guid companyId, ITwinOrchestrator orchestrator) =>
{
    var result = await orchestrator.GetCompanyTwinAsync(companyId);
    if (!result.IsSuccess)
        return Results.NotFound(result.Error);

    var company = result.Value;
    var dto = new CompanyDto(
        company.Id,
        company.Name,
        company.RegistrationId,
        company.TaxId,
        company.Industry,
        company.Website,
        company.CreatedUtc,
        company.UpdatedUtc
    );

    return Results.Ok(dto);
})
.RequireAuthorization()
.WithName("GetDigitalTwin")
.WithOpenApi();

// Twin validation endpoint
app.MapGet("/api/twin/{companyId:guid}/validate", async (Guid companyId, ITwinOrchestrator orchestrator) =>
{
    var result = await orchestrator.ValidateTwinAsync(companyId);
    return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
})
.WithName("ValidateDigitalTwin")
.WithOpenApi();

// Authentication endpoints
app.MapPost("/api/auth/register", async (RegisterDto dto, UserManager<IdentityUser> userManager) =>
{
    var user = new IdentityUser { UserName = dto.Email, Email = dto.Email };
    var result = await userManager.CreateAsync(user, dto.Password);
    
    if (!result.Succeeded)
    {
        return Results.BadRequest(new { errors = result.Errors.Select(e => e.Description) });
    }
    
    var token = GenerateJwtToken(user, jwtKey, jwtIssuer, jwtAudience);
    return Results.Ok(new { token, email = user.Email });
})
.WithName("Register")
.WithOpenApi();

app.MapPost("/api/auth/login", async (LoginDto dto, UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager) =>
{
    var user = await userManager.FindByEmailAsync(dto.Email);
    if (user == null)
    {
        return Results.Unauthorized();
    }
    
    var result = await signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
    if (!result.Succeeded)
    {
        return Results.Unauthorized();
    }
    
    var token = GenerateJwtToken(user, jwtKey, jwtIssuer, jwtAudience);
    return Results.Ok(new { token, email = user.Email });
})
.WithName("Login")
.WithOpenApi();

// Demo data endpoint
app.MapGet("/api/demo/contoso-gears", () =>
{
    var demoData = SeedData.GetContosoGearsData();
    return Results.Ok(demoData);
})
.WithName("GetDemoData")
.WithOpenApi();

// Paginated endpoints for progressive loading
app.MapGet("/api/companies", async (int page = 0, int size = 20, MAK3RDbContext context = null!) =>
{
    var totalCount = await context.Companies.CountAsync();
    var companies = await context.Companies
        .Skip(page * size)
        .Take(size)
        .Select(c => new CompanyDto(c.Id, c.Name, c.RegistrationId, c.TaxId, c.Industry, c.Website, c.CreatedUtc, c.UpdatedUtc))
        .ToListAsync();
    
    var result = new PagedResult<CompanyDto>
    {
        Items = companies,
        TotalCount = totalCount,
        Page = page,
        PageSize = size
    };
    
    return Results.Ok(result);
})
.RequireAuthorization()
.WithName("GetCompanies")
.WithOpenApi();

app.MapGet("/api/companies/{companyId:guid}/sites", async (Guid companyId, int page = 0, int size = 20, MAK3RDbContext context = null!) =>
{
    var totalCount = await context.Sites.Where(s => s.CompanyId == companyId).CountAsync();
    var sites = await context.Sites
        .Where(s => s.CompanyId == companyId)
        .Skip(page * size)
        .Take(size)
        .Select(s => new { s.Id, s.Name, s.Address, s.City, s.Country, s.Description, s.CreatedUtc })
        .ToListAsync();
    
    var result = new PagedResult<object>
    {
        Items = sites,
        TotalCount = totalCount,
        Page = page,
        PageSize = size
    };
    
    return Results.Ok(result);
})
.RequireAuthorization()
.WithName("GetCompanySites")
.WithOpenApi();

app.MapGet("/api/companies/{companyId:guid}/machines", async (Guid companyId, int page = 0, int size = 20, MAK3RDbContext context = null!) =>
{
    var totalCount = await context.Machines
        .Join(context.Sites, m => m.SiteId, s => s.Id, (m, s) => new { Machine = m, Site = s })
        .Where(x => x.Site.CompanyId == companyId)
        .CountAsync();
        
    var machines = await context.Machines
        .Join(context.Sites, m => m.SiteId, s => s.Id, (m, s) => new { Machine = m, Site = s })
        .Where(x => x.Site.CompanyId == companyId)
        .Skip(page * size)
        .Take(size)
        .Select(x => new { 
            x.Machine.Id, x.Machine.Name, x.Machine.Make, x.Machine.Model, x.Machine.SerialNumber, 
            x.Machine.Status, x.Machine.OpcUaNode, x.Machine.CreatedUtc,
            Site = x.Site.Name 
        })
        .ToListAsync();
    
    var result = new PagedResult<object>
    {
        Items = machines,
        TotalCount = totalCount,
        Page = page,
        PageSize = size
    };
    
    return Results.Ok(result);
})
.RequireAuthorization()
.WithName("GetCompanyMachines")
.WithOpenApi();

app.MapGet("/api/companies/{companyId:guid}/products", async (Guid companyId, int page = 0, int size = 20, MAK3RDbContext context = null!) =>
{
    var totalCount = await context.Products
        .Where(p => p.CompanyId == companyId)
        .CountAsync();
        
    var products = await context.Products
        .Where(p => p.CompanyId == companyId)
        .Skip(page * size)
        .Take(size)
        .Select(p => new { 
            p.Id, p.Name, p.Sku, p.Price, p.Currency, 
            p.Description, p.Category, p.CreatedUtc 
        })
        .ToListAsync();
    
    var result = new PagedResult<object>
    {
        Items = products,
        TotalCount = totalCount,
        Page = page,
        PageSize = size
    };
    
    return Results.Ok(result);
})
.RequireAuthorization()
.WithName("GetCompanyProducts")
.WithOpenApi();

// Map connector endpoints
app.MapConnectorEndpoints();

// JWT Token Generation Helper
static string GenerateJwtToken(IdentityUser user, string jwtKey, string jwtIssuer, string jwtAudience)
{
    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.UTF8.GetBytes(jwtKey);
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(ClaimTypes.Name, user.UserName ?? "")
        }),
        Expires = DateTime.UtcNow.AddHours(12),
        Issuer = jwtIssuer,
        Audience = jwtAudience,
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };
    var token = tokenHandler.CreateToken(tokenDescriptor);
    return tokenHandler.WriteToken(token);
}

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MAK3RDbContext>();
    await context.Database.EnsureCreatedAsync();
    await SeedData.SeedDatabaseAsync(context);
}

try
{
    Log.Information("Starting MAK3R API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
