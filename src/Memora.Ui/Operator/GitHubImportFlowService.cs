using Memora.Core.Import;
using Memora.Import.Attachment;
using Memora.Import.Evidence;
using Memora.Import.GitHub;
using Memora.Storage.Workspaces;

namespace Memora.Ui.Operator;

public sealed class GitHubImportFlowService
{
    private const int RepositoryListPageSize = 100;
    private const int EvidenceImportPageSize = 100;

    private readonly OperatorShellOptions _options;
    private readonly LocalOperatorWorkspaceService _workspaceService;
    private readonly Func<GitHubApiAccountClient> _accountClientFactory;
    private readonly Func<string, IGitHubEvidenceClient> _evidenceClientFactory;
    private readonly Func<RepositoryAttachmentService> _attachmentServiceFactory;
    private readonly Func<IGitHubEvidenceClient, GitHubEvidenceImporter> _evidenceImporterFactory;

    public GitHubImportFlowService(OperatorShellOptions options, LocalOperatorWorkspaceService workspaceService)
        : this(
            options,
            workspaceService,
            accountClientFactory: () => new GitHubApiAccountClient(),
            evidenceClientFactory: token => new GitHubApiEvidenceClient(token),
            attachmentServiceFactory: () => new RepositoryAttachmentService(options.NormalizedWorkspacesRootPath),
            evidenceImporterFactory: client => new GitHubEvidenceImporter(options.NormalizedWorkspacesRootPath, client))
    {
    }

    internal GitHubImportFlowService(
        OperatorShellOptions options,
        LocalOperatorWorkspaceService workspaceService,
        Func<GitHubApiAccountClient> accountClientFactory,
        Func<string, IGitHubEvidenceClient> evidenceClientFactory,
        Func<RepositoryAttachmentService> attachmentServiceFactory,
        Func<IGitHubEvidenceClient, GitHubEvidenceImporter> evidenceImporterFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));
        _accountClientFactory = accountClientFactory ?? throw new ArgumentNullException(nameof(accountClientFactory));
        _evidenceClientFactory = evidenceClientFactory ?? throw new ArgumentNullException(nameof(evidenceClientFactory));
        _attachmentServiceFactory = attachmentServiceFactory ?? throw new ArgumentNullException(nameof(attachmentServiceFactory));
        _evidenceImporterFactory = evidenceImporterFactory ?? throw new ArgumentNullException(nameof(evidenceImporterFactory));
    }

    public GitHubRepositoryDiscoveryResult ListUserRepositories(string personalAccessToken)
    {
        if (string.IsNullOrWhiteSpace(personalAccessToken))
        {
            return GitHubRepositoryDiscoveryResult.Invalid(["GitHub personal access token is required."]);
        }

        var accountClient = _accountClientFactory();
        var validation = accountClient.ValidateToken(personalAccessToken);
        if (!validation.IsSuccess || validation.Account is null)
        {
            return GitHubRepositoryDiscoveryResult.Invalid([validation.Error?.Message ?? "GitHub did not accept the provided token."]);
        }

        var listResult = accountClient.ListRepositories(personalAccessToken, RepositoryListPageSize);
        if (!listResult.IsSuccess)
        {
            return GitHubRepositoryDiscoveryResult.Invalid([listResult.Error?.Message ?? "GitHub did not return a repository list."]);
        }

        return GitHubRepositoryDiscoveryResult.Success(validation.Account, listResult.Repositories);
    }

    public GitHubProjectSetupResult SetupProject(GitHubProjectSetupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.PersonalAccessToken))
        {
            errors.Add("GitHub personal access token is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RepositoryFullName))
        {
            errors.Add("Repository selection is required.");
        }

        if (!ImportModeExtensions.TryParseSchemaValue(request.ImportMode, out var parsedImportMode))
        {
            errors.Add($"Import mode '{request.ImportMode}' is not a known value.");
        }

        if (errors.Count > 0)
        {
            return GitHubProjectSetupResult.Invalid(errors);
        }

        var creationResult = _workspaceService.CreateProject(new OperatorCreateProjectInput(
            request.ProjectId,
            request.Name,
            LocalRepositoryPath: null));

        if (!creationResult.IsSuccess || creationResult.ProjectId is null)
        {
            return GitHubProjectSetupResult.Invalid(creationResult.ValidationErrors);
        }

        var projectId = creationResult.ProjectId;
        var remoteUrl = $"https://github.com/{request.RepositoryFullName.Trim()}.git";

        var attachmentService = _attachmentServiceFactory();
        var attachResult = attachmentService.Attach(new RepositoryAttachmentRequest(
            projectId,
            RepositoryAttachmentKind.GitHub,
            null,
            remoteUrl,
            null));

        if (!attachResult.IsSuccess || attachResult.Attachment is null)
        {
            return GitHubProjectSetupResult.Invalid(
                attachResult.Errors.Select(error => $"{error.Code}: {error.Message}").ToArray(),
                projectId);
        }

        IGitHubEvidenceClient evidenceClient;
        try
        {
            evidenceClient = _evidenceClientFactory(request.PersonalAccessToken);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return GitHubProjectSetupResult.Invalid([$"GitHub HTTP client could not be initialized: {exception.Message}"], projectId);
        }

        var importer = _evidenceImporterFactory(evidenceClient);
        GitHubEvidenceImportResult importResult;
        try
        {
            importResult = importer.Import(new GitHubEvidenceImportRequest(
                projectId,
                parsedImportMode,
                attachResult.Attachment.AttachmentId,
                EvidenceImportPageSize));
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return GitHubProjectSetupResult.Invalid([$"GitHub import failed: {exception.Message}"], projectId);
        }

        if (!importResult.IsSuccess)
        {
            var errorMessages = importResult.Diagnostics
                .Where(diagnostic => diagnostic.Severity == GitHubImportDiagnosticSeverity.Error)
                .Select(diagnostic => diagnostic.Message)
                .DefaultIfEmpty("GitHub evidence import returned errors.")
                .ToArray();
            return GitHubProjectSetupResult.Invalid(errorMessages, projectId);
        }

        var warnings = importResult.Diagnostics
            .Where(diagnostic => diagnostic.Severity != GitHubImportDiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Message)
            .ToArray();

        return GitHubProjectSetupResult.Succeeded(projectId, importResult.Progress, warnings);
    }
}

