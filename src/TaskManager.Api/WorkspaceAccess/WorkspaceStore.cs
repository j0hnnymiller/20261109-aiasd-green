using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace TaskManager.Api.WorkspaceAccess;

public sealed class WorkspaceStore(NpgsqlDataSource dataSource)
{
    private const string InitializationConstraint = "uq_task_workspaces_initialization_key_hash";

    public async Task<ProvisioningResult> ProvisionAsync(
        string initializationKey,
        CredentialIssue credential,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var keyHash = SHA256.HashData(Encoding.UTF8.GetBytes(initializationKey));
        try
        {
            await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
            {
                await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
                {
                    try
                    {
                        var workspaceId = Guid.NewGuid();
                        await using (var workspaceCommand = new NpgsqlCommand(
                            """
                            INSERT INTO task_workspaces (id, initialization_key_hash)
                            VALUES (@id, @initialization_key_hash)
                            """,
                            connection,
                            transaction))
                        {
                            workspaceCommand.Parameters.AddWithValue("id", workspaceId);
                            workspaceCommand.Parameters.AddWithValue("initialization_key_hash", keyHash);
                            await workspaceCommand.ExecuteNonQueryAsync(cancellationToken);
                        }

                        await using (var credentialCommand = new NpgsqlCommand(
                            """
                            INSERT INTO workspace_credentials (workspace_id, verifier, expires_at)
                            VALUES (@workspace_id, @verifier, @expires_at)
                            """,
                            connection,
                            transaction))
                        {
                            credentialCommand.Parameters.AddWithValue("workspace_id", workspaceId);
                            credentialCommand.Parameters.AddWithValue("verifier", credential.Verifier);
                            credentialCommand.Parameters.AddWithValue("expires_at", expiresAt);
                            await credentialCommand.ExecuteNonQueryAsync(cancellationToken);
                        }

                        await transaction.CommitAsync(cancellationToken);
                        return ProvisioningResult.Created(credential.Credential, expiresAt);
                    }
                    catch (PostgresException exception)
                        when (exception.SqlState == PostgresErrorCodes.UniqueViolation
                              && exception.ConstraintName == InitializationConstraint)
                    {
                        await transaction.RollbackAsync(CancellationToken.None);
                    }
                }
            }

            await RevokeDuplicateInitializationAsync(keyHash, CancellationToken.None);
            return ProvisioningResult.Conflict();
        }
        catch (OperationCanceledException)
        {
            await RevokeDuplicateInitializationAsync(keyHash, CancellationToken.None);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyHash);
            CryptographicOperations.ZeroMemory(credential.Verifier);
        }
    }

    public async Task<WorkspaceAccess?> FindWorkspaceAsync(byte[] verifier, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT workspace_id, activated_at IS NOT NULL
            FROM workspace_credentials
            WHERE verifier = @verifier
              AND revoked_at IS NULL
              AND expires_at > @now
            """);
        command.Parameters.AddWithValue("verifier", verifier);
        command.Parameters.AddWithValue("now", now);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new WorkspaceAccess(reader.GetGuid(0), reader.GetBoolean(1));
    }

    public async Task<bool> ActivateAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            UPDATE workspace_credentials
            SET activated_at = clock_timestamp()
            WHERE workspace_id = @workspace_id
              AND activated_at IS NULL
              AND revoked_at IS NULL
              AND expires_at > clock_timestamp()
            """);
        command.Parameters.AddWithValue("workspace_id", workspaceId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 1)
        {
            return true;
        }

        await using var verification = dataSource.CreateCommand(
            """
            SELECT EXISTS (
                SELECT 1
                FROM workspace_credentials
                WHERE workspace_id = @workspace_id
                  AND activated_at IS NOT NULL
                  AND revoked_at IS NULL
                  AND expires_at > clock_timestamp()
            )
            """);
        verification.Parameters.AddWithValue("workspace_id", workspaceId);
        return (bool)(await verification.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<bool> RecordActivityAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            UPDATE task_workspaces
            SET last_accessed_at = clock_timestamp()
            WHERE id = @workspace_id
              AND EXISTS (
                  SELECT 1
                  FROM workspace_credentials
                  WHERE workspace_id = task_workspaces.id
                    AND activated_at IS NOT NULL
                    AND revoked_at IS NULL
                    AND expires_at > clock_timestamp()
              )
            """);
        command.Parameters.AddWithValue("workspace_id", workspaceId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private async Task RevokeDuplicateInitializationAsync(byte[] keyHash, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            UPDATE workspace_credentials
            SET revoked_at = clock_timestamp()
            WHERE workspace_id = (
                SELECT id
                FROM task_workspaces
                WHERE initialization_key_hash = @initialization_key_hash
            )
              AND revoked_at IS NULL
              AND activated_at IS NULL
            """);
        command.Parameters.AddWithValue("initialization_key_hash", keyHash);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed record ProvisioningResult(bool WasCreated, string? Credential, DateTimeOffset? ExpiresAt)
{
    public static ProvisioningResult Created(string credential, DateTimeOffset expiresAt) =>
        new(true, credential, expiresAt);

    public static ProvisioningResult Conflict() => new(false, null, null);
}

public sealed record WorkspaceAccess(Guid WorkspaceId, bool IsActivated);
