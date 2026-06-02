using EnhancedBlocker.Api.Configuration;
using EnhancedBlocker.Api.Endpoints;
using EnhancedBlocker.Api.Middleware;
using EnhancedBlocker.Application;
using EnhancedBlocker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration ----
builder.Services
    .AddOptions<EnhancedBlockerOptions>()
    .Bind(builder.Configuration.GetSection(EnhancedBlockerOptions.SectionName))
    .ValidateOnStart();

var options = builder.Configuration
    .GetSection(EnhancedBlockerOptions.SectionName)
    .Get<EnhancedBlockerOptions>() ?? new EnhancedBlockerOptions();

var connectionString =
    builder.Configuration.GetConnectionString("AppDb")
    ?? throw new InvalidOperationException("Missing connection string 'AppDb'.");

// ---- Kestrel: loopback only ----
builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.ListenLocalhost(options.Port));

// ---- CORS for the extension origin ----
const string CorsPolicy = "extension";
builder.Services.AddCors(cors => cors.AddPolicy(CorsPolicy, policy =>
{
    if (options.AllowDevOrigins)
    {
        // Dev: permissive so the unpacked extension (id not yet stable) and curl both work.
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    }
    else if (!string.IsNullOrWhiteSpace(options.ExtensionId))
    {
        policy.WithOrigins($"chrome-extension://{options.ExtensionId}")
            .AllowAnyHeader()
            .AllowAnyMethod();
    }
}));

// ---- Composition root: wire Infrastructure into Application ports ----
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

app.UseCors(CorsPolicy);
app.UseMiddleware<TokenAuthMiddleware>();

app.MapApiEndpoints();

app.Run();

/// <summary>Exposed so an integration test host (WebApplicationFactory) can target this entry point.</summary>
public partial class Program;
