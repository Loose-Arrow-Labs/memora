using Memora.Core.AgentInteraction;
using Memora.Core.Artifacts;
using Memora.Mcp.Server;

namespace Memora.Mcp.Tests;

public sealed class MemoraMcpServerTests
{
    [Fact]
    public void Server_ExposesExpectedCoreAgentLoopTools()
    {
        var server = new MemoraMcpServer(new TestAgentInteractionService());

        var tools = server.Tools.ToArray();

        Assert.Equal(["get_context", "propose_artifact", "propose_update", "record_outcome"], tools.Select(tool => tool.Name));
        Assert.Equal(nameof(GetContextRequest), tools[0].RequestContract);
        Assert.Equal(nameof(GetContextResponse), tools[0].ResponseContract);
        Assert.Contains(tools[0].Errors, error => error.Code == "context.validation");
        Assert.Equal(nameof(ProposeArtifactRequest), tools[1].RequestContract);
        Assert.Equal(nameof(ProposalResponse), tools[1].ResponseContract);
        Assert.Equal(nameof(RecordOutcomeRequest), tools[3].RequestContract);
        Assert.Equal(nameof(OutcomeResponse), tools[3].ResponseContract);
    }

    [Fact]
    public void Server_ExposesProjectResourceTemplate()
    {
        var server = new MemoraMcpServer(new TestAgentInteractionService());

        var resource = Assert.Single(server.Resources);
        Assert.Equal("memora://projects/{projectId}", resource.UriTemplate);
        Assert.Equal(nameof(ProjectLookupResponse), resource.ResponseContract);
        Assert.Contains(resource.Errors, error => error.Code == "mcp.resource.uri.invalid");
    }

    [Fact]
    public void ReadResource_ForProjectUri_ForwardsToSharedAgentInteractionService()
    {
        var server = new MemoraMcpServer(new TestAgentInteractionService());

        var result = server.ReadResource("memora://projects/memora");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.Equal("memora", result.Payload.ProjectId);
    }

