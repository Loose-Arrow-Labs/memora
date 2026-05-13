using Memora.Core.Approval;
using Memora.Core.Artifacts;
using Memora.Core.Editing;
using Memora.Core.Import;
using Memora.Core.Revisions;
using Memora.Index.Rebuild;
using Memora.Import.Evidence;
using Memora.Import.Readiness;
using Memora.Storage.Parsing;
using Memora.Storage.Persistence;
using Memora.Storage.Workspaces;
using Microsoft.Data.Sqlite;

namespace Memora.Ui.Operator;

public sealed class LocalOperatorWorkspaceService
{
    private readonly WorkspaceDiscovery _workspaceDiscovery = new();
    private readonly ArtifactMarkdownParser _markdownParser = new();
    private readonly ArtifactFileStore _artifactFileStore = new();
    private readonly ArtifactMarkdownWriter _artifactMarkdownWriter = new();
    private readonly ApprovalQueueBuilder _approvalQueueBuilder = new();
    private readonly ArtifactApprovalWorkflow _approvalWorkflow = new();
    private readonly DraftArtifactEditor _draftArtifactEditor = new();
    private readonly ArtifactRevisionDiffBuilder _diffBuilder = new();
    private readonly SqliteIndexRebuilder _indexRebuilder = new();
    private readonly FileBackedImportedEvidenceStore _evidenceStore = new();
    private readonly FileBackedFirstRunReportStore _firstRunReportStore = new();
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
            BuildReviewQueueContext(project, selectedRecord),
            BuildProvenanceReview(project.Workspace, selectedRecord.Artifact));
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

    public OperatorReviewDecisionResult ApplyReviewDecision(
        string projectId,
        string relativePath,
        OperatorReviewDecision decision)
    {
        var artifactView = TryGetArtifactView(projectId, relativePath);
        if (artifactView is null || !artifactView.SelectedArtifact.IsPendingReview)
        {
            return OperatorReviewDecisionResult.NotFound();
        }

        return decision switch
        {
            OperatorReviewDecision.Approve => ApproveReviewItem(artifactView),
            OperatorReviewDecision.Reject => RejectReviewItem(artifactView),
            OperatorReviewDecision.Promote => PromoteReviewItem(artifactView),
            _ => OperatorReviewDecisionResult.Invalid(["Unsupported review decision."])
        };
    }

    private OperatorReviewDecisionResult ApproveReviewItem(OperatorArtifactView artifactView)
    {
        if (!artifactView.ProvenanceReview.IsApprovalReady)
        {
            return OperatorReviewDecisionResult.Invalid([artifactView.ProvenanceReview.ReadinessMessage]);
        }

        var decision = _approvalWorkflow.Approve(
            artifactView.SelectedArtifact.Artifact,
            DateTimeOffset.UtcNow,
            artifactView.CurrentApprovedArtifact);

        if (!decision.IsSuccess || decision.ApprovedArtifact is null)
        {
            return OperatorReviewDecisionResult.Invalid(
                decision.Validation.Issues.Select(issue => issue.DiagnosticMessage));
        }

        var pendingPath = artifactView.SelectedArtifact.FilePath;
        var pendingBackupPath = CreateBackupPath(pendingPath, ".approving");
        var currentApprovedRecord = decision.SupersededArtifact is null
            ? null
            : FindCurrentApprovedRecord(artifactView);
        var currentApprovedPath = currentApprovedRecord?.FilePath;
        var currentApprovedBackupPath = currentApprovedPath is null
            ? null
            : CreateBackupPath(currentApprovedPath, ".superseding");
        string? approvedPath = null;
        string? supersededPath = null;

        try
        {
            File.Move(pendingPath, pendingBackupPath);

            if (currentApprovedPath is not null && currentApprovedBackupPath is not null)
            {
                File.Move(currentApprovedPath, currentApprovedBackupPath);
            }

            if (decision.SupersededArtifact is not null)
            {
                supersededPath = _artifactFileStore.Save(artifactView.Project.Workspace, decision.SupersededArtifact);
            }

            approvedPath = _artifactFileStore.Save(artifactView.Project.Workspace, decision.ApprovedArtifact);
            DeleteIfExists(pendingBackupPath);
            DeleteIfExists(currentApprovedBackupPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            RollBackApprovalPersistence(
                pendingPath,
                pendingBackupPath,
                currentApprovedPath,
                currentApprovedBackupPath,
                approvedPath,
                supersededPath);

            return OperatorReviewDecisionResult.Invalid([$"Approval persistence failed: {exception.Message}"]);
        }

        return OperatorReviewDecisionResult.Success(
            $"Approved {decision.ApprovedArtifact.Id} revision {decision.ApprovedArtifact.Revision}.");
    }

    private OperatorReviewDecisionResult PromoteReviewItem(OperatorArtifactView artifactView)
    {
        var decision = _approvalWorkflow.Promote(
            artifactView.SelectedArtifact.Artifact,
            DateTimeOffset.UtcNow);

        if (!decision.IsSuccess || decision.PromotedArtifact is null)
        {
            return OperatorReviewDecisionResult.Invalid(
                decision.Validation.Issues.Select(issue => issue.DiagnosticMessage));
        }

        try
        {
            File.WriteAllText(
                artifactView.SelectedArtifact.FilePath,
                _artifactMarkdownWriter.Write(decision.PromotedArtifact));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return OperatorReviewDecisionResult.Invalid([$"Promotion persistence failed: {exception.Message}"]);
        }

        return OperatorReviewDecisionResult.Success(
            $"Promoted {decision.PromotedArtifact.Id} revision {decision.PromotedArtifact.Revision} to draft. Re-open the queue to review and approve it.");
    }

    private OperatorReviewDecisionResult RejectReviewItem(OperatorArtifactView artifactView)
    {
        var decision = _approvalWorkflow.Reject(
            artifactView.SelectedArtifact.Artifact,
            DateTimeOffset.UtcNow);

        if (!decision.IsSuccess || decision.RejectedArtifact is null)
        {
            return OperatorReviewDecisionResult.Invalid(
                decision.Validation.Issues.Select(issue => issue.DiagnosticMessage));
        }

        try
        {
            File.WriteAllText(
                artifactView.SelectedArtifact.FilePath,
                _artifactMarkdownWriter.Write(decision.RejectedArtifact));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return OperatorReviewDecisionResult.Invalid([$"Rejection persistence failed: {exception.Message}"]);
        }

        return OperatorReviewDecisionResult.Success(
            $"Rejected {decision.RejectedArtifact.Id} revision {decision.RejectedArtifact.Revision}.");
    }

    private static OperatorArtifactRecord? FindCurrentApprovedRecord(OperatorArtifactView artifactView) =>
        artifactView.CurrentApprovedArtifact is null
            ? null
            : artifactView.Project.Artifacts.Single(record =>
                string.Equals(record.Artifact.Id, artifactView.CurrentApprovedArtifact.Id, StringComparison.Ordinal) &&
                record.Artifact.Status == ArtifactStatus.Approved &&
                record.Artifact.Revision == artifactView.CurrentApprovedArtifact.Revision &&
                record.Artifact.UpdatedAtUtc == artifactView.CurrentApprovedArtifact.UpdatedAtUtc);

    private static string CreateBackupPath(string path, string suffix)
    {
        var backupPath = path + suffix;
        if (File.Exists(backupPath))
        {
            throw new IOException($"Approval backup file '{backupPath}' already exists.");
        }

        return backupPath;
    }

    private static void RollBackApprovalPersistence(
        string pendingPath,
        string pendingBackupPath,
        string? currentApprovedPath,
        string? currentApprovedBackupPath,
        string? approvedPath,
        string? supersededPath)
    {
        DeleteIfExists(approvedPath);
        DeleteIfExists(supersededPath);
        RestoreIfMissing(currentApprovedBackupPath, currentApprovedPath);
        RestoreIfMissing(pendingBackupPath, pendingPath);
    }

    private static void RestoreIfMissing(string? backupPath, string? restorePath)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || string.IsNullOrWhiteSpace(restorePath))
        {
            return;
        }

        if (File.Exists(backupPath) && !File.Exists(restorePath))
        {
            File.Move(backupPath, restorePath);
        }
    }

    private static void DeleteIfExists(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public OperatorTrustDashboard? TryBuildTrustDashboard(string projectId)
    {
        var snapshot = TryGetProject(projectId);
        if (snapshot is null)
        {
            return null;
        }

        var importWarnings = new List<string>();
        var firstRunReport = ReadFirstRunReport(snapshot.Workspace, importWarnings);
        AddReadinessWarnings(firstRunReport, importWarnings);
        var rebuildResult = BuildRebuildDiagnostics(snapshot, importWarnings);
        var staleCutoff = DateTimeOffset.UtcNow.AddDays(-30);
        var staleDraftCount = snapshot.Artifacts.Count(record =>
            record.Artifact.Status == ArtifactStatus.Draft &&
            record.Artifact.UpdatedAtUtc < staleCutoff);
        var missingMemoryCount = firstRunReport is null
            ? 1
            : firstRunReport.ReadinessReport.MissingContext.Count;
        var relationshipDiagnosticCount = rebuildResult.Diagnostics.Count(diagnostic => diagnostic.Code == "index.relationship.target.invalid");

        var metrics = new[]
        {
            new OperatorTrustDashboardMetric(
                "Pending proposals",
                snapshot.ProposedItems.Count,
                snapshot.ProposedItems.Count == 0 ? OperatorTrustMetricState.Ready : OperatorTrustMetricState.NeedsReview,
                "Reviewable proposed artifacts waiting for human inspection.",
                $"/projects/{Uri.EscapeDataString(snapshot.Workspace.ProjectId)}/proposals"),
            new OperatorTrustDashboardMetric(
                "Stale drafts",
                staleDraftCount,
                staleDraftCount == 0 ? OperatorTrustMetricState.Ready : OperatorTrustMetricState.NeedsReview,
                "Draft artifacts older than 30 days from their last filesystem-backed update.",
                $"/projects/{Uri.EscapeDataString(snapshot.Workspace.ProjectId)}/queue"),
            new OperatorTrustDashboardMetric(
                "Broken relationships",
                relationshipDiagnosticCount,
                relationshipDiagnosticCount == 0 ? OperatorTrustMetricState.Ready : OperatorTrustMetricState.Blocked,
                "Approved relationship references that fail derived-index rebuild validation.",
                $"/understanding?projectId={Uri.EscapeDataString(snapshot.Workspace.ProjectId)}"),
            new OperatorTrustDashboardMetric(
                "Rebuild diagnostics",
                rebuildResult.Diagnostics.Count,
                rebuildResult.Success ? OperatorTrustMetricState.Ready : OperatorTrustMetricState.Blocked,
                rebuildResult.Summary,
                $"/understanding?projectId={Uri.EscapeDataString(snapshot.Workspace.ProjectId)}"),
            new OperatorTrustDashboardMetric(
                "Missing project memory",
                missingMemoryCount,
                missingMemoryCount == 0 ? OperatorTrustMetricState.Ready : OperatorTrustMetricState.NeedsReview,
                firstRunReport is null ? "No first-run readiness report is stored for this workspace." : "Missing context items reported by first-run readiness.",
                $"/projects/{Uri.EscapeDataString(snapshot.Workspace.ProjectId)}/first-run-import"),
            new OperatorTrustDashboardMetric(
                "Recent import warnings",
                importWarnings.Count,
                importWarnings.Count == 0 ? OperatorTrustMetricState.Ready : OperatorTrustMetricState.NeedsReview,
                importWarnings.Count == 0 ? "No import loading warnings were reported while building this dashboard." : string.Join("; ", importWarnings),
                $"/projects/{Uri.EscapeDataString(snapshot.Workspace.ProjectId)}/first-run-import")
        };

        return new OperatorTrustDashboard(snapshot.Workspace.ProjectId, snapshot.Workspace.Metadata.Name, metrics);
    }

    private IndexRebuildResult BuildRebuildDiagnostics(
        OperatorProjectSnapshot snapshot,
        ICollection<string> warnings)
    {
        try
        {
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            var result = _indexRebuilder.Rebuild(connection, _options.NormalizedWorkspacesRootPath);
            return ScopeRebuildResult(result, snapshot);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or SqliteException)
        {
            warnings.Add($"Derived rebuild diagnostics could not be loaded: {exception.Message}");
            return new IndexRebuildResult(
                0,
                0,
                0,
                0,
                [new IndexRebuildDiagnostic(_options.NormalizedWorkspacesRootPath, "trust.rebuild.failed", exception.Message, "workspacesRoot")],
                0,
                0,
                IndexRebuildStatus.Failed);
        }
    }

    private static IndexRebuildResult ScopeRebuildResult(
        IndexRebuildResult result,
        OperatorProjectSnapshot snapshot)
    {
        var diagnostics = result.Diagnostics
            .Where(diagnostic => IsUnderRoot(diagnostic.FilePath, snapshot.Workspace.RootPath))
            .ToArray();
        var filesystemArtifactFileCount = snapshot.Artifacts.Count;

        if (diagnostics.Length > 0)
        {
            return new IndexRebuildResult(0, 0, 0, 0, diagnostics, 1, filesystemArtifactFileCount, IndexRebuildStatus.Failed);
        }

        var artifactCount = snapshot.Artifacts
            .Select(record => record.Artifact.Id)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var relationshipCount = snapshot.Artifacts
            .Where(record => record.Artifact.Status == ArtifactStatus.Approved)
            .Where(record => record.RelativePath.StartsWith("canonical/", StringComparison.Ordinal))
            .Sum(record => record.Artifact.Links.Relationships.Count);

        return new IndexRebuildResult(
            1,
            artifactCount,
            snapshot.Artifacts.Count,
            relationshipCount,
            diagnostics,
            1,
            filesystemArtifactFileCount,
            IndexRebuildStatus.Succeeded);
    }

    private static void AddReadinessWarnings(
        FirstRunMemoryGenerationResult? firstRunReport,
        ICollection<string> warnings)
    {
        if (firstRunReport is null)
        {
            return;
        }

        foreach (var warning in firstRunReport.ReadinessReport.MissingContext
                     .Concat(firstRunReport.ReadinessReport.MissingTests)
                     .Concat(firstRunReport.ReadinessReport.RiskyModules)
                     .Concat(firstRunReport.ReadinessReport.AdvisoryDiscoveryGaps))
        {
            warnings.Add(warning);
        }
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

    private OperatorProvenanceReview BuildProvenanceReview(ProjectWorkspace workspace, ArtifactDocument artifact)
    {
        var warnings = new List<string>();
        var evidence = ReadEvidence(workspace, warnings);
        var evidenceById = evidence.ToDictionary(record => record.StableId, StringComparer.Ordinal);
        var evidenceIds = ExtractEvidenceIds(artifact, evidenceById);
        var missingEvidenceIds = evidenceIds
            .Where(id => !evidenceById.ContainsKey(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var directEvidence = evidenceIds
            .Where(evidenceById.ContainsKey)
            .Select(id => evidenceById[id])
            .Select(record => new OperatorEvidenceProvenanceItem(
                record.StableId,
                record.SourceType,
                record.SourceReference,
                record.Title,
                record.Summary,
                record.Provenance,
                record.TrustState,
                record.ObservedAtUtc,
                record.ImportedAtUtc))
            .ToArray();
        var candidates = ReadFirstRunReport(workspace, warnings)?.Candidates ?? [];
        var candidateNotes = candidates
            .Where(candidate => ReferencesArtifact(candidate, artifact, evidenceIds))
            .Select(candidate => new OperatorCandidateProvenanceItem(
                candidate.CandidateId,
                candidate.Kind,
                candidate.Source,
                candidate.Title,
                candidate.Summary,
                candidate.Confidence,
                candidate.Ambiguity,
                candidate.ExtractionReason,
                candidate.Disposition,
                candidate.EvidenceStableIds))
            .ToArray();
        var requiresImportedEvidence = artifact.Status == ArtifactStatus.Proposed;
        var isApprovalReady = !requiresImportedEvidence || (directEvidence.Length > 0 && missingEvidenceIds.Length == 0);
        var readinessMessage = isApprovalReady
            ? "Required provenance is present for this review item."
            : "Approval readiness is blocked until required imported evidence provenance resolves.";

        return new OperatorProvenanceReview(
            requiresImportedEvidence,
            isApprovalReady,
            readinessMessage,
            artifact.Provenance,
            evidenceIds,
            missingEvidenceIds,
            directEvidence,
            candidateNotes,
            warnings);
    }

    private IReadOnlyList<ImportedEvidenceRecord> ReadEvidence(ProjectWorkspace workspace, ICollection<string> warnings)
    {
        try
        {
            return _evidenceStore.ReadAll(workspace.RootPath);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            warnings.Add($"Imported evidence could not be loaded: {exception.Message}");
            return [];
        }
    }

    private FirstRunMemoryGenerationResult? ReadFirstRunReport(ProjectWorkspace workspace, ICollection<string> warnings)
    {
        try
        {
            return _firstRunReportStore.Load(workspace.RootPath);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            warnings.Add($"First-run readiness report could not be loaded: {exception.Message}");
            return null;
        }
    }

    private static IReadOnlyList<string> ExtractEvidenceIds(
        ArtifactDocument artifact,
        IReadOnlyDictionary<string, ImportedEvidenceRecord> evidenceById)
    {
        var ids = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var token in artifact.Provenance.Split([' ', ',', ';', '|', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = token.Trim('.', ':', '#', '[', ']', '(', ')');
            if (normalized.StartsWith("evidence:", StringComparison.Ordinal))
            {
                ids.Add(normalized["evidence:".Length..]);
                continue;
            }

            if (evidenceById.ContainsKey(normalized))
            {
                ids.Add(normalized);
            }
        }

        return ids.ToArray();
    }

    private static bool ReferencesArtifact(
        CandidateMemoryRecord candidate,
        ArtifactDocument artifact,
        IReadOnlyList<string> evidenceIds) =>
        string.Equals(candidate.CandidateId, artifact.Provenance, StringComparison.Ordinal) ||
        artifact.Provenance.Contains(candidate.CandidateId, StringComparison.Ordinal) ||
        candidate.EvidenceStableIds.Intersect(evidenceIds, StringComparer.Ordinal).Any();

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath
            .Trim()
            .Replace('\\', '/');

    private static bool IsUnderRoot(string filePath, string rootPath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var normalizedRootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath)) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase);
    }
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
    OperatorReviewQueueContext? ReviewQueueContext,
    OperatorProvenanceReview ProvenanceReview);

public sealed record OperatorReviewQueueContext(
    int Position,
    int TotalItems,
    OperatorPendingReviewItem? PreviousItem,
    OperatorPendingReviewItem? NextItem);

public sealed record OperatorTrustDashboard(
    string ProjectId,
    string ProjectName,
    IReadOnlyList<OperatorTrustDashboardMetric> Metrics);

public sealed record OperatorTrustDashboardMetric(
    string Label,
    int Count,
    OperatorTrustMetricState State,
    string Detail,
    string Url);

public enum OperatorTrustMetricState
{
    Ready,
    NeedsReview,
    Blocked
}

public sealed record OperatorProvenanceReview(
    bool RequiresImportedEvidence,
    bool IsApprovalReady,
    string ReadinessMessage,
    string DeclaredProvenance,
    IReadOnlyList<string> DeclaredEvidenceIds,
    IReadOnlyList<string> MissingEvidenceIds,
    IReadOnlyList<OperatorEvidenceProvenanceItem> DirectEvidence,
    IReadOnlyList<OperatorCandidateProvenanceItem> CandidateNotes,
    IReadOnlyList<string> Warnings);

public sealed record OperatorEvidenceProvenanceItem(
    string StableId,
    ImportedEvidenceSourceType SourceType,
    string SourceReference,
    string Title,
    string Summary,
    string Provenance,
    ImportedEvidenceTrustState TrustState,
    DateTimeOffset ObservedAtUtc,
    DateTimeOffset ImportedAtUtc);

public sealed record OperatorCandidateProvenanceItem(
    string CandidateId,
    CandidateMemoryKind Kind,
    CandidateMemorySource Source,
    string Title,
    string Summary,
    double Confidence,
    string Ambiguity,
    string ExtractionReason,
    CandidateMemoryDisposition Disposition,
    IReadOnlyList<string> EvidenceStableIds);

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

public enum OperatorReviewDecision
{
    Approve,
    Reject,
    Promote
}

public sealed class OperatorReviewDecisionResult
{
    private OperatorReviewDecisionResult(
        bool isSuccess,
        bool isNotFound,
        string? message,
        IReadOnlyList<string> validationErrors)
    {
        IsSuccess = isSuccess;
        IsNotFound = isNotFound;
        Message = message;
        ValidationErrors = validationErrors;
    }

    public bool IsSuccess { get; }

    public bool IsNotFound { get; }

    public string? Message { get; }

    public IReadOnlyList<string> ValidationErrors { get; }

    public static OperatorReviewDecisionResult Success(string message) =>
        new(true, false, message, []);

    public static OperatorReviewDecisionResult Invalid(IEnumerable<string> validationErrors) =>
        new(false, false, null, validationErrors.ToArray());

    public static OperatorReviewDecisionResult NotFound() =>
        new(false, true, null, []);
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
