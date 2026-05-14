using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Memora.Import.GitHub;

public sealed class GitHubApiEvidenceClient : IGitHubEvidenceClient
{
    public const string DefaultBaseAddress = "https://api.github.com";
    public const int DefaultTimeoutMs = 30_000;

    private static readonly Uri DefaultBaseUri = new(DefaultBaseAddress);

    private readonly Func<HttpRequestMessage, HttpResponseMessage> _send;
    private readonly string _personalAccessToken;
    private readonly Uri _baseAddress;

    public GitHubApiEvidenceClient(string personalAccessToken)
        : this(CreateDefaultSend(DefaultTimeoutMs), personalAccessToken, DefaultBaseUri)
    {
    }

    public GitHubApiEvidenceClient(string personalAccessToken, int timeoutMs)
        : this(CreateDefaultSend(timeoutMs), personalAccessToken, DefaultBaseUri)
    {
    }

    internal GitHubApiEvidenceClient(
        Func<HttpRequestMessage, HttpResponseMessage> send,
        string personalAccessToken,
        Uri? baseAddress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personalAccessToken);
        _send = send ?? throw new ArgumentNullException(nameof(send));
        _personalAccessToken = personalAccessToken;
        _baseAddress = baseAddress ?? DefaultBaseUri;
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

        var pageSize = Math.Clamp(maxItems, 1, 100);
        var diagnostics = new List<GitHubImportDiagnostic>();
        var issues = new List<GitHubIssueEvidence>();
        var pullRequests = new List<GitHubPullRequestEvidence>();
        var commits = new List<GitHubCommitEvidence>();
        var releases = new List<GitHubReleaseEvidence>();

        var issuesResult = FetchArray(
            $"/repos/{owner}/{repo}/issues?state=all&per_page={pageSize.ToString(CultureInfo.InvariantCulture)}",
            pageSize,
            diagnostics);
        if (issuesResult.CredentialsRejected)
        {
            return GitHubEvidenceClientResult.Failed(diagnostics.ToArray());
        }

        foreach (var element in issuesResult.Elements)
        {
            if (element.TryGetProperty("pull_request", out _))
            {
                continue;
            }

            var issue = ReadIssue(element);
            if (issue is not null)
            {
                issues.Add(issue);
                if (issues.Count >= maxItems)
                {
                    break;
                }
            }
        }

        var pullsResult = FetchArray(
            $"/repos/{owner}/{repo}/pulls?state=all&per_page={pageSize.ToString(CultureInfo.InvariantCulture)}",
            pageSize,
            diagnostics);
        foreach (var element in pullsResult.Elements)
        {
            var pullRequest = ReadPullRequest(element);
            if (pullRequest is not null)
            {
                pullRequests.Add(pullRequest);
                if (pullRequests.Count >= maxItems)
                {
                    break;
                }
            }
        }

        var commitsResult = FetchArray(
            $"/repos/{owner}/{repo}/commits?per_page={pageSize.ToString(CultureInfo.InvariantCulture)}",
            pageSize,
            diagnostics);
        foreach (var element in commitsResult.Elements)
        {
            var commit = ReadCommit(element);
            if (commit is not null)
            {
                commits.Add(commit);
                if (commits.Count >= maxItems)
                {
                    break;
                }
            }
        }

        var releasesResult = FetchArray(
            $"/repos/{owner}/{repo}/releases?per_page={pageSize.ToString(CultureInfo.InvariantCulture)}",
            pageSize,
            diagnostics);
        foreach (var element in releasesResult.Elements)
        {
            var release = ReadRelease(element);
            if (release is not null)
            {
                releases.Add(release);
                if (releases.Count >= maxItems)
                {
                    break;
                }
            }
        }

        diagnostics.Add(
            GitHubImportDiagnostic.Info(
                "github.api.scope.partial",
                "GitHub HTTP import covers issues, pull requests, commits, and releases. Reviews, review comments, and discussions are deferred to a follow-up.",
                "scope"));

        var isPartial = diagnostics.Any(diagnostic =>
            diagnostic.Code is "github.import.partial" or
                "github.response.unexpected" or
                "github.response.invalid_json");

