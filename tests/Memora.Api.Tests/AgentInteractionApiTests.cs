using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Memora.Core.AgentInteraction;
using Memora.Core.Artifacts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Memora.Api.Tests;

public sealed class AgentInteractionApiTests
{
    [Fact]
    public async Task OpenApiDocument_IsPublishedForCompanionToolClients()
    {
        using var factory = CreateFactory(new TestAgentInteractionService());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        var root = payload.RootElement;
        Assert.StartsWith("3.", root.GetProperty("openapi").GetString());
        Assert.True(root.GetProperty("paths").TryGetProperty("/api/context", out _));
        Assert.True(root.GetProperty("paths").TryGetProperty("/api/outcomes", out _));
        Assert.True(root.GetProperty("paths").TryGetProperty("/api/projects/{projectId}/review/inbox", out _));
        Assert.True(root.GetProperty("paths").TryGetProperty("/api/projects/{projectId}/review/preview", out _));
    }

    [Fact]
    public async Task GetProject_ReturnsConfiguredProjectContract()
    {
        using var factory = CreateFactory(new TestAgentInteractionService());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/memora");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ProjectLookupResponse>();
        Assert.NotNull(payload);
        Assert.Equal("memora", payload.ProjectId);
        Assert.Equal("Memora", payload.Name);
    }

    [Fact]
    public async Task GetContext_ReturnsBundleContract()
    {
        using var factory = CreateFactory(new TestAgentInteractionService());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/context",
            new GetContextRequest("memora", "Prepare milestone 3 context."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        var root = payload.RootElement;
        Assert.True(root.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(3, root.GetProperty("bundle").GetProperty("layers").GetArrayLength());
    }

    [Fact]
    public async Task ProposeArtifact_ReturnsAcceptedProposalContract()
    {
        using var factory = CreateFactory(new TestAgentInteractionService());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/artifacts/proposals",
            new ProposeArtifactRequest(
                "memora",
                "ADR-001",
                ArtifactType.Decision,
                CreateContent()));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ProposalResponse>();
        Assert.NotNull(payload);
        Assert.Equal(ArtifactStatus.Proposed, payload.ResultingStatus);
    }

    [Fact]
    public async Task RecordOutcome_ReturnsAcceptedOutcomeContract()
    {
        using var factory = CreateFactory(new TestAgentInteractionService());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/outcomes",
            new RecordOutcomeRequest("memora", "OUT-001", CreateContent()));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<OutcomeResponse>();
        Assert.NotNull(payload);
        Assert.Equal(OutcomeKind.Success, payload.OutcomeKind);
    }

    [Fact]
    public async Task GetReviewInbox_ReturnsReviewableArtifactsForIdeClients()
    {
        using var factory = CreateFactory(new TestAgentInteractionService());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/memora/review/inbox");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ReviewInboxResponse>();
        Assert.NotNull(payload);
        var item = Assert.Single(payload.Items);
        Assert.Equal("ADR-002", item.ArtifactId);
        Assert.Equal(ArtifactStatus.Proposed, item.Status);
        Assert.Equal("drafts/decision/ADR-002.r0001.md", item.RelativePath);
    }

    [Fact]
    public async Task GetReviewPreview_ReturnsArtifactPreviewForIdeClients()
    {
        using var factory = CreateFactory(new TestAgentInteractionService());
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/projects/memora/review/preview?path={Uri.EscapeDataString("drafts/decision/ADR-002.r0001.md")}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ReviewArtifactPreviewResponse>();
        Assert.NotNull(payload);
        Assert.NotNull(payload.Item);
        Assert.Equal("ADR-002", payload.Item.ArtifactId);
        Assert.Contains("## Decision", payload.Body, StringComparison.Ordinal);
        Assert.Equal("Reuse the shared service contract.", payload.Sections["Decision"]);
    }

    [Fact]
    public async Task ValidationErrors_MapToBadRequest()
    {
        using var factory = CreateFactory(new FailingAgentInteractionService());
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/context",
            new GetContextRequest("memora", "Prepare milestone 3 context."));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<GetContextResponse>();
        Assert.NotNull(payload);
        Assert.False(payload.IsSuccess);
        Assert.Equal("context.validation", payload.Errors[0].Code);
    }

    private static WebApplicationFactory<Program> CreateFactory(IAgentInteractionService service) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IAgentInteractionService>();
                    services.RemoveAll<IReviewInboxService>();
                    services.AddSingleton(service);
                    if (service is IReviewInboxService reviewInboxService)
                    {
                        services.AddSingleton(reviewInboxService);
                    }
                }));

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

    private sealed class TestAgentInteractionService : IAgentInteractionService, IReviewInboxService
    {
        public ProjectLookupResponse GetProject(string projectId) =>
            new(projectId, "Memora", "active", []);

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

        public ReviewInboxResponse GetReviewInbox(string projectId) =>
            new(projectId, [CreateReviewInboxItem()], []);

        public ReviewArtifactPreviewResponse GetReviewArtifactPreview(string projectId, string relativePath) =>
            new(
                projectId,
                CreateReviewInboxItem(),
                """
                ## Context
                Deterministic context is required.

                ## Decision
                Reuse the shared service contract.
                """,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Context"] = "Deterministic context is required.",
                    ["Decision"] = "Reuse the shared service contract."
                },
                []);

        private static ReviewInboxItem CreateReviewInboxItem() =>
            new(
                "ADR-002",
                ArtifactType.Decision,
                ArtifactStatus.Proposed,
                "Reviewable decision",
                1,
                "agent",
                "api tests",
                "drafts/decision/ADR-002.r0001.md",
                @"C:\memora\memora\memora\memora\memora\drafts\decision\ADR-002.r0001.md",
                "valid",
                new DateTimeOffset(2026, 4, 17, 10, 15, 0, TimeSpan.Zero));

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
                    "api tests",
                    ArtifactLinks.Empty,
                    """
                    ## Context
                    Deterministic context is required.

                    ## Decision
                    Keep the API contract thin.

                    ## Alternatives Considered
                    Duplicated endpoint logic.

                    ## Consequences
                    Shared services stay reusable.
                    """,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["Context"] = "Deterministic context is required.",
                        ["Decision"] = "Keep the API contract thin.",
                        ["Alternatives Considered"] = "Duplicated endpoint logic.",
                        ["Consequences"] = "Shared services stay reusable."
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
}
