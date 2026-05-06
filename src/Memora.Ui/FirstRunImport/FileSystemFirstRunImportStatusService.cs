using Memora.Core.Import;
using Memora.Import.Evidence;
using Memora.Import.Readiness;
using Memora.Storage.Workspaces;

namespace Memora.Ui.FirstRunImport;

public sealed class FileSystemFirstRunImportStatusService
{
    private readonly string _workspacesRootPath;
    private readonly WorkspaceDiscovery _workspaceDiscovery = new();
    private readonly FileBackedImportedEvidenceStore _evidenceStore = new();
    private readonly FileBackedFirstRunReportStore _reportStore = new();

    public FileSystemFirstRunImportStatusService(string workspacesRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacesRootPath);
        _workspacesRootPath = Path.GetFullPath(workspacesRootPath);
    }

    public FirstRunImportStatusPage? TryBuildPage(
        string projectId,
        string? requestedImportMode,
        FirstRunGitHubImportRunResult? githubImportResult = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        var workspace = _workspaceDiscovery
            .Discover(_workspacesRootPath)
            .SingleOrDefault(candidate => string.Equals(candidate.ProjectId, projectId, StringComparison.Ordinal));

        if (workspace is null)
        {
            return null;
        }

        var warnings = new List<string>();
        var evidence = LoadEvidence(workspace.RootPath, warnings);
        var report = LoadReadinessReport(workspace.RootPath, warnings);
        var importModeSelection = SelectImportMode(requestedImportMode, evidence, report);
        var candidates = BuildCandidateViews(report, evidence);

        warnings.AddRange(BuildReadinessWarnings(workspace.Metadata.RepositoryAttachments.Count, evidence.Count, report));

        return new FirstRunImportStatusPage(
            workspace.ProjectId,
            workspace.Metadata.Name,
            workspace.RootPath,
            importModeSelection.Mode,
            importModeSelection.Source,
            workspace.Metadata.RepositoryAttachments,
            BuildSuggestedGitHubRemoteUrl(workspace.Metadata.RepositoryAttachments),
            BuildProgressSteps(workspace.Metadata.RepositoryAttachments.Count, evidence.Count, report),
            BuildEvidenceSummaries(evidence),
            candidates,
            report?.ReadinessReport,
            warnings.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            BuildNextActions(workspace.ProjectId, report),
            githubImportResult);
    }

    private IReadOnlyList<ImportedEvidenceRecord> LoadEvidence(string workspaceRootPath, ICollection<string> warnings)
    {
        try
        {
            return _evidenceStore.ReadAll(workspaceRootPath);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            warnings.Add($"Imported evidence could not be loaded: {exception.Message}");
            return [];
        }
    }

    private FirstRunMemoryGenerationResult? LoadReadinessReport(string workspaceRootPath, ICollection<string> warnings)
    {
        try
        {
            return _reportStore.Load(workspaceRootPath);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            warnings.Add($"First-run readiness report could not be loaded: {exception.Message}");
            return null;
        }
    }

    private static FirstRunImportModeSelection SelectImportMode(
        string? requestedImportMode,
        IReadOnlyList<ImportedEvidenceRecord> evidence,
        FirstRunMemoryGenerationResult? report)
    {
        if (ImportModeExtensions.TryParseSchemaValue(requestedImportMode, out var parsedMode))
        {
            return new FirstRunImportModeSelection(parsedMode, FirstRunImportModeSelectionSource.OperatorSelected);
        }

        if (evidence.Any(record => record.TrustState == ImportedEvidenceTrustState.CanonicalEvidence))
        {
            return new FirstRunImportModeSelection(ImportMode.EvidenceCanonical, FirstRunImportModeSelectionSource.InferredFromEvidence);
        }

        if (evidence.Any(record => record.TrustState == ImportedEvidenceTrustState.BaselineEvidence))
        {
            return new FirstRunImportModeSelection(ImportMode.FastBaseline, FirstRunImportModeSelectionSource.InferredFromEvidence);
        }

        if (report?.Candidates.Any(candidate => candidate.Disposition == CandidateMemoryDisposition.GroupedBaselineReview) == true)
        {
            return new FirstRunImportModeSelection(ImportMode.BulkApproval, FirstRunImportModeSelectionSource.InferredFromReadiness);
        }

        return new FirstRunImportModeSelection(ImportMode.StrictGovernance, FirstRunImportModeSelectionSource.Defaulted);
    }

    private static IReadOnlyList<FirstRunProgressStep> BuildProgressSteps(
        int attachmentCount,
        int evidenceCount,
        FirstRunMemoryGenerationResult? report) =>
        [
            new(
                "Repository attached",
                attachmentCount > 0 ? FirstRunProgressState.Complete : FirstRunProgressState.Waiting,
                attachmentCount > 0 ? $"{attachmentCount} repository attachment(s) found." : "No repository attachment is recorded yet."),
            new(
                "Evidence import",
                evidenceCount > 0 ? FirstRunProgressState.Complete : FirstRunProgressState.Waiting,
                evidenceCount > 0 ? $"{evidenceCount} imported evidence record(s) found." : "No imported evidence records are stored yet."),
            new(
                "Candidate memory",
                report is not null ? FirstRunProgressState.Complete : FirstRunProgressState.Waiting,
                report is null ? "No first-run candidate memory report is stored yet." : $"{report.Candidates.Count} candidate memory item(s) found."),
            new(
                "Agent readiness",
                report?.ReadinessReport.ReadyForAgentUse == true ? FirstRunProgressState.Ready : report is null ? FirstRunProgressState.Waiting : FirstRunProgressState.NeedsReview,
                report is null
                    ? "Readiness is waiting on candidate generation."
                    : report.ReadinessReport.ReadyForAgentUse
                        ? "Grounded context is ready for agent setup."
                        : "Operator review is needed before agent setup.")
        ];

    private static IReadOnlyList<FirstRunEvidenceSummary> BuildEvidenceSummaries(IReadOnlyList<ImportedEvidenceRecord> evidence) =>
        evidence
            .GroupBy(record => new { record.SourceType, record.TrustState })
            .Select(group => new FirstRunEvidenceSummary(group.Key.SourceType, group.Key.TrustState, group.Count()))
            .OrderBy(summary => summary.SourceType)
            .ThenBy(summary => summary.TrustState)
            .ToArray();

    private static string? BuildSuggestedGitHubRemoteUrl(IReadOnlyList<ProjectRepositoryAttachment> attachments)
    {
        var githubAttachmentRemote = attachments
            .Where(attachment => attachment.Kind == RepositoryAttachmentKind.GitHub)
            .Select(attachment => attachment.RemoteUrl ?? attachment.OriginUrl)
            .FirstOrDefault(IsGitHubRemote);

        if (!string.IsNullOrWhiteSpace(githubAttachmentRemote))
        {
            return githubAttachmentRemote;
        }

        return attachments
            .SelectMany(attachment => new[] { attachment.RemoteUrl, attachment.OriginUrl })
            .FirstOrDefault(IsGitHubRemote);
    }

    private static bool IsGitHubRemote(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Contains("github.com", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<FirstRunCandidateView> BuildCandidateViews(
        FirstRunMemoryGenerationResult? report,
        IReadOnlyList<ImportedEvidenceRecord> evidence)
    {
        if (report is null)
        {
            return [];
        }

        var evidenceByStableId = evidence.ToDictionary(record => record.StableId, StringComparer.Ordinal);

        return report.Candidates
            .Select(candidate => new FirstRunCandidateView(
                candidate.CandidateId,
                candidate.Kind,
                candidate.Source,
                candidate.Title,
                candidate.Summary,
                candidate.Confidence,
                candidate.Ambiguity,
                candidate.ExtractionReason,
                candidate.Disposition,
                candidate.EvidenceStableIds
                    .Select(id => BuildEvidenceProvenance(id, evidenceByStableId))
                    .ToArray()))
            .OrderBy(candidate => candidate.Kind)
            .ThenBy(candidate => candidate.Title, StringComparer.Ordinal)
            .ToArray();
    }

    private static string BuildEvidenceProvenance(
        string stableId,
        IReadOnlyDictionary<string, ImportedEvidenceRecord> evidenceByStableId)
    {
        if (!evidenceByStableId.TryGetValue(stableId, out var evidence))
        {
            return stableId;
        }

        return $"{evidence.StableId} | {evidence.SourceType.ToSchemaValue()} | {evidence.Provenance}";
    }

    private static IEnumerable<string> BuildReadinessWarnings(
        int attachmentCount,
        int evidenceCount,
        FirstRunMemoryGenerationResult? report)
    {
        if (attachmentCount == 0)
        {
            yield return "No attached source repository is recorded for this project.";
        }

        if (evidenceCount == 0)
        {
            yield return "No imported evidence is recorded yet.";
        }

        if (report is null)
        {
            yield return "No first-run readiness report is recorded yet.";
            yield break;
        }

        foreach (var warning in report.ReadinessReport.MissingContext)
        {
            yield return warning;
        }

        foreach (var warning in report.ReadinessReport.MissingTests)
        {
            yield return warning;
        }

        foreach (var warning in report.ReadinessReport.RiskyModules)
        {
            yield return warning;
        }
    }

    private static IReadOnlyList<FirstRunNextAction> BuildNextActions(
        string projectId,
        FirstRunMemoryGenerationResult? report)
    {
        var actions = new List<FirstRunNextAction>
        {
            new("Review candidates", $"/projects/{Uri.EscapeDataString(projectId)}/queue", "Inspect review-needed project memory."),
            new("Check agent setup", $"/context-viewer?projectId={Uri.EscapeDataString(projectId)}&taskDescription=Prepare%20agent%20readiness", "Open governed context for the attached project."),
            new("Re-import", $"/projects/{Uri.EscapeDataString(projectId)}/first-run-import", "Refresh this status after running a bounded import again.")
        };

        actions.AddRange(
            report?.ReadinessReport.NextReviewSteps.Select(step => new FirstRunNextAction("Next review step", null, step))
            ?? []);

        actions.AddRange(
            report?.ReadinessReport.AdvisoryDiscoveryGaps.Select(gap => new FirstRunNextAction("Later advisory discovery", null, gap))
            ?? []);

        return actions;
    }
}

public sealed record FirstRunImportStatusPage(
    string ProjectId,
    string ProjectName,
    string WorkspaceRootPath,
    ImportMode SelectedImportMode,
    FirstRunImportModeSelectionSource ImportModeSelectionSource,
    IReadOnlyList<ProjectRepositoryAttachment> RepositoryAttachments,
    string? SuggestedGitHubRemoteUrl,
    IReadOnlyList<FirstRunProgressStep> ProgressSteps,
    IReadOnlyList<FirstRunEvidenceSummary> EvidenceSummaries,
    IReadOnlyList<FirstRunCandidateView> Candidates,
    AgentReadinessReport? ReadinessReport,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<FirstRunNextAction> NextActions,
    FirstRunGitHubImportRunResult? GitHubImportResult = null)
{
    public int EvidenceRecordCount => EvidenceSummaries.Sum(summary => summary.Count);

    public int BaselineEvidenceCount => EvidenceSummaries
        .Where(summary => summary.TrustState == ImportedEvidenceTrustState.BaselineEvidence)
        .Sum(summary => summary.Count);

    public int CanonicalEvidenceCount => EvidenceSummaries
        .Where(summary => summary.TrustState == ImportedEvidenceTrustState.CanonicalEvidence)
        .Sum(summary => summary.Count);

    public int ReviewableEvidenceCount => EvidenceSummaries
        .Where(summary => summary.TrustState == ImportedEvidenceTrustState.ReviewableEvidence)
        .Sum(summary => summary.Count);

    public int BaselineMemoryCandidateCount => Candidates.Count(candidate => candidate.Disposition == CandidateMemoryDisposition.BaselineMemory);

    public int ReviewRequiredCandidateCount => Candidates.Count(candidate => candidate.Disposition == CandidateMemoryDisposition.ReviewRequired);

    public int GroupedBaselineReviewCandidateCount => Candidates.Count(candidate => candidate.Disposition == CandidateMemoryDisposition.GroupedBaselineReview);

    public int EvidenceDerivedCandidateCount => Candidates.Count(candidate => candidate.Source == CandidateMemorySource.EvidenceDerived);

    public int InferredCandidateCount => Candidates.Count(candidate => candidate.Source == CandidateMemorySource.Inferred);

    public int AdvisoryCandidateCount => Candidates.Count(candidate => candidate.Source == CandidateMemorySource.Advisory);

    public int FutureAdvisoryGapCount => ReadinessReport?.AdvisoryDiscoveryGaps.Count ?? 0;
}

public sealed record FirstRunImportModeSelection(
    ImportMode Mode,
    FirstRunImportModeSelectionSource Source);

public enum FirstRunImportModeSelectionSource
{
    OperatorSelected,
    InferredFromEvidence,
    InferredFromReadiness,
    Defaulted
}

public sealed record FirstRunProgressStep(
    string Label,
    FirstRunProgressState State,
    string Detail);

public enum FirstRunProgressState
{
    Waiting,
    Complete,
    NeedsReview,
    Ready
}

public sealed record FirstRunEvidenceSummary(
    ImportedEvidenceSourceType SourceType,
    ImportedEvidenceTrustState TrustState,
    int Count);

public sealed record FirstRunCandidateView(
    string CandidateId,
    CandidateMemoryKind Kind,
    CandidateMemorySource Source,
    string Title,
    string Summary,
    double Confidence,
    string Ambiguity,
    string ExtractionReason,
    CandidateMemoryDisposition Disposition,
    IReadOnlyList<string> EvidenceProvenance);

public sealed record FirstRunNextAction(
    string Label,
    string? Url,
    string Detail);
