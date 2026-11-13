using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Npgsql;
using TaskManager.Api.WorkspaceAccess;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("TaskManager")
    ?? throw new InvalidOperationException("ConnectionStrings:TaskManager must be configured.");
var allowedOrigin = ParseAllowedOrigin(builder.Configuration["Security:AllowedOrigin"]);
if (!builder.Environment.IsDevelopment()
    && !builder.Environment.IsEnvironment("Testing")
    && allowedOrigin.Scheme != Uri.UriSchemeHttps)
{
    throw new InvalidOperationException("Security:AllowedOrigin must use HTTPS outside development and tests.");
}
var provisioningLimits = ReadRateLimit(builder.Configuration, "AbuseControls:Provisioning");
var protectedRequestLimits = ReadRateLimit(builder.Configuration, "AbuseControls:ProtectedRequests");

builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<CredentialIssuer>();
builder.Services.AddSingleton<WorkspaceStore>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("SanitizedOperationalSignals");
        logger.LogWarning("Configured request rate limit exceeded. TraceId {TraceId}", context.HttpContext.TraceIdentifier);
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "rate_limited" },
            cancellationToken);
    };
    options.AddPolicy("provisioning", context => Partition(context, provisioningLimits));
    options.AddPolicy("protected-requests", context => Partition(context, protectedRequestLimits));
});

var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("SanitizedOperationalSignals");
    logger.LogError("Request failed. TraceId {TraceId}", context.TraceIdentifier);
    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    await context.Response.WriteAsJsonAsync(new { error = "service_unavailable" });
}));
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "no-store";
    }

    if (context.Request.Path.StartsWithSegments("/api")
        && context.Request.Method is not "GET" and not "HEAD"
        && !HasAllowedOrigin(context.Request.Headers.Origin, allowedOrigin))
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("SanitizedOperationalSignals");
        logger.LogWarning("State-changing request rejected by origin policy. TraceId {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { error = "origin_rejected" });
        return;
    }

    await next();
});
app.UseRateLimiter();

var issuer = app.Services.GetRequiredService<CredentialIssuer>();
var store = app.Services.GetRequiredService<WorkspaceStore>();
var clock = app.Services.GetRequiredService<TimeProvider>();

app.MapGet("/api/workspace", async (HttpContext context) =>
{
    var workspace = await AuthenticateAsync(context, store, clock);
    return workspace is null
        ? Results.Json(new { error = "workspace_inaccessible" }, statusCode: StatusCodes.Status401Unauthorized)
        : Results.Ok(new { accessible = true, confirmed = workspace.IsActivated });
}).RequireRateLimiting("protected-requests");

