using System.Net;
using System.Net.Http.Json;
using Memora.Core.AgentInteraction;
using Memora.Core.Artifacts;
using Memora.Mcp.Server;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Memora.Api.Tests;

public sealed class ProtocolSurfaceParityTests
{
    [Fact]
    public async Task ProjectLookup_RemainsAlignedAcrossApiAndMcpSurfaces()
    {
        using var harness = await ProtocolHarness.CreateAsync(new SharedAgentInteractionService());

        var apiResponse = await harness.Client.GetFromJsonAsync<ProjectLookupResponse>("/api/projects/memora");
        var mcpResponse = harness.Mcp.ReadProject("memora");
        var mcpResourceResponse = harness.Mcp.ReadResource("memora://projects/memora");

        Assert.NotNull(apiResponse);
        Assert.Equal(mcpResponse.ProjectId, apiResponse.ProjectId);
        Assert.Equal(mcpResponse.Name, apiResponse.Name);
        Assert.Equal(mcpResponse.Status, apiResponse.Status);
        Assert.NotNull(apiResponse.ImportReadiness);
        Assert.NotNull(mcpResponse.ImportReadiness);
        Assert.Equal(mcpResponse.ImportReadiness.GroundedContextReady, apiResponse.ImportReadiness.GroundedContextReady);
        Assert.Equal(mcpResponse.ImportReadiness.EvidenceDerivedCandidateCount, apiResponse.ImportReadiness.EvidenceDerivedCandidateCount);
        Assert.Equal(mcpResponse.ImportReadiness.InferredCandidateCount, apiResponse.ImportReadiness.InferredCandidateCount);
        Assert.Equal(mcpResponse.ImportReadiness.AdvisoryCandidateCount, apiResponse.ImportReadiness.AdvisoryCandidateCount);
        Assert.Equal(mcpResponse.ImportReadiness.FutureAdvisoryGapCount, apiResponse.ImportReadiness.FutureAdvisoryGapCount);
        Assert.True(mcpResourceResponse.IsSuccess);
        Assert.NotNull(mcpResourceResponse.Payload);
        Assert.Equal(apiResponse.ProjectId, mcpResourceResponse.Payload.ProjectId);
        Assert.Equal(apiResponse.ImportReadiness.GroundedContextReady, mcpResourceResponse.Payload.ImportReadiness?.GroundedContextReady);
    }

    [Fact]
    public async Task ContextErrors_RemainAlignedAcrossApiAndMcpSurfaces()
    {
        using var harness = await ProtocolHarness.CreateAsync(new FailingAgentInteractionService());
        var request = new GetContextRequest("memora", "Prepare integration validation.");

        using var apiHttpResponse = await harness.Client.PostAsJsonAsync("/api/context", request);
        var apiResponse = await apiHttpResponse.Content.ReadFromJsonAsync<GetContextResponse>();
        var mcpResponse = harness.Mcp.GetContext(request);

        Assert.Equal(HttpStatusCode.BadRequest, apiHttpResponse.StatusCode);
        Assert.NotNull(apiResponse);
        Assert.False(apiResponse.IsSuccess);
        Assert.False(mcpResponse.IsSuccess);
        Assert.Equal(mcpResponse.Errors[0].Code, apiResponse.Errors[0].Code);
        Assert.Equal(mcpResponse.Errors[0].Path, apiResponse.Errors[0].Path);
    }

