using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memora.Api.Services;
using Memora.Core.AgentInteraction;
using Memora.Core.Artifacts;
using Memora.Core.Projects;
using Memora.Hosting;
using Memora.Storage.Persistence;
using Memora.Storage.Workspaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Memora.Api.Tests;

public sealed class RuntimeFacingPrototypeTests : IDisposable
{
    private readonly string _workspacesRootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-runtime-prototype-tests",
        Guid.NewGuid().ToString("N"));

    private readonly ArtifactFileStore _fileStore = new();

    [Fact]
    public async Task OpenApiPrototype_ContextSubmit_NoCanonicalMutation()
    {
        var workspace = CreateWorkspace("memora");
        _fileStore.Save(workspace, CreateCharterArtifact());
        _fileStore.Save(workspace, CreatePlanArtifact());
        _fileStore.Save(workspace, CreateDecisionArtifact());

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Memora:LocalAccessRootPath"] = _workspacesRootPath,
                            ["Memora:WorkspacesRootPath"] = _workspacesRootPath
                        }));
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IAgentInteractionService>();
                    services.AddSingleton<IAgentInteractionService>(_ => new FileSystemAgentInteractionService(_workspacesRootPath));
                });
            });

        var client = new OpenApiRuntimePrototypeClient(CreateAuthorizedClient(factory));

        var context = await client.GetContextAsync(
            new GetContextRequest(
                "memora",
                "Prepare runtime-facing context for implementation guidance.",
                focusArtifactIds: ["ADR-001"],
                focusTags: ["runtime"]));
        var proposal = await client.ProposeArtifactAsync(
            new ProposeArtifactRequest(
                "memora",
                "ADR-200",
                ArtifactType.Decision,
                CreateProposalContent()));

        Assert.Equal(HttpStatusCode.OK, context.StatusCode);
        Assert.NotNull(context.Response);
        Assert.Equal("memora", context.Response.RootElement.GetProperty("bundle").GetProperty("request").GetProperty("projectId").GetString());
        Assert.Contains(
            context.Response.RootElement
                .GetProperty("bundle")
                .GetProperty("layers")
                .EnumerateArray()
                .SelectMany(layer => layer.GetProperty("artifacts").EnumerateArray())
                .Select(artifact => artifact.GetProperty("artifact").GetProperty("id").GetString()),
            artifactId => artifactId == "ADR-001");

        Assert.NotNull(proposal.Response);
        Assert.True(
            proposal.StatusCode == HttpStatusCode.Accepted,
            string.Join(" | ", proposal.Response.Errors.Select(error => $"{error.Code}:{error.Path}:{error.Message}")));
        Assert.True(proposal.Response.IsSuccess);
        Assert.Equal(ArtifactStatus.Proposed, proposal.Response.ResultingStatus);
        Assert.True(File.Exists(Path.Combine(workspace.DraftsRootPath, "decision", "ADR-200.r0001.md")));
        Assert.False(File.Exists(Path.Combine(workspace.CanonicalDecisionsPath, "ADR-200.r0001.md")));
    }

    [Fact]
    public async Task OpenApiPrototype_GoldenPath_PreservesDefaultView()
    {
        var workspace = CreateWorkspace("memora");
        _fileStore.Save(workspace, CreateCharterArtifact());
        _fileStore.Save(workspace, CreatePlanArtifact());
        _fileStore.Save(workspace, CreateDecisionArtifact());

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Memora:LocalAccessRootPath"] = _workspacesRootPath,
                            ["Memora:WorkspacesRootPath"] = _workspacesRootPath
                        }));
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IAgentInteractionService>();
                    services.AddSingleton<IAgentInteractionService>(_ => new FileSystemAgentInteractionService(_workspacesRootPath));
                });
            });

        var client = new OpenApiRuntimePrototypeClient(CreateAuthorizedClient(factory));
        var request = new GetContextRequest(
            "memora",
            "Validate the Codex golden path against the shared runtime-facing contract.",
            focusArtifactIds: ["ADR-001"],
            focusTags: ["runtime"]);

        var before = await client.GetContextAsync(request);
        var proposal = await client.ProposeArtifactAsync(
            new ProposeArtifactRequest(
                "memora",
                "ADR-200",
                ArtifactType.Decision,
                CreateProposalContent()));
        var outcome = await client.RecordOutcomeAsync(
            new RecordOutcomeRequest(
                "memora",
                "OUT-200",
                CreateOutcomeContent("ADR-200")));
        var after = await client.GetContextAsync(request);

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.NotNull(before.Response);
        Assert.NotNull(after.Response);
        Assert.Equal(
            before.Response.RootElement.GetProperty("bundle").GetRawText(),
            after.Response.RootElement.GetProperty("bundle").GetRawText());

        Assert.NotNull(proposal.Response);
        Assert.True(proposal.Response.IsSuccess);
        Assert.Equal(ArtifactStatus.Proposed, proposal.Response.ResultingStatus);

        Assert.NotNull(outcome.Response);
        Assert.True(outcome.Response.IsSuccess);
        Assert.Equal(ArtifactStatus.Proposed, outcome.Response.ResultingStatus);
        Assert.Equal(OutcomeKind.Success, outcome.Response.OutcomeKind);

        Assert.True(File.Exists(Path.Combine(workspace.DraftsRootPath, "decision", "ADR-200.r0001.md")));
        Assert.True(File.Exists(Path.Combine(workspace.DraftsRootPath, "outcome", "OUT-200.r0001.md")));
        Assert.False(File.Exists(Path.Combine(workspace.CanonicalDecisionsPath, "ADR-200.r0001.md")));
        Assert.False(File.Exists(Path.Combine(workspace.CanonicalOutcomesPath, "OUT-200.r0001.md")));
    }

    [Fact]
    public async Task OpenApiPrototype_RepeatedRuns_StableStateView()
    {
        var workspace = CreateWorkspace("memora");
        _fileStore.Save(workspace, CreateCharterArtifact());
        _fileStore.Save(workspace, CreatePlanArtifact());
        _fileStore.Save(workspace, CreateDecisionArtifact());

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Memora:LocalAccessRootPath"] = _workspacesRootPath,
                            ["Memora:WorkspacesRootPath"] = _workspacesRootPath
                        }));
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IAgentInteractionService>();
                    services.AddSingleton<IAgentInteractionService>(_ => new FileSystemAgentInteractionService(_workspacesRootPath));
                });
            });

        var client = new OpenApiRuntimePrototypeClient(CreateAuthorizedClient(factory));
        var request = new GetContextRequest(
            "memora",
            "Prepare runtime-facing context for implementation guidance.",
            focusArtifactIds: ["ADR-001"],
            focusTags: ["runtime"]);
        var bundles = new List<string>();
        var hashes = new List<string>();

        for (var index = 0; index < 3; index++)
        {
            var context = await client.GetContextAsync(request);
            Assert.Equal(HttpStatusCode.OK, context.StatusCode);

            var bundleJson = context.Response.RootElement.GetProperty("bundle").GetRawText();
            bundles.Add(bundleJson);
            hashes.Add(Hash(bundleJson));
        }

        Assert.All(bundles, bundle => Assert.Equal(bundles[0], bundle));
        Assert.All(hashes, hash => Assert.Equal(hashes[0], hash));

        using var document = JsonDocument.Parse(bundles[0]);
        var decisionArtifact = document.RootElement
            .GetProperty("layers")
            .EnumerateArray()
            .SelectMany(layer => layer.GetProperty("artifacts").EnumerateArray())
            .Single(artifact => artifact.GetProperty("artifact").GetProperty("id").GetString() == "ADR-001")
            .GetProperty("artifact");

        Assert.Equal(
            ["alpha", "runtime"],
            decisionArtifact.GetProperty("tags").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray());
        Assert.Equal(
            ["Alternatives Considered", "Consequences", "Context", "Decision"],
            decisionArtifact.GetProperty("sections").EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(
            [
                $"{(int)ArtifactRelationshipKind.Affects}:PLN-001",
                $"{(int)ArtifactRelationshipKind.DerivedFrom}:CHR-001"
            ],
            decisionArtifact.GetProperty("links").GetProperty("relationships").EnumerateArray()
                .Select(relationship => $"{relationship.GetProperty("kind").GetInt32()}:{relationship.GetProperty("targetArtifactId").GetString()}")
                .ToArray());
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

    private static ProjectCharterArtifact CreateCharterArtifact() =>
        new(
            "CHR-001",
            "memora",
            ArtifactStatus.Approved,
            "Memora charter",
            new DateTimeOffset(2026, 4, 23, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 23, 10, 10, 0, TimeSpan.Zero),
            1,
            ["runtime"],
            "user",
            "runtime prototype seed",
            ArtifactLinks.Empty,
            """
            ## Problem Statement
            Runtime integrations need grounded memory.

            ## Primary Users / Stakeholders
            Runtime operators.

            ## Current Pain
            Runtime work loses project context.

            ## Desired Outcome
            Runtimes can request deterministic context safely.

            ## Definition of Success
            Proposal-only runtime flows stay governed.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Problem Statement"] = "Runtime integrations need grounded memory.",
                ["Primary Users / Stakeholders"] = "Runtime operators.",
                ["Current Pain"] = "Runtime work loses project context.",
                ["Desired Outcome"] = "Runtimes can request deterministic context safely.",
                ["Definition of Success"] = "Proposal-only runtime flows stay governed."
            });

    private static PlanArtifact CreatePlanArtifact() =>
        new(
            "PLN-001",
            "memora",
            ArtifactStatus.Approved,
            "Runtime alignment plan",
            new DateTimeOffset(2026, 4, 23, 10, 15, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 23, 10, 25, 0, TimeSpan.Zero),
            1,
            ["runtime"],
            "user",
            "runtime prototype seed",
            ArtifactLinks.Empty,
            """
            ## Goal
            Prove a runtime-facing context and proposal path.

            ## Scope
            Reuse current Memora surfaces.

            ## Acceptance Criteria
            - runtime can retrieve context
            - runtime can submit a reviewable proposal

            ## Notes
            Keep canonical truth approval-governed.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Goal"] = "Prove a runtime-facing context and proposal path.",
                ["Scope"] = "Reuse current Memora surfaces.",
                ["Acceptance Criteria"] = "- runtime can retrieve context\n- runtime can submit a reviewable proposal",
                ["Notes"] = "Keep canonical truth approval-governed."
            },
            ArtifactPriority.High,
            true);

    private static ArchitectureDecisionArtifact CreateDecisionArtifact() =>
        new(
            "ADR-001",
            "memora",
            ArtifactStatus.Approved,
            "Runtime contract reuse",
            new DateTimeOffset(2026, 4, 23, 10, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 23, 10, 40, 0, TimeSpan.Zero),
            1,
            ["runtime", "alpha"],
            "user",
            "runtime prototype seed",
            new ArtifactLinks(
                [
                    new ArtifactRelationship(ArtifactRelationshipKind.DerivedFrom, "CHR-001"),
                    new ArtifactRelationship(ArtifactRelationshipKind.Affects, "PLN-001")
                ]),
            """
            ## Context
            Runtime alignment must not create provider-specific core behavior.

            ## Decision
            Reuse the shared Memora contract through existing surfaces.

            ## Alternatives Considered
            Adding runtime-specific core services.

            ## Consequences
            Runtime prototypes stay narrow and reviewable.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Runtime alignment must not create provider-specific core behavior.",
                ["Decision"] = "Reuse the shared Memora contract through existing surfaces.",
                ["Alternatives Considered"] = "Adding runtime-specific core services.",
                ["Consequences"] = "Runtime prototypes stay narrow and reviewable."
            },
            "2026-04-23");

    private static ArtifactProposalContent CreateProposalContent() =>
        new(
            "Runtime integration proposal",
            "runtime",
            "Need a reviewable proposal from the runtime-facing prototype.",
            ["runtime"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "The runtime consumed deterministic Memora context.",
                ["Decision"] = "Submit a reviewable proposal through the shared API surface.",
                ["Alternatives Considered"] = "Writing canonical artifacts directly.",
                ["Consequences"] = "Human approval remains in control."
            },
            AgentArtifactLinks.Empty,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["decision_date"] = "2026-04-23"
            });

    private static ArtifactProposalContent CreateOutcomeContent(string proposalArtifactId) =>
        new(
            "Runtime workflow outcome",
            "runtime",
            "Capture the result of the golden-path runtime workflow.",
            ["runtime"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["What Happened"] = "The runtime retrieved deterministic context, submitted a proposal, and recorded an outcome.",
                ["Why"] = "The golden path should remain proposal-only.",
                ["Impact"] = "The shared workflow now covers retrieval, proposal submission, and outcome recording.",
                ["Follow-up"] = "Review the proposed decision and recorded outcome."
            },
            new AgentArtifactLinks([proposalArtifactId], [], [], []),
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["outcome"] = "success"
            });

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static HttpClient CreateAuthorizedClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var token = factory.Services.GetRequiredService<LocalAccessTokenStore>().GetOrCreateToken();
        client.DefaultRequestHeaders.Add(LocalAccessDefaults.HeaderName, token);
        return client;
    }

    private sealed class OpenApiRuntimePrototypeClient(HttpClient httpClient)
    {
        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        public async Task<(HttpStatusCode StatusCode, JsonDocument Response)> GetContextAsync(GetContextRequest request)
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/context", request);
            var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
            if (body is null)
            {
                throw new InvalidOperationException("Context response body was missing.");
            }

            return (response.StatusCode, body);
        }

        public async Task<(HttpStatusCode StatusCode, ProposalResponse? Response)> ProposeArtifactAsync(ProposeArtifactRequest request)
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/artifacts/proposals", request);
            var body = await response.Content.ReadFromJsonAsync<ProposalResponse>();
            return (response.StatusCode, body);
        }

        public async Task<(HttpStatusCode StatusCode, OutcomeResponse? Response)> RecordOutcomeAsync(RecordOutcomeRequest request)
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/outcomes", request);
            var body = await response.Content.ReadFromJsonAsync<OutcomeResponse>();
            return (response.StatusCode, body);
        }
    }
}
