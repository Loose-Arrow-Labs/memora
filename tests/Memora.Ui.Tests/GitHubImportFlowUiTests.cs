using Memora.Ui.Operator;

namespace Memora.Ui.Tests;

public sealed class GitHubImportFlowUiTests : IClassFixture<OperatorShellFactory>
{
    private readonly OperatorShellFactory _factory;

    public GitHubImportFlowUiTests(OperatorShellFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetStarted_RendersGitHubPatForm()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);

        var html = await client.GetStringAsync("/get-started");

        Assert.Contains("Import From GitHub", html, StringComparison.Ordinal);
        Assert.Contains("personalAccessToken", html, StringComparison.Ordinal);
        Assert.Contains("/get-started/github/repos", html, StringComparison.Ordinal);
        Assert.Contains("type=\"password\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStarted_NoLongerShowsRawGhAuthLoginAsTheOnlyGitHubPath()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);

        var html = await client.GetStringAsync("/get-started");

        Assert.DoesNotContain("<h2>GitHub Login</h2>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<pre>gh auth login</pre>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStartedGithubRepos_MissingFields_RendersValidationError()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);

        var response = await client.PostAsync(
            "/get-started/github/repos",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["projectId"] = string.Empty,
                ["personalAccessToken"] = string.Empty
            }));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Project id and personal access token are required", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GitHubImportFlowService_ListUserRepositories_RejectsBlankToken()
    {
        var options = new OperatorShellOptions(_factory.WorkspacesRootPath, UsesSeededSampleRoot: false);
        var workspaceService = new LocalOperatorWorkspaceService(options);
        var service = new GitHubImportFlowService(options, workspaceService);

        var result = service.ListUserRepositories(string.Empty);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.ValidationErrors, error => error.Contains("token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GitHubImportFlowService_SetupProject_RejectsBlankImportMode()
    {
        var options = new OperatorShellOptions(_factory.WorkspacesRootPath, UsesSeededSampleRoot: false);
        var workspaceService = new LocalOperatorWorkspaceService(options);
        var service = new GitHubImportFlowService(options, workspaceService);

        var result = service.SetupProject(new GitHubProjectSetupRequest(
            ProjectId: "freshly-attached",
            Name: "Freshly attached",
            PersonalAccessToken: "ghp_test_token",
            RepositoryFullName: "octocat/hello-world",
            ImportMode: "not_a_real_mode"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.ValidationErrors, error => error.Contains("Import mode", StringComparison.Ordinal));
    }
}
