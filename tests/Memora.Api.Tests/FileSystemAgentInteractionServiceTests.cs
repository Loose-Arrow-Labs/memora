using Memora.Api.Services;
using Memora.Core.AgentInteraction;
using Memora.Core.Artifacts;
using Memora.Core.Automation;
using Memora.Core.Import;
using Memora.Core.Projects;
using Memora.Import.Evidence;
using Memora.Import.Readiness;
using Memora.Storage.Parsing;
using Memora.Storage.Persistence;
using Memora.Storage.Workspaces;

namespace Memora.Api.Tests;

public sealed class FileSystemAgentInteractionServiceTests : IDisposable
{
    private readonly string _workspacesRootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-agent-service-tests",
        Guid.NewGuid().ToString("N"));

    private readonly ArtifactFileStore _fileStore = new();

    [Fact]
    public void GetProject_ReturnsAttachedRepositoryMetadataForIntegrationResolution()
    {
        var workspace = CreateWorkspace("memora");
        File.WriteAllText(
            workspace.ProjectMetadataPath,
            """
            {
              "projectId": "memora",
              "name": "Memora",
              "status": "active",
              "repositoryAttachments": [
                {
                  "attachmentId": "ATT-123",
                  "projectId": "memora",
                  "kind": "github",
                  "repositoryIdentity": "github:https://github.com/alucero270/memora.git",
                  "remoteUrl": "https://github.com/alucero270/memora.git",
                  "defaultBranch": "main",
                  "originRemoteName": "origin",
                  "originUrl": "https://github.com/alucero270/memora.git",
                  "attachedAtUtc": "2026-05-05T18:00:00Z"
                }
              ]
            }
            """);
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.GetProject("memora");

        Assert.True(response.IsSuccess);
        var attachment = Assert.Single(response.RepositoryAttachments);
        Assert.Equal("ATT-123", attachment.AttachmentId);
        Assert.Equal("github:https://github.com/alucero270/memora.git", attachment.RepositoryIdentity);
    }

    [Fact]
    public void GetProject_ReturnsImportedReadinessStateForAttachedWorkspace()
    {
        var workspace = CreateWorkspace("memora");
        File.WriteAllText(
            workspace.ProjectMetadataPath,
            """
            {
              "projectId": "memora",
              "name": "Memora",
              "status": "active",
              "repositoryAttachments": [
                {
                  "attachmentId": "ATT-123",
                  "projectId": "memora",
                  "kind": "github",
                  "repositoryIdentity": "github:https://github.com/alucero270/memora.git",
                  "remoteUrl": "https://github.com/alucero270/memora.git",
                  "defaultBranch": "main",
                  "originRemoteName": "origin",
                  "originUrl": "https://github.com/alucero270/memora.git",
                  "attachedAtUtc": "2026-05-05T18:00:00Z"
                }
              ]
            }
            """);
        SeedFirstRunImport(workspace.RootPath, workspace.ProjectId);
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.GetProject("memora");

        Assert.True(response.IsSuccess);
        var readiness = Assert.IsType<ImportedProjectReadinessState>(response.ImportReadiness);
        Assert.True(readiness.HasReadinessReport);
        Assert.True(readiness.GroundedContextReady);
        Assert.Equal("summaries/first-run-readiness.json", readiness.ReadinessReportPath);
        Assert.Equal(3, readiness.EvidenceRecordCount);
        Assert.Equal(1, readiness.BaselineEvidenceCount);
        Assert.Equal(1, readiness.CanonicalEvidenceCount);
        Assert.Equal(1, readiness.ReviewableEvidenceCount);
        Assert.Equal(3, readiness.CandidateCount);
        Assert.Equal(1, readiness.EvidenceDerivedCandidateCount);
        Assert.Equal(1, readiness.InferredCandidateCount);
        Assert.Equal(1, readiness.AdvisoryCandidateCount);
        Assert.Equal(1, readiness.FutureAdvisoryGapCount);
        Assert.Contains("Advisory discovery can inspect CI for extra readiness hints.", readiness.AdvisoryDiscoveryGaps);
    }

    [Fact]
    public void ProposeArtifact_PersistsProposalInDraftStorage()
    {
        var workspace = CreateWorkspace("memora");
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.ProposeArtifact(
            new ProposeArtifactRequest(
                "memora",
                "ADR-101",
                ArtifactType.Decision,
                CreateDecisionContent()));

        Assert.True(response.IsSuccess);
        Assert.Equal(ArtifactStatus.Proposed, response.ResultingStatus);
        Assert.True(File.Exists(Path.Combine(workspace.DraftsRootPath, "decision", "ADR-101.r0001.md")));
        Assert.False(File.Exists(Path.Combine(workspace.CanonicalDecisionsPath, "ADR-101.r0001.md")));
    }

    [Fact]
    public async Task ProposeArtifact_ConcurrentDuplicateWrites_ReturnStructuredConflicts()
    {
        var workspace = CreateWorkspace("memora");
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);
        var request = new ProposeArtifactRequest("memora", "ADR-101", ArtifactType.Decision, CreateDecisionContent());

        var responses = await RunConcurrently(() => service.ProposeArtifact(request));

        Assert.Equal(1, responses.Count(response => response.IsSuccess));
        Assert.Equal(
            15,
            responses.Count(response =>
                !response.IsSuccess &&
                response.Errors.Any(error => error.Code is "proposal.conflict" or "proposal.artifact_id.exists")));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(workspace.DraftsRootPath, "decision"), "ADR-101.r0001.md"));
    }

    [Fact]
    public void ProposeArtifact_UnrelatedMalformedExistingArtifact_StillPersistsProposalWithDiagnostics()
    {
        var workspace = CreateWorkspace("memora");
        WriteMalformedDraft(workspace, "BROKEN", revision: 1);
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.ProposeArtifact(
            new ProposeArtifactRequest(
                "memora",
                "ADR-292",
                ArtifactType.Decision,
                CreateDecisionContent()));

        Assert.True(response.IsSuccess);
        Assert.Contains(response.Diagnostics, diagnostic => diagnostic.Code == "frontmatter.parse");
        Assert.True(File.Exists(Path.Combine(workspace.DraftsRootPath, "decision", "ADR-292.r0001.md")));
    }

    [Fact]
    public void ProposeArtifact_MalformedExistingArtifactWithSameId_BlocksProposal()
    {
        var workspace = CreateWorkspace("memora");
        WriteMalformedDraft(workspace, "ADR-292", revision: 1);
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.ProposeArtifact(
            new ProposeArtifactRequest(
                "memora",
                "ADR-292",
                ArtifactType.Decision,
                CreateDecisionContent()));

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Code == "frontmatter.parse");
        Assert.DoesNotContain(response.Diagnostics, diagnostic => diagnostic.Code == "frontmatter.parse");
    }

    [Fact]
    public void ProposeArtifact_ForeignProjectExistingArtifactWithSameId_BlocksProposal()
    {
        var workspace = CreateWorkspace("memora");
        WriteForeignProjectDraft(workspace, "ADR-292", revision: 1);
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.ProposeArtifact(
            new ProposeArtifactRequest(
                "memora",
                "ADR-292",
                ArtifactType.Decision,
                CreateDecisionContent()));

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Code == "artifact.project_id.mismatch");
        Assert.DoesNotContain(response.Diagnostics, diagnostic => diagnostic.Code == "artifact.project_id.mismatch");
    }

    [Fact]
    public void ProposeUpdate_CreatesNewProposedRevisionWithoutChangingApprovedFile()
    {
        var workspace = CreateWorkspace("memora");
        _fileStore.Save(workspace, CreateApprovedDecisionArtifact());
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.ProposeUpdate(
            new ProposeUpdateRequest(
                "memora",
                "ADR-001",
                1,
                CreateDecisionContent("Updated context decision")));

        Assert.True(response.IsSuccess);
        Assert.Equal(2, response.Revision);
        Assert.True(File.Exists(Path.Combine(workspace.CanonicalDecisionsPath, "ADR-001.r0001.md")));
        Assert.True(File.Exists(Path.Combine(workspace.DraftsRootPath, "decision", "ADR-001.r0002.md")));
    }

    [Fact]
    public async Task ProposeUpdate_ConcurrentSameRevisionWrites_ReturnStructuredConflicts()
    {
        var workspace = CreateWorkspace("memora");
        _fileStore.Save(workspace, CreateApprovedDecisionArtifact());
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);
        var request = new ProposeUpdateRequest("memora", "ADR-001", 1, CreateDecisionContent("Concurrent context decision"));

        var responses = await RunConcurrently(() => service.ProposeUpdate(request));

        Assert.Equal(1, responses.Count(response => response.IsSuccess));
        Assert.Equal(
            15,
            responses.Count(response =>
                !response.IsSuccess &&
                response.Errors.Any(error => error.Code is "proposal.conflict" or "proposal.revision.mismatch")));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(workspace.DraftsRootPath, "decision"), "ADR-001.r0002.md"));
    }

    [Fact]
    public void ProposeUpdate_ForeignProjectExistingArtifactWithSameId_BlocksProposal()
    {
        var workspace = CreateWorkspace("memora");
        WriteForeignProjectDraft(workspace, "ADR-292", revision: 1);
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.ProposeUpdate(
            new ProposeUpdateRequest(
                "memora",
                "ADR-292",
                1,
                CreateDecisionContent("Updated context decision")));

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Code == "artifact.project_id.mismatch");
        Assert.DoesNotContain(response.Diagnostics, diagnostic => diagnostic.Code == "artifact.project_id.mismatch");
    }

    [Fact]
    public void ProposeArtifact_InvalidProposal_ReturnsValidationErrorsAndDoesNotWriteFile()
    {
        var workspace = CreateWorkspace("memora");
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.ProposeArtifact(
            new ProposeArtifactRequest(
                "memora",
                "ADR-102",
                ArtifactType.Decision,
                new ArtifactProposalContent(
                    "Invalid decision",
                    "agent",
                    "Missing decision date.",
                    ["context"],
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["Context"] = "Need deterministic context.",
                        ["Decision"] = "Still missing required type-specific values.",
                        ["Alternatives Considered"] = "Implicit behavior.",
                        ["Consequences"] = "Validation should reject this."
                    })));

        Assert.False(response.IsSuccess);
        var error = Assert.Single(response.Errors, error => error.Code == "artifact.frontmatter.missing");
        Assert.Contains("code: artifact.frontmatter.missing", error.Message, StringComparison.Ordinal);
        Assert.Contains("path: decision_date", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(workspace.DraftsRootPath, "decision", "ADR-102.r0001.md")));
    }

    [Fact]
    public void RecordOutcome_PersistsOutcomeArtifactInDraftStorage()
    {
        var workspace = CreateWorkspace("memora");
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.RecordOutcome(
            new RecordOutcomeRequest(
                "memora",
                "OUT-001",
                CreateOutcomeContent()));

        Assert.True(response.IsSuccess);
        Assert.Equal(ArtifactStatus.Proposed, response.ResultingStatus);
        Assert.Equal(OutcomeKind.Success, response.OutcomeKind);
        Assert.True(File.Exists(Path.Combine(workspace.DraftsRootPath, "outcome", "OUT-001.r0001.md")));
    }

    [Fact]
    public void RecordOutcome_InvalidOutcome_ReturnsValidationErrorsAndDoesNotWriteFile()
    {
        var workspace = CreateWorkspace("memora");
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.RecordOutcome(
            new RecordOutcomeRequest(
                "memora",
                "OUT-002",
                new ArtifactProposalContent(
                    "Incomplete outcome",
                    "agent",
                    "Missing outcome kind.",
                    ["outcome"],
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["What Happened"] = "Outcome data exists.",
                        ["Why"] = "Need to validate outcome submissions.",
                        ["Impact"] = "Still missing type-specific outcome data.",
                        ["Follow-up"] = "Add the missing outcome kind."
                    })));

        Assert.False(response.IsSuccess);
        var error = Assert.Single(response.Errors, error => error.Code == "artifact.frontmatter.missing");
        Assert.Contains("code: artifact.frontmatter.missing", error.Message, StringComparison.Ordinal);
        Assert.Contains("path: outcome", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(workspace.DraftsRootPath, "outcome", "OUT-002.r0001.md")));
    }

    [Fact]
    public void GetReviewInbox_ReturnsReviewableDraftAndProposedArtifactsOnly()
    {
        var workspace = CreateWorkspace("memora");
        _fileStore.Save(workspace, CreateApprovedDecisionArtifact());
        _fileStore.Save(workspace, CreateReviewDecisionArtifact("ADR-002", ArtifactStatus.Draft, "Draft context decision"));
        _fileStore.Save(workspace, CreateReviewDecisionArtifact("ADR-003", ArtifactStatus.Proposed, "Proposed context decision"));
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.GetReviewInbox("memora");

        Assert.True(response.IsSuccess);
        Assert.Equal("memora", response.ProjectId);
        Assert.Collection(
            response.Items,
            item =>
            {
                Assert.Equal("ADR-003", item.ArtifactId);
                Assert.Equal(ArtifactStatus.Proposed, item.Status);
                Assert.Equal("drafts/decision/ADR-003.r0001.md", item.RelativePath);
                Assert.Equal("valid", item.ValidationState);
            },
            item =>
            {
                Assert.Equal("ADR-002", item.ArtifactId);
                Assert.Equal(ArtifactStatus.Draft, item.Status);
                Assert.Equal("drafts/decision/ADR-002.r0001.md", item.RelativePath);
                Assert.Equal("valid", item.ValidationState);
            });
        Assert.DoesNotContain(response.Items, item => item.ArtifactId == "ADR-001");
    }

    [Fact]
    public void GetReviewArtifactPreview_ReturnsMetadataBodyAndSections()
    {
        var workspace = CreateWorkspace("memora");
        var path = _fileStore.Save(workspace, CreateReviewDecisionArtifact("ADR-004", ArtifactStatus.Draft, "Previewable decision"));
        var relativePath = Path.GetRelativePath(workspace.RootPath, path).Replace('\\', '/');
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.GetReviewArtifactPreview("memora", relativePath);

        Assert.True(response.IsSuccess);
        Assert.NotNull(response.Item);
        Assert.Equal("ADR-004", response.Item.ArtifactId);
        Assert.Equal(ArtifactStatus.Draft, response.Item.Status);
        Assert.Contains("## Context", response.Body, StringComparison.Ordinal);
        Assert.Equal("Keep the contract explicit.", response.Sections["Decision"]);
    }

    [Fact]
    public void GetReviewArtifactPreview_RejectsApprovedCanonicalArtifacts()
    {
        var workspace = CreateWorkspace("memora");
        var path = _fileStore.Save(workspace, CreateApprovedDecisionArtifact());
        var relativePath = Path.GetRelativePath(workspace.RootPath, path).Replace('\\', '/');
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.GetReviewArtifactPreview("memora", relativePath);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Code == "review.status.not_reviewable");
    }

    [Fact]
    public void ApplyReviewDecision_Approve_PersistsApprovedArtifactAndRemovesDraft()
    {
        var workspace = CreateWorkspace("memora");
        var draftPath = _fileStore.Save(workspace, CreateReviewDecisionArtifact("ADR-005", ArtifactStatus.Draft, "Draft decision"));
        var relativePath = Path.GetRelativePath(workspace.RootPath, draftPath).Replace('\\', '/');
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.ApplyReviewDecision("memora", new ReviewDecisionRequest(relativePath, "approve"));

        Assert.True(response.IsSuccess);
        Assert.Equal("approve", response.Decision);
        Assert.NotNull(response.Item);
        Assert.Equal(ArtifactStatus.Approved, response.Item.Status);
        Assert.Equal("canonical/decisions/ADR-005.r0001.md", response.Item.RelativePath);
        Assert.False(File.Exists(draftPath));
        Assert.True(File.Exists(Path.Combine(workspace.CanonicalDecisionsPath, "ADR-005.r0001.md")));
    }

    [Fact]
    public void ApplyReviewDecision_Reject_PersistsDeprecatedArtifactInReviewPath()
    {
        var workspace = CreateWorkspace("memora");
        var draftPath = _fileStore.Save(workspace, CreateReviewDecisionArtifact("ADR-006", ArtifactStatus.Proposed, "Proposed decision"));
        var relativePath = Path.GetRelativePath(workspace.RootPath, draftPath).Replace('\\', '/');
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.ApplyReviewDecision("memora", new ReviewDecisionRequest(relativePath, "reject"));

        Assert.True(response.IsSuccess);
        Assert.Equal("reject", response.Decision);
        Assert.NotNull(response.Item);
        Assert.Equal(ArtifactStatus.Deprecated, response.Item.Status);
        Assert.Equal(relativePath, response.Item.RelativePath);
        Assert.True(File.Exists(draftPath));
        var parsed = new ArtifactMarkdownParser().Parse(File.ReadAllText(draftPath));
        Assert.NotNull(parsed.Artifact);
        Assert.Equal(ArtifactStatus.Deprecated, parsed.Artifact.Status);
    }

    [Fact]
    public void ApplyReviewDecision_Approve_ReturnsLifecycleErrorForProposedArtifact()
    {
        var workspace = CreateWorkspace("memora");
        var draftPath = _fileStore.Save(workspace, CreateReviewDecisionArtifact("ADR-007", ArtifactStatus.Proposed, "Proposed decision"));
        var relativePath = Path.GetRelativePath(workspace.RootPath, draftPath).Replace('\\', '/');
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.ApplyReviewDecision("memora", new ReviewDecisionRequest(relativePath, "approve"));

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Code == "approval.approve.status.invalid");
        Assert.True(File.Exists(draftPath));
        Assert.False(File.Exists(Path.Combine(workspace.CanonicalDecisionsPath, "ADR-007.r0001.md")));
    }

    [Fact]
    public void WriteSessionSummary_ExplicitPolicyGovernedTrigger_PersistsToSummaryStorage()
    {
        var workspace = CreateWorkspace("memora");
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var response = service.WriteSessionSummary(
            new PolicyGovernedSessionSummaryWriteRequest(
                "memora",
                "SUM-001",
                CreateSessionSummaryContent(),
                CreateSessionSummaryPolicy(),
                CreateExplicitSessionSummaryTrigger("SUM-001")));

        Assert.True(response.IsSuccess);
        Assert.Equal(ArtifactStatus.Proposed, response.ResultingStatus);
        Assert.Equal(AutomationStorageScope.Summary, response.StorageScope);
        Assert.True(File.Exists(Path.Combine(workspace.SummariesRootPath, "SUM-001.r0001.md")));
        Assert.False(File.Exists(Path.Combine(workspace.CanonicalRootPath, "SUM-001.r0001.md")));
    }

    [Fact]
    public void WriteSessionSummary_LifecycleTrigger_IsBlockedAndDoesNotWrite()
    {
        var workspace = CreateWorkspace("memora");
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);
        var before = CreateSummaryArtifact(ArtifactStatus.Proposed);
        var after = CreateSummaryArtifact(ArtifactStatus.Draft);

        var response = service.WriteSessionSummary(
            new PolicyGovernedSessionSummaryWriteRequest(
                "memora",
                "SUM-001",
                CreateSessionSummaryContent(),
                CreateSessionSummaryPolicy(),
                ControlledAutomationTriggerEvent.FromLifecycleTransition(
                    "event-001",
                    before,
                    after,
                    new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero))));

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Code == "automation.trigger.explicit_required");
        Assert.False(File.Exists(Path.Combine(workspace.SummariesRootPath, "SUM-001.r0001.md")));
    }

    [Fact]
    public void WriteSessionSummary_InvalidPolicy_BlockedNoWrite()
    {
        var workspace = CreateWorkspace("memora");
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);
        var policy = new ControlledAutomationPolicy(
            "plan-direct-write",
            "Plan direct-write attempt",
            enabled: true,
            requiresExplicitTrigger: true,
            [
                new ControlledAutomationPermission(
                    ControlledAutomationAction.DirectWrite,
                    ArtifactType.Plan,
                    AutomationStorageScope.Canonical,
                    ["reviewed by operator"])
            ]);

        var response = service.WriteSessionSummary(
            new PolicyGovernedSessionSummaryWriteRequest(
                "memora",
                "SUM-001",
                CreateSessionSummaryContent(),
                policy,
                CreateExplicitSessionSummaryTrigger("SUM-001")));

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Code == "automation.policy.artifact_type.not_low_risk");
        Assert.False(File.Exists(Path.Combine(workspace.SummariesRootPath, "SUM-001.r0001.md")));
    }

    [Fact]
    public void WriteSessionSummary_CanonicalTrueContent_IsBlockedAndDoesNotWrite()
    {
        var workspace = CreateWorkspace("memora");
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);
        var content = CreateSessionSummaryContent(canonical: true);

        var response = service.WriteSessionSummary(
            new PolicyGovernedSessionSummaryWriteRequest(
                "memora",
                "SUM-001",
                content,
                CreateSessionSummaryPolicy(),
                CreateExplicitSessionSummaryTrigger("SUM-001")));

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Code == "artifact.session_summary.canonical.invalid");
        Assert.False(File.Exists(Path.Combine(workspace.SummariesRootPath, "SUM-001.r0001.md")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspacesRootPath))
        {
            Directory.Delete(_workspacesRootPath, recursive: true);
        }
    }

    private ProjectWorkspace CreateWorkspace(string projectId)
    {
        Directory.CreateDirectory(_workspacesRootPath);
        var workspaceRootPath = Path.Combine(_workspacesRootPath, projectId);
        Directory.CreateDirectory(workspaceRootPath);
        File.WriteAllText(
            Path.Combine(workspaceRootPath, "project.json"),
            $$"""
              {
                "projectId": "{{projectId}}",
                "name": "Memora",
                "status": "active"
              }
              """);

        return new ProjectWorkspace(new ProjectMetadata(projectId, "Memora", "active"), workspaceRootPath);
    }

    private static ArtifactProposalContent CreateDecisionContent(string title = "Context decision") =>
        new(
            title,
            "agent",
            "Need a reviewable proposal.",
            ["context"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Need deterministic context.",
                ["Decision"] = "Keep the contract explicit.",
                ["Alternatives Considered"] = "Duplicated endpoint logic.",
                ["Consequences"] = "Shared services stay reusable."
            },
            AgentArtifactLinks.Empty,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["decision_date"] = "2026-04-17"
            });

    private static async Task<IReadOnlyList<ProposalResponse>> RunConcurrently(Func<ProposalResponse> action)
    {
        using var start = new ManualResetEventSlim(initialState: false);
        var tasks = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                return action();
            }))
            .ToArray();

        start.Set();
        return await Task.WhenAll(tasks);
    }

    private static void WriteMalformedDraft(ProjectWorkspace workspace, string artifactId, int revision)
    {
        var directory = Path.Combine(workspace.DraftsRootPath, "decision");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, $"{artifactId}.r{revision:D4}.md"),
            """
            ---
            id ADR-999
            type: decision
            ---
            ## Context
            malformed
            """);
    }

    private static void WriteForeignProjectDraft(ProjectWorkspace workspace, string artifactId, int revision)
    {
        var directory = Path.Combine(workspace.DraftsRootPath, "decision");
        Directory.CreateDirectory(directory);
        var artifact = CreateApprovedDecisionArtifact() with
        {
            Id = artifactId,
            ProjectId = "other-project",
            Status = ArtifactStatus.Draft,
            Revision = revision
        };
        File.WriteAllText(
            Path.Combine(directory, $"{artifactId}.r{revision:D4}.md"),
            new ArtifactMarkdownWriter().Write(artifact));
    }

    private static ArtifactProposalContent CreateOutcomeContent() =>
        new(
            "Execution outcome",
            "agent",
            "Need a reviewable outcome record.",
            ["outcome"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["What Happened"] = "Execution completed successfully.",
                ["Why"] = "Outcome recording should stay proposal-only.",
                ["Impact"] = "The proposal path can now record structured outcomes.",
                ["Follow-up"] = "Review and approve the recorded outcome."
            },
            AgentArtifactLinks.Empty,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["outcome"] = "success"
            });

    private static ArtifactProposalContent CreateSessionSummaryContent(bool canonical = false) =>
        new(
            "Execution summary",
            "automation",
            "Record a non-canonical execution summary.",
            ["automation", "summary"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Summary"] = "The session completed with controlled automation validation.",
                ["Artifacts Created"] = "- SUM-001",
                ["Artifacts Updated"] = "None.",
                ["Open Threads"] = "Review the generated summary."
            },
            AgentArtifactLinks.Empty,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["session_type"] = "execution",
                ["canonical"] = canonical
            });

    private static ControlledAutomationPolicy CreateSessionSummaryPolicy()
    {
        LowRiskArtifactClassCatalog.TryGetDefinition(ArtifactType.SessionSummary, out var definition);

        return new ControlledAutomationPolicy(
            "summary-direct-write",
            "Summary direct-write prototype",
            enabled: true,
            requiresExplicitTrigger: true,
            [
                new ControlledAutomationPermission(
                    ControlledAutomationAction.DirectWrite,
                    ArtifactType.SessionSummary,
                    definition.StorageScope,
                    definition.RequiredGuardrails)
            ]);
    }

    private static ControlledAutomationTriggerEvent CreateExplicitSessionSummaryTrigger(string artifactId) =>
        ControlledAutomationTriggerEvent.ExplicitOperatorRequest(
            "event-001",
            "memora",
            ArtifactType.SessionSummary,
            artifactId,
            new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero));

    private static SessionSummaryArtifact CreateSummaryArtifact(ArtifactStatus status) =>
        new(
            "SUM-001",
            "memora",
            status,
            "Execution summary",
            new DateTimeOffset(2026, 4, 21, 11, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 21, 11, 30, 0, TimeSpan.Zero),
            1,
            ["summary"],
            "test",
            "trigger test",
            ArtifactLinks.Empty,
            """
            ## Summary
            The session completed.

            ## Artifacts Created
            - SUM-001

            ## Artifacts Updated
            None.

            ## Open Threads
            Review the generated summary.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Summary"] = "The session completed.",
                ["Artifacts Created"] = "- SUM-001",
                ["Artifacts Updated"] = "None.",
                ["Open Threads"] = "Review the generated summary."
            },
            SessionType.Execution,
            false);

    private static ArchitectureDecisionArtifact CreateApprovedDecisionArtifact() =>
        new(
            "ADR-001",
            "memora",
            ArtifactStatus.Approved,
            "Current context decision",
            new DateTimeOffset(2026, 4, 17, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 9, 30, 0, TimeSpan.Zero),
            1,
            ["context"],
            "user",
            "seed approved decision",
            ArtifactLinks.Empty,
            """
            ## Context
            Deterministic context is required.

            ## Decision
            Keep the current approved decision.

            ## Alternatives Considered
            Replacing approved truth directly.

            ## Consequences
            Updates must stay proposal-only.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Deterministic context is required.",
                ["Decision"] = "Keep the current approved decision.",
                ["Alternatives Considered"] = "Replacing approved truth directly.",
                ["Consequences"] = "Updates must stay proposal-only."
            },
            "2026-04-17");

    private static ArchitectureDecisionArtifact CreateReviewDecisionArtifact(
        string id,
        ArtifactStatus status,
        string title) =>
        new(
            id,
            "memora",
            status,
            title,
            new DateTimeOffset(2026, 4, 17, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 10, 30, 0, TimeSpan.Zero),
            1,
            ["context"],
            "agent",
            "review inbox test",
            ArtifactLinks.Empty,
            """
            ## Context
            Deterministic context is required.

            ## Decision
            Keep the contract explicit.

            ## Alternatives Considered
            Duplicated endpoint logic.

            ## Consequences
            Shared services stay reusable.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Deterministic context is required.",
                ["Decision"] = "Keep the contract explicit.",
                ["Alternatives Considered"] = "Duplicated endpoint logic.",
                ["Consequences"] = "Shared services stay reusable."
            },
            "2026-04-17");

    private static void SeedFirstRunImport(string workspaceRootPath, string projectId)
    {
        var importedAtUtc = new DateTimeOffset(2026, 5, 6, 9, 0, 0, TimeSpan.Zero);
        var records = new[]
        {
            new ImportedEvidenceRecord(
                "local-commit-001",
                projectId,
                ImportedEvidenceSourceType.LocalGitCommit,
                "ATT-123",
                "github:https://github.com/alucero270/memora.git",
                "abc1234",
                "feat(import): add readiness",
                "Changed source files.",
                importedAtUtc.AddMinutes(-10),
                importedAtUtc,
                "git commit abc1234",
                ImportedEvidenceTrustState.BaselineEvidence),
            new ImportedEvidenceRecord(
                "github-pr-244",
                projectId,
                ImportedEvidenceSourceType.GitHubPullRequest,
                "ATT-123",
                "github:https://github.com/alucero270/memora.git",
                "https://github.com/alucero270/memora/pull/244",
                "M10-07 UI",
                "Draft pull request evidence.",
                importedAtUtc.AddMinutes(-5),
                importedAtUtc,
                "github pull request #244",
                ImportedEvidenceTrustState.ReviewableEvidence),
            new ImportedEvidenceRecord(
                "github-release-001",
                projectId,
                ImportedEvidenceSourceType.GitHubRelease,
                "ATT-123",
                "github:https://github.com/alucero270/memora.git",
                "v0.10.0",
                "Milestone readiness release",
                "Release evidence.",
                importedAtUtc.AddMinutes(-3),
                importedAtUtc,
                "github release v0.10.0",
                ImportedEvidenceTrustState.CanonicalEvidence)
        };

        new FileBackedImportedEvidenceStore()
            .Save(new ProjectEvidenceWriteRequest(workspaceRootPath, records));

        var candidates = new[]
        {
            new CandidateMemoryRecord(
                "candidate-repo-structure",
                CandidateMemoryKind.RepoStructure,
                CandidateMemorySource.EvidenceDerived,
                "Imported UI area",
                "Direct evidence references UI files.",
                0.9,
                "Ownership still needs review.",
                "Grouped changed-file paths.",
                CandidateMemoryDisposition.BaselineMemory,
                ["local-commit-001"]),
            new CandidateMemoryRecord(
                "candidate-style",
                CandidateMemoryKind.ContributionStyle,
                CandidateMemorySource.Inferred,
                "Use scoped prefixes",
                "Titles suggest conventional commit prefixes.",
                0.7,
                "Style inference needs review.",
                "Matched commit title pattern.",
                CandidateMemoryDisposition.ReviewRequired,
                ["local-commit-001"]),
            new CandidateMemoryRecord(
                "candidate-advisory",
                CandidateMemoryKind.OpenQuestion,
                CandidateMemorySource.Advisory,
                "Inspect CI later",
                "Advisory discovery can suggest missing CI details later.",
                0.5,
                "Future advisory candidate is not approved meaning.",
                "Recorded advisory gap.",
                CandidateMemoryDisposition.ReviewRequired,
                ["github-pr-244"])
        };

        var report = new AgentReadinessReport(
            projectId,
            importedAtUtc.AddMinutes(1),
            records.Length,
            candidates.Length,
            ReadyForAgentUse: true,
            MissingContext: [],
            MissingTests: [],
            RiskyModules: [],
            AdvisoryDiscoveryGaps: ["Advisory discovery can inspect CI for extra readiness hints."],
            NextReviewSteps: ["Review inferred and advisory candidates before promotion."]);

        new FileBackedFirstRunReportStore()
            .Save(workspaceRootPath, new FirstRunMemoryGenerationResult(candidates, report));
    }
}
