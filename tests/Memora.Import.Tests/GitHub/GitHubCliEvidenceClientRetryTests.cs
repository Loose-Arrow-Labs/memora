using Memora.Import.GitHub;

namespace Memora.Import.Tests.GitHub;

public sealed class GitHubCliEvidenceClientRetryTests
{
    [Fact]
    public void Fetch_TimeoutDiagnostic_IsStructuredAndActionable()
    {
        var client = new GitHubCliEvidenceClient(
            (args, _) =>
            {
                if (args.SequenceEqual(["auth", "status"]))
                {
                    return GitHubCliEvidenceClient.GhCommandResult.Succeeded(string.Empty);
                }

                return GitHubCliEvidenceClient.GhCommandResult.Failed("GitHub CLI command timed out after 5s.");
            },
            timeoutMs: 5_000,
            maxRetries: 0);

        var result = client.Fetch("https://github.com/owner/repo.git", 10);

        Assert.Contains(result.Diagnostics, d =>
            d.Code is "github.import.partial" or "github.rate_limited" or "github.private_denied");
    }

    [Fact]
    public void Fetch_TransientFailure_RetriesUpToMaxRetries()
    {
        var callCount = 0;
        var client = new GitHubCliEvidenceClient(
            (args, _) =>
            {
                if (args.SequenceEqual(["auth", "status"]))
                {
                    return GitHubCliEvidenceClient.GhCommandResult.Succeeded(string.Empty);
                }

                callCount++;
                return callCount >= 3
                    ? GitHubCliEvidenceClient.GhCommandResult.Succeeded("[]")
                    : GitHubCliEvidenceClient.GhCommandResult.Failed("transient connection error");
            },
            timeoutMs: 5_000,
            maxRetries: 3);

        var result = client.Fetch("https://github.com/owner/repo.git", 10);

        Assert.True(result.IsSuccess || result.Diagnostics.All(d => d.Severity != GitHubImportDiagnosticSeverity.Error));
        Assert.True(callCount >= 3);
    }

    [Fact]
    public void Fetch_RateLimitError_DoesNotRetry()
    {
        var callsByEndpoint = new Dictionary<string, int>(StringComparer.Ordinal);
        var client = new GitHubCliEvidenceClient(
            (args, _) =>
            {
                if (args.SequenceEqual(["auth", "status"]))
                {
                    return GitHubCliEvidenceClient.GhCommandResult.Succeeded(string.Empty);
                }

                var key = string.Join(" ", args);
                callsByEndpoint[key] = callsByEndpoint.GetValueOrDefault(key) + 1;
                return GitHubCliEvidenceClient.GhCommandResult.Failed("API rate limit exceeded for user.");
            },
            timeoutMs: 5_000,
            maxRetries: 3);

        client.Fetch("https://github.com/owner/repo.git", 10);

        Assert.All(callsByEndpoint.Values, count => Assert.Equal(1, count));
    }

    [Fact]
    public void Constructor_ConfigurableTimeout_IsAccepted()
    {
        var receivedTimeout = 0;
        var client = new GitHubCliEvidenceClient(
            (args, timeout) =>
            {
                receivedTimeout = timeout;
                return args.SequenceEqual(["auth", "status"])
                    ? GitHubCliEvidenceClient.GhCommandResult.Failed("not authed")
                    : GitHubCliEvidenceClient.GhCommandResult.Succeeded("[]");
            },
            timeoutMs: 60_000);

        client.Fetch("https://github.com/owner/repo.git", 10);

        Assert.Equal(60_000, receivedTimeout);
    }
}