    [Fact]
    public void ReadResource_ForImportedProject_IncludesHybridReadinessStatus()
    {
        var server = new MemoraMcpServer(new TestAgentInteractionService());

        var result = server.ReadResource("memora://projects/memora");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Payload);
        var readiness = Assert.IsType<ImportedProjectReadinessState>(result.Payload.ImportReadiness);
        Assert.True(readiness.GroundedContextReady);
        Assert.Equal(1, readiness.EvidenceDerivedCandidateCount);
        Assert.Equal(1, readiness.InferredCandidateCount);
        Assert.Equal(1, readiness.AdvisoryCandidateCount);
        Assert.Equal(1, readiness.FutureAdvisoryGapCount);
        Assert.Contains("Advisory discovery can inspect CI after governed setup.", readiness.AdvisoryDiscoveryGaps);
    }

    [Fact]
    public void ReadResource_ForInvalidUri_ReturnsExplicitContractError()
    {
        var server = new MemoraMcpServer(new TestAgentInteractionService());

        var result = server.ReadResource("memora://projects/memora/extra");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Payload);
        Assert.Equal("mcp.resource.uri.invalid", result.Errors[0].Code);
        Assert.Equal("uri", result.Errors[0].Path);
    }

    [Fact]
    public void GetContext_ForwardsToSharedAgentInteractionService()
    {
        var server = new MemoraMcpServer(new TestAgentInteractionService());

        var response = server.GetContext(new GetContextRequest("memora", "Prepare milestone 3 context."));

        Assert.True(response.IsSuccess);
        Assert.NotNull(response.Bundle);
        Assert.Equal(3, response.Bundle.Layers.Count);
    }

    [Fact]
    public void ProposeArtifact_ForwardsProposalOnlyBehavior()
    {
        var server = new MemoraMcpServer(new TestAgentInteractionService());

        var result = server.InvokeTool(
            "propose_artifact",
            new ProposeArtifactRequest("memora", "ADR-001", ArtifactType.Decision, CreateContent()));

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(ProposeArtifactRequest), result.RequestContract);
        Assert.Equal(nameof(ProposalResponse), result.ResponseContract);
        var response = Assert.IsType<ProposalResponse>(result.Payload);
        Assert.True(response.IsSuccess);
        Assert.Equal(ArtifactStatus.Proposed, response.ResultingStatus);
    }

    [Fact]
    public void RecordOutcome_ForwardsProposalOnlyBehavior()
    {
        var server = new MemoraMcpServer(new TestAgentInteractionService());

        var result = server.InvokeTool(
            "record_outcome",
            new RecordOutcomeRequest("memora", "OUT-001", CreateContent()));

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(RecordOutcomeRequest), result.RequestContract);
        Assert.Equal(nameof(OutcomeResponse), result.ResponseContract);
        var response = Assert.IsType<OutcomeResponse>(result.Payload);
        Assert.True(response.IsSuccess);
        Assert.Equal(ArtifactStatus.Proposed, response.ResultingStatus);
    }

    [Fact]
    public void InvokeTool_ForKnownTool_UsesPublishedContractsAndReturnsTypedPayload()
    {
        var server = new MemoraMcpServer(new TestAgentInteractionService());

        var result = server.InvokeTool("get_context", new GetContextRequest("memora", "Prepare milestone 3 context."));

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(GetContextRequest), result.RequestContract);
        Assert.Equal(nameof(GetContextResponse), result.ResponseContract);
        var payload = Assert.IsType<GetContextResponse>(result.Payload);
        Assert.True(payload.IsSuccess);
        Assert.NotNull(payload.Bundle);
        Assert.Equal("memora", payload.Bundle.Request.ProjectId);
        Assert.Equal(
            [AgentContextLayerKind.Layer1, AgentContextLayerKind.Layer2, AgentContextLayerKind.Layer3],
            payload.Bundle.Layers.Select(layer => layer.Kind).ToArray());
    }

    [Fact]
    public void InvokeTool_ForUnknownTool_ReturnsStableDiagnostic()
    {
        var server = new MemoraMcpServer(new TestAgentInteractionService());

        var result = server.InvokeTool("debug_context", new GetContextRequest("memora", "Prepare milestone 3 context."));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Payload);
        Assert.Equal("mcp.tool.not_found", result.Errors[0].Code);
        Assert.Equal("name", result.Errors[0].Path);
    }

    [Fact]
    public void InvokeTool_ForWrongRequestContract_ReturnsStableDiagnostic()
    {
        var server = new MemoraMcpServer(new TestAgentInteractionService());

        var result = server.InvokeTool("get_context", CreateContent());

        Assert.False(result.IsSuccess);
        Assert.Null(result.Payload);
        Assert.Equal(nameof(GetContextRequest), result.RequestContract);
        Assert.Equal("mcp.tool.request.invalid", result.Errors[0].Code);
        Assert.Equal("arguments", result.Errors[0].Path);
    }

    [Fact]
    public void Errors_AreSurfacedWithoutProtocolLayerRewriting()
    {
        var server = new MemoraMcpServer(new FailingAgentInteractionService());

        var response = server.GetContext(new GetContextRequest("memora", "Prepare milestone 3 context."));

        Assert.False(response.IsSuccess);
        Assert.Equal("context.validation", response.Errors[0].Code);
    }

    [Fact]
    public void ToolExecutionExceptions_ReturnStableDiagnostics()
    {
        var server = new MemoraMcpServer(new ThrowingAgentInteractionService());

        var response = server.GetContext(new GetContextRequest("memora", "Prepare milestone 3 context."));

        Assert.False(response.IsSuccess);
        Assert.Equal("internal.unhandled_exception", response.Errors[0].Code);
        Assert.DoesNotContain("transport", response.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("request", response.Errors[0].Path);
    }

    [Fact]
    public void ResourceExecutionExceptions_ReturnStableDiagnostics()
    {
        var server = new MemoraMcpServer(new ThrowingAgentInteractionService());

        var response = server.ReadResource("memora://projects/memora");

        Assert.False(response.IsSuccess);
        Assert.NotNull(response.Payload);
        Assert.Equal("internal.unhandled_exception", response.Errors[0].Code);
        Assert.DoesNotContain("transport", response.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("project_id", response.Errors[0].Path);
    }

    private static ArtifactProposalContent CreateContent() =>
        new(
            "Context decision",
            "agent",
            "Need a reviewable proposal.",
            ["context"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Need deterministic context.",
                ["Decision"] = "Keep the contract explicit."
            });

    private sealed class TestAgentInteractionService : IAgentInteractionService
    {
        public ProjectLookupResponse GetProject(string projectId) =>
            new(
                projectId,
                "Memora",
                "active",
                [],
                importReadiness: CreateImportReadiness());

        public GetContextResponse GetContext(GetContextRequest request) =>
            new(
                new AgentContextBundle(
                    request,
                    [
                        new AgentContextLayer(AgentContextLayerKind.Layer1, [CreateArtifact("CHR-001")]),
                        new AgentContextLayer(AgentContextLayerKind.Layer2, [CreateArtifact("ADR-001")]),
                        new AgentContextLayer(AgentContextLayerKind.Layer3, [])
                    ]),
                []);

        public ProposalResponse ProposeArtifact(ProposeArtifactRequest request) =>
            new(request.ProjectId, request.ArtifactId, request.ArtifactType, ArtifactStatus.Proposed, 1, []);

        public ProposalResponse ProposeUpdate(ProposeUpdateRequest request) =>
            new(request.ProjectId, request.ArtifactId, ArtifactType.Decision, ArtifactStatus.Proposed, request.ExpectedRevision + 1, []);

        public OutcomeResponse RecordOutcome(RecordOutcomeRequest request) =>
            new(request.ProjectId, request.ArtifactId, ArtifactStatus.Proposed, 1, OutcomeKind.Success, []);

        private static ImportedProjectReadinessState CreateImportReadiness() =>
            new(
                "summaries/first-run-readiness.json",
                HasReadinessReport: true,
                GroundedContextReady: true,
                EvidenceRecordCount: 3,
                CandidateCount: 3,
                BaselineEvidenceCount: 1,
                CanonicalEvidenceCount: 1,
                ReviewableEvidenceCount: 1,
                EvidenceDerivedCandidateCount: 1,
                InferredCandidateCount: 1,
                AdvisoryCandidateCount: 1,
                FutureAdvisoryGapCount: 1,
                AdvisoryDiscoveryGaps: ["Advisory discovery can inspect CI after governed setup."],
                MissingContext: [],
                MissingTests: [],
                RiskyModules: [],
                NextReviewSteps: ["Review inferred and advisory candidates before promotion."]);

        private static AgentContextArtifact CreateArtifact(string id) =>
            new(
                new ArchitectureDecisionArtifact(
                    id,
                    "memora",
                    ArtifactStatus.Approved,
                    "Context decision",
                    new DateTimeOffset(2026, 4, 17, 10, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 4, 17, 10, 15, 0, TimeSpan.Zero),
                    1,
                    ["context"],
                    "user",
                    "mcp tests",
                    ArtifactLinks.Empty,
                    """
                    ## Context
                    Deterministic context is required.

                    ## Decision
                    Keep MCP as a thin adapter.

                    ## Alternatives Considered
                    Protocol-specific business logic.

                    ## Consequences
                    Shared services remain reusable.
                    """,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["Context"] = "Deterministic context is required.",
                        ["Decision"] = "Keep MCP as a thin adapter.",
                        ["Alternatives Considered"] = "Protocol-specific business logic.",
                        ["Consequences"] = "Shared services remain reusable."
                    },
                    "2026-04-17"),
                [new AgentContextInclusionReason("approved-default", "Included from approved context grounding.", [])]);
    }

    private sealed class FailingAgentInteractionService : IAgentInteractionService
    {
        public ProjectLookupResponse GetProject(string projectId) =>
            new(projectId, null, null, [new AgentInteractionError("project.not_found", "Project not found.", "project_id")]);

        public GetContextResponse GetContext(GetContextRequest request) =>
            new(null, [new AgentInteractionError("context.validation", "Task description is invalid.", "task_description")]);

        public ProposalResponse ProposeArtifact(ProposeArtifactRequest request) =>
            new(request.ProjectId, request.ArtifactId, request.ArtifactType, ArtifactStatus.Proposed, 0, [new AgentInteractionError("proposal.validation", "Invalid proposal.", "body")]);

        public ProposalResponse ProposeUpdate(ProposeUpdateRequest request) =>
            new(request.ProjectId, request.ArtifactId, ArtifactType.Decision, ArtifactStatus.Proposed, 0, [new AgentInteractionError("proposal.validation", "Invalid update.", "body")]);

        public OutcomeResponse RecordOutcome(RecordOutcomeRequest request) =>
            new(request.ProjectId, request.ArtifactId, ArtifactStatus.Proposed, 0, OutcomeKind.Mixed, [new AgentInteractionError("outcome.validation", "Invalid outcome.", "body")]);
    }

    private sealed class ThrowingAgentInteractionService : IAgentInteractionService
    {
        public ProjectLookupResponse GetProject(string projectId) =>
            throw new InvalidOperationException("Lookup transport was unavailable.");

        public GetContextResponse GetContext(GetContextRequest request) =>
            throw new InvalidOperationException("Context transport was unavailable.");

        public ProposalResponse ProposeArtifact(ProposeArtifactRequest request) =>
            throw new InvalidOperationException("Proposal transport was unavailable.");

        public ProposalResponse ProposeUpdate(ProposeUpdateRequest request) =>
            throw new InvalidOperationException("Update transport was unavailable.");

        public OutcomeResponse RecordOutcome(RecordOutcomeRequest request) =>
            throw new InvalidOperationException("Outcome transport was unavailable.");
    }
}
