using Microsoft.EntityFrameworkCore;
using UrbanDiagnosticCentre.SyncService;

var builder = WebApplication.CreateBuilder(args);

// Support running as a Windows Service (survives user logoff/restart).
builder.Host.UseWindowsService();

builder.Services.AddControllers();

// ── HTTPS (optional) ──────────────────────────────────────────────────────────
// Set SyncService:Https:CertPath + SyncService:Https:CertPassword in appsettings.json
// to enable HTTPS on port 5433. For LAN deployments without a cert, run on a VPN-only
// network interface so traffic never leaves the building on plain HTTP.
var certPath = builder.Configuration["SyncService:Https:CertPath"];
if (!string.IsNullOrEmpty(certPath) && System.IO.File.Exists(certPath))
{
    var certPwd = builder.Configuration["SyncService:Https:CertPassword"] ?? string.Empty;
    builder.WebHost.ConfigureKestrel(k =>
        k.ListenAnyIP(5433, o => o.UseHttps(certPath, certPwd)));
}

// ── Database ─────────────────────────────────────────────────────────────────
// Resolve DB path from config (expand environment variables for %LOCALAPPDATA% etc.)
var rawDbPath = builder.Configuration["SyncService:DatabasePath"]
    ?? @"%LOCALAPPDATA%\UrbanDiagnosticCentre\udc.db";
var dbPath = Environment.ExpandEnvironmentVariables(rawDbPath);

// Use IDbContextFactory so controllers get scoped, short-lived contexts.
// WAL mode must match what the WPF app uses.
builder.Services.AddDbContextFactory<SyncDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath};journal mode=WAL;"));

// ── Logging ───────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddEventLog(settings =>
{
    settings.SourceName = "UDC SyncService";
});

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────
// API key auth before any controller.
app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();

// ── API key safety check ──────────────────────────────────────────────────────
var configuredKey = app.Configuration["SyncService:ApiKey"];
if (string.IsNullOrEmpty(configuredKey) || configuredKey == "CHANGE-THIS-TO-A-RANDOM-SECRET-KEY")
    app.Logger.LogCritical("SECURITY WARNING: SyncService ApiKey is the default placeholder. " +
        "Update SyncService:ApiKey in appsettings.json before exposing this service on the network.");

// Verify DB connectivity at startup (do NOT run migrations — WPF app owns them).
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SyncDbContext>>();
    using var db = factory.CreateDbContext();
    try
    {
        db.Database.OpenConnection();
        db.Database.CloseConnection();
        app.Logger.LogInformation("SyncService database connection verified: {DbPath}", dbPath);
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "Cannot open database at {DbPath} — ensure WPF app has run migrations first", dbPath);
        throw;
    }
}

app.Logger.LogInformation("UrbanDiagnosticCentre.SyncService starting on {Urls}",
    builder.Configuration["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5432");

app.Run();
