using System.Security.Cryptography;
using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TaskManager.Api.WorkspaceAccess;

namespace TaskManager.Api.Tests.WorkspaceAccess;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TASK_MANAGER_TEST_CONNECTION_STRING")))
        {
            Skip = "Set TASK_MANAGER_TEST_CONNECTION_STRING to an isolated disposable PostgreSQL database.";
        }
    }
}

public sealed class PostgresIntegrationTests
{
    private const string AllowedOrigin = "http://localhost";

    [PostgresFact]
    public async Task First_visit_requires_explicit_creation_and_issues_only_a_protected_cookie()
    {
        await using var harness = await PostgresHarness.StartAsync();
        using var client = harness.CreateClient();

        var firstVisit = await client.GetAsync("/api/workspace");
        Assert.Equal(HttpStatusCode.Unauthorized, firstVisit.StatusCode);
        Assert.Equal(0, await harness.CountWorkspacesAsync());

        var before = DateTimeOffset.UtcNow;
        using var response = await harness.CreateWorkspaceAsync(client, Guid.NewGuid().ToString("D"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", setCookie, StringComparison.OrdinalIgnoreCase);

        var cookie = CookieValue(setCookie);
        var expires = CookieExpiry(setCookie);
        Assert.InRange(expires, before.AddDays(30).AddSeconds(-2), before.AddDays(30).AddSeconds(2));
        Assert.Equal(1, await harness.CountWorkspacesAsync());

        var verifier = await harness.GetVerifierAsync();
        Assert.Equal(SHA256.HashData(WebEncoders.Base64UrlDecode(cookie)), verifier);
        Assert.False(Encoding.UTF8.GetBytes(cookie).SequenceEqual(verifier));
        Assert.InRange(
            await harness.GetCredentialExpiryAsync(),
            before.AddDays(30).AddSeconds(-2),
            before.AddDays(30).AddSeconds(2));

        using var authorized = await harness.GetWorkspaceAsync(client, cookie);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        var body = await authorized.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("accessible").GetBoolean());
        Assert.False(body.GetProperty("confirmed").GetBoolean());
        Assert.False(body.TryGetProperty("workspaceId", out _));
        using var confirmation = await harness.ConfirmWorkspaceAsync(client, cookie);
        Assert.Equal(HttpStatusCode.NoContent, confirmation.StatusCode);
        using var confirmed = await harness.GetWorkspaceAsync(client, cookie);
        Assert.True((await confirmed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("confirmed").GetBoolean());
    }

    [PostgresFact]
    public async Task Missing_invalid_expired_and_revoked_credentials_never_provision_replacements()
    {
        await using var harness = await PostgresHarness.StartAsync();
        using var client = harness.CreateClient();

        using var missing = await harness.GetWorkspaceAsync(client, null);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        using var invalid = await harness.GetWorkspaceAsync(client, "not-a-valid-credential");
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);

        using var expiredCreation = await harness.CreateWorkspaceAsync(client, Guid.NewGuid().ToString("D"));
        var expiredCookie = CookieValue(Assert.Single(expiredCreation.Headers.GetValues("Set-Cookie")));
        await harness.SetCredentialExpiredAsync(expiredCookie);
        using var expired = await harness.GetWorkspaceAsync(client, expiredCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);

        using var revokedCreation = await harness.CreateWorkspaceAsync(client, Guid.NewGuid().ToString("D"));
        var revokedCookie = CookieValue(Assert.Single(revokedCreation.Headers.GetValues("Set-Cookie")));
        await harness.RevokeCredentialAsync(revokedCookie);
        using var revoked = await harness.GetWorkspaceAsync(client, revokedCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);

        Assert.Equal(2, await harness.CountWorkspacesAsync());
    }

    [PostgresFact]
    public async Task Protected_write_uses_credential_workspace_and_ignores_client_workspace_ids()
    {
        await using var harness = await PostgresHarness.StartAsync();
        using var client = harness.CreateClient();

        using var first = await harness.CreateWorkspaceAsync(client, Guid.NewGuid().ToString("D"));
        var firstCookie = CookieValue(Assert.Single(first.Headers.GetValues("Set-Cookie")));
        var firstWorkspaceId = await harness.GetWorkspaceIdForCredentialAsync(firstCookie);

        using var second = await harness.CreateWorkspaceAsync(client, Guid.NewGuid().ToString("D"));
        var secondCookie = CookieValue(Assert.Single(second.Headers.GetValues("Set-Cookie")));
        var secondWorkspaceId = await harness.GetWorkspaceIdForCredentialAsync(secondCookie);
        var secondLastAccessed = await harness.GetLastAccessedAsync(secondWorkspaceId);

        using var firstConfirmation = await harness.ConfirmWorkspaceAsync(client, firstCookie);
        using var secondConfirmation = await harness.ConfirmWorkspaceAsync(client, secondCookie);
        Assert.Equal(HttpStatusCode.NoContent, firstConfirmation.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, secondConfirmation.StatusCode);
        Assert.Equal(firstWorkspaceId, await harness.ResolveWorkspaceAsync(firstCookie));
        Assert.NotEqual(secondWorkspaceId, await harness.ResolveWorkspaceAsync(firstCookie));
        using var read = await harness.GetWorkspaceAsync(client, firstCookie);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        using var response = await harness.RecordActivityAsync(client, firstCookie, secondWorkspaceId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotEqual(secondLastAccessed, await harness.GetLastAccessedAsync(firstWorkspaceId));
        Assert.Equal(secondLastAccessed, await harness.GetLastAccessedAsync(secondWorkspaceId));
        Assert.Equal(2, await harness.CountWorkspacesAsync());
    }

    [PostgresFact]
    public async Task Duplicate_initialization_key_revokes_the_unconfirmed_workspace_without_creating_another()
    {
        await using var harness = await PostgresHarness.StartAsync();
        using var client = harness.CreateClient();
        var initializationKey = Guid.NewGuid().ToString("D");

        using var first = await harness.CreateWorkspaceAsync(client, initializationKey);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var retry = await harness.CreateWorkspaceAsync(client, initializationKey);

        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
        Assert.Equal("initialization_conflict", (await retry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
        Assert.Equal(1, await harness.CountWorkspacesAsync());
        Assert.Equal(0, await harness.CountActiveCredentialsAsync());
    }

    [PostgresFact]
    public async Task Concurrent_initialization_requests_do_not_leave_duplicate_active_workspaces()
    {
        await using var harness = await PostgresHarness.StartAsync();
        using var client = harness.CreateClient();
        var initializationKey = Guid.NewGuid().ToString("D");

        var responses = await Task.WhenAll(
            harness.CreateWorkspaceAsync(client, initializationKey),
            harness.CreateWorkspaceAsync(client, initializationKey));
        using var first = responses[0];
        using var second = responses[1];

        Assert.Contains(HttpStatusCode.Created, responses.Select(response => response.StatusCode));
        Assert.Contains(HttpStatusCode.Conflict, responses.Select(response => response.StatusCode));
        Assert.Equal(1, await harness.CountWorkspacesAsync());
        Assert.Equal(0, await harness.CountActiveCredentialsAsync());
    }

    [PostgresFact]
    public async Task Repeated_initialization_with_the_issued_cookie_is_idempotent()
    {
        await using var harness = await PostgresHarness.StartAsync();
        using var client = harness.CreateClient();
        var initializationKey = Guid.NewGuid().ToString("D");

        using var first = await harness.CreateWorkspaceAsync(client, initializationKey);
        var cookie = CookieValue(Assert.Single(first.Headers.GetValues("Set-Cookie")));
        using var retry = await harness.CreateWorkspaceAsync(client, initializationKey, cookie);
        using var confirmation = await harness.ConfirmWorkspaceAsync(client, cookie);

        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, confirmation.StatusCode);
        Assert.Equal(1, await harness.CountWorkspacesAsync());
        Assert.Equal(1, await harness.CountActiveCredentialsAsync());
    }

    [PostgresFact]
    public async Task Duplicate_initialization_without_a_cookie_does_not_revoke_an_activated_credential()
    {
        await using var harness = await PostgresHarness.StartAsync();
        using var client = harness.CreateClient();
        var initializationKey = Guid.NewGuid().ToString("D");

        using var created = await harness.CreateWorkspaceAsync(client, initializationKey);
        var cookie = CookieValue(Assert.Single(created.Headers.GetValues("Set-Cookie")));
        using var firstAccess = await harness.GetWorkspaceAsync(client, cookie);
        Assert.Equal(HttpStatusCode.OK, firstAccess.StatusCode);
        using var confirmation = await harness.ConfirmWorkspaceAsync(client, cookie);
        Assert.Equal(HttpStatusCode.NoContent, confirmation.StatusCode);

        using var duplicate = await harness.CreateWorkspaceAsync(client, initializationKey);
        using var subsequentAccess = await harness.GetWorkspaceAsync(client, cookie);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, subsequentAccess.StatusCode);
        Assert.Equal(1, await harness.CountActiveCredentialsAsync());
    }

    [PostgresFact]
    public async Task Cancelled_duplicate_initialization_cleans_up_the_unconfirmed_attempt()
    {
        await using var harness = await PostgresHarness.StartAsync();
        using var client = harness.CreateClient();
        var initializationKey = Guid.NewGuid().ToString("D");

        using var created = await harness.CreateWorkspaceAsync(client, initializationKey);
        var cookie = CookieValue(Assert.Single(created.Headers.GetValues("Set-Cookie")));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harness.ProvisionDuplicateWithCancelledTokenAsync(initializationKey));

        using var access = await harness.GetWorkspaceAsync(client, cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, access.StatusCode);
        Assert.Equal(1, await harness.CountWorkspacesAsync());
        Assert.Equal(0, await harness.CountActiveCredentialsAsync());
    }

    [PostgresFact]
    public async Task Concurrent_confirmation_requests_are_idempotent()
    {
        await using var harness = await PostgresHarness.StartAsync();
        using var client = harness.CreateClient();

        using var created = await harness.CreateWorkspaceAsync(client, Guid.NewGuid().ToString("D"));
        var cookie = CookieValue(Assert.Single(created.Headers.GetValues("Set-Cookie")));
        var confirmations = await Task.WhenAll(
            harness.ConfirmWorkspaceAsync(client, cookie),
            harness.ConfirmWorkspaceAsync(client, cookie));

        Assert.All(confirmations, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
        Assert.Equal(1, await harness.CountActiveCredentialsAsync());
        foreach (var response in confirmations)
        {
            response.Dispose();
        }
    }

    [PostgresFact]
    public async Task State_changes_reject_missing_or_unapproved_origins()
    {
        await using var harness = await PostgresHarness.StartAsync();
        using var client = harness.CreateClient();

        using var missingOrigin = await harness.CreateWorkspaceAsync(client, Guid.NewGuid().ToString("D"), origin: null);
        Assert.Equal(HttpStatusCode.Forbidden, missingOrigin.StatusCode);

        using var wrongOrigin = await harness.CreateWorkspaceAsync(
            client,
            Guid.NewGuid().ToString("D"),
            origin: "https://attacker.example");
        Assert.Equal(HttpStatusCode.Forbidden, wrongOrigin.StatusCode);
        Assert.Equal(0, await harness.CountWorkspacesAsync());
    }

    [PostgresFact]
    public async Task Configured_provisioning_limit_returns_a_safe_rate_limit_outcome()
    {
        await using var harness = await PostgresHarness.StartAsync(provisioningLimit: 1);
        using var client = harness.CreateClient();

        using var first = await harness.CreateWorkspaceAsync(client, Guid.NewGuid().ToString("D"));
        using var limited = await harness.CreateWorkspaceAsync(client, Guid.NewGuid().ToString("D"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("rate_limited", (await limited.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
        Assert.Equal(1, await harness.CountWorkspacesAsync());
    }

    [PostgresFact]
    public async Task Configured_protected_request_limit_returns_a_safe_rate_limit_outcome()
    {
        await using var harness = await PostgresHarness.StartAsync(protectedRequestLimit: 1);
        using var client = harness.CreateClient();
        using var created = await harness.CreateWorkspaceAsync(client, Guid.NewGuid().ToString("D"));
        var cookie = CookieValue(Assert.Single(created.Headers.GetValues("Set-Cookie")));

        using var first = await harness.GetWorkspaceAsync(client, cookie);
        using var limited = await harness.GetWorkspaceAsync(client, cookie);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("rate_limited", (await limited.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    private static string CookieValue(string setCookie)
    {
        var cookie = setCookie.Split(';', 2)[0];
        return cookie[(cookie.IndexOf('=') + 1)..];
    }

    private static DateTimeOffset CookieExpiry(string setCookie)
    {
        var attribute = setCookie.Split(';').Single(part => part.TrimStart().StartsWith("expires=", StringComparison.OrdinalIgnoreCase));
        return DateTimeOffset.Parse(attribute[(attribute.IndexOf('=') + 1)..]);
    }

    private sealed class PostgresHarness : IAsyncDisposable
    {
        private readonly string _connectionString;
        private readonly string _schema;
        private readonly string _administrativeConnectionString;
        private readonly int _provisioningLimit;
        private readonly int _protectedRequestLimit;
        private readonly WebApplicationFactory<Program> _factory;

        private PostgresHarness(
            string connectionString,
            string schema,
            string administrativeConnectionString,
            int provisioningLimit,
            int protectedRequestLimit,
            WebApplicationFactory<Program> factory)
        {
            _connectionString = connectionString;
            _schema = schema;
            _administrativeConnectionString = administrativeConnectionString;
            _provisioningLimit = provisioningLimit;
            _protectedRequestLimit = protectedRequestLimit;
            _factory = factory;
        }

        public static async Task<PostgresHarness> StartAsync(int provisioningLimit = 50, int protectedRequestLimit = 100)
        {
            var administrativeConnectionString =
                Environment.GetEnvironmentVariable("TASK_MANAGER_TEST_CONNECTION_STRING")!;
            var schema = $"s01_{Guid.NewGuid():N}";
            await using (var admin = new NpgsqlConnection(administrativeConnectionString))
            {
                await admin.OpenAsync();
                await using var createSchema = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin);
                await createSchema.ExecuteNonQueryAsync();
            }

            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(administrativeConnectionString)
            {
                SearchPath = schema
            };
            var connectionString = connectionStringBuilder.ConnectionString;
            var migrationPath = Path.Combine(AppContext.BaseDirectory, "Migrations", "001_create_workspace_tables.sql");
            var migration = await File.ReadAllTextAsync(migrationPath);
            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(migration, connection);
                await command.ExecuteNonQueryAsync();
            }

            var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
            return new PostgresHarness(
                connectionString,
                schema,
                administrativeConnectionString,
                provisioningLimit,
                protectedRequestLimit,
                factory);
        }

        public HttpClient CreateClient()
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings__TaskManager"] = _connectionString,
                ["Security__AllowedOrigin"] = AllowedOrigin,
                ["AbuseControls__Provisioning__PermitLimit"] = _provisioningLimit.ToString(),
                ["AbuseControls__Provisioning__WindowSeconds"] = "60",
                ["AbuseControls__ProtectedRequests__PermitLimit"] = _protectedRequestLimit.ToString(),
                ["AbuseControls__ProtectedRequests__WindowSeconds"] = "60"
            };
            var previous = settings.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
            foreach (var (key, value) in settings)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            try
            {
                return _factory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    HandleCookies = false
                });
            }
            finally
            {
                foreach (var (key, value) in previous)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }

        public async Task<HttpResponseMessage> CreateWorkspaceAsync(
            HttpClient client,
            string initializationKey,
            string? cookie = null,
            string? origin = AllowedOrigin)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/workspaces");
            request.Headers.Add("X-Initialization-Key", initializationKey);
            if (origin is not null)
            {
                request.Headers.Add("Origin", origin);
            }

            if (cookie is not null)
            {
                request.Headers.Add("Cookie", $"{CredentialIssuer.CookieName}={cookie}");
            }

            return await client.SendAsync(request);
        }

        public async Task<HttpResponseMessage> GetWorkspaceAsync(HttpClient client, string? cookie)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/workspace");
            if (cookie is not null)
            {
                request.Headers.Add("Cookie", $"{CredentialIssuer.CookieName}={cookie}");
            }

            return await client.SendAsync(request);
        }

        public async Task<HttpResponseMessage> ConfirmWorkspaceAsync(HttpClient client, string cookie)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/workspace/confirm");
            request.Headers.Add("Origin", AllowedOrigin);
            request.Headers.Add("Cookie", $"{CredentialIssuer.CookieName}={cookie}");
            return await client.SendAsync(request);
        }

        public async Task ProvisionDuplicateWithCancelledTokenAsync(string initializationKey)
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var issuer = new CredentialIssuer();
            var store = _factory.Services.GetRequiredService<WorkspaceStore>();
            await store.ProvisionAsync(
                initializationKey,
                issuer.Issue(),
                DateTimeOffset.UtcNow.AddDays(30),
                cancellation.Token);
        }

        public async Task<HttpResponseMessage> RecordActivityAsync(HttpClient client, string cookie, Guid requestedWorkspaceId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/workspace/activity?workspaceId={requestedWorkspaceId}");
            request.Headers.Add("Origin", AllowedOrigin);
            request.Headers.Add("Cookie", $"{CredentialIssuer.CookieName}={cookie}");
            request.Content = JsonContent.Create(new { workspaceId = requestedWorkspaceId });
            return await client.SendAsync(request);
        }

        public async Task<int> CountWorkspacesAsync() =>
            await ExecuteScalarAsync<int>("SELECT count(*) FROM task_workspaces");

        public async Task<int> CountActiveCredentialsAsync() =>
            await ExecuteScalarAsync<int>("SELECT count(*) FROM workspace_credentials WHERE activated_at IS NOT NULL AND revoked_at IS NULL AND expires_at > clock_timestamp()");

        public async Task<byte[]> GetVerifierAsync() =>
            await ExecuteScalarAsync<byte[]>("SELECT verifier FROM workspace_credentials");

        public async Task<DateTimeOffset> GetCredentialExpiryAsync() =>
            await ExecuteScalarAsync<DateTimeOffset>("SELECT expires_at FROM workspace_credentials");

        public async Task<Guid> GetWorkspaceIdForCredentialAsync(string credential) =>
            await ExecuteScalarAsync<Guid>(
                "SELECT workspace_id FROM workspace_credentials WHERE verifier = @verifier",
                new NpgsqlParameter<byte[]>("verifier", SHA256.HashData(WebEncoders.Base64UrlDecode(credential))));

        public async Task<Guid?> ResolveWorkspaceAsync(string credential)
        {
            var verifier = SHA256.HashData(WebEncoders.Base64UrlDecode(credential));
            try
            {
                var store = _factory.Services.GetRequiredService<WorkspaceStore>();
                var workspace = await store.FindWorkspaceAsync(verifier, DateTimeOffset.UtcNow, CancellationToken.None);
                return workspace?.WorkspaceId;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(verifier);
            }
        }

        public async Task<DateTimeOffset> GetLastAccessedAsync(Guid workspaceId) =>
            await ExecuteScalarAsync<DateTimeOffset>(
                "SELECT last_accessed_at FROM task_workspaces WHERE id = @id",
                new NpgsqlParameter<Guid>("id", workspaceId));

        public async Task SetCredentialExpiredAsync(string credential) =>
            await ExecuteNonQueryAsync(
                "UPDATE workspace_credentials SET expires_at = clock_timestamp() - interval '1 second' WHERE verifier = @verifier",
                new NpgsqlParameter<byte[]>("verifier", SHA256.HashData(WebEncoders.Base64UrlDecode(credential))));

        public async Task RevokeCredentialAsync(string credential) =>
            await ExecuteNonQueryAsync(
                "UPDATE workspace_credentials SET revoked_at = clock_timestamp() WHERE verifier = @verifier",
                new NpgsqlParameter<byte[]>("verifier", SHA256.HashData(WebEncoders.Base64UrlDecode(credential))));

        private async Task<T> ExecuteScalarAsync<T>(string sql, params NpgsqlParameter[] parameters)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddRange(parameters);
            var value = (await command.ExecuteScalarAsync())!;
            if (value is long count && typeof(T) == typeof(int))
            {
                return (T)(object)checked((int)count);
            }

            if (value is DateTime timestamp && typeof(T) == typeof(DateTimeOffset))
            {
                return (T)(object)new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc));
            }

            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }

        private async Task ExecuteNonQueryAsync(string sql, params NpgsqlParameter[] parameters)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddRange(parameters);
            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _factory.DisposeAsync();
            await using var connection = new NpgsqlConnection(_administrativeConnectionString);
            await connection.OpenAsync();
            await using var dropSchema = new NpgsqlCommand($"DROP SCHEMA \"{_schema}\" CASCADE", connection);
            await dropSchema.ExecuteNonQueryAsync();
        }
    }
}
