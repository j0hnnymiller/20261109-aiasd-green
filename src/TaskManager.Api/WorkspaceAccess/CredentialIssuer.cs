using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace TaskManager.Api.WorkspaceAccess;

public sealed class CredentialIssuer
{
    public const string CookieName = "__Host-taskspace";
    private const int CredentialLength = 32;

    public CredentialIssue Issue()
    {
        var bytes = RandomNumberGenerator.GetBytes(CredentialLength);
        var credential = WebEncoders.Base64UrlEncode(bytes);
        var verifier = SHA256.HashData(bytes);
        CryptographicOperations.ZeroMemory(bytes);
        return new CredentialIssue(credential, verifier);
    }

    public static bool TryGetVerifier(string? credential, out byte[] verifier)
    {
        verifier = [];
        if (string.IsNullOrEmpty(credential))
        {
            return false;
        }

        try
        {
            var bytes = WebEncoders.Base64UrlDecode(credential);
            if (bytes.Length != CredentialLength)
            {
                CryptographicOperations.ZeroMemory(bytes);
                return false;
            }

            verifier = SHA256.HashData(bytes);
            CryptographicOperations.ZeroMemory(bytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record CredentialIssue(string Credential, byte[] Verifier);
