namespace UrbanDiagnosticCentre.SyncService;

/// <summary>
/// Middleware that validates the X-Api-Key header against the configured pre-shared key.
/// Rejects unauthorized callers with 401 before any controller logic runs.
/// </summary>
public class ApiKeyMiddleware
{
    private const string ApiKeyHeader = "X-Api-Key";
    private readonly RequestDelegate _next;
    private readonly string _expectedKey;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next        = next;
        _expectedKey = config["SyncService:ApiKey"] ?? string.Empty;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Health endpoint bypasses auth so monitoring tools can reach it without a key.
        if (context.Request.Path.StartsWithSegments("/sync/health"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var key) ||
            key != _expectedKey ||
            string.IsNullOrEmpty(_expectedKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        await _next(context);
    }
}
