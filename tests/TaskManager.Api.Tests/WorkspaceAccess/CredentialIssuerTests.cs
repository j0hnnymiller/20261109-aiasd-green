using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using TaskManager.Api.WorkspaceAccess;

namespace TaskManager.Api.Tests.WorkspaceAccess;

public sealed class CredentialIssuerTests
{
    [Fact]
    public void Issue_returns_an_opaque_credential_and_only_its_verifier()
    {
        var issuer = new CredentialIssuer();

        var issued = issuer.Issue();

        var rawCredential = WebEncoders.Base64UrlDecode(issued.Credential);
        Assert.Equal(32, rawCredential.Length);
        Assert.Equal(SHA256.HashData(rawCredential), issued.Verifier);
        Assert.NotEqual(Encoding.UTF8.GetBytes(issued.Credential), issued.Verifier);
    }

    [Fact]
    public void Issue_generates_a_different_credential_for_each_workspace()
    {
        var issuer = new CredentialIssuer();

        var first = issuer.Issue();
        var second = issuer.Issue();

        Assert.NotEqual(first.Credential, second.Credential);
        Assert.NotEqual(first.Verifier, second.Verifier);
    }
}
