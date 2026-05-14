using System.Net;
using System.Net.Http;
using Memora.Import.GitHub;

namespace Memora.Import.Tests.GitHub;

public sealed class GitHubApiAccountClientTests
{
    [Fact]
    public void ValidateToken_MissingToken_ReturnsTokenMissingError()
    {
        var client = new GitHubApiAccountClient(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var result = client.ValidateToken(string.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal("github.token.missing", result.Error?.Code);
    }

    [Fact]
    public void ValidateToken_Unauthorized_ReturnsTokenInvalidError()
    {
        var client = new GitHubApiAccountClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = client.ValidateToken("ghp_invalid");

        Assert.False(result.IsSuccess);
        Assert.Equal("github.token.invalid", result.Error?.Code);
    }

    [Fact]
    public void ValidateToken_Forbidden_ReturnsTokenForbiddenError()
    {
        var client = new GitHubApiAccountClient(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var result = client.ValidateToken("ghp_scoped_too_narrow");

        Assert.False(result.IsSuccess);
        Assert.Equal("github.token.forbidden", result.Error?.Code);
    }

    [Fact]
    public void ValidateToken_NetworkFailure_ReturnsHostUnreachableError()
    {
        var client = new GitHubApiAccountClient(_ => throw new HttpRequestException("DNS failure"));

        var result = client.ValidateToken("ghp_valid");

        Assert.False(result.IsSuccess);
        Assert.Equal("github.host.unreachable", result.Error?.Code);
    }

    [Fact]
    public void ValidateToken_Success_ReturnsAccount()
    {
        var client = new GitHubApiAccountClient(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"login":"octocat","name":"The Octocat"}""")
            });

        var result = client.ValidateToken("ghp_valid");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Account);
        Assert.Equal("octocat", result.Account!.Login);
        Assert.Equal("The Octocat", result.Account.Name);
    }

    [Fact]
    public void ValidateToken_UnexpectedBody_ReturnsResponseUnexpectedError()
    {
        var client = new GitHubApiAccountClient(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"unrelated":"value"}""")
            });

        var result = client.ValidateToken("ghp_valid");

        Assert.False(result.IsSuccess);
        Assert.Equal("github.response.unexpected", result.Error?.Code);
    }

    [Fact]
    public void ListRepositories_Success_ReturnsParsedEntries()
    {
        var client = new GitHubApiAccountClient(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    [
                      {
                        "full_name": "octocat/hello-world",
                        "html_url": "https://github.com/octocat/hello-world",
                        "clone_url": "https://github.com/octocat/hello-world.git",
                        "default_branch": "main",
                        "private": false,
                        "updated_at": "2026-05-12T10:00:00Z"
                      },
                      {
                        "full_name": "octocat/private-thing",
                        "html_url": "https://github.com/octocat/private-thing",
                        "clone_url": "https://github.com/octocat/private-thing.git",
                        "default_branch": "trunk",
                        "private": true,
                        "updated_at": "2026-05-13T11:11:11Z"
                      }
                    ]
                    """)
            });

        var result = client.ListRepositories("ghp_valid");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Repositories.Count);
        var first = result.Repositories[0];
        Assert.Equal("octocat", first.OwnerLogin);
        Assert.Equal("hello-world", first.Name);
        Assert.Equal("octocat/hello-world", first.FullName);
        Assert.False(first.IsPrivate);
        Assert.Equal("main", first.DefaultBranch);
        Assert.NotNull(first.UpdatedAtUtc);

        Assert.True(result.Repositories[1].IsPrivate);
        Assert.Equal("trunk", result.Repositories[1].DefaultBranch);
    }

    [Fact]
    public void ListRepositories_Forbidden_ReturnsTokenForbiddenError()
    {
        var client = new GitHubApiAccountClient(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var result = client.ListRepositories("ghp_valid");

        Assert.False(result.IsSuccess);
        Assert.Equal("github.token.forbidden", result.Error?.Code);
        Assert.Empty(result.Repositories);
    }

    [Fact]
    public void ListRepositories_InvalidJson_ReturnsInvalidJsonError()
    {
        var client = new GitHubApiAccountClient(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not json")
            });

        var result = client.ListRepositories("ghp_valid");

        Assert.False(result.IsSuccess);
        Assert.Equal("github.response.invalid_json", result.Error?.Code);
    }

    [Fact]
    public void Request_CarriesAuthorizationAndAcceptHeaders()
    {
        HttpRequestMessage? captured = null;
        var client = new GitHubApiAccountClient(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"login":"octocat"}""")
            };
        });

        client.ValidateToken("ghp_valid_token");

        Assert.NotNull(captured);
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("ghp_valid_token", captured.Headers.Authorization?.Parameter);
        Assert.Contains(captured.Headers.Accept, header => header.MediaType == "application/vnd.github+json");
        Assert.True(captured.Headers.Contains("X-GitHub-Api-Version"));
    }
}
