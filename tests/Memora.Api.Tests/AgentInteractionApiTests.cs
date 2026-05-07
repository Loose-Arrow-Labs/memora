using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Memora.Core.AgentInteraction;
using Memora.Core.Artifacts;
using Memora.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Memora.Api.Tests;

public sealed class AgentInteractionApiTests
{
    [Fact]
    public async Task OpenApiDocument_IsPublishedForCompanionToolClients()
    {
        using var factory = CreateFactory(new TestAgentInteractionService());
        using var client = CreateAuthorizedClient(factory);

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
        Assert.True(root.GetProperty("paths").TryGetProperty("/api/projects/{projectId}/review/decisions", out _));
    }

    [Fact]
    public async Task GetProject_ReturnsConfiguredProjectContract()
    {
        using var factory = CreateFactory(new TestAgentInteractionService());
        using var client = CreateAuthorizedClient(factory);

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
        using var client = CreateAuthorizedClient(factory);

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
        using var client = CreateAuthorizedClient(factory);

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
        using var client = CreateAuthorizedClient(factory);

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
        using var client = CreateAuthorizedClient(factory);

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
        using var client = CreateAuthorizedClient(factory);

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
    public async Task PostReviewDecision_ReturnsGovernedDecisionContractForIdeClients()
    {
        using var factory = CreateFactory(new TestAgentInteractionService());
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/projects/memora/review/decisions",
            new ReviewDecisionRequest("drafts/decision/ADR-002.r0001.md", "reject"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ReviewDecisionResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.IsSuccess);
        Assert.Equal("reject", payload.Decision);
        Assert.NotNull(payload.Item);
        Assert.Equal(ArtifactStatus.Deprecated, payload.Item.Status);
        Assert.Equal("Rejected ADR-002 revision 1.", payload.Message);
    }

    [Fact]
    public async Task ValidationErrors_MapToBadRequest()
    {
        using var factory = CreateFactory(new FailingAgentInteractionService());
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/context",
            new GetContextRequest("memora", "Prepare milestone 3 context."));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<GetContextResponse>();
        Assert.NotNull(payload);
        Assert.False(payload.IsSuccess);
        Assert.Equal("context.validation", payload.Errors[0].Code);
    }

    [Fact]
    public async Task ProposalConflicts_MapToConflict()
    {
        using var factory = CreateFactory(new ConflictingAgentInteractionService());
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/artifacts/proposals",
            new ProposeArtifactRequest(
                "memora",
                "ADR-001",
                ArtifactType.Decision,
                CreateContent()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ProposalResponse>();
        Assert.NotNull(payload);
        Assert.False(payload.IsSuccess);
        Assert.Equal("proposal.conflict", payload.Errors[0].Code);
    }

    [Fact]
    public async Task RequestsWithoutLocalToken_ReturnUnauthorized()
    {
        using var factory = CreateFactory(new TestAgentInteractionService());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/projects/memora");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        Assert.Equal("local_auth.required", payload.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public void Startup_RejectsNonLoopbackUrls()
    {
        using var factory = CreateFactory(new TestAgentInteractionService())
            .WithWebHostBuilder(builder => builder.UseSetting(WebHostDefaults.ServerUrlsKey, "http://0.0.0.0:5081"));

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("loopback", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocalAccessTokenStore_GeneratesWorkspaceLocalTokenFile()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "memora-api-local-access-token-tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalAccessTokenStore(rootPath);

            var token = store.GetOrCreateToken();

            Assert.True(File.Exists(Path.Combine(rootPath, ".memora", "local-access-token")));
            Assert.True(store.IsValidToken(token));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(store.TokenPath));
            }
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(IAgentInteractionService service) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                var localAccessRootPath = Path.Combine(
                    Path.GetTempPath(),
                    "memora-api-local-access-tests",
                    Guid.NewGuid().ToString("N"));
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Memora:LocalAccessRootPath"] = localAccessRootPath,
                            ["Memora:WorkspacesRootPath"] = localAccessRootPath
                        }));
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IAgentInteractionService>();
                    services.RemoveAll<IReviewInboxService>();
                    services.AddSingleton(service);
                    if (service is IReviewInboxService reviewInboxService)
                    {
                        services.AddSingleton(reviewInboxService);
                    }
                    else
                    {
                        services.AddSingleton<IReviewInboxService>(new EmptyReviewInboxService());
                    }
                });
            });

    private static HttpClient CreateAuthorizedClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var token = factory.Services.GetRequiredService<LocalAccessTokenStore>().GetOrCreateToken();
        client.DefaultRequestHeaders.Add(LocalAccessDefaults.HeaderName, token);
        return client;
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

        public ReviewDecisionResponse ApplyReviewDecision(string projectId, ReviewDecisionRequest request) =>
            new(
                projectId,
                request.Decision,
                CreateReviewInboxItem(ArtifactStatus.Deprecated),
                "Rejected ADR-002 revision 1.",
                []);

        private static ReviewInboxItem CreateReviewInboxItem(ArtifactStatus status = ArtifactStatus.Proposed) =>
            new(
                "ADR-002",
                ArtifactType.Decision,
                status,
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

    private sealed class ConflictingAgentInteractionService : IAgentInteractionService
    {
        public ProjectLookupResponse GetProject(string projectId) =>
            new(projectId, "Memora", "active", []);

        public GetContextResponse GetContext(GetContextRequest request) =>
            new(null, [new AgentInteractionError("context.not_configured", "Context service is not configured.", "service")]);

        public ProposalResponse ProposeArtifact(ProposeArtifactRequest request) =>
            new(request.ProjectId, request.ArtifactId, request.ArtifactType, ArtifactStatus.Proposed, 0, [new AgentInteractionError("proposal.conflict", "Proposal write conflicted with existing storage state.", "artifact_id")]);

        public ProposalResponse ProposeUpdate(ProposeUpdateRequest request) =>
            new(request.ProjectId, request.ArtifactId, ArtifactType.Decision, ArtifactStatus.Proposed, 0, [new AgentInteractionError("proposal.conflict", "Proposal write conflicted with existing storage state.", "artifact_id")]);

        public OutcomeResponse RecordOutcome(RecordOutcomeRequest request) =>
            new(request.ProjectId, request.ArtifactId, ArtifactStatus.Proposed, 0, OutcomeKind.Mixed, [new AgentInteractionError("outcome.not_configured", "Outcome service is not configured.", "service")]);
    }

    private sealed class EmptyReviewInboxService : IReviewInboxService
    {
        public ReviewInboxResponse GetReviewInbox(string projectId) =>
            new(projectId, [], [new AgentInteractionError("review.not_configured", "Review inbox service is not configured.", "service")]);

        public ReviewArtifactPreviewResponse GetReviewArtifactPreview(string projectId, string relativePath) =>
            new(projectId, null, string.Empty, new Dictionary<string, string>(StringComparer.Ordinal), [new AgentInteractionError("review.not_configured", "Review preview service is not configured.", "service")]);

        public ReviewDecisionResponse ApplyReviewDecision(string projectId, ReviewDecisionRequest request) =>
            new(projectId, request.Decision, null, "Review decision service is not configured.", [new AgentInteractionError("review.not_configured", "Review decision service is not configured.", "service")]);
    }
}
