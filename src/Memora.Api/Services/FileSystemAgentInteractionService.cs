using System.Globalization;
using System.Text.Json;
using Memora.Context.Assembly;
using Memora.Context.Models;
using Memora.Core.AgentInteraction;
using Memora.Core.Approval;
using Memora.Core.Artifacts;
using Memora.Core.Automation;
using Memora.Core.Import;
using Memora.Core.Validation;
using Memora.Import.Evidence;
using Memora.Import.Readiness;
using Memora.Storage.Parsing;
using Memora.Storage.Persistence;
using Memora.Storage.Workspaces;

namespace Memora.Api.Services;

public sealed class FileSystemAgentInteractionService : IAgentInteractionService, IReviewInboxService
{
    private readonly string _workspacesRootPath;
    private readonly WorkspaceDiscovery _workspaceDiscovery = new();
    private readonly ArtifactMarkdownParser _markdownParser = new();
    private readonly ArtifactMarkdownWriter _markdownWriter = new();
    private readonly ArtifactFactory _artifactFactory = new();
    private readonly ArtifactFileStore _fileStore = new();
    private readonly ArtifactApprovalWorkflow _approvalWorkflow = new();
    private readonly ContextBundleBuilder _contextBundleBuilder = new();
    private readonly ContextPackageCache _contextPackageCache = new();
    private readonly PolicyGovernedWriteSafetyValidator _writeSafetyValidator = new();
    private readonly FileBackedImportedEvidenceStore _evidenceStore = new();
    private readonly FileBackedFirstRunReportStore _firstRunReportStore = new();

    public FileSystemAgentInteractionService(string workspacesRootPath)
    {
        _workspacesRootPath = Path.GetFullPath(workspacesRootPath ?? throw new ArgumentNullException(nameof(workspacesRootPath)));
    }

    public ProjectLookupResponse GetProject(string projectId)
    {
        var workspace = FindWorkspace(projectId);
        return workspace is null
            ? new ProjectLookupResponse(
                projectId,
                null,
                null,
                [new AgentInteractionError("project.not_found", $"Project '{projectId}' was not found.", "project_id")])
            : new ProjectLookupResponse(
                workspace.ProjectId,
                workspace.Metadata.Name,
                workspace.Metadata.Status,
                [],
                workspace.Metadata.RepositoryAttachments,
                BuildImportReadinessState(workspace));
    }

    public ReviewInboxResponse GetReviewInbox(string projectId)
    {
        var workspace = FindWorkspace(projectId);
        if (workspace is null)
        {
            return new ReviewInboxResponse(
                projectId,
                [],
                [new AgentInteractionError("project.not_found", $"Project '{projectId}' was not found.", "project_id")]);
        }

        var records = LoadReviewArtifactRecords(workspace, out var errors);
        var items = records
            .Where(record => record.Artifact.Status is ArtifactStatus.Draft or ArtifactStatus.Proposed)
            .OrderBy(record => record.Artifact.Status == ArtifactStatus.Proposed ? 0 : 1)
            .ThenBy(record => record.Artifact.UpdatedAtUtc)
            .ThenBy(record => record.Artifact.Id, StringComparer.Ordinal)
            .Select(record => MapReviewInboxItem(record))
            .ToArray();

        return new ReviewInboxResponse(workspace.ProjectId, items, errors);
    }

    public ReviewArtifactPreviewResponse GetReviewArtifactPreview(string projectId, string relativePath)
    {
        var workspace = FindWorkspace(projectId);
        if (workspace is null)
        {
            return new ReviewArtifactPreviewResponse(
                projectId,
                null,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal),
                [new AgentInteractionError("project.not_found", $"Project '{projectId}' was not found.", "project_id")]);
        }

