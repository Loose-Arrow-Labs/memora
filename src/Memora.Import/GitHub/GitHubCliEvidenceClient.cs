using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Memora.Import.GitHub;

public sealed class GitHubCliEvidenceClient : IGitHubEvidenceClient
{
    private readonly Func<IReadOnlyList<string>, GhCommandResult> _runGh;

    public GitHubCliEvidenceClient()
        : this(RunGh)
    {
    }

    internal GitHubCliEvidenceClient(Func<IReadOnlyList<string>, GhCommandResult> runGh)
    {
        _runGh = runGh ?? throw new ArgumentNullException(nameof(runGh));
    }

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

        var auth = _runGh(["auth", "status"]);
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
        var issueElements = FetchArray($"/repos/{owner}/{repo}/issues?state=all&per_page={pageSize}", pageSize, diagnostics, _runGh)
            .Where(element => !element.TryGetProperty("pull_request", out _))
            .ToArray();

        var issues = ReadRecords(issueElements, "issues", diagnostics, ReadIssue)
            .Take(maxItems)
            .ToArray();
        var pullRequests = ReadRecords(
                FetchArray($"/repos/{owner}/{repo}/pulls?state=all&per_page={pageSize}", pageSize, diagnostics, _runGh),
                "pulls",
                diagnostics,
                ReadPullRequest)
            .Take(maxItems)
            .ToArray();
        var reviewComments = ReadRecords(
                FetchArray($"/repos/{owner}/{repo}/pulls/comments?per_page={pageSize}", pageSize, diagnostics, _runGh),
                "pulls/comments",
                diagnostics,
                ReadReviewComment)
            .Take(maxItems)
            .ToArray();
        var commits = ReadRecords(
                FetchArray($"/repos/{owner}/{repo}/commits?per_page={pageSize}", pageSize, diagnostics, _runGh),
                "commits",
                diagnostics,
                ReadCommit)
            .Take(maxItems)
            .ToArray();
        var releases = ReadRecords(
                FetchArray($"/repos/{owner}/{repo}/releases?per_page={pageSize}", pageSize, diagnostics, _runGh),
                "releases",
                diagnostics,
                ReadRelease)
            .Take(maxItems)
            .ToArray();
        var reviews = pullRequests
            .SelectMany(pullRequest => ReadRecords(
                FetchArray($"/repos/{owner}/{repo}/pulls/{pullRequest.Number}/reviews?per_page={pageSize}", pageSize, diagnostics, _runGh),
                $"pulls/{pullRequest.Number}/reviews",
                diagnostics,
                (review, path, currentDiagnostics) => ReadReview(pullRequest.Number, review, path, currentDiagnostics)))
            .Take(maxItems)
            .ToArray();

        diagnostics.Add(
            GitHubImportDiagnostic.Info(
                "github.discussions.not_available",
                "GitHub discussion linkage is not available through the current CLI evidence fetch and was skipped.",
                "discussions"));

