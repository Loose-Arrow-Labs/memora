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
using Memora.Mcp.Server;
using Memora.Storage.Persistence;
using Memora.Storage.Workspaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Memora.Api.Tests;

public sealed class RuntimeContractCompatibilityTests : IDisposable
{
    private readonly string _workspacesRootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-runtime-compatibility-tests",
        Guid.NewGuid().ToString("N"));

    private readonly ArtifactFileStore _fileStore = new();

    [Fact]
    public async Task PublishedSurfaces_SharedContextProposalFlows()
    {
        var workspace = CreateWorkspace("memora");
        _fileStore.Save(workspace, CreateCharterArtifact());
        _fileStore.Save(workspace, CreatePlanArtifact());
        _fileStore.Save(workspace, CreateDecisionArtifact());

        using var harness = CreateHarness();
        var clients = harness.CreateClients();
        var contract = ExternalRuntimeContract.Current;

        Assert.Contains(contract.Operations, operation => operation.Name == "get_context");
        Assert.Contains(contract.Operations, operation => operation.Name == "propose_artifact");

        for (var index = 0; index < clients.Count; index++)
        {
            var client = clients[index];
            var artifactId = $"ADR-30{index + 1}";

            var context = await client.GetContextAsync(
                new GetContextRequest(
                    "memora",
                    $"Prepare compatibility context for {client.Name}.",
                    focusArtifactIds: ["ADR-001"],
                    focusTags: ["runtime"]));
            var proposal = await client.ProposeArtifactAsync(
                new ProposeArtifactRequest(
                    "memora",
                    artifactId,
                    ArtifactType.Decision,
                    CreateValidProposalContent(client.Name)));

            Assert.Equal(client.Surface, contract.Operations.Single(operation => operation.Name == "get_context").SupportedSurfaces.Single(surface => surface == client.Surface));
            Assert.True(context.IsSuccess, $"{client.Name} context errors: {FormatErrors(context.Errors)}");
            Assert.Contains("ADR-001", context.ArtifactIds);

            Assert.True(proposal.IsSuccess, $"{client.Name} proposal errors: {FormatErrors(proposal.Errors)}");
            Assert.Equal(ArtifactStatus.Proposed, proposal.ResultingStatus);
            Assert.True(File.Exists(Path.Combine(workspace.DraftsRootPath, "decision", $"{artifactId}.r0001.md")));
            Assert.False(File.Exists(Path.Combine(workspace.CanonicalDecisionsPath, $"{artifactId}.r0001.md")));
        }
    }

    [Fact]
    public async Task PublishedSurfaces_ProposalValidationErrors()
    {
        CreateWorkspace("memora");

        using var harness = CreateHarness();

        foreach (var client in harness.CreateClients())
        {
            var response = await client.ProposeArtifactAsync(
                new ProposeArtifactRequest(
                    "memora",
                    $"ADR-40{(int)client.Surface + 1}",
                    ArtifactType.Decision,
                    CreateInvalidProposalContent()));

            Assert.False(response.IsSuccess);
            Assert.Contains(response.Errors, error => error.Code == "artifact.frontmatter.missing");
            Assert.Contains(response.Errors, error => error.Path == "decision_date");
        }
    }

    [Fact]
    public async Task PublishedSurfaces_RepeatedRuns_StableStateView()
    {
        var workspace = CreateWorkspace("memora");
        _fileStore.Save(workspace, CreateCharterArtifact());
        _fileStore.Save(workspace, CreatePlanArtifact());
        _fileStore.Save(workspace, CreateDecisionArtifact());

        using var harness = CreateHarness();
        var request = new GetContextRequest(
            "memora",
            "Prepare compatibility context for deterministic verification.",
            focusArtifactIds: ["ADR-001"],
            focusTags: ["runtime"]);
        var serializedBundlesByClient = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var client in harness.CreateClients())
        {
            var bundles = new List<string>();
            var hashes = new List<string>();

            for (var index = 0; index < 3; index++)
            {
                var response = await client.GetContextAsync(request);
                Assert.True(response.IsSuccess, $"{client.Name} context errors: {FormatErrors(response.Errors)}");
                bundles.Add(response.SerializedBundle);
                hashes.Add(Hash(response.SerializedBundle));
            }

            Assert.All(bundles, bundle => Assert.Equal(bundles[0], bundle));
            Assert.All(hashes, hash => Assert.Equal(hashes[0], hash));
            serializedBundlesByClient[client.Name] = bundles;
        }

        Assert.Equal(serializedBundlesByClient["OpenAPI"][0], serializedBundlesByClient["MCP"][0]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspacesRootPath))
        {
            Directory.Delete(_workspacesRootPath, recursive: true);
        }
    }

    private CompatibilityHarness CreateHarness()
    {
        var service = new FileSystemAgentInteractionService(_workspacesRootPath);
        var factory = new WebApplicationFactory<Program>()
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
                    services.AddSingleton<IAgentInteractionService>(_ => service);
                });
            });

        var client = factory.CreateClient();
        var token = factory.Services.GetRequiredService<LocalAccessTokenStore>().GetOrCreateToken();
        client.DefaultRequestHeaders.Add(LocalAccessDefaults.HeaderName, token);

        return new CompatibilityHarness(factory, client, new MemoraMcpServer(service));
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

    private static ArtifactProposalContent CreateValidProposalContent(string provenance) =>
        new(
            "Compatibility proposal",
            provenance,
            "Need a reviewable compatibility proposal.",
            ["runtime"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "The runtime is using the shared Memora contract.",
                ["Decision"] = "Submit a proposal without mutating approved truth.",
                ["Alternatives Considered"] = "Provider-specific integration paths.",
                ["Consequences"] = "Compatibility checks remain repeatable."
            },
            AgentArtifactLinks.Empty,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["decision_date"] = "2026-04-23"
            });

    private static ArtifactProposalContent CreateInvalidProposalContent() =>
        new(
            "Invalid compatibility proposal",
            "runtime",
            "Missing the required decision date.",
            ["runtime"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "A validation path needs to be exercised.",
                ["Decision"] = "Omit the required type-specific field.",
                ["Alternatives Considered"] = "Skipping runtime compatibility checks.",
                ["Consequences"] = "Both surfaces should return the same validation signal."
            });

    private static ProjectCharterArtifact CreateCharterArtifact() =>
        new(
            "CHR-001",
            "memora",
            ArtifactStatus.Approved,
            "Runtime compatibility charter",
            new DateTimeOffset(2026, 4, 23, 11, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 23, 11, 10, 0, TimeSpan.Zero),
            1,
            ["runtime"],
            "user",
            "compatibility seed",
            ArtifactLinks.Empty,
            """
            ## Problem Statement
            External runtimes need a stable governed memory boundary.

            ## Primary Users / Stakeholders
            Runtime operators.

            ## Current Pain
            Surface drift can break runtime integrations.

            ## Desired Outcome
            Shared contract usage stays stable across surfaces.

            ## Definition of Success
            Compatibility checks stay repeatable.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Problem Statement"] = "External runtimes need a stable governed memory boundary.",
                ["Primary Users / Stakeholders"] = "Runtime operators.",
                ["Current Pain"] = "Surface drift can break runtime integrations.",
                ["Desired Outcome"] = "Shared contract usage stays stable across surfaces.",
                ["Definition of Success"] = "Compatibility checks stay repeatable."
            });

    private static PlanArtifact CreatePlanArtifact() =>
        new(
            "PLN-001",
            "memora",
            ArtifactStatus.Approved,
            "Runtime compatibility validation",
            new DateTimeOffset(2026, 4, 23, 11, 15, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 23, 11, 25, 0, TimeSpan.Zero),
            1,
            ["runtime"],
            "user",
            "compatibility seed",
            ArtifactLinks.Empty,
            """
            ## Goal
            Validate the shared runtime-facing contract across surfaces.

            ## Scope
            Compare MCP and OpenAPI using the same governed behavior.

            ## Acceptance Criteria
            - success paths agree
            - validation failures stay repeatable

            ## Notes
            Do not add provider-specific core behavior.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Goal"] = "Validate the shared runtime-facing contract across surfaces.",
                ["Scope"] = "Compare MCP and OpenAPI using the same governed behavior.",
                ["Acceptance Criteria"] = "- success paths agree\n- validation failures stay repeatable",
                ["Notes"] = "Do not add provider-specific core behavior."
            },
            ArtifactPriority.High,
            true);

    private static ArchitectureDecisionArtifact CreateDecisionArtifact() =>
        new(
            "ADR-001",
            "memora",
            ArtifactStatus.Approved,
            "Shared runtime contract reuse",
            new DateTimeOffset(2026, 4, 23, 11, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 23, 11, 40, 0, TimeSpan.Zero),
            1,
            ["runtime"],
            "user",
            "compatibility seed",
            ArtifactLinks.Empty,
            """
            ## Context
            Shared runtime behavior must not drift across surfaces.

            ## Decision
            Validate MCP and OpenAPI against the same Memora contract.

            ## Alternatives Considered
            Provider-specific compatibility checks.

            ## Consequences
            Runtime alignment remains provider-agnostic.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Shared runtime behavior must not drift across surfaces.",
                ["Decision"] = "Validate MCP and OpenAPI against the same Memora contract.",
                ["Alternatives Considered"] = "Provider-specific compatibility checks.",
                ["Consequences"] = "Runtime alignment remains provider-agnostic."
            },
            "2026-04-23");

    private static string FormatErrors(IReadOnlyList<AgentInteractionError> errors) =>
        string.Join(" | ", errors.Select(error => $"{error.Code}:{error.Path}:{error.Message}"));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record RuntimeContextCompatibilityResult(
        bool IsSuccess,
        IReadOnlyList<string> ArtifactIds,
        string SerializedBundle,
        IReadOnlyList<AgentInteractionError> Errors);

    private sealed record RuntimeProposalCompatibilityResult(
        bool IsSuccess,
        ArtifactStatus ResultingStatus,
        IReadOnlyList<AgentInteractionError> Errors);

    private interface IRuntimeCompatibilityClient
    {
        string Name { get; }

        ExternalRuntimeContractSurface Surface { get; }

        Task<RuntimeContextCompatibilityResult> GetContextAsync(GetContextRequest request);

        Task<RuntimeProposalCompatibilityResult> ProposeArtifactAsync(ProposeArtifactRequest request);
    }

    private sealed class CompatibilityHarness : IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;

        public CompatibilityHarness(WebApplicationFactory<Program> factory, HttpClient client, MemoraMcpServer mcp)
        {
            _factory = factory;
            Client = client;
            Mcp = mcp;
        }

        public HttpClient Client { get; }

        public MemoraMcpServer Mcp { get; }

        public IReadOnlyList<IRuntimeCompatibilityClient> CreateClients() =>
            [
                new OpenApiRuntimeCompatibilityClient(Client),
                new McpRuntimeCompatibilityClient(Mcp)
            ];

        public void Dispose()
        {
            Client.Dispose();
            _factory.Dispose();
        }
    }

    private sealed class OpenApiRuntimeCompatibilityClient(HttpClient httpClient) : IRuntimeCompatibilityClient
    {
        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        public string Name => "OpenAPI";

        public ExternalRuntimeContractSurface Surface => ExternalRuntimeContractSurface.OpenApi;

        public async Task<RuntimeContextCompatibilityResult> GetContextAsync(GetContextRequest request)
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/context", request);
            var document = await response.Content.ReadFromJsonAsync<JsonDocument>();
            if (document is null)
            {
                throw new InvalidOperationException("OpenAPI context response body was missing.");
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return new RuntimeContextCompatibilityResult(false, [], string.Empty, ReadErrors(document.RootElement));
            }

            var bundle = document.RootElement.GetProperty("bundle");
            var artifactIds = bundle
                .GetProperty("layers")
                .EnumerateArray()
                .SelectMany(layer => layer.GetProperty("artifacts").EnumerateArray())
                .Select(artifact => artifact.GetProperty("artifact").GetProperty("id").GetString())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .ToArray();

            return new RuntimeContextCompatibilityResult(true, artifactIds, bundle.GetRawText(), []);
        }

        public async Task<RuntimeProposalCompatibilityResult> ProposeArtifactAsync(ProposeArtifactRequest request)
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/artifacts/proposals", request);
            var body = await response.Content.ReadFromJsonAsync<ProposalResponse>();
            if (body is null)
            {
                throw new InvalidOperationException("OpenAPI proposal response body was missing.");
            }

            return new RuntimeProposalCompatibilityResult(body.IsSuccess, body.ResultingStatus, body.Errors);
        }

        private static IReadOnlyList<AgentInteractionError> ReadErrors(JsonElement root)
        {
            if (!root.TryGetProperty("errors", out var errorsElement))
            {
                return [];
            }

            return errorsElement
                .EnumerateArray()
                .Select(error => new AgentInteractionError(
                    error.GetProperty("code").GetString() ?? string.Empty,
                    error.GetProperty("message").GetString() ?? string.Empty,
                    error.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : null))
                .ToArray();
        }
    }

    private sealed class McpRuntimeCompatibilityClient(MemoraMcpServer server) : IRuntimeCompatibilityClient
    {
        private readonly MemoraMcpServer _server = server ?? throw new ArgumentNullException(nameof(server));

        public string Name => "MCP";

        public ExternalRuntimeContractSurface Surface => ExternalRuntimeContractSurface.Mcp;

        public Task<RuntimeContextCompatibilityResult> GetContextAsync(GetContextRequest request)
        {
            var invocation = _server.InvokeTool("get_context", request);
            var response = invocation.Payload as GetContextResponse;
            if (!invocation.IsSuccess || response is null)
            {
                return Task.FromResult(new RuntimeContextCompatibilityResult(
                    false,
                    [],
                    string.Empty,
                    invocation.Errors));
            }

            var artifactIds = response.Bundle is null
                ? []
                : response.Bundle.Layers
                    .SelectMany(layer => layer.Artifacts)
                    .Select(artifact => artifact.Artifact.Id)
                    .ToArray();

            return Task.FromResult(new RuntimeContextCompatibilityResult(
                response.IsSuccess,
                artifactIds,
                response.Bundle is null ? string.Empty : ProjectStateViewSerializer.Serialize(response.Bundle),
                response.Errors));
        }

        public Task<RuntimeProposalCompatibilityResult> ProposeArtifactAsync(ProposeArtifactRequest request)
        {
            var invocation = _server.InvokeTool("propose_artifact", request);
            var response = invocation.Payload as ProposalResponse;
            if (!invocation.IsSuccess || response is null)
            {
                return Task.FromResult(new RuntimeProposalCompatibilityResult(false, ArtifactStatus.Proposed, invocation.Errors));
            }

            return Task.FromResult(new RuntimeProposalCompatibilityResult(response.IsSuccess, response.ResultingStatus, response.Errors));
        }
    }
}