        if (!TryResolveWorkspacePath(workspace, relativePath, out var filePath))
        {
            return new ReviewArtifactPreviewResponse(
                workspace.ProjectId,
                null,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal),
                [new AgentInteractionError("review.path.invalid", "Review artifact path must stay inside the workspace.", "path")]);
        }

        if (!File.Exists(filePath))
        {
            return new ReviewArtifactPreviewResponse(
                workspace.ProjectId,
                null,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal),
                [new AgentInteractionError("review.artifact.not_found", $"Review artifact '{relativePath}' was not found.", "path")]);
        }

        var parsed = _markdownParser.Parse(File.ReadAllText(filePath));
        if (!parsed.Validation.IsValid || parsed.Artifact is null)
        {
            return new ReviewArtifactPreviewResponse(
                workspace.ProjectId,
                null,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal),
                MapErrors(parsed.Validation));
        }

        if (parsed.Artifact.Status is not ArtifactStatus.Draft and not ArtifactStatus.Proposed)
        {
            return new ReviewArtifactPreviewResponse(
                workspace.ProjectId,
                null,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal),
                [new AgentInteractionError("review.status.not_reviewable", "Only draft or proposed artifacts are available in the review inbox.", "status")]);
        }

        var record = new ParsedReviewArtifactRecord(
            parsed.Artifact,
            Path.GetFullPath(filePath),
            NormalizeRelativePath(Path.GetRelativePath(workspace.RootPath, filePath)));

        return new ReviewArtifactPreviewResponse(
            workspace.ProjectId,
            MapReviewInboxItem(record),
            parsed.Artifact.Body,
            parsed.Artifact.Sections,
            []);
    }

    public ReviewDecisionResponse ApplyReviewDecision(string projectId, ReviewDecisionRequest request)
    {
        var workspace = FindWorkspace(projectId);
        if (workspace is null)
        {
            return new ReviewDecisionResponse(
                projectId,
                request.Decision,
                null,
                null,
                [new AgentInteractionError("project.not_found", $"Project '{projectId}' was not found.", "project_id")]);
        }

        if (!TryLoadReviewArtifactRecord(workspace, request.RelativePath, out var record, out var errors))
        {
            return new ReviewDecisionResponse(workspace.ProjectId, request.Decision, null, null, errors);
        }

        return request.Decision switch
        {
            "approve" => ApproveReviewRecord(workspace, record!),
            "reject" => RejectReviewRecord(workspace, record!),
            _ => new ReviewDecisionResponse(
                workspace.ProjectId,
                request.Decision,
                null,
                null,
                [new AgentInteractionError("review.decision.invalid", "Review decision must be 'approve' or 'reject'.", "decision")])
        };
    }

    public GetContextResponse GetContext(GetContextRequest request)
    {
        var workspace = FindWorkspace(request.ProjectId);
        if (workspace is null)
        {
            return new GetContextResponse(
                null,
                [new AgentInteractionError("project.not_found", $"Project '{request.ProjectId}' was not found.", "project_id")]);
        }

        var artifacts = LoadArtifacts(workspace, includeDrafts: request.IncludeDraftArtifacts, includeSummaries: request.IncludeLayer3History, out var errors);
        if (errors.Count > 0)
        {
            return new GetContextResponse(null, errors);
        }

        var contextRequest = new ContextBundleRequest(
            request.ProjectId,
            request.TaskDescription,
            request.IncludeDraftArtifacts,
            request.IncludeLayer3History,
            request.FocusArtifactIds,
            request.FocusTags,
            request.MaxLayer2Artifacts,
            request.MaxLayer3Artifacts);
        var cachedPackage = _contextPackageCache.GetOrBuild(
            contextRequest,
            artifacts,
            _contextBundleBuilder.Build);
        var bundle = cachedPackage.Bundle;

        return new GetContextResponse(MapBundle(request, bundle), []);
    }

    public ProposalResponse ProposeArtifact(ProposeArtifactRequest request)
    {
        var workspace = FindWorkspace(request.ProjectId);
        if (workspace is null)
        {
            return new ProposalResponse(
                request.ProjectId,
                request.ArtifactId,
                request.ArtifactType,
                ArtifactStatus.Proposed,
                0,
                [new AgentInteractionError("project.not_found", $"Project '{request.ProjectId}' was not found.", "project_id")]);
        }

        var existingArtifacts = LoadArtifacts(workspace, includeDrafts: true, includeSummaries: false, out var loadErrors);
        if (HasConflictingLoadError(loadErrors, request.ArtifactId))
        {
            return new ProposalResponse(request.ProjectId, request.ArtifactId, request.ArtifactType, ArtifactStatus.Proposed, 0, loadErrors);
        }

        if (existingArtifacts.Any(artifact => string.Equals(artifact.Id, request.ArtifactId, StringComparison.Ordinal)))
        {
            return new ProposalResponse(
                request.ProjectId,
                request.ArtifactId,
                request.ArtifactType,
                ArtifactStatus.Proposed,
                0,
                [new AgentInteractionError("proposal.artifact_id.exists", $"Artifact '{request.ArtifactId}' already exists.", "artifact_id")]);
        }

        var createdArtifact = CreateArtifact(
            request.ProjectId,
            request.ArtifactId,
            request.ArtifactType,
            request.Content,
            ArtifactStatus.Proposed,
            revision: 1,
            createdAtUtc: DateTimeOffset.UtcNow,
            updatedAtUtc: DateTimeOffset.UtcNow);

        if (createdArtifact.Errors.Count > 0 || createdArtifact.Artifact is null)
        {
            return new ProposalResponse(request.ProjectId, request.ArtifactId, request.ArtifactType, ArtifactStatus.Proposed, 0, createdArtifact.Errors);
        }

        try
        {
            _fileStore.Save(workspace, createdArtifact.Artifact);
        }
        catch (ArtifactPersistenceException exception) when (exception.Code == "artifact.revision.exists")
        {
            return new ProposalResponse(
                request.ProjectId,
                request.ArtifactId,
                request.ArtifactType,
                ArtifactStatus.Proposed,
                0,
                [CreateProposalConflictError(request.ArtifactId)]);
        }

        return new ProposalResponse(request.ProjectId, request.ArtifactId, request.ArtifactType, createdArtifact.Artifact.Status, createdArtifact.Artifact.Revision, [], loadErrors);
    }

    public ProposalResponse ProposeUpdate(ProposeUpdateRequest request)
    {
        var workspace = FindWorkspace(request.ProjectId);
        if (workspace is null)
        {
            return new ProposalResponse(
                request.ProjectId,
                request.ArtifactId,
                ArtifactType.Plan,
                ArtifactStatus.Proposed,
                0,
                [new AgentInteractionError("project.not_found", $"Project '{request.ProjectId}' was not found.", "project_id")]);
        }

        var existingArtifacts = LoadArtifacts(workspace, includeDrafts: true, includeSummaries: false, out var loadErrors);
        if (HasConflictingLoadError(loadErrors, request.ArtifactId))
        {
            return new ProposalResponse(request.ProjectId, request.ArtifactId, ArtifactType.Plan, ArtifactStatus.Proposed, 0, loadErrors);
        }

        var currentArtifact = existingArtifacts
            .Where(artifact => string.Equals(artifact.Id, request.ArtifactId, StringComparison.Ordinal))
            .OrderByDescending(artifact => artifact.Revision)
            .FirstOrDefault();

        if (currentArtifact is null)
        {
            return new ProposalResponse(
                request.ProjectId,
                request.ArtifactId,
                ArtifactType.Plan,
                ArtifactStatus.Proposed,
                0,
                [new AgentInteractionError("proposal.artifact_id.not_found", $"Artifact '{request.ArtifactId}' was not found.", "artifact_id")]);
        }

        if (currentArtifact.Revision != request.ExpectedRevision)
        {
            return new ProposalResponse(
                request.ProjectId,
                request.ArtifactId,
                currentArtifact.Type,
                ArtifactStatus.Proposed,
                0,
                [new AgentInteractionError("proposal.revision.mismatch", $"Expected revision {request.ExpectedRevision} but found {currentArtifact.Revision}.", "expected_revision")]);
        }

        var updatedArtifact = CreateArtifact(
            request.ProjectId,
            request.ArtifactId,
            currentArtifact.Type,
            request.Content,
            ArtifactStatus.Proposed,
            revision: currentArtifact.Revision + 1,
            createdAtUtc: currentArtifact.CreatedAtUtc,
            updatedAtUtc: DateTimeOffset.UtcNow);

        if (updatedArtifact.Errors.Count > 0 || updatedArtifact.Artifact is null)
        {
            return new ProposalResponse(request.ProjectId, request.ArtifactId, currentArtifact.Type, ArtifactStatus.Proposed, 0, updatedArtifact.Errors);
        }

        try
        {
            _fileStore.Save(workspace, updatedArtifact.Artifact);
        }
        catch (ArtifactPersistenceException exception) when (exception.Code == "artifact.revision.exists")
        {
            return new ProposalResponse(
                request.ProjectId,
                request.ArtifactId,
                currentArtifact.Type,
                ArtifactStatus.Proposed,
                0,
                [CreateProposalConflictError(request.ArtifactId)]);
        }

        return new ProposalResponse(request.ProjectId, request.ArtifactId, currentArtifact.Type, updatedArtifact.Artifact.Status, updatedArtifact.Artifact.Revision, [], loadErrors);
    }

    public OutcomeResponse RecordOutcome(RecordOutcomeRequest request) =>
        RecordOutcomeInternal(request);

    public PolicyGovernedWriteResponse WriteSessionSummary(
        PolicyGovernedSessionSummaryWriteRequest request)
    {
        var workspace = FindWorkspace(request.ProjectId);
        if (workspace is null)
        {
            return new PolicyGovernedWriteResponse(
                request.ProjectId,
                request.ArtifactId,
                request.ArtifactType,
                ArtifactStatus.Proposed,
                0,
                AutomationStorageScope.Summary,
                null,
                [new AgentInteractionError("project.not_found", $"Project '{request.ProjectId}' was not found.", "project_id")]);
        }

        var safetyResult = _writeSafetyValidator.Validate(
            new PolicyGovernedWriteSafetyRequest(
                request.ProjectId,
                request.ArtifactId,
                ArtifactType.SessionSummary,
                AutomationStorageScope.Summary,
                request.Policy,
                request.TriggerEvent));
        if (!safetyResult.IsAllowed)
        {
            return new PolicyGovernedWriteResponse(
                request.ProjectId,
                request.ArtifactId,
                request.ArtifactType,
                ArtifactStatus.Proposed,
                0,
                AutomationStorageScope.Summary,
                null,
                MapSafetyErrors(safetyResult));
        }

        var existingArtifacts = LoadArtifacts(workspace, includeDrafts: true, includeSummaries: true, out var loadErrors);
        if (loadErrors.Count > 0)
        {
            return new PolicyGovernedWriteResponse(
                request.ProjectId,
                request.ArtifactId,
                request.ArtifactType,
                ArtifactStatus.Proposed,
                0,
                AutomationStorageScope.Summary,
                null,
                loadErrors);
        }

        if (existingArtifacts.Any(artifact => string.Equals(artifact.Id, request.ArtifactId, StringComparison.Ordinal)))
        {
            return new PolicyGovernedWriteResponse(
                request.ProjectId,
                request.ArtifactId,
                request.ArtifactType,
                ArtifactStatus.Proposed,
                0,
                AutomationStorageScope.Summary,
                null,
                [new AgentInteractionError("automation.write.artifact_id.exists", $"Artifact '{request.ArtifactId}' already exists.", "artifact_id")]);
        }

        var createdArtifact = CreateArtifact(
            request.ProjectId,
            request.ArtifactId,
            ArtifactType.SessionSummary,
            request.Content,
            ArtifactStatus.Proposed,
            revision: 1,
            createdAtUtc: DateTimeOffset.UtcNow,
            updatedAtUtc: DateTimeOffset.UtcNow);

        if (createdArtifact.Errors.Count > 0 || createdArtifact.Artifact is not SessionSummaryArtifact summaryArtifact)
        {
            return new PolicyGovernedWriteResponse(
                request.ProjectId,
                request.ArtifactId,
                request.ArtifactType,
                ArtifactStatus.Proposed,
                0,
                AutomationStorageScope.Summary,
                null,
                createdArtifact.Errors);
        }

        var writtenPath = _fileStore.Save(workspace, summaryArtifact);
        return new PolicyGovernedWriteResponse(
            request.ProjectId,
            request.ArtifactId,
            request.ArtifactType,
            summaryArtifact.Status,
            summaryArtifact.Revision,
            AutomationStorageScope.Summary,
            writtenPath,
            []);
    }

    private ProjectWorkspace? FindWorkspace(string projectId)
    {
        if (!Directory.Exists(_workspacesRootPath))
        {
            return null;
        }

        return _workspaceDiscovery
            .Discover(_workspacesRootPath)
            .SingleOrDefault(workspace => string.Equals(workspace.ProjectId, projectId, StringComparison.Ordinal));
    }

    private ImportedProjectReadinessState BuildImportReadinessState(ProjectWorkspace workspace)
    {
        var diagnostics = new List<ImportedProjectReadinessDiagnostic>();
        var evidenceRecords = ReadEvidence(workspace, diagnostics);
        var firstRunReport = ReadFirstRunReport(workspace, diagnostics);
        IReadOnlyList<CandidateMemoryRecord> candidates = firstRunReport?.Candidates ?? Array.Empty<CandidateMemoryRecord>();
        var readinessReport = firstRunReport?.ReadinessReport;

        return new ImportedProjectReadinessState(
            readinessReport is null ? null : "summaries/first-run-readiness.json",
            HasReadinessReport: readinessReport is not null,
            GroundedContextReady: readinessReport?.ReadyForAgentUse ?? false,
            EvidenceRecordCount: evidenceRecords.Count,
            CandidateCount: candidates.Count,
            BaselineEvidenceCount: evidenceRecords.Count(record => record.TrustState == ImportedEvidenceTrustState.BaselineEvidence),
            CanonicalEvidenceCount: evidenceRecords.Count(record => record.TrustState == ImportedEvidenceTrustState.CanonicalEvidence),
            ReviewableEvidenceCount: evidenceRecords.Count(record => record.TrustState == ImportedEvidenceTrustState.ReviewableEvidence),
            EvidenceDerivedCandidateCount: candidates.Count(candidate => candidate.Source == CandidateMemorySource.EvidenceDerived),
            InferredCandidateCount: candidates.Count(candidate => candidate.Source == CandidateMemorySource.Inferred),
            AdvisoryCandidateCount: candidates.Count(candidate => candidate.Source == CandidateMemorySource.Advisory),
            FutureAdvisoryGapCount: readinessReport?.AdvisoryDiscoveryGaps.Count ?? 0,
            readinessReport?.AdvisoryDiscoveryGaps ?? [],
            readinessReport?.MissingContext ?? [],
            readinessReport?.MissingTests ?? [],
            readinessReport?.RiskyModules ?? [],
            readinessReport?.NextReviewSteps ?? [],
            diagnostics);
    }

    private static AgentInteractionError CreateProposalConflictError(string artifactId) =>
        new(
            "proposal.conflict",
            $"Artifact '{artifactId}' was changed by another writer before the proposal could be saved. Refresh project state and retry.",
            "artifact_id");

    private static bool HasConflictingLoadError(IReadOnlyList<AgentInteractionError> loadErrors, string artifactId) =>
        loadErrors.Any(error =>
            !string.IsNullOrWhiteSpace(error.Path) &&
            Path.GetFileName(error.Path).StartsWith($"{artifactId}.r", StringComparison.Ordinal));

    private IReadOnlyList<ImportedEvidenceRecord> ReadEvidence(
        ProjectWorkspace workspace,
        ICollection<ImportedProjectReadinessDiagnostic> diagnostics)
    {
        try
        {
            return _evidenceStore.ReadAll(workspace.RootPath);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            diagnostics.Add(new ImportedProjectReadinessDiagnostic(
                "import.evidence.load_failed",
                $"Imported evidence could not be loaded: {exception.Message}",
                "evidence"));
            return [];
        }
    }

    private FirstRunMemoryGenerationResult? ReadFirstRunReport(
        ProjectWorkspace workspace,
        ICollection<ImportedProjectReadinessDiagnostic> diagnostics)
    {
        try
        {
            return _firstRunReportStore.Load(workspace.RootPath);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            diagnostics.Add(new ImportedProjectReadinessDiagnostic(
                "import.readiness.load_failed",
                $"First-run readiness report could not be loaded: {exception.Message}",
                "summaries/first-run-readiness.json"));
            return null;
        }
    }

    private IReadOnlyList<ArtifactDocument> LoadArtifacts(
        ProjectWorkspace workspace,
        bool includeDrafts,
        bool includeSummaries,
        out IReadOnlyList<AgentInteractionError> errors)
    {
        var artifacts = new List<ArtifactDocument>();
        var collectedErrors = new List<AgentInteractionError>();

        foreach (var filePath in EnumerateArtifactFiles(workspace, includeDrafts, includeSummaries))
        {
            var parsed = _markdownParser.Parse(File.ReadAllText(filePath));
            if (!parsed.Validation.IsValid || parsed.Artifact is null)
            {
                collectedErrors.AddRange(parsed.Validation.Issues.Select(issue =>
                    new AgentInteractionError(issue.Code, issue.DiagnosticMessage, filePath)));
                continue;
            }

            if (!string.Equals(parsed.Artifact.ProjectId, workspace.ProjectId, StringComparison.Ordinal))
            {
                collectedErrors.Add(new AgentInteractionError(
                    "artifact.project_id.mismatch",
                    $"Artifact '{parsed.Artifact.Id}' belongs to project '{parsed.Artifact.ProjectId}' instead of '{workspace.ProjectId}'.",
                    filePath));
                continue;
            }

            artifacts.Add(parsed.Artifact);
        }

        errors = collectedErrors;
        return artifacts;
    }

    private IReadOnlyList<ParsedReviewArtifactRecord> LoadReviewArtifactRecords(
        ProjectWorkspace workspace,
        out IReadOnlyList<AgentInteractionError> errors)
    {
        var records = new List<ParsedReviewArtifactRecord>();
        var collectedErrors = new List<AgentInteractionError>();

        foreach (var filePath in EnumerateArtifactFiles(workspace, includeDrafts: true, includeSummaries: false))
        {
            var parsed = _markdownParser.Parse(File.ReadAllText(filePath));
            if (!parsed.Validation.IsValid || parsed.Artifact is null)
            {
                collectedErrors.AddRange(parsed.Validation.Issues.Select(issue =>
                    new AgentInteractionError(issue.Code, issue.DiagnosticMessage, issue.Path ?? filePath)));
                continue;
            }

            records.Add(new ParsedReviewArtifactRecord(
                parsed.Artifact,
                Path.GetFullPath(filePath),
                NormalizeRelativePath(Path.GetRelativePath(workspace.RootPath, filePath))));
        }

        errors = collectedErrors;
        return records;
    }

    private bool TryLoadReviewArtifactRecord(
        ProjectWorkspace workspace,
        string relativePath,
        out ParsedReviewArtifactRecord? record,
        out IReadOnlyList<AgentInteractionError> errors)
    {
        record = null;

        if (!TryResolveWorkspacePath(workspace, relativePath, out var filePath))
        {
            errors =
            [
                new AgentInteractionError("review.path.invalid", "Review artifact path must stay inside the workspace.", "path")
            ];
            return false;
        }

        if (!File.Exists(filePath))
        {
            errors =
            [
                new AgentInteractionError("review.artifact.not_found", $"Review artifact '{relativePath}' was not found.", "path")
            ];
            return false;
        }

        var parsed = _markdownParser.Parse(File.ReadAllText(filePath));
        if (!parsed.Validation.IsValid || parsed.Artifact is null)
        {
            errors = MapErrors(parsed.Validation);
            return false;
        }

        if (parsed.Artifact.Status is not ArtifactStatus.Draft and not ArtifactStatus.Proposed)
        {
            errors =
            [
                new AgentInteractionError("review.status.not_reviewable", "Only draft or proposed artifacts are available for review decisions.", "status")
            ];
            return false;
        }

        record = new ParsedReviewArtifactRecord(
            parsed.Artifact,
            Path.GetFullPath(filePath),
            NormalizeRelativePath(Path.GetRelativePath(workspace.RootPath, filePath)));
        errors = [];
        return true;
    }

    private ReviewDecisionResponse ApproveReviewRecord(
        ProjectWorkspace workspace,
        ParsedReviewArtifactRecord record)
    {
        var currentApprovedArtifact = LoadArtifacts(workspace, includeDrafts: false, includeSummaries: false, out var loadErrors)
            .Where(artifact => string.Equals(artifact.Id, record.Artifact.Id, StringComparison.Ordinal))
            .Where(artifact => artifact.Status == ArtifactStatus.Approved)
            .OrderByDescending(artifact => artifact.Revision)
            .ThenByDescending(artifact => artifact.UpdatedAtUtc)
            .FirstOrDefault();

        if (loadErrors.Count > 0)
        {
            return new ReviewDecisionResponse(workspace.ProjectId, "approve", null, null, loadErrors);
        }

        var decision = _approvalWorkflow.Approve(
            record.Artifact,
            DateTimeOffset.UtcNow,
            currentApprovedArtifact);

        if (!decision.IsSuccess || decision.ApprovedArtifact is null)
        {
            return new ReviewDecisionResponse(
                workspace.ProjectId,
                "approve",
                null,
                null,
                MapErrors(decision.Validation));
        }

        try
        {
            var approvedPath = _fileStore.Save(workspace, decision.ApprovedArtifact);
            File.Delete(record.FilePath);
            var approvedRecord = new ParsedReviewArtifactRecord(
                decision.ApprovedArtifact,
                Path.GetFullPath(approvedPath),
                NormalizeRelativePath(Path.GetRelativePath(workspace.RootPath, approvedPath)));

            return new ReviewDecisionResponse(
                workspace.ProjectId,
                "approve",
                MapReviewInboxItem(approvedRecord),
                $"Approved {decision.ApprovedArtifact.Id} revision {decision.ApprovedArtifact.Revision}.",
                []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new ReviewDecisionResponse(
                workspace.ProjectId,
                "approve",
                null,
                null,
                [new AgentInteractionError("review.approval.persistence_failed", $"Approval persistence failed: {exception.Message}", "path")]);
        }
    }

    private ReviewDecisionResponse RejectReviewRecord(
        ProjectWorkspace workspace,
        ParsedReviewArtifactRecord record)
    {
        var decision = _approvalWorkflow.Reject(record.Artifact, DateTimeOffset.UtcNow);
        if (!decision.IsSuccess || decision.RejectedArtifact is null)
        {
            return new ReviewDecisionResponse(
                workspace.ProjectId,
                "reject",
                null,
                null,
                MapErrors(decision.Validation));
        }

        try
        {
            File.WriteAllText(record.FilePath, _markdownWriter.Write(decision.RejectedArtifact));
            var rejectedRecord = new ParsedReviewArtifactRecord(
                decision.RejectedArtifact,
                record.FilePath,
                record.RelativePath);

            return new ReviewDecisionResponse(
                workspace.ProjectId,
                "reject",
                MapReviewInboxItem(rejectedRecord),
                $"Rejected {decision.RejectedArtifact.Id} revision {decision.RejectedArtifact.Revision}.",
                []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ReviewDecisionResponse(
                workspace.ProjectId,
                "reject",
                null,
                null,
                [new AgentInteractionError("review.rejection.persistence_failed", $"Rejection persistence failed: {exception.Message}", "path")]);
        }
    }

    private static ReviewInboxItem MapReviewInboxItem(ParsedReviewArtifactRecord record) =>
        new(
            record.Artifact.Id,
            record.Artifact.Type,
            record.Artifact.Status,
            record.Artifact.Title,
            record.Artifact.Revision,
            record.Artifact.Provenance,
            record.Artifact.Reason,
            record.RelativePath,
            record.FilePath,
            "valid",
            record.Artifact.UpdatedAtUtc);

    private static bool TryResolveWorkspacePath(ProjectWorkspace workspace, string relativePath, out string filePath)
    {
        filePath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(workspace.RootPath, NormalizeRelativePath(relativePath)));
        var workspaceRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspace.RootPath)) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        filePath = candidate;
        return true;
    }

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath.Trim().Replace('\\', '/');

    private static IReadOnlyList<string> EnumerateArtifactFiles(ProjectWorkspace workspace, bool includeDrafts, bool includeSummaries)
    {
        var files = new List<string>();
        AddMarkdownFiles(files, workspace.CanonicalRootPath);

        if (includeDrafts)
        {
            AddMarkdownFiles(files, workspace.DraftsRootPath);
        }

        if (includeSummaries)
        {
            AddMarkdownFiles(files, workspace.SummariesRootPath);
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }

    private static void AddMarkdownFiles(ICollection<string> files, string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(rootPath, "*.md", SearchOption.AllDirectories))
        {
            files.Add(filePath);
        }
    }

    private CreatedArtifactResult CreateArtifact(
        string projectId,
        string artifactId,
        ArtifactType artifactType,
        ArtifactProposalContent content,
        ArtifactStatus status,
        int revision,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        var frontmatter = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = artifactId,
            ["project_id"] = projectId,
            ["type"] = artifactType.ToSchemaValue(),
            ["status"] = status.ToSchemaValue(),
            ["title"] = content.Title,
            ["created_at"] = createdAtUtc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["updated_at"] = updatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["revision"] = revision,
            ["tags"] = content.Tags.Select(tag => (object?)tag).ToList(),
            ["provenance"] = content.Provenance,
            ["reason"] = content.Reason,
            ["links"] = BuildLinksMap(content.Links.ToArtifactLinks())
        };

        foreach (var pair in content.TypeSpecificValues)
        {
            frontmatter[pair.Key] = NormalizeRuntimeValue(pair.Value);
        }

        var sections = new Dictionary<string, string>(content.Sections, StringComparer.Ordinal);
        var body = string.Join("\n\n", sections.Select(section => $"## {section.Key}\n{section.Value}"));
        var result = _artifactFactory.Create(frontmatter, body, sections);

        return result.Artifact is null
            ? new CreatedArtifactResult(null, MapErrors(result.Validation))
            : new CreatedArtifactResult(result.Artifact, []);
    }

    private static Dictionary<string, object?> BuildLinksMap(ArtifactLinks links)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var key in ArtifactLinks.FrontmatterKeys)
        {
            ArtifactLinks.TryParseKind(key, out var kind);
            map[key] = links.GetTargetArtifactIds(kind).Select(target => (object?)target).ToList();
        }

        return map;
    }

    private static object? NormalizeRuntimeValue(object? value) =>
        value switch
        {
            JsonElement element => NormalizeJsonElement(element),
            _ => value
        };

    private static object? NormalizeJsonElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => element.EnumerateArray().Select(NormalizeJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                property => property.Name,
                property => NormalizeJsonElement(property.Value),
                StringComparer.Ordinal),
            _ => null
        };

    private static IReadOnlyList<AgentInteractionError> MapErrors(ArtifactValidationResult validation) =>
        validation.Issues
            .Select(issue => new AgentInteractionError(issue.Code, issue.DiagnosticMessage, issue.Path))
            .ToArray();

    private static IReadOnlyList<AgentInteractionError> MapSafetyErrors(
        PolicyGovernedWriteSafetyResult safetyResult) =>
        safetyResult.Issues
            .Select(issue => new AgentInteractionError(issue.Code, issue.DiagnosticMessage, issue.Path))
            .ToArray();

    // The Normalize step here is load-bearing: it produces the same canonical ordering used by
    // ProjectStateViewSerializer on the MCP path, which is what keeps the OpenAPI HTTP body
    // byte-identical to the MCP-serialized bundle in RuntimeContractCompatibilityTests.
    private static AgentContextBundle MapBundle(GetContextRequest request, ContextBundle bundle) =>
        ProjectStateViewSerializer.Normalize(
            new(
            request,
            bundle.Layers.Select(layer =>
                new AgentContextLayer(
                    layer.Kind switch
                    {
                        ContextLayerKind.Layer1 => AgentContextLayerKind.Layer1,
                        ContextLayerKind.Layer2 => AgentContextLayerKind.Layer2,
                        _ => AgentContextLayerKind.Layer3
                    },
                    layer.Artifacts.Select(artifact =>
                        new AgentContextArtifact(
                            artifact.Artifact,
                            artifact.InclusionReasons.Select(reason =>
                                new AgentContextInclusionReason(reason.Code, reason.Description, reason.RelatedArtifactIds)).ToArray()))
                        .ToArray()))
                .ToArray()));

    private OutcomeResponse RecordOutcomeInternal(RecordOutcomeRequest request)
    {
        var workspace = FindWorkspace(request.ProjectId);
        if (workspace is null)
        {
            return new OutcomeResponse(
                request.ProjectId,
                request.ArtifactId,
                ArtifactStatus.Proposed,
                0,
                OutcomeKind.Mixed,
                [new AgentInteractionError("project.not_found", $"Project '{request.ProjectId}' was not found.", "project_id")]);
        }

        var existingArtifacts = LoadArtifacts(workspace, includeDrafts: true, includeSummaries: false, out var loadErrors);
        if (loadErrors.Count > 0)
        {
            return new OutcomeResponse(request.ProjectId, request.ArtifactId, ArtifactStatus.Proposed, 0, OutcomeKind.Mixed, loadErrors);
        }

        var currentArtifact = existingArtifacts
            .Where(artifact => string.Equals(artifact.Id, request.ArtifactId, StringComparison.Ordinal))
            .OrderByDescending(artifact => artifact.Revision)
            .FirstOrDefault();

        if (currentArtifact is not null && currentArtifact.Type != ArtifactType.Outcome)
        {
            return new OutcomeResponse(
                request.ProjectId,
                request.ArtifactId,
                ArtifactStatus.Proposed,
                0,
                OutcomeKind.Mixed,
                [new AgentInteractionError("outcome.artifact_type.invalid", $"Artifact '{request.ArtifactId}' is not an outcome artifact.", "artifact_id")]);
        }

        var createdAtUtc = currentArtifact?.CreatedAtUtc ?? DateTimeOffset.UtcNow;
        var revision = currentArtifact?.Revision + 1 ?? 1;
        var recordedOutcome = CreateArtifact(
            request.ProjectId,
            request.ArtifactId,
            ArtifactType.Outcome,
            request.Content,
            ArtifactStatus.Proposed,
            revision,
            createdAtUtc,
            DateTimeOffset.UtcNow);

        if (recordedOutcome.Errors.Count > 0 || recordedOutcome.Artifact is not OutcomeArtifact outcomeArtifact)
        {
            return new OutcomeResponse(request.ProjectId, request.ArtifactId, ArtifactStatus.Proposed, 0, OutcomeKind.Mixed, recordedOutcome.Errors);
        }

        _fileStore.Save(workspace, outcomeArtifact);
        return new OutcomeResponse(request.ProjectId, request.ArtifactId, outcomeArtifact.Status, outcomeArtifact.Revision, outcomeArtifact.Outcome, []);
    }

    private sealed record CreatedArtifactResult(
        ArtifactDocument? Artifact,
        IReadOnlyList<AgentInteractionError> Errors)
    {
        public ArtifactDocument? Artifact { get; } = Artifact;
        public IReadOnlyList<AgentInteractionError> Errors { get; } = Errors;
    }

    private sealed record ParsedReviewArtifactRecord(
        ArtifactDocument Artifact,
        string FilePath,
        string RelativePath);
}
