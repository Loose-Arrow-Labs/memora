using Memora.Api.Services;
using Memora.Core.AgentInteraction;
using Memora.Core.Artifacts;
using Memora.Core.Automation;
using Memora.Core.Projects;
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
    public void WriteSessionSummary_InvalidPolicy_IsBlockedAndDoesNotWrite()
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
}
