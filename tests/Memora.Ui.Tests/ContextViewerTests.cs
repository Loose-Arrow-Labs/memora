using System.Net;
using Memora.Core.Artifacts;
using Memora.Core.Projects;
using Memora.Storage.Persistence;
using Memora.Storage.Workspaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Memora.Ui.Tests;

public sealed class ContextViewerTests : IDisposable
{
    private readonly string _workspacesRootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-context-viewer-tests",
        Guid.NewGuid().ToString("N"));

    private readonly ArtifactFileStore _fileStore = new();

    [Fact]
    public async Task ContextViewer_LoadsAndRendersBundleDetails()
    {
        var workspace = CreateWorkspace("memora");
        _fileStore.Save(workspace, CreateCharterArtifact());
        _fileStore.Save(workspace, CreatePlanArtifact());
        _fileStore.Save(workspace, CreateDecisionArtifact());

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Memora:WorkspacesRootPath"] = _workspacesRootPath
                    })));

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/context-viewer?projectId=memora&taskDescription=Prepare%20milestone%203%20context");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Context Viewer", html, StringComparison.Ordinal);
        Assert.Contains("GetContextResponse.bundle", html, StringComparison.Ordinal);
        Assert.Contains("Layer1", html, StringComparison.Ordinal);
        Assert.Contains("artifact.status: approved", html, StringComparison.Ordinal);
        Assert.Contains("CHR-001 - Memora charter", html, StringComparison.Ordinal);
        Assert.Contains("PLN-001 - Active plan", html, StringComparison.Ordinal);
        Assert.Contains("approved artifacts are the default context grounding", html, StringComparison.Ordinal);
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
            The viewer shows shared context data.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Problem Statement"] = "Keep project memory structured.",
                ["Primary Users / Stakeholders"] = "Engineers.",
                ["Current Pain"] = "Context drifts between sessions.",
                ["Desired Outcome"] = "Context stays deterministic.",
                ["Definition of Success"] = "The viewer shows shared context data."
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
            Keep the viewer minimal.

            ## Acceptance Criteria
            - operators can inspect the bundle

            ## Notes
            Preserve shared selection logic.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Goal"] = "Assemble deterministic context.",
                ["Scope"] = "Keep the viewer minimal.",
                ["Acceptance Criteria"] = "- operators can inspect the bundle",
                ["Notes"] = "Preserve shared selection logic."
            },
            ArtifactPriority.High,
            true);

    private static ArchitectureDecisionArtifact CreateDecisionArtifact() =>
        new(
            "ADR-001",
            "memora",
            ArtifactStatus.Approved,
            "Context viewer decision",
            new DateTimeOffset(2026, 4, 17, 8, 45, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 9, 0, 0, TimeSpan.Zero),
            1,
            ["milestone-3"],
            "user",
            "decision seed",
            ArtifactLinks.Empty,
            """
            ## Context
            Operators need to inspect context bundles.

            ## Decision
            Show the layered bundle in the local UI.

            ## Alternatives Considered
            Recompute selection separately in the UI.

            ## Consequences
            The viewer stays grounded in shared context logic.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Operators need to inspect context bundles.",
                ["Decision"] = "Show the layered bundle in the local UI.",
                ["Alternatives Considered"] = "Recompute selection separately in the UI.",
                ["Consequences"] = "The viewer stays grounded in shared context logic."
            },
            "2026-04-17");
}