        return GitHubEvidenceClientResult.Succeeded(
            new GitHubEvidenceSnapshot(
                issues,
                pullRequests,
                Array.Empty<GitHubReviewEvidence>(),
                Array.Empty<GitHubReviewCommentEvidence>(),
                commits,
                releases,
                Array.Empty<GitHubDiscussionEvidence>(),
                isPartial,
                diagnostics));
    }

    private FetchArrayResult FetchArray(string endpoint, int pageSize, ICollection<GitHubImportDiagnostic> diagnostics)
    {
        using var request = BuildAuthorizedGet(endpoint);
        HttpResponseMessage response;
        try
        {
            response = _send(request);
        }
        catch (HttpRequestException exception)
        {
            diagnostics.Add(
                GitHubImportDiagnostic.Warning(
                    "github.import.partial",
                    $"GitHub endpoint '{endpoint}' could not be reached: {exception.Message}",
                    endpoint));
            return new FetchArrayResult(Array.Empty<JsonElement>(), CredentialsRejected: false);
        }
        catch (TaskCanceledException)
        {
            diagnostics.Add(
                GitHubImportDiagnostic.Warning(
                    "github.import.partial",
                    $"GitHub endpoint '{endpoint}' did not respond before the request timed out.",
                    endpoint));
            return new FetchArrayResult(Array.Empty<JsonElement>(), CredentialsRejected: false);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                diagnostics.Add(
                    GitHubImportDiagnostic.Error(
                        "github.credentials.missing",
                        "GitHub rejected the provided personal access token.",
                        endpoint));
                return new FetchArrayResult(Array.Empty<JsonElement>(), CredentialsRejected: true);
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                diagnostics.Add(
                    GitHubImportDiagnostic.Error(
                        "github.private_denied",
                        "GitHub denied the request. The token may be missing scopes or rate limited.",
                        endpoint));
                return new FetchArrayResult(Array.Empty<JsonElement>(), CredentialsRejected: false);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                diagnostics.Add(
                    GitHubImportDiagnostic.Warning(
                        "github.import.partial",
                        $"GitHub endpoint '{endpoint}' was not found and was skipped.",
                        endpoint));
                return new FetchArrayResult(Array.Empty<JsonElement>(), CredentialsRejected: false);
            }

            if (!response.IsSuccessStatusCode)
            {
                diagnostics.Add(
                    GitHubImportDiagnostic.Warning(
                        "github.import.partial",
                        $"GitHub endpoint '{endpoint}' returned status {(int)response.StatusCode}.",
                        endpoint));
                return new FetchArrayResult(Array.Empty<JsonElement>(), CredentialsRejected: false);
            }

            string body;
            using (var stream = response.Content.ReadAsStream())
            using (var reader = new StreamReader(stream))
            {
                body = reader.ReadToEnd();
            }

            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    diagnostics.Add(
                        GitHubImportDiagnostic.Warning(
                            "github.response.unexpected",
                            $"GitHub response for '{endpoint}' was not an array.",
                            endpoint));
                    return new FetchArrayResult(Array.Empty<JsonElement>(), CredentialsRejected: false);
                }

                var elements = document.RootElement.EnumerateArray()
                    .Select(element => element.Clone())
                    .ToArray();
                if (elements.Length >= pageSize)
                {
                    diagnostics.Add(
                        GitHubImportDiagnostic.Warning(
                            "github.import.partial",
                            $"GitHub endpoint '{endpoint}' may have more results than the bounded import fetched.",
                            endpoint));
                }

                return new FetchArrayResult(elements, CredentialsRejected: false);
            }
            catch (JsonException exception)
            {
                diagnostics.Add(
                    GitHubImportDiagnostic.Warning(
                        "github.response.invalid_json",
                        $"GitHub response for '{endpoint}' could not be parsed as JSON and was skipped: {exception.Message}",
                        endpoint));
                return new FetchArrayResult(Array.Empty<JsonElement>(), CredentialsRejected: false);
            }
        }
    }

    private HttpRequestMessage BuildAuthorizedGet(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseAddress, path));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Memora", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _personalAccessToken);
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
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

    private static GitHubIssueEvidence? ReadIssue(JsonElement element)
    {
        if (!TryGetInt(element, "number", out var number)) return null;
        if (!TryGetString(element, "html_url", out var url)) return null;
        if (!TryGetString(element, "title", out var title)) return null;
        if (!TryGetString(element, "state", out var state)) return null;
        if (!TryGetDateTime(element, "created_at", out var createdAt)) return null;
        if (!TryGetDateTime(element, "updated_at", out var updatedAt)) return null;

        return new GitHubIssueEvidence(number, url, title, state, createdAt, updatedAt);
    }

    private static GitHubPullRequestEvidence? ReadPullRequest(JsonElement element)
    {
        if (!TryGetInt(element, "number", out var number)) return null;
        if (!TryGetString(element, "html_url", out var url)) return null;
        if (!TryGetString(element, "title", out var title)) return null;
        if (!TryGetString(element, "state", out var state)) return null;
        if (!TryGetDateTime(element, "created_at", out var createdAt)) return null;
        if (!TryGetDateTime(element, "updated_at", out var updatedAt)) return null;
        TryGetOptionalString(element, "merge_commit_sha", out var mergeCommitSha);
        TryGetOptionalDateTime(element, "merged_at", out var mergedAtUtc);

        return new GitHubPullRequestEvidence(number, url, title, state, mergeCommitSha, mergedAtUtc, createdAt, updatedAt);
    }

    private static GitHubCommitEvidence? ReadCommit(JsonElement element)
    {
        if (!TryGetString(element, "sha", out var sha)) return null;
        if (!TryGetString(element, "html_url", out var url)) return null;
        if (!element.TryGetProperty("commit", out var commitElement) || commitElement.ValueKind != JsonValueKind.Object) return null;
        if (!TryGetString(commitElement, "message", out var message)) return null;

        string? author = null;
        if (commitElement.TryGetProperty("author", out var authorElement) &&
            authorElement.ValueKind == JsonValueKind.Object &&
            authorElement.TryGetProperty("name", out var authorNameElement) &&
            authorNameElement.ValueKind == JsonValueKind.String)
        {
            author = authorNameElement.GetString();
        }

        if (!commitElement.TryGetProperty("author", out authorElement) ||
            authorElement.ValueKind != JsonValueKind.Object ||
            !authorElement.TryGetProperty("date", out var dateElement) ||
            dateElement.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(
                dateElement.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var authoredAt))
        {
            return null;
        }

        return new GitHubCommitEvidence(sha, url, message, author, authoredAt);
    }

    private static GitHubReleaseEvidence? ReadRelease(JsonElement element)
    {
        if (!element.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.Number) return null;
        if (!TryGetString(element, "html_url", out var url)) return null;
        if (!TryGetString(element, "tag_name", out var tagName)) return null;
        TryGetOptionalString(element, "name", out var name);
        TryGetOptionalDateTime(element, "published_at", out var publishedAt);

        var releaseId = idElement.GetInt64().ToString("D", CultureInfo.InvariantCulture);
        return new GitHubReleaseEvidence(releaseId, url, name ?? tagName, tagName, publishedAt);
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryGetOptionalString(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return true;
        }

        if (property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return true;
        }

        return true;
    }

    private static bool TryGetInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out value);
    }

    private static bool TryGetDateTime(JsonElement element, string propertyName, out DateTimeOffset value)
    {
        value = default;
        if (!TryGetString(element, propertyName, out var text))
        {
            return false;
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value);
    }

    private static bool TryGetOptionalDateTime(JsonElement element, string propertyName, out DateTimeOffset? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return true;
        }

        if (property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return true;
        }

        var text = property.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            value = parsed;
        }

        return true;
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> CreateDefaultSend(int timeoutMs)
    {
        if (timeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        }

        var handler = new HttpClientHandler();
        var httpClient = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromMilliseconds(timeoutMs)
        };
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(GitHubApiAccountClient.UserAgent, "1.0"));
        return request => httpClient.Send(request, HttpCompletionOption.ResponseContentRead);
    }

    private sealed record FetchArrayResult(IReadOnlyList<JsonElement> Elements, bool CredentialsRejected);
}
