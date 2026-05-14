using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Memora.Import.GitHub;

public sealed class GitHubApiAccountClient
{
    public const string DefaultBaseAddress = "https://api.github.com";
    public const int DefaultTimeoutMs = 15_000;
    public const string UserAgent = "Memora-Local-Operator";

    private static readonly Uri DefaultBaseUri = new(DefaultBaseAddress);

    private readonly Func<HttpRequestMessage, HttpResponseMessage> _send;
    private readonly Uri _baseAddress;

    public GitHubApiAccountClient()
        : this(CreateDefaultSend(DefaultTimeoutMs), DefaultBaseUri)
    {
    }

    public GitHubApiAccountClient(int timeoutMs)
        : this(CreateDefaultSend(timeoutMs), DefaultBaseUri)
    {
    }

    internal GitHubApiAccountClient(
        Func<HttpRequestMessage, HttpResponseMessage> send,
        Uri? baseAddress = null)
    {
        _send = send ?? throw new ArgumentNullException(nameof(send));
        _baseAddress = baseAddress ?? DefaultBaseUri;
    }

    public GitHubAccountValidationResult ValidateToken(string personalAccessToken)
    {
        if (string.IsNullOrWhiteSpace(personalAccessToken))
        {
            return GitHubAccountValidationResult.Failed(
                GitHubImportDiagnostic.Error(
                    "github.token.missing",
                    "GitHub personal access token is required.",
                    "personal_access_token"));
        }

        using var request = BuildAuthorizedGet("/user", personalAccessToken);
        HttpResponseMessage response;
        try
        {
            response = _send(request);
        }
        catch (HttpRequestException exception)
        {
            return GitHubAccountValidationResult.Failed(
                GitHubImportDiagnostic.Error(
                    "github.host.unreachable",
                    $"GitHub host could not be reached: {exception.Message}",
                    "personal_access_token"));
        }
        catch (TaskCanceledException)
        {
            return GitHubAccountValidationResult.Failed(
                GitHubImportDiagnostic.Error(
                    "github.host.unreachable",
                    "GitHub host did not respond before the request timed out.",
                    "personal_access_token"));
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return GitHubAccountValidationResult.Failed(
                    GitHubImportDiagnostic.Error(
                        "github.token.invalid",
                        "GitHub rejected the personal access token. Check that it is current and has the right scopes.",
                        "personal_access_token"));
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return GitHubAccountValidationResult.Failed(
                    GitHubImportDiagnostic.Error(
                        "github.token.forbidden",
                        "GitHub denied the request. The token may be missing scopes such as 'repo' or be rate limited.",
                        "personal_access_token"));
            }

            if (!response.IsSuccessStatusCode)
            {
                return GitHubAccountValidationResult.Failed(
                    GitHubImportDiagnostic.Error(
                        "github.host.unreachable",
                        $"GitHub responded with status {(int)response.StatusCode}.",
                        "personal_access_token"));
            }

            var body = ReadBody(response);
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind != JsonValueKind.Object ||
                    !document.RootElement.TryGetProperty("login", out var loginElement) ||
                    loginElement.ValueKind != JsonValueKind.String)
                {
                    return GitHubAccountValidationResult.Failed(
                        GitHubImportDiagnostic.Error(
                            "github.response.unexpected",
                            "GitHub /user response did not contain a 'login' field.",
                            "/user"));
                }

                var login = loginElement.GetString() ?? string.Empty;
                string? name = null;
                if (document.RootElement.TryGetProperty("name", out var nameElement) &&
                    nameElement.ValueKind == JsonValueKind.String)
                {
                    name = nameElement.GetString();
                }