public sealed record GitHubProjectSetupRequest(
    string? ProjectId,
    string? Name,
    string PersonalAccessToken,
    string RepositoryFullName,
    string ImportMode);

public sealed record GitHubRepositoryDiscoveryResult
{
    private GitHubRepositoryDiscoveryResult(
        bool isSuccess,
        GitHubAccount? account,
        IReadOnlyList<GitHubRepositoryEntry> repositories,
        IReadOnlyList<string> validationErrors)
    {
        IsSuccess = isSuccess;
        Account = account;
        Repositories = repositories;
        ValidationErrors = validationErrors;
    }

    public bool IsSuccess { get; }
    public GitHubAccount? Account { get; }
    public IReadOnlyList<GitHubRepositoryEntry> Repositories { get; }
    public IReadOnlyList<string> ValidationErrors { get; }

    public static GitHubRepositoryDiscoveryResult Success(GitHubAccount account, IReadOnlyList<GitHubRepositoryEntry> repositories) =>
        new(true, account, repositories, []);

    public static GitHubRepositoryDiscoveryResult Invalid(IEnumerable<string> validationErrors) =>
        new(false, null, [], validationErrors.ToArray());
}

public sealed record GitHubRepoPickerPageModel(
    string ProjectId,
    string Name,
    string PersonalAccessToken,
    string AccountLogin,
    IReadOnlyList<GitHubRepositoryEntry> Repositories,
    IReadOnlyList<string> ValidationErrors);

public sealed record GitHubProjectSetupResult
{
    private GitHubProjectSetupResult(
        bool isSuccess,
        string? projectId,
        GitHubEvidenceImportProgress? progress,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> validationErrors)
    {
        IsSuccess = isSuccess;
        ProjectId = projectId;
        Progress = progress;
        Warnings = warnings;
        ValidationErrors = validationErrors;
    }

    public bool IsSuccess { get; }
    public string? ProjectId { get; }
    public GitHubEvidenceImportProgress? Progress { get; }
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyList<string> ValidationErrors { get; }

    public static GitHubProjectSetupResult Succeeded(
        string projectId,
        GitHubEvidenceImportProgress progress,
        IReadOnlyList<string> warnings) =>
        new(true, projectId, progress, warnings, []);

    public static GitHubProjectSetupResult Invalid(IEnumerable<string> validationErrors, string? projectId = null) =>
        new(false, projectId, null, [], validationErrors.ToArray());
}
