using Microsoft.EntityFrameworkCore;
using UrbanDiagnosticCentre.SyncService;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();
builder.Services.AddControllers();

var certPath = builder.Configuration["SyncService:Https:CertPath"];
if (!string.IsNullOrEmpty(certPath) && System.IO.File.Exists(certPath))
{
    var certPwd = builder.Configuration["SyncService:Https:CertPassword"] ?? string.Empty;
    builder.WebHost.ConfigureKestrel(k =>
        k.ListenAnyIP(5433, o => o.UseHttps(certPath, certPwd)));
}

var rawDbPath = builder.Configuration["SyncService:DatabasePath"]
    ?? @"%LOCALAPPDATA%\UrbanDiagnosticCentre\udc.db";
var dbPath = Environment.ExpandEnvironmentVariables(rawDbPath);

builder.Services.AddDbContextFactory<SyncDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath};journal mode=WAL;"));

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddEventLog(settings => settings.SourceName = "UDC SyncService");

var app = builder.Build();
app.UseMiddleware<ApiKeyMiddleware>();
app.MapControllers();

var configuredKey = app.Configuration["SyncService:ApiKey"];
if (string.IsNullOrEmpty(configuredKey) || configuredKey == "CHANGE-THIS-TO-A-RANDOM-SECRET-KEY")
    app.Logger.LogCritical("SyncService ApiKey must be replaced before network deployment.");

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SyncDbContext>>();
    using var db = factory.CreateDbContext();
    db.Database.OpenConnection();
    db.Database.CloseConnection();
}

app.Run();