                return GitHubAccountValidationResult.Succeeded(new GitHubAccount(login, name));
            }
            catch (JsonException exception)
            {
                return GitHubAccountValidationResult.Failed(
                    GitHubImportDiagnostic.Error(
                        "github.response.invalid_json",
                        $"GitHub /user response could not be parsed as JSON: {exception.Message}",
                        "/user"));
            }
        }
    }

    public GitHubRepositoryListResult ListRepositories(string personalAccessToken, int maxRepositories = 100)
    {
        if (string.IsNullOrWhiteSpace(personalAccessToken))
        {
            return GitHubRepositoryListResult.Failed(
                GitHubImportDiagnostic.Error(
                    "github.token.missing",
                    "GitHub personal access token is required.",
                    "personal_access_token"));
        }

        var clampedMax = Math.Clamp(maxRepositories, 1, 100);
        var endpoint = $"/user/repos?per_page={clampedMax.ToString(CultureInfo.InvariantCulture)}&sort=updated&affiliation=owner,collaborator,organization_member";
        using var request = BuildAuthorizedGet(endpoint, personalAccessToken);
        HttpResponseMessage response;
        try
        {
            response = _send(request);
        }
        catch (HttpRequestException exception)
        {
            return GitHubRepositoryListResult.Failed(
                GitHubImportDiagnostic.Error(
                    "github.host.unreachable",
                    $"GitHub host could not be reached: {exception.Message}",
                    "personal_access_token"));
        }
        catch (TaskCanceledException)
        {
            return GitHubRepositoryListResult.Failed(
                GitHubImportDiagnostic.Error(
                    "github.host.unreachable",
                    "GitHub host did not respond before the request timed out.",
                    "personal_access_token"));
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return GitHubRepositoryListResult.Failed(
                    GitHubImportDiagnostic.Error(
                        "github.token.invalid",
                        "GitHub rejected the personal access token while listing repositories.",
                        "personal_access_token"));
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return GitHubRepositoryListResult.Failed(
                    GitHubImportDiagnostic.Error(
                        "github.token.forbidden",
                        "GitHub denied the repository list. The token may be missing the 'repo' scope.",
                        "personal_access_token"));
            }

            if (!response.IsSuccessStatusCode)
            {
                return GitHubRepositoryListResult.Failed(
                    GitHubImportDiagnostic.Error(
                        "github.host.unreachable",
                        $"GitHub responded with status {(int)response.StatusCode}.",
                        "personal_access_token"));
            }

            var body = ReadBody(response);
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return GitHubRepositoryListResult.Failed(
                        GitHubImportDiagnostic.Error(
                            "github.response.unexpected",
                            "GitHub /user/repos response was not a JSON array.",
                            "/user/repos"));
                }

                var entries = new List<GitHubRepositoryEntry>();
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    var entry = TryReadRepositoryEntry(element);
                    if (entry is not null)
                    {
                        entries.Add(entry);
                    }
                }

                return GitHubRepositoryListResult.Succeeded(entries);
            }
            catch (JsonException exception)
            {
                return GitHubRepositoryListResult.Failed(
                    GitHubImportDiagnostic.Error(
                        "github.response.invalid_json",
                        $"GitHub /user/repos response could not be parsed as JSON: {exception.Message}",
                        "/user/repos"));
            }
        }
    }

    private static GitHubRepositoryEntry? TryReadRepositoryEntry(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("full_name", out var fullNameElement) ||
            fullNameElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var fullName = fullNameElement.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        var parts = fullName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        var htmlUrl = ReadString(element, "html_url") ?? $"https://github.com/{fullName}";
        var cloneUrl = ReadString(element, "clone_url") ?? $"https://github.com/{fullName}.git";
        var defaultBranch = ReadString(element, "default_branch") ?? "main";
        var isPrivate = element.TryGetProperty("private", out var privateElement) &&
                        privateElement.ValueKind is JsonValueKind.True;
        DateTimeOffset? updatedAt = null;
        if (element.TryGetProperty("updated_at", out var updatedAtElement) &&
            updatedAtElement.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(
                updatedAtElement.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            updatedAt = parsed;
        }

        return new GitHubRepositoryEntry(
            parts[0],
            parts[1],
            fullName,
            isPrivate,
            htmlUrl,
            cloneUrl,
            defaultBranch,
            updatedAt);
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private HttpRequestMessage BuildAuthorizedGet(string path, string personalAccessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseAddress, path));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Memora", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", personalAccessToken);
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private static string ReadBody(HttpResponseMessage response)
    {
        using var stream = response.Content.ReadAsStream();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
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
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, "1.0"));
        return request => httpClient.Send(request, HttpCompletionOption.ResponseContentRead);
    }
}
