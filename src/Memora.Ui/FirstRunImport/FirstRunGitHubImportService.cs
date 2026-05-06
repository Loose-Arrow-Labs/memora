using Memora.Core.Import;
using Memora.Import.Attachment;
using Memora.Import.GitHub;

namespace Memora.Ui.FirstRunImport;

public sealed class FirstRunGitHubImportService
{
    private const int DefaultMaxItems = 25;
    private const int MaxAllowedItems = 100;

    private readonly string _workspacesRootPath;
    private readonly IGitHubEvidenceClient _client;

    public FirstRunGitHubImportService(
        string workspacesRootPath,
        IGitHubEvidenceClient client)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacesRootPath);

        _workspacesRootPath = Path.GetFullPath(workspacesRootPath);
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public FirstRunGitHubImportRunResult Run(FirstRunGitHubImportRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ProjectId))
        {
            return FirstRunGitHubImportRunResult.Failed(
                "Project id is required.",
                FirstRunGitHubImportRunDiagnostic.Error("github_import.project_id.missing", "Project id is required.", "project_id"));
        }

        var maxItems = NormalizeMaxItems(request.MaxItems);
        var diagnostics = new List<FirstRunGitHubImportRunDiagnostic>();
        var normalizedRemoteUrl = string.IsNullOrWhiteSpace(request.RemoteUrl)
            ? null
            : request.RemoteUrl.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedRemoteUrl))
        {
            var attachmentResult = new RepositoryAttachmentService(_workspacesRootPath)
                .Attach(new RepositoryAttachmentRequest(
                    request.ProjectId,
                    RepositoryAttachmentKind.GitHub,
                    remoteUrl: normalizedRemoteUrl,
                    defaultBranch: "main"));

            if (attachmentResult.IsSuccess && attachmentResult.Attachment is not null)
            {
                diagnostics.Add(FirstRunGitHubImportRunDiagnostic.Info(
                    "github_attachment.created",
                    $"Attached GitHub repository '{attachmentResult.Attachment.RepositoryIdentity}'.",
                    "remote_url"));
            }
            else if (attachmentResult.Errors.Any(error => error.Code == "attachment.duplicate"))
            {
                diagnostics.Add(FirstRunGitHubImportRunDiagnostic.Info(
                    "github_attachment.already_attached",
                    "GitHub repository is already attached to this workspace.",
                    "remote_url"));
            }
            else
            {
                diagnostics.AddRange(attachmentResult.Errors.Select(MapAttachmentError));
                return FirstRunGitHubImportRunResult.FromImportState(
                    false,
                    request.ImportMode,
                    maxItems,
                    new GitHubEvidenceImportProgress(0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                    diagnostics);
            }
        }

        GitHubEvidenceImportResult importResult;
        try
        {
            importResult = new GitHubEvidenceImporter(_workspacesRootPath, _client)
                .Import(new GitHubEvidenceImportRequest(
                    request.ProjectId,
                    request.ImportMode,
                    maxItems: maxItems));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            diagnostics.Add(FirstRunGitHubImportRunDiagnostic.Error(
                "github_import.failed",
                $"GitHub import could not run: {exception.Message}",
                "github_import"));

            return FirstRunGitHubImportRunResult.FromImportState(
                false,
                request.ImportMode,
                maxItems,
                new GitHubEvidenceImportProgress(0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                diagnostics);
        }

        diagnostics.AddRange(importResult.Diagnostics.Select(MapGitHubDiagnostic));
        return FirstRunGitHubImportRunResult.FromImportState(
            importResult.IsSuccess,
            request.ImportMode,
            maxItems,
            importResult.Progress,
            diagnostics);
    }

    private static int NormalizeMaxItems(int? maxItems)
    {
        if (maxItems is null)
        {
            return DefaultMaxItems;
        }

        return Math.Clamp(maxItems.Value, 1, MaxAllowedItems);
    }

    private static FirstRunGitHubImportRunDiagnostic MapAttachmentError(RepositoryAttachmentError error) =>
        FirstRunGitHubImportRunDiagnostic.Error(error.Code, error.Message, error.Path);

    private static FirstRunGitHubImportRunDiagnostic MapGitHubDiagnostic(GitHubImportDiagnostic diagnostic) =>
        new(
            diagnostic.Code,
            diagnostic.Message,
            diagnostic.Severity.ToString(),
            diagnostic.Path);
}

public sealed record FirstRunGitHubImportRunRequest(
    string ProjectId,
    ImportMode ImportMode,
    string? RemoteUrl,
    int? MaxItems);

public sealed record FirstRunGitHubImportRunResult(
    bool IsSuccess,
    string Summary,
    ImportMode ImportMode,
    int MaxItems,
    int TotalRecords,
    int CreatedRecords,
    int ExistingRecords,
    int IssueCount,
    int PullRequestCount,
    int ReviewCount,
    int ReviewCommentCount,
    int CommitCount,
    int ReleaseCount,
    int DiscussionCount,
    IReadOnlyList<FirstRunGitHubImportRunDiagnostic> Diagnostics)
{
    public IReadOnlyList<FirstRunGitHubImportRunDiagnostic> Diagnostics { get; } =
        Diagnostics?.ToArray() ?? throw new ArgumentNullException(nameof(Diagnostics));

    public static FirstRunGitHubImportRunResult Failed(
        string summary,
        params FirstRunGitHubImportRunDiagnostic[] diagnostics) =>
        new(
            false,
            summary,
            ImportMode.StrictGovernance,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            diagnostics);

    public static FirstRunGitHubImportRunResult FromImportState(
        bool isSuccess,
        ImportMode importMode,
        int maxItems,
        GitHubEvidenceImportProgress progress,
        IReadOnlyList<FirstRunGitHubImportRunDiagnostic> diagnostics)
    {
        var summary = isSuccess
            ? $"Imported {progress.TotalRecords} GitHub evidence record(s): {progress.CreatedRecords} new, {progress.ExistingRecords} already present."
            : "GitHub import did not complete.";

        return new FirstRunGitHubImportRunResult(
            isSuccess,
            summary,
            importMode,
            maxItems,
            progress.TotalRecords,
            progress.CreatedRecords,
            progress.ExistingRecords,
            progress.IssueCount,
            progress.PullRequestCount,
            progress.ReviewCount,
            progress.ReviewCommentCount,
            progress.CommitCount,
            progress.ReleaseCount,
            progress.DiscussionCount,
            diagnostics);
    }
}

public sealed record FirstRunGitHubImportRunDiagnostic(
    string Code,
    string Message,
    string Severity,
    string? Path = null)
{
    public static FirstRunGitHubImportRunDiagnostic Error(string code, string message, string? path = null) =>
        new(code, message, "Error", path);

    public static FirstRunGitHubImportRunDiagnostic Info(string code, string message, string? path = null) =>
        new(code, message, "Info", path);
}
