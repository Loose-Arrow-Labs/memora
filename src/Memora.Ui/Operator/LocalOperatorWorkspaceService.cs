using Memora.Core.Approval;
using Memora.Core.Artifacts;
using Memora.Core.Editing;
using Memora.Core.Revisions;
using Memora.Storage.Parsing;
using Memora.Storage.Persistence;
using Memora.Storage.Workspaces;

namespace Memora.Ui.Operator;

public sealed class LocalOperatorWorkspaceService
{
    private readonly WorkspaceDiscovery _workspaceDiscovery = new();
    private readonly ArtifactMarkdownParser _markdownParser = new();
    private readonly ArtifactFileStore _artifactFileStore = new();
    private readonly ApprovalQueueBuilder _approvalQueueBuilder = new();
    private readonly DraftArtifactEditor _draftArtifactEditor = new();
    private readonly ArtifactRevisionDiffBuilder _diffBuilder = new();
    private readonly OperatorShellOptions _options;

    public LocalOperatorWorkspaceService(OperatorShellOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public IReadOnlyList<OperatorProjectSummary> GetProjects() =>
        _workspaceDiscovery
            .Discover(_options.NormalizedWorkspacesRootPath)
            .Select(workspace =>
            {
                var records = LoadArtifactRecords(workspace);
                var pendingCount = records.Count(record => record.IsPendingReview);

                return new OperatorProjectSummary(
                    workspace.ProjectId,
                    workspace.Metadata.Name,
                    workspace.Metadata.Status,
                    workspace.RootPath,
                    records.Count,
                    pendingCount);
            })
            .OrderBy(project => project.ProjectId, StringComparer.Ordinal)
            .ToArray();

    public OperatorProjectSnapshot? TryGetProject(string projectId)
    {
        var workspace = TryGetWorkspace(projectId);
        return workspace is null ? null : LoadProjectSnapshot(workspace);
    }

    public OperatorArtifactView? TryGetArtifactView(string projectId, string relativePath)
    {
        var project = TryGetProject(projectId);
        if (project is null)
        {
            return null;
        }

        var normalizedRelativePath = NormalizeRelativePath(relativePath);
        var selectedRecord = project.Artifacts.SingleOrDefault(record =>
            string.Equals(record.RelativePath, normalizedRelativePath, StringComparison.Ordinal));

        if (selectedRecord is null)
        {
            return null;
        }

        var currentApprovedArtifact = project.Artifacts
            .Where(record => string.Equals(record.Artifact.Id, selectedRecord.Artifact.Id, StringComparison.Ordinal))
            .Where(record => record.Artifact.Status == ArtifactStatus.Approved)
            .OrderByDescending(record => record.Artifact.Revision)
            .ThenByDescending(record => record.Artifact.UpdatedAtUtc)
            .Select(record => record.Artifact)
            .FirstOrDefault();

        var diffIssues = Array.Empty<string>();
        ArtifactRevisionDiff? revisionDiff = null;

        if (selectedRecord.IsPendingReview && currentApprovedArtifact is not null)
        {
            var diffResult = _diffBuilder.Build(currentApprovedArtifact, selectedRecord.Artifact);
            diffIssues = diffResult.Validation.Issues.Select(issue => issue.DiagnosticMessage).ToArray();
            revisionDiff = diffResult.Diff;
        }

        return new OperatorArtifactView(
            project,
            selectedRecord,
            currentApprovedArtifact,
            revisionDiff,
            diffIssues,
            BuildReviewQueueContext(project, selectedRecord));
    }

    public OperatorMutationResult EditDraft(
        string projectId,
        string relativePath,
        OperatorArtifactEditInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var artifactView = TryGetArtifactView(projectId, relativePath);
        if (artifactView is null)
        {
            return OperatorMutationResult.NotFound();
        }

        var editRequest = new DraftArtifactEditRequest(
            input.Title,
            input.Reason,
            input.ParseTags(),
            input.Sections);

        var result = _draftArtifactEditor.Edit(
            artifactView.SelectedArtifact.Artifact,
            editRequest,
            DateTimeOffset.UtcNow);

        if (!result.IsSuccess || result.EditedArtifact is null)
        {
            return OperatorMutationResult.Invalid(
                result.Validation.Issues.Select(issue => issue.DiagnosticMessage));
        }

        var savedPath = _artifactFileStore.Save(artifactView.Project.Workspace, result.EditedArtifact);
        var savedRelativePath = NormalizeRelativePath(Path.GetRelativePath(artifactView.Project.Workspace.RootPath, savedPath));

        return OperatorMutationResult.Success(savedRelativePath);
    }

    private ProjectWorkspace? TryGetWorkspace(string projectId) =>
        _workspaceDiscovery
            .Discover(_options.NormalizedWorkspacesRootPath)
            .SingleOrDefault(workspace => string.Equals(workspace.ProjectId, projectId, StringComparison.Ordinal));

    private OperatorProjectSnapshot LoadProjectSnapshot(ProjectWorkspace workspace)
    {
        var records = LoadArtifactRecords(workspace)
            .OrderBy(record => record.IsPendingReview ? 0 : 1)
            .ThenBy(record => record.Artifact.Type.ToString(), StringComparer.Ordinal)
            .ThenBy(record => record.Artifact.Title, StringComparer.Ordinal)
            .ThenByDescending(record => record.Artifact.Revision)
            .ThenBy(record => record.RelativePath, StringComparer.Ordinal)
            .ToArray();

        var queue = _approvalQueueBuilder.Build(workspace.ProjectId, records.Select(record => record.Artifact));
        var pendingItems = queue.Items
            .Select(item => new OperatorPendingReviewItem(item, FindRecord(records, item)))
            .ToArray();

        return new OperatorProjectSnapshot(workspace, records, pendingItems);
    }

    private IReadOnlyList<OperatorArtifactRecord> LoadArtifactRecords(ProjectWorkspace workspace)
    {
        var rootDirectories =
            new[]
            {
                workspace.CanonicalRootPath,
                workspace.DraftsRootPath,
                workspace.SummariesRootPath
            };

        return rootDirectories
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.md", SearchOption.AllDirectories))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => ParseArtifactRecord(workspace, path))
            .ToArray();
    }

    private OperatorArtifactRecord ParseArtifactRecord(ProjectWorkspace workspace, string filePath)
    {
        var markdown = File.ReadAllText(filePath);
        var parseResult = _markdownParser.Parse(markdown);

        if (!parseResult.Validation.IsValid || parseResult.Artifact is null)
        {
            var issues = string.Join(
                "; ",
                parseResult.Validation.Issues.Select(issue => issue.DiagnosticMessage));

            throw new InvalidDataException($"Artifact file '{filePath}' is invalid. {issues}");
        }

        var relativePath = NormalizeRelativePath(Path.GetRelativePath(workspace.RootPath, filePath));
        return new OperatorArtifactRecord(relativePath, filePath, parseResult.Artifact);
    }

    private static OperatorArtifactRecord FindRecord(
        IReadOnlyList<OperatorArtifactRecord> records,
        ApprovalQueueItem item) =>
        records.Single(record =>
            string.Equals(record.Artifact.Id, item.ArtifactId, StringComparison.Ordinal) &&
            record.Artifact.Status == item.PendingStatus &&
            record.Artifact.Revision == item.Revision &&
            record.Artifact.UpdatedAtUtc == item.PendingSinceUtc);

    private static OperatorReviewQueueContext? BuildReviewQueueContext(
        OperatorProjectSnapshot project,
        OperatorArtifactRecord selectedRecord)
    {
        var selectedIndex = Array.FindIndex(
            project.PendingItems.ToArray(),
            item => string.Equals(item.Record.RelativePath, selectedRecord.RelativePath, StringComparison.Ordinal));

        if (selectedIndex < 0)
        {
            return null;
        }

        return new OperatorReviewQueueContext(
            selectedIndex + 1,
            project.PendingItems.Count,
            selectedIndex > 0 ? project.PendingItems[selectedIndex - 1] : null,
            selectedIndex + 1 < project.PendingItems.Count ? project.PendingItems[selectedIndex + 1] : null);
    }

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath
            .Trim()
            .Replace('\\', '/');
}

public sealed record OperatorProjectSummary(
    string ProjectId,
    string Name,
    string? Status,
    string RootPath,
    int ArtifactCount,
    int PendingCount);

public sealed record OperatorArtifactRecord(
    string RelativePath,
    string FilePath,
    ArtifactDocument Artifact)
{
    public bool IsPendingReview => Artifact.Status is ArtifactStatus.Draft or ArtifactStatus.Proposed;
}

public sealed record OperatorPendingReviewItem(
    ApprovalQueueItem QueueItem,
    OperatorArtifactRecord Record);

public sealed record OperatorProjectSnapshot(
    ProjectWorkspace Workspace,
    IReadOnlyList<OperatorArtifactRecord> Artifacts,
    IReadOnlyList<OperatorPendingReviewItem> PendingItems)
{
    public IReadOnlyList<OperatorPendingReviewItem> ProposedItems { get; } =
        PendingItems
            .Where(item => item.QueueItem.PendingStatus == ArtifactStatus.Proposed)
            .ToArray();
}

public sealed record OperatorArtifactView(
    OperatorProjectSnapshot Project,
    OperatorArtifactRecord SelectedArtifact,
    ArtifactDocument? CurrentApprovedArtifact,
    ArtifactRevisionDiff? RevisionDiff,
    IReadOnlyList<string> DiffIssues,
    OperatorReviewQueueContext? ReviewQueueContext);

public sealed record OperatorReviewQueueContext(
    int Position,
    int TotalItems,
    OperatorPendingReviewItem? PreviousItem,
    OperatorPendingReviewItem? NextItem);

public sealed record OperatorArtifactEditInput(
    string? Title,
    string? Reason,
    string? Tags,
    IReadOnlyDictionary<string, string> Sections)
{
    public static OperatorArtifactEditInput FromForm(IFormCollection form)
    {
        ArgumentNullException.ThrowIfNull(form);

        var sections = form.Keys
            .Where(key => key.StartsWith("section:", StringComparison.Ordinal))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToDictionary(
                key => key["section:".Length..],
                key => form[key].ToString(),
                StringComparer.Ordinal);

        return new OperatorArtifactEditInput(
            form["title"].ToString(),
            form["reason"].ToString(),
            form["tags"].ToString(),
            sections);
    }

    public IReadOnlyList<string> ParseTags() =>
        string.IsNullOrWhiteSpace(Tags)
            ? []
            : Tags
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
}

public sealed class OperatorMutationResult
{
    private OperatorMutationResult(
        bool isSuccess,
        bool isNotFound,
        string? relativePath,
        IReadOnlyList<string> validationErrors)
    {
        IsSuccess = isSuccess;
        IsNotFound = isNotFound;
        RelativePath = relativePath;
        ValidationErrors = validationErrors;
    }

    public bool IsSuccess { get; }

    public bool IsNotFound { get; }

    public string? RelativePath { get; }

    public IReadOnlyList<string> ValidationErrors { get; }

    public static OperatorMutationResult Success(string relativePath) =>
        new(true, false, relativePath, []);

    public static OperatorMutationResult Invalid(IEnumerable<string> validationErrors) =>
        new(false, false, null, validationErrors.ToArray());

    public static OperatorMutationResult NotFound() =>
        new(false, true, null, []);
}
