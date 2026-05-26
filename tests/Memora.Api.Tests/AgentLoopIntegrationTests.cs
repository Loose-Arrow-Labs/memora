using Memora.Api.Services;
using Memora.Core.AgentInteraction;
using Memora.Core.Approval;
using Memora.Core.Artifacts;
using Memora.Core.Projects;
using Memora.Storage.Parsing;
using Memora.Storage.Persistence;
using Memora.Storage.Workspaces;

namespace Memora.Api.Tests;

public sealed class AgentLoopIntegrationTests : IDisposable
{
    private readonly string _workspacesRootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-agent-loop-integration-tests",
        Guid.NewGuid().ToString("N"));

    private readonly ArtifactFileStore _fileStore = new();
    private readonly ArtifactMarkdownParser _parser = new();
    private readonly ArtifactApprovalWorkflow _approvalWorkflow = new();
    private readonly ApprovalQueueBuilder _approvalQueueBuilder = new();

    [Fact]
    public void GetContext_IsDeterministicForRepeatedRequests()
    {
        var workspace = CreateWorkspace("memora");
        _fileStore.Save(workspace, CreateCharterArtifact());
        _fileStore.Save(workspace, CreatePlanArtifact());
        _fileStore.Save(workspace, CreateDecisionArtifact());
        _fileStore.Save(workspace, CreateOutcomeArtifact());
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var first = service.GetContext(new GetContextRequest("memora", "Prepare milestone 3 context.", includeLayer3History: false));
        var second = service.GetContext(new GetContextRequest("memora", "Prepare milestone 3 context.", includeLayer3History: false));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(
            first.Bundle!.Layers.Select(layer => string.Join(",", layer.Artifacts.Select(artifact => artifact.Artifact.Id))),
            second.Bundle!.Layers.Select(layer => string.Join(",", layer.Artifacts.Select(artifact => artifact.Artifact.Id))));
    }

    [Fact]
    public void ProposalAndOutcomeSubmissions_RemainNonCanonicalByDefault()
    {
        var workspace = CreateWorkspace("memora");
        _fileStore.Save(workspace, CreateDecisionArtifact());
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var proposal = service.ProposeUpdate(
            new ProposeUpdateRequest(
                "memora",
                "ADR-001",
                1,
                CreateDecisionContent("Updated context decision")));
        var outcome = service.RecordOutcome(
            new RecordOutcomeRequest(
                "memora",
                "OUT-001",
                CreateOutcomeContent()));
        var defaultContext = service.GetContext(new GetContextRequest("memora", "Prepare milestone 3 context."));
        var draftContext = service.GetContext(new GetContextRequest("memora", "Prepare milestone 3 context.", includeDraftArtifacts: true));

        Assert.True(proposal.IsSuccess);
        Assert.True(outcome.IsSuccess);
        Assert.True(File.Exists(Path.Combine(workspace.CanonicalDecisionsPath, "ADR-001.r0001.md")));
        Assert.True(File.Exists(Path.Combine(workspace.DraftsRootPath, "decision", "ADR-001.r0002.md")));
        Assert.True(File.Exists(Path.Combine(workspace.DraftsRootPath, "outcome", "OUT-001.r0001.md")));
        Assert.DoesNotContain(defaultContext.Bundle!.Layers.SelectMany(layer => layer.Artifacts), artifact => artifact.Artifact.Id == "OUT-001");
        Assert.Contains(draftContext.Bundle!.Layers.SelectMany(layer => layer.Artifacts), artifact => artifact.Artifact.Id == "OUT-001");
    }

    [Fact]
    public void ProposedUpdate_NonCanonicalUntilDraftApproved()
    {
        var workspace = CreateWorkspace("memora");
        var approvedPath = _fileStore.Save(workspace, CreateDecisionArtifact());
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var proposal = service.ProposeUpdate(
            new ProposeUpdateRequest(
                "memora",
                "ADR-001",
                1,
                CreateDecisionContent("Updated approved decision")));
        var draftPath = Path.Combine(workspace.DraftsRootPath, "decision", "ADR-001.r0002.md");
        var approvedRevisionTwoPath = Path.Combine(workspace.CanonicalDecisionsPath, "ADR-001.r0002.md");

        Assert.True(proposal.IsSuccess);
        Assert.True(File.Exists(draftPath));
        Assert.False(File.Exists(approvedRevisionTwoPath));

        var currentApproved = Assert.IsType<ArchitectureDecisionArtifact>(ParseArtifact(approvedPath));
        var pendingProposal = Assert.IsType<ArchitectureDecisionArtifact>(ParseArtifact(draftPath));
        var directApproval = _approvalWorkflow.Approve(
            pendingProposal,
            new DateTimeOffset(2026, 04, 18, 8, 45, 0, TimeSpan.Zero),
            currentApproved);

        Assert.False(directApproval.IsSuccess);
        Assert.Contains(directApproval.Validation.Issues, issue => issue.Code == "approval.approve.status.invalid");
        Assert.False(File.Exists(approvedRevisionTwoPath));

        var reviewedDraft = pendingProposal with { Status = ArtifactStatus.Draft };
        var approval = _approvalWorkflow.Approve(
            reviewedDraft,
            new DateTimeOffset(2026, 04, 18, 9, 0, 0, TimeSpan.Zero),
            currentApproved);

        Assert.True(approval.IsSuccess);
        var approvedArtifact = Assert.IsType<ArchitectureDecisionArtifact>(approval.ApprovedArtifact);

        _fileStore.Save(workspace, approvedArtifact);

        Assert.True(File.Exists(approvedRevisionTwoPath));
        Assert.Equal(ArtifactStatus.Approved, ParseArtifact(approvedRevisionTwoPath).Status);
        Assert.True(File.Exists(Path.Combine(workspace.CanonicalDecisionsPath, "ADR-001.r0001.md")));
    }

    [Fact]
    public void RejectedProposal_NonCanonical_StaysInQueue()
    {
        var workspace = CreateWorkspace("memora");
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var proposal = service.ProposeArtifact(
            new ProposeArtifactRequest(
                "memora",
                "ADR-200",
                ArtifactType.Decision,
                CreateDecisionContent("Rejected decision proposal")));
        var draftPath = Path.Combine(workspace.DraftsRootPath, "decision", "ADR-200.r0001.md");

        Assert.True(proposal.IsSuccess);

        var pendingProposal = Assert.IsType<ArchitectureDecisionArtifact>(ParseArtifact(draftPath));
        var initialQueue = _approvalQueueBuilder.Build("memora", [pendingProposal]);

        Assert.Contains(initialQueue.Items, item => item.ArtifactId == "ADR-200");

        var rejection = _approvalWorkflow.Reject(
            pendingProposal,
            new DateTimeOffset(2026, 04, 18, 9, 15, 0, TimeSpan.Zero));

        Assert.True(rejection.IsSuccess);
        var rejectedArtifact = Assert.IsType<ArchitectureDecisionArtifact>(rejection.RejectedArtifact);
        var queueAfterReject = _approvalQueueBuilder.Build("memora", [rejectedArtifact]);

        Assert.Equal(ArtifactStatus.Deprecated, rejectedArtifact.Status);
        Assert.DoesNotContain(queueAfterReject.Items, item => item.ArtifactId == "ADR-200");
        Assert.False(File.Exists(Path.Combine(workspace.CanonicalDecisionsPath, "ADR-200.r0001.md")));
    }

    [Fact]
    public void InvalidProposalAndOutcome_DoNotWriteArtifacts()
    {
        var workspace = CreateWorkspace("memora");
        _fileStore.Save(workspace, CreateDecisionArtifact());
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);

        var invalidProposal = service.ProposeUpdate(
            new ProposeUpdateRequest(
                "memora",
                "ADR-001",
                1,
                new ArtifactProposalContent(
                    "Invalid decision update",
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
        var invalidOutcome = service.RecordOutcome(
            new RecordOutcomeRequest(
                "memora",
                "OUT-002",
                new ArtifactProposalContent(
                    "Invalid outcome",
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

        Assert.False(invalidProposal.IsSuccess);
        Assert.False(invalidOutcome.IsSuccess);
        Assert.False(File.Exists(Path.Combine(workspace.DraftsRootPath, "decision", "ADR-001.r0002.md")));
        Assert.False(File.Exists(Path.Combine(workspace.DraftsRootPath, "outcome", "OUT-002.r0001.md")));
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

    private ArtifactDocument ParseArtifact(string path)
    {
        var result = _parser.Parse(File.ReadAllText(path));

        Assert.True(result.Validation.IsValid);
        return Assert.IsAssignableFrom<ArtifactDocument>(result.Artifact);
    }

    private static ProjectCharterArtifact CreateCharterArtifact() =>
        new(
            "CHR-001",
            "memora",
            ArtifactStatus.Approved,
            "Memora charter",
            new DateTimeOffset(2026, 4, 17, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 8, 10, 0, TimeSpan.Zero),
            1,
            ["milestone-3"],
            "user",
            "charter seed",
            ArtifactLinks.Empty,
            """
            ## Problem Statement
            Keep project memory structured.

            ## Primary Users / Stakeholders
            Engineers.

            ## Current Pain
            Context drifts between sessions.

            ## Desired Outcome
            Context stays deterministic.

            ## Definition of Success
            The agent loop stays grounded.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Problem Statement"] = "Keep project memory structured.",
                ["Primary Users / Stakeholders"] = "Engineers.",
                ["Current Pain"] = "Context drifts between sessions.",
                ["Desired Outcome"] = "Context stays deterministic.",
                ["Definition of Success"] = "The agent loop stays grounded."
            });

    private static PlanArtifact CreatePlanArtifact() =>
        new(
            "PLN-001",
            "memora",
            ArtifactStatus.Approved,
            "Active plan",
            new DateTimeOffset(2026, 4, 17, 8, 15, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 8, 30, 0, TimeSpan.Zero),
            1,
            ["milestone-3"],
            "user",
            "plan seed",
            ArtifactLinks.Empty,
            """
            ## Goal
            Assemble deterministic context.

            ## Scope
            Keep the agent loop reviewable.

            ## Acceptance Criteria
            - context stays deterministic

            ## Notes
            Preserve proposal-only writes.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Goal"] = "Assemble deterministic context.",
                ["Scope"] = "Keep the agent loop reviewable.",
                ["Acceptance Criteria"] = "- context stays deterministic",
                ["Notes"] = "Preserve proposal-only writes."
            },
            ArtifactPriority.High,
            true);

    private static ArchitectureDecisionArtifact CreateDecisionArtifact() =>
        new(
            "ADR-001",
            "memora",
            ArtifactStatus.Approved,
            "Context decision",
            new DateTimeOffset(2026, 4, 17, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 9, 20, 0, TimeSpan.Zero),
            1,
            ["milestone-3", "context"],
            "user",
            "decision seed",
            ArtifactLinks.Empty,
            """
            ## Context
            Context assembly must stay deterministic.

            ## Decision
            Use layered bundles with explicit reasons.

            ## Alternatives Considered
            Unstructured retrieval.

            ## Consequences
            Operators can inspect the bundle.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Context assembly must stay deterministic.",
                ["Decision"] = "Use layered bundles with explicit reasons.",
                ["Alternatives Considered"] = "Unstructured retrieval.",
                ["Consequences"] = "Operators can inspect the bundle."
            },
            "2026-04-17");

    private static OutcomeArtifact CreateOutcomeArtifact() =>
        new(
            "OUT-000",
            "memora",
            ArtifactStatus.Approved,
            "Seed outcome",
            new DateTimeOffset(2026, 4, 17, 9, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 9, 40, 0, TimeSpan.Zero),
            1,
            ["outcome"],
            "user",
            "outcome seed",
            ArtifactLinks.Empty,
            """
            ## What Happened
            Seed execution completed.

            ## Why
            Need a baseline outcome artifact.

            ## Impact
            The service can include approved outcomes in context.

            ## Follow-up
            Record new outcomes as proposals.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["What Happened"] = "Seed execution completed.",
                ["Why"] = "Need a baseline outcome artifact.",
                ["Impact"] = "The service can include approved outcomes in context.",
                ["Follow-up"] = "Record new outcomes as proposals."
            },
            OutcomeKind.Success);

    private static ArtifactProposalContent CreateDecisionContent(string title) =>
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
}