    [Fact]
    public async Task ProposalAndOutcomeFlows_PreserveProposalOnlyBehaviorAcrossApiAndMcp()
    {
        using var harness = await ProtocolHarness.CreateAsync(new SharedAgentInteractionService());

        var proposalRequest = new ProposeArtifactRequest("memora", "ADR-100", ArtifactType.Decision, CreateContent());
        using var proposalHttpResponse = await harness.Client.PostAsJsonAsync("/api/artifacts/proposals", proposalRequest);
        var apiProposal = await proposalHttpResponse.Content.ReadFromJsonAsync<ProposalResponse>();
        var mcpProposalResult = harness.Mcp.InvokeTool("propose_artifact", proposalRequest);
        var mcpProposal = Assert.IsType<ProposalResponse>(mcpProposalResult.Payload);

        var outcomeRequest = new RecordOutcomeRequest("memora", "OUT-100", CreateContent());
        using var outcomeHttpResponse = await harness.Client.PostAsJsonAsync("/api/outcomes", outcomeRequest);
        var apiOutcome = await outcomeHttpResponse.Content.ReadFromJsonAsync<OutcomeResponse>();
        var mcpOutcomeResult = harness.Mcp.InvokeTool("record_outcome", outcomeRequest);
        var mcpOutcome = Assert.IsType<OutcomeResponse>(mcpOutcomeResult.Payload);

        Assert.Equal(HttpStatusCode.Accepted, proposalHttpResponse.StatusCode);
        Assert.NotNull(apiProposal);
        Assert.Equal(ArtifactStatus.Proposed, apiProposal.ResultingStatus);
        Assert.True(mcpProposalResult.IsSuccess);
        Assert.Equal(apiProposal.ResultingStatus, mcpProposal.ResultingStatus);

        Assert.Equal(HttpStatusCode.Accepted, outcomeHttpResponse.StatusCode);
        Assert.NotNull(apiOutcome);
        Assert.Equal(ArtifactStatus.Proposed, apiOutcome.ResultingStatus);
        Assert.True(mcpOutcomeResult.IsSuccess);
        Assert.Equal(apiOutcome.ResultingStatus, mcpOutcome.ResultingStatus);
        Assert.Equal(apiOutcome.OutcomeKind, mcpOutcome.OutcomeKind);
    }

    private static ArtifactProposalContent CreateContent() =>
        new(
            "Integration guidance",
            "agent",
            "Need a reviewable proposal.",
            ["integration"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Keep protocol surfaces aligned.",
                ["Decision"] = "Reuse the shared service contract."
            });

    private sealed class ProtocolHarness : IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;

        private ProtocolHarness(WebApplicationFactory<Program> factory, HttpClient client, MemoraMcpServer mcp)
        {
            _factory = factory;
            Client = client;
            Mcp = mcp;
        }

        public HttpClient Client { get; }

        public MemoraMcpServer Mcp { get; }

        public static Task<ProtocolHarness> CreateAsync(IAgentInteractionService service)
        {
            var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<IAgentInteractionService>();
                        services.AddSingleton(service);
                    }));

            return Task.FromResult(new ProtocolHarness(factory, factory.CreateClient(), new MemoraMcpServer(service)));
        }

        public void Dispose()
        {
            Client.Dispose();
            _factory.Dispose();
        }
    }

    private sealed class SharedAgentInteractionService : IAgentInteractionService
    {
        public ProjectLookupResponse GetProject(string projectId) =>
            new(
                projectId,
                "Memora",
                "active",
                [],
                importReadiness: new ImportedProjectReadinessState(
                    "summaries/first-run-readiness.json",
                    HasReadinessReport: true,
                    GroundedContextReady: true,
                    EvidenceRecordCount: 2,
                    CandidateCount: 3,
                    BaselineEvidenceCount: 1,
                    CanonicalEvidenceCount: 0,
                    ReviewableEvidenceCount: 1,
                    EvidenceDerivedCandidateCount: 1,
                    InferredCandidateCount: 1,
                    AdvisoryCandidateCount: 1,
                    FutureAdvisoryGapCount: 1,
                    AdvisoryDiscoveryGaps: ["Advisory discovery can inspect CI after governed setup."],
                    MissingContext: [],
                    MissingTests: [],
                    RiskyModules: [],
                    NextReviewSteps: ["Review imported candidate memory."]));

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

        private static AgentContextArtifact CreateArtifact(string id) =>
            new(
                new ArchitectureDecisionArtifact(
                    id,
                    "memora",
                    ArtifactStatus.Approved,
                    "Protocol alignment",
                    new DateTimeOffset(2026, 4, 20, 10, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 4, 20, 10, 15, 0, TimeSpan.Zero),
                    1,
                    ["integration"],
                    "user",
                    "integration tests",
                    ArtifactLinks.Empty,
                    """
                    ## Context
                    Protocol parity must stay intact.

                    ## Decision
                    Reuse shared contracts.

                    ## Alternatives Considered
                    Divergent protocol logic.

                    ## Consequences
                    Integration validation stays meaningful.
                    """,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["Context"] = "Protocol parity must stay intact.",
                        ["Decision"] = "Reuse shared contracts.",
                        ["Alternatives Considered"] = "Divergent protocol logic.",
                        ["Consequences"] = "Integration validation stays meaningful."
                    },
                    "2026-04-20"),
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
}