        var isPartial = diagnostics.Any(diagnostic =>
            diagnostic.Code is "github.import.partial" or
                "github.response.field.missing" or
                "github.response.field.invalid" or
                "github.response.record_skipped" or
                "github.response.invalid_json" or
                "github.response.unexpected");

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
        ICollection<GitHubImportDiagnostic> diagnostics,
        Func<IReadOnlyList<string>, GhCommandResult> runGh)
    {
        var result = runGh(["api", endpoint]);
        if (!result.IsSuccess)
        {
            diagnostics.Add(MapGhError(result.Error, endpoint));
            return [];
        }

        try
        {
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
        catch (JsonException exception)
        {
            diagnostics.Add(
                GitHubImportDiagnostic.Warning(
                    "github.response.invalid_json",
                    $"GitHub response for '{endpoint}' could not be parsed as JSON and was skipped: {exception.Message}",
                    endpoint));
            return [];
        }
    }

    private static GitHubIssueEvidence? ReadIssue(
        JsonElement element,
        string path,
        ICollection<GitHubImportDiagnostic> diagnostics)
    {
        var valid = true;
        valid &= TryReadInt(element, "number", path, diagnostics, out var number);
        valid &= TryReadString(element, "html_url", path, diagnostics, out var url);
        valid &= TryReadString(element, "title", path, diagnostics, out var title);
        valid &= TryReadString(element, "state", path, diagnostics, out var state);
        valid &= TryReadDateTime(element, "created_at", path, diagnostics, out var createdAtUtc);
        valid &= TryReadDateTime(element, "updated_at", path, diagnostics, out var updatedAtUtc);

        return valid
            ? new GitHubIssueEvidence(number, url, title, state, createdAtUtc, updatedAtUtc)
            : null;
    }

    private static GitHubPullRequestEvidence? ReadPullRequest(
        JsonElement element,
        string path,
        ICollection<GitHubImportDiagnostic> diagnostics)
    {
        var valid = true;
        valid &= TryReadInt(element, "number", path, diagnostics, out var number);
        valid &= TryReadString(element, "html_url", path, diagnostics, out var url);
        valid &= TryReadString(element, "title", path, diagnostics, out var title);
        valid &= TryReadString(element, "state", path, diagnostics, out var state);
        valid &= TryReadOptionalString(element, "merge_commit_sha", path, diagnostics, out var mergeCommitSha);
        valid &= TryReadOptionalDateTime(element, "merged_at", path, diagnostics, out var mergedAtUtc);
        valid &= TryReadDateTime(element, "created_at", path, diagnostics, out var createdAtUtc);
        valid &= TryReadDateTime(element, "updated_at", path, diagnostics, out var updatedAtUtc);

        return valid
            ? new GitHubPullRequestEvidence(number, url, title, state, mergeCommitSha, mergedAtUtc, createdAtUtc, updatedAtUtc)
            : null;
    }

    private static GitHubReviewEvidence? ReadReview(
        int pullRequestNumber,
        JsonElement element,
        string path,
        ICollection<GitHubImportDiagnostic> diagnostics)
    {
        var valid = true;
        valid &= TryReadInt64(element, "id", path, diagnostics, out var id);
        valid &= TryReadString(element, "html_url", path, diagnostics, out var url);
        valid &= TryReadString(element, "state", path, diagnostics, out var state);
        valid &= TryReadNestedOptionalString(element, ["user", "login"], out var author);
        valid &= TryReadOptionalDateTime(element, "submitted_at", path, diagnostics, out var submittedAtUtc);

        return valid
            ? new GitHubReviewEvidence(pullRequestNumber, id.ToString("D"), url, state, author, submittedAtUtc)
            : null;
    }

    private static GitHubReviewCommentEvidence? ReadReviewComment(
        JsonElement element,
        string path,
        ICollection<GitHubImportDiagnostic> diagnostics)
    {
        var valid = true;
        var pullRequestNumber = 0;
        valid &= TryReadString(element, "pull_request_url", path, diagnostics, out var pullRequestUrl);
        if (valid && !TryReadLastPathSegmentAsInt(pullRequestUrl, out pullRequestNumber))
        {
            AddInvalidFieldDiagnostic(diagnostics, $"{path}.pull_request_url", "pull request URL ending in a numeric PR number");
            valid = false;
        }

        valid &= TryReadInt64(element, "id", path, diagnostics, out var id);
        valid &= TryReadString(element, "html_url", path, diagnostics, out var url);
        valid &= TryReadOptionalString(element, "path", path, diagnostics, out var commentPath);
        valid &= TryReadDateTime(element, "created_at", path, diagnostics, out var createdAtUtc);
        valid &= TryReadDateTime(element, "updated_at", path, diagnostics, out var updatedAtUtc);

        return valid
            ? new GitHubReviewCommentEvidence(pullRequestNumber, id.ToString("D"), url, commentPath, createdAtUtc, updatedAtUtc)
            : null;
    }

    private static GitHubCommitEvidence? ReadCommit(
        JsonElement element,
        string path,
        ICollection<GitHubImportDiagnostic> diagnostics)
    {
        var valid = true;
        valid &= TryReadString(element, "sha", path, diagnostics, out var sha);
        valid &= TryReadString(element, "html_url", path, diagnostics, out var url);
        valid &= TryReadNestedString(element, path, diagnostics, out var message, "commit", "message");
        valid &= TryReadNestedOptionalString(element, ["commit", "author", "name"], out var author);
        valid &= TryReadNestedDateTime(element, path, diagnostics, out var authoredAtUtc, "commit", "author", "date");

        return valid
            ? new GitHubCommitEvidence(sha, url, message, author, authoredAtUtc)
            : null;
    }

    private static GitHubReleaseEvidence? ReadRelease(
        JsonElement element,
        string path,
        ICollection<GitHubImportDiagnostic> diagnostics)
    {
        var valid = true;
        valid &= TryReadInt64(element, "id", path, diagnostics, out var id);
        valid &= TryReadString(element, "html_url", path, diagnostics, out var url);
        valid &= TryReadString(element, "tag_name", path, diagnostics, out var tagName);
        valid &= TryReadOptionalString(element, "name", path, diagnostics, out var name);
        valid &= TryReadOptionalDateTime(element, "published_at", path, diagnostics, out var publishedAtUtc);

        return valid
            ? new GitHubReleaseEvidence(id.ToString("D"), url, name ?? tagName, tagName, publishedAtUtc)
            : null;
    }

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

    private static IReadOnlyList<T> ReadRecords<T>(
        IEnumerable<JsonElement> elements,
        string collectionPath,
        ICollection<GitHubImportDiagnostic> diagnostics,
        Func<JsonElement, string, ICollection<GitHubImportDiagnostic>, T?> read)
        where T : class
    {
        var records = new List<T>();
        var index = 0;

        foreach (var element in elements)
        {
            var path = $"{collectionPath}[{index}]";
            index++;

            try
            {
                var record = read(element, path, diagnostics);
                if (record is not null)
                {
                    records.Add(record);
                }
            }
            catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
            {
                diagnostics.Add(
                    GitHubImportDiagnostic.Warning(
                        "github.response.record_skipped",
                        $"GitHub record '{path}' could not be parsed and was skipped: {exception.Message}",
                        path));
            }
        }

        return records;
    }

    private static bool TryReadString(
        JsonElement element,
        string propertyName,
        string recordPath,
        ICollection<GitHubImportDiagnostic> diagnostics,
        out string value)
    {
        value = string.Empty;
        if (!TryReadProperty(element, propertyName, recordPath, diagnostics, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }

        AddInvalidFieldDiagnostic(diagnostics, $"{recordPath}.{propertyName}", "string");
        return false;
    }

    private static bool TryReadOptionalString(
        JsonElement element,
        string propertyName,
        string recordPath,
        ICollection<GitHubImportDiagnostic> diagnostics,
        out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return true;
        }

        AddInvalidFieldDiagnostic(diagnostics, $"{recordPath}.{propertyName}", "string or null");
        return false;
    }

    private static bool TryReadInt(
        JsonElement element,
        string propertyName,
        string recordPath,
        ICollection<GitHubImportDiagnostic> diagnostics,
        out int value)
    {
        value = 0;
        if (!TryReadProperty(element, propertyName, recordPath, diagnostics, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value))
        {
            return true;
        }

        AddInvalidFieldDiagnostic(diagnostics, $"{recordPath}.{propertyName}", "integer");
        return false;
    }

    private static bool TryReadInt64(
        JsonElement element,
        string propertyName,
        string recordPath,
        ICollection<GitHubImportDiagnostic> diagnostics,
        out long value)
    {
        value = 0;
        if (!TryReadProperty(element, propertyName, recordPath, diagnostics, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out value))
        {
            return true;
        }

        AddInvalidFieldDiagnostic(diagnostics, $"{recordPath}.{propertyName}", "integer");
        return false;
    }

    private static bool TryReadDateTime(
        JsonElement element,
        string propertyName,
        string recordPath,
        ICollection<GitHubImportDiagnostic> diagnostics,
        out DateTimeOffset value)
    {
        value = default;
        return TryReadString(element, propertyName, recordPath, diagnostics, out var text) &&
               TryParseDateTime(text, $"{recordPath}.{propertyName}", diagnostics, out value);
    }

    private static bool TryReadOptionalDateTime(
        JsonElement element,
        string propertyName,
        string recordPath,
        ICollection<GitHubImportDiagnostic> diagnostics,
        out DateTimeOffset? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            AddInvalidFieldDiagnostic(diagnostics, $"{recordPath}.{propertyName}", "date-time string or null");
            return false;
        }

        var text = property.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (TryParseDateTime(text, $"{recordPath}.{propertyName}", diagnostics, out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static bool TryReadNestedString(
        JsonElement element,
        string recordPath,
        ICollection<GitHubImportDiagnostic> diagnostics,
        out string value,
        params string[] path)
    {
        value = string.Empty;
        if (!TryReadNestedProperty(element, path, recordPath, diagnostics, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }

        AddInvalidFieldDiagnostic(diagnostics, BuildPath(recordPath, path), "string");
        return false;
    }

    private static bool TryReadNestedOptionalString(
        JsonElement element,
        IReadOnlyList<string> path,
        out string? value)
    {
        value = null;
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return true;
            }

            if (current.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return true;
            }
        }

        value = current.ValueKind == JsonValueKind.String ? current.GetString() : null;
        return true;
    }

    private static bool TryReadNestedDateTime(
        JsonElement element,
        string recordPath,
        ICollection<GitHubImportDiagnostic> diagnostics,
        out DateTimeOffset value,
        params string[] path)
    {
        value = default;
        return TryReadNestedString(element, recordPath, diagnostics, out var text, path) &&
               TryParseDateTime(text, BuildPath(recordPath, path), diagnostics, out value);
    }

    private static bool TryReadProperty(
        JsonElement element,
        string propertyName,
        string recordPath,
        ICollection<GitHubImportDiagnostic> diagnostics,
        out JsonElement property)
    {
        property = default;
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out property) &&
            property.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            return true;
        }

        AddMissingFieldDiagnostic(diagnostics, $"{recordPath}.{propertyName}");
        return false;
    }

    private static bool TryReadNestedProperty(
        JsonElement element,
        IReadOnlyList<string> path,
        string recordPath,
        ICollection<GitHubImportDiagnostic> diagnostics,
        out JsonElement property)
    {
        property = default;
        var current = element;

        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(segment, out current) ||
                current.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                AddMissingFieldDiagnostic(diagnostics, BuildPath(recordPath, path));
                return false;
            }
        }

        property = current;
        return true;
    }

    private static bool TryParseDateTime(
        string text,
        string path,
        ICollection<GitHubImportDiagnostic> diagnostics,
        out DateTimeOffset value)
    {
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value))
        {
            return true;
        }

        AddInvalidFieldDiagnostic(diagnostics, path, "invariant date-time");
        return false;
    }

    private static void AddMissingFieldDiagnostic(ICollection<GitHubImportDiagnostic> diagnostics, string path)
    {
        diagnostics.Add(
            GitHubImportDiagnostic.Warning(
                "github.response.field.missing",
                $"GitHub response field '{path}' was missing or null; record was skipped.",
                path));
    }

    private static void AddInvalidFieldDiagnostic(ICollection<GitHubImportDiagnostic> diagnostics, string path, string expected)
    {
        diagnostics.Add(
            GitHubImportDiagnostic.Warning(
                "github.response.field.invalid",
                $"GitHub response field '{path}' was not a valid {expected}; record was skipped.",
                path));
    }

    private static string BuildPath(string recordPath, IReadOnlyList<string> path) =>
        $"{recordPath}.{string.Join(".", path)}";

    private static bool TryReadLastPathSegmentAsInt(string url, out int value)
    {
        value = 0;
        var segment = url.TrimEnd('/').Split('/').LastOrDefault();
        return int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    internal sealed record GhCommandResult(bool IsSuccess, string Output, string Error)
    {
        public static GhCommandResult Succeeded(string output) => new(true, output, string.Empty);

        public static GhCommandResult Failed(string error) => new(false, string.Empty, error.Trim());
    }
}