app.MapPost("/api/workspace/confirm", async (HttpContext context) =>
{
    var workspace = await AuthenticateAsync(context, store, clock);
    if (workspace is null)
    {
        return Results.Json(new { error = "workspace_inaccessible" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    return await store.ActivateAsync(workspace.WorkspaceId, context.RequestAborted)
        ? Results.NoContent()
        : Results.Json(new { error = "workspace_inaccessible" }, statusCode: StatusCodes.Status401Unauthorized);
}).RequireRateLimiting("protected-requests");

app.MapPost("/api/workspace/activity", async (HttpContext context) =>
{
    var workspace = await AuthenticateAsync(context, store, clock);
    if (workspace is null || !workspace.IsActivated)
    {
        return Results.Json(new { error = "workspace_inaccessible" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    return await store.RecordActivityAsync(workspace.WorkspaceId, context.RequestAborted)
        ? Results.NoContent()
        : Results.Json(new { error = "workspace_inaccessible" }, statusCode: StatusCodes.Status401Unauthorized);
}).RequireRateLimiting("protected-requests");

app.MapPost("/api/workspaces", async (HttpContext context) =>
{
    var currentWorkspace = await AuthenticateAsync(context, store, clock);
    if (currentWorkspace is not null)
    {
        return Results.NoContent();
    }

    var initializationKey = context.Request.Headers["X-Initialization-Key"].ToString();
    if (!IsInitializationKey(initializationKey)
        || context.Request.ContentLength is > 0
        || context.Request.Headers.ContainsKey("Transfer-Encoding"))
    {
        return Results.Json(new { error = "initialization_required" }, statusCode: StatusCodes.Status400BadRequest);
    }

    var issued = issuer.Issue();
    var expiresAt = clock.GetUtcNow().AddDays(30);
    var result = await store.ProvisionAsync(initializationKey, issued, expiresAt, context.RequestAborted);
    if (!result.WasCreated)
    {
        var conflictLogger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("SanitizedOperationalSignals");
        conflictLogger.LogWarning("Workspace initialization retry rejected. TraceId {TraceId}", context.TraceIdentifier);
        return Results.Json(new { error = "initialization_conflict" }, statusCode: StatusCodes.Status409Conflict);
    }

    context.Response.Cookies.Append(CredentialIssuer.CookieName, result.Credential!, new CookieOptions
    {
        Expires = result.ExpiresAt,
        HttpOnly = true,
        IsEssential = true,
        Path = "/",
        SameSite = SameSiteMode.Strict,
        Secure = true
    });

    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("SanitizedOperationalSignals");
    logger.LogInformation("Workspace provisioning completed");
    return Results.StatusCode(StatusCodes.Status201Created);
}).RequireRateLimiting("provisioning");

app.Run();

static async Task<WorkspaceAccess?> AuthenticateAsync(
    HttpContext context,
    WorkspaceStore store,
    TimeProvider clock)
{
    if (!CredentialIssuer.TryGetVerifier(context.Request.Cookies[CredentialIssuer.CookieName], out var verifier))
    {
        LogAccessRejection(context);
        return null;
    }

    try
    {
        var workspaceId = await store.FindWorkspaceAsync(verifier, clock.GetUtcNow(), context.RequestAborted);
        if (workspaceId is null)
        {
            LogAccessRejection(context);
        }

        return workspaceId;
    }
    finally
    {
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(verifier);
    }
}

static bool IsInitializationKey(string key)
{
    if (!Guid.TryParseExact(key, "D", out _)
        || key.Length != 36
        || key[14] != '4')
    {
        return false;
    }

    return char.ToLowerInvariant(key[19]) is '8' or '9' or 'a' or 'b';
}

static void LogAccessRejection(HttpContext context)
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("SanitizedOperationalSignals");
    logger.LogWarning("Workspace access rejected. TraceId {TraceId}", context.TraceIdentifier);
}

static Uri ParseAllowedOrigin(string? value)
{
    if (!Uri.TryCreate(value, UriKind.Absolute, out var origin)
        || origin.Scheme is not "http" and not "https"
        || !string.IsNullOrEmpty(origin.UserInfo)
        || origin.AbsolutePath != "/"
        || !string.IsNullOrEmpty(origin.Query)
        || !string.IsNullOrEmpty(origin.Fragment))
    {
        throw new InvalidOperationException("Security:AllowedOrigin must be one absolute HTTP(S) origin.");
    }

    return origin;
}

static bool HasAllowedOrigin(Microsoft.Extensions.Primitives.StringValues requestOrigin, Uri allowedOrigin)
{
    if (requestOrigin.Count != 1
        || !Uri.TryCreate(requestOrigin[0], UriKind.Absolute, out var parsed)
        || !string.IsNullOrEmpty(parsed.UserInfo)
        || parsed.AbsolutePath != "/"
        || !string.IsNullOrEmpty(parsed.Query)
        || !string.IsNullOrEmpty(parsed.Fragment))
    {
        return false;
    }

    return string.Equals(
        parsed.GetLeftPart(UriPartial.Authority),
        allowedOrigin.GetLeftPart(UriPartial.Authority),
        StringComparison.OrdinalIgnoreCase);
}

static FixedWindowRateLimiterOptions ReadRateLimit(IConfiguration configuration, string section)
{
    var permitLimit = configuration.GetValue<int?>($"{section}:PermitLimit");
    var windowSeconds = configuration.GetValue<int?>($"{section}:WindowSeconds");
    if (permitLimit is null or <= 0 || windowSeconds is null or <= 0)
    {
        throw new InvalidOperationException($"{section} must configure positive PermitLimit and WindowSeconds values.");
    }

    return new FixedWindowRateLimiterOptions
    {
        AutoReplenishment = true,
        PermitLimit = permitLimit.Value,
        QueueLimit = 0,
        Window = TimeSpan.FromSeconds(windowSeconds.Value)
    };
}

static RateLimitPartition<string> Partition(HttpContext context, FixedWindowRateLimiterOptions limits)
{
    var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => limits);
}

public partial class Program;
