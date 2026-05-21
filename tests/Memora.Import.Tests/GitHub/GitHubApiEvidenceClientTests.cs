using System.Net;
using System.Net.Http;
using Memora.Import.GitHub;

namespace Memora.Import.Tests.GitHub;

public sealed class GitHubApiEvidenceClientTests
{
    [Fact]
    public void Fetch_UnsupportedRemote_ReturnsUnsupportedError()
    {
        var client = new GitHubApiEvidenceClient(_ => new HttpResponseMessage(HttpStatusCode.OK), "ghp_valid");

        var result = client.Fetch("https://gitlab.com/owner/repo.git", 10);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "github.remote.unsupported");
    }

    [Fact]
    public void Fetch_Unauthorized_ReturnsCredentialsMissingError()
    {
        var client = new GitHubApiEvidenceClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized), "ghp_invalid");

        var result = client.Fetch("https://github.com/owner/repo.git", 10);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "github.credentials.missing");
    }

    [Fact]
    public void Fetch_HappyPath_ReturnsFullSnapshot()
    {
        var responsesByPath = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/repos/owner/repo/issues"] = """
                [
                  {
                    "number": 1,
                    "html_url": "https://github.com/owner/repo/issues/1",
                    "title": "First issue",
                    "state": "open",
                    "created_at": "2026-05-01T10:00:00Z",
                    "updated_at": "2026-05-02T10:00:00Z"
                  }
                ]
                """,
            ["/repos/owner/repo/pulls"] = """
                [
                  {
                    "number": 7,
                    "html_url": "https://github.com/owner/repo/pull/7",
                    "title": "Add docs",
                    "state": "open",
                    "merge_commit_sha": null,
                    "merged_at": null,
                    "created_at": "2026-05-03T10:00:00Z",
                    "updated_at": "2026-05-04T10:00:00Z"
                  }
                ]
                """,
            ["/repos/owner/repo/commits"] = """
                [
                  {
                    "sha": "abc1234567",
                    "html_url": "https://github.com/owner/repo/commit/abc1234567",
                    "commit": {
                      "message": "Initial commit",
                      "author": {
                        "name": "Alex",
                        "date": "2026-04-30T10:00:00Z"
                      }
                    }
                  }
                ]
                """,
            ["/repos/owner/repo/releases"] = """
                [
                  {
                    "id": 12345,
                    "html_url": "https://github.com/owner/repo/releases/tag/v1.0.0",
                    "tag_name": "v1.0.0",
                    "name": "Release 1.0.0",
                    "published_at": "2026-04-15T10:00:00Z"
                  }
                ]
                """
        };

        var client = new GitHubApiEvidenceClient(
            request =>
            {
                var path = request.RequestUri!.AbsolutePath;
                if (!responsesByPath.TryGetValue(path, out var body))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body)
                };
            },
            "ghp_valid");

        var result = client.Fetch("https://github.com/owner/repo.git", 10);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Snapshot);
        Assert.Single(result.Snapshot!.Issues);
        Assert.Single(result.Snapshot.PullRequests);
        Assert.Single(result.Snapshot.Commits);
        Assert.Single(result.Snapshot.Releases);
        Assert.Empty(result.Snapshot.Reviews);
        Assert.Empty(result.Snapshot.ReviewComments);
        Assert.Empty(result.Snapshot.Discussions);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "github.api.scope.partial");
    }

    [Fact]
    public void Fetch_FiltersPullRequestsOutOfIssuesArray()
    {
        var issuesBody = """
            [
              {
                "number": 1,
                "html_url": "https://github.com/owner/repo/issues/1",
                "title": "Real issue",
                "state": "open",
                "created_at": "2026-05-01T10:00:00Z",
                "updated_at": "2026-05-02T10:00:00Z"
              },
              {
                "number": 2,
                "html_url": "https://github.com/owner/repo/pull/2",
                "title": "PR not issue",
                "state": "open",
                "created_at": "2026-05-01T10:00:00Z",
                "updated_at": "2026-05-02T10:00:00Z",
                "pull_request": {"url": "https://github.com/owner/repo/pulls/2"}
              }
            ]
            """;

        var client = new GitHubApiEvidenceClient(
            request =>
            {
                var path = request.RequestUri!.AbsolutePath;
                if (path == "/repos/owner/repo/issues")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(issuesBody)
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]")
                };
            },
            "ghp_valid");

        var result = client.Fetch("https://github.com/owner/repo.git", 10);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Snapshot);
        var issue = Assert.Single(result.Snapshot!.Issues);
        Assert.Equal(1, issue.Number);
    }

    [Fact]
    public void Fetch_PageSizeLimit_AddsPartialDiagnostic()
    {
        var pageOfFifty = "[" + string.Join(",", Enumerable.Range(1, 50).Select(BuildIssueJson)) + "]";
        var client = new GitHubApiEvidenceClient(
            request =>
            {
                if (request.RequestUri!.AbsolutePath == "/repos/owner/repo/issues")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(pageOfFifty)
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]")
                };
            },
            "ghp_valid");

        var result = client.Fetch("https://github.com/owner/repo.git", 50);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Snapshot);
        Assert.Equal(50, result.Snapshot!.Issues.Count);
        Assert.Contains(
            result.Diagnostics,
            diagnostic => diagnostic.Code == "github.import.partial");
    }

    private static string BuildIssueJson(int number) =>
        $$"""
            {
              "number": {{number}},
              "html_url": "https://github.com/owner/repo/issues/{{number}}",
              "title": "Issue {{number}}",
              "state": "open",
              "created_at": "2026-05-01T10:00:00Z",
              "updated_at": "2026-05-02T10:00:00Z"
            }
            """;
}
