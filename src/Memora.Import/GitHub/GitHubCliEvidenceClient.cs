using System.Diagnostics;
using System.Text.Json;

namespace Memora.Import.GitHub;

public sealed class GitHubCliEvidenceClient : IGitHubEvidenceClient
{
    public GitHubEvidenceClientResult Fetch(string remoteUrl, int maxItems)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteUrl);

        if (!TryParseGitHubRemote(remoteUrl, out var owner, out var repo))
        {
            return GitHubEvidenceClientResult.Failed(
                GitHubImportDiagnostic.Error(
                    "github.remote.unsupported",
                    "Only github.com repository remotes can be imported.",
                    "remote_url"));
        }

        var auth = RunGh(["auth", "status"]);
        if (!auth.IsSuccess)
        {
            return GitHubEvidenceClientResult.Failed(
                GitHubImportDiagnostic.Error(
                    "github.credentials.missing",
                    "GitHub CLI authentication is required before importing GitHub evidence.",
                    "gh auth status"));
        }

        var diagnostics = new List<GitHubImportDiagnostic>();
        var pageSize = Math.Clamp(maxItems, 1, 100);

        var issues = FetchArray($"/repos/{owner}/{repo}/issues?state=all&per_page={pageSize}", pageSize, diagnostics)
            .Where(element => !element.TryGetProperty("pull_request", out _))
            .Select(ReadIssue)
            .Take(maxItems)
            .ToArray();
        var pullRequests = FetchArray($"/repos/{owner}/{repo}/pulls?state=all&per_page={pageSize}", pageSize, diagnostics)
            .Select(ReadPullRequest)
            .Take(maxItems)
            .ToArray();
        var reviewComments = FetchArray($"/repos/{owner}/{repo}/pulls/comments?per_page={pageSize}", pageSize, diagnostics)
            .Select(ReadReviewComment)
            .Take(maxItems)
            .ToArray();
        var commits = FetchArray($"/repos/{owner}/{repo}/commits?per_page={pageSize}", pageSize, diagnostics)
            .Select(ReadCommit)
            .Take(maxItems)
            .ToArray();
        var releases = FetchArray($"/repos/{owner}/{repo}/releases?per_page={pageSize}", pageSize, diagnostics)
            .Select(ReadRelease)
            .Take(maxItems)
            .ToArray();
        var reviews = pullRequests
            .SelectMany(pullRequest => FetchArray($"/repos/{owner}/{repo}/pulls/{pullRequest.Number}/reviews?per_page={pageSize}", pageSize, diagnostics)
                .Select(review => ReadReview(pullRequest.Number, review)))
            .Take(maxItems)
            .ToArray();

        diagnostics.Add(
            GitHubImportDiagnostic.Info(
                "github.discussions.not_available",
                "GitHub discussion linkage is not available through the current CLI evidence fetch and was skipped.",
                "discussions"));

        var isPartial = diagnostics.Any(diagnostic => diagnostic.Code == "github.import.partial");

        return GitHubEvidenceClientResult.Succeeded(
            new GitHubEvidenceSnapshot(
                issues,
                pullRequests,
                reviews,
                reviewComments,
                commits,
                releases,
                [],
                isPartial,
                diagnostics));
    }

    private static IReadOnlyList<JsonElement> FetchArray(
        string endpoint,
        int pageSize,
        ICollection<GitHubImportDiagnostic> diagnostics)
    {
        var result = RunGh(["api", endpoint]);
        if (!result.IsSuccess)
        {
            diagnostics.Add(MapGhError(result.Error, endpoint));
            return [];
        }

        using var document = JsonDocument.Parse(result.Output);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(GitHubImportDiagnostic.Warning("github.response.unexpected", $"GitHub response for '{endpoint}' was not an array.", endpoint));
            return [];
        }

        var elements = document.RootElement.EnumerateArray().Select(element => element.Clone()).ToArray();
        if (elements.Length >= pageSize)
        {
            diagnostics.Add(
                GitHubImportDiagnostic.Warning(
                    "github.import.partial",
                    $"GitHub endpoint '{endpoint}' may have more results than the bounded import fetched.",
                    endpoint));
        }

        return elements;
    }

    private static GitHubIssueEvidence ReadIssue(JsonElement element) =>
        new(
            ReadInt(element, "number"),
            ReadString(element, "html_url"),
            ReadString(element, "title"),
            ReadString(element, "state"),
            ReadDateTime(element, "created_at"),
            ReadDateTime(element, "updated_at"));

    private static GitHubPullRequestEvidence ReadPullRequest(JsonElement element) =>
        new(
            ReadInt(element, "number"),
            ReadString(element, "html_url"),
            ReadString(element, "title"),
            ReadString(element, "state"),
            ReadOptionalString(element, "merge_commit_sha"),
            ReadDateTime(element, "created_at"),
            ReadDateTime(element, "updated_at"));

    private static GitHubReviewEvidence ReadReview(int pullRequestNumber, JsonElement element) =>
        new(
            pullRequestNumber,
            ReadInt64(element, "id").ToString("D"),
            ReadString(element, "html_url"),
            ReadString(element, "state"),
            ReadNestedOptionalString(element, "user", "login"),
            ReadDateTime(element, "submitted_at"));

    private static GitHubReviewCommentEvidence ReadReviewComment(JsonElement element)
    {
        var pullRequestUrl = ReadString(element, "pull_request_url");
        return new GitHubReviewCommentEvidence(
            TryReadLastPathSegmentAsInt(pullRequestUrl, out var pullRequestNumber) ? pullRequestNumber : 0,
            ReadInt64(element, "id").ToString("D"),
            ReadString(element, "html_url"),
            ReadOptionalString(element, "path"),
            ReadDateTime(element, "created_at"),
            ReadDateTime(element, "updated_at"));
    }

    private static GitHubCommitEvidence ReadCommit(JsonElement element) =>
        new(
            ReadString(element, "sha"),
            ReadString(element, "html_url"),
            ReadNestedString(element, "commit", "message"),
            ReadNestedOptionalString(element, "commit", "author", "name"),
            ReadNestedDateTime(element, "commit", "author", "date"));

    private static GitHubReleaseEvidence ReadRelease(JsonElement element) =>
        new(
            ReadInt64(element, "id").ToString("D"),
            ReadString(element, "html_url"),
            ReadOptionalString(element, "name") ?? ReadString(element, "tag_name"),
            ReadString(element, "tag_name"),
            TryReadDateTime(element, "published_at", out var publishedAtUtc) ? publishedAtUtc : null);

    private static GitHubImportDiagnostic MapGhError(string error, string endpoint)
    {
        if (error.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            return GitHubImportDiagnostic.Error("github.rate_limited", "GitHub rate limit prevented evidence import.", endpoint);
        }

        if (error.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("Resource not accessible", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("Bad credentials", StringComparison.OrdinalIgnoreCase))
        {
            return GitHubImportDiagnostic.Error("github.private_denied", "GitHub repository access was denied or unavailable.", endpoint);
        }

        return GitHubImportDiagnostic.Warning("github.import.partial", $"GitHub endpoint '{endpoint}' could not be imported: {error}", endpoint);
    }

    private static bool TryParseGitHubRemote(string remoteUrl, out string owner, out string repo)
    {
        owner = string.Empty;
        repo = string.Empty;
        var trimmed = remoteUrl.Trim();
        string path;

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            path = uri.AbsolutePath.Trim('/');
        }
        else if (trimmed.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
        {
            path = trimmed["git@github.com:".Length..];
        }
        else
        {
            return false;
        }

        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^4];
        }

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        owner = parts[0];
        repo = parts[1];
        return true;
    }

    private static GhCommandResult RunGh(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo("gh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return GhCommandResult.Failed("Unable to start gh.");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        if (!process.WaitForExit(30000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            return GhCommandResult.Failed("GitHub CLI command timed out.");
        }

        return process.ExitCode == 0
            ? GhCommandResult.Succeeded(output)
            : GhCommandResult.Failed(string.IsNullOrWhiteSpace(error) ? output : error);
    }

    private static string ReadString(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).GetString() ?? string.Empty;

    private static string? ReadOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int ReadInt(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).GetInt32();

    private static long ReadInt64(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName).GetInt64();

    private static DateTimeOffset ReadDateTime(JsonElement element, string propertyName) =>
        DateTimeOffset.Parse(ReadString(element, propertyName));

    private static bool TryReadDateTime(JsonElement element, string propertyName, out DateTimeOffset value)
    {
        value = default;
        var text = ReadOptionalString(element, propertyName);
        return !string.IsNullOrWhiteSpace(text) && DateTimeOffset.TryParse(text, out value);
    }

    private static string ReadNestedString(JsonElement element, params string[] path) =>
        ReadNested(element, path).GetString() ?? string.Empty;

    private static string? ReadNestedOptionalString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static DateTimeOffset ReadNestedDateTime(JsonElement element, params string[] path) =>
        DateTimeOffset.Parse(ReadNestedString(element, path));

    private static JsonElement ReadNested(JsonElement element, IReadOnlyList<string> path)
    {
        var current = element;
        foreach (var segment in path)
        {
            current = current.GetProperty(segment);
        }

        return current;
    }

    private static bool TryReadLastPathSegmentAsInt(string url, out int value)
    {
        value = 0;
        var segment = url.TrimEnd('/').Split('/').LastOrDefault();
        return int.TryParse(segment, out value);
    }

    private sealed record GhCommandResult(bool IsSuccess, string Output, string Error)
    {
        public static GhCommandResult Succeeded(string output) => new(true, output, string.Empty);

        public static GhCommandResult Failed(string error) => new(false, string.Empty, error.Trim());
    }
}
