using System.Net;
using Memora.Core.Artifacts;
using Memora.Core.Projects;
using Memora.Storage.Persistence;
using Memora.Storage.Workspaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Memora.Ui.Tests;

public sealed class UnderstandingOutputTests : IDisposable
{
    private readonly string _workspacesRootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-understanding-output-tests",
        Guid.NewGuid().ToString("N"));

    private readonly ArtifactFileStore _fileStore = new();

    [Fact]
    public async Task UnderstandingRoute_RendersContextTraceabilityAndComponentSummary()
    {
        var workspace = CreateWorkspace("memora");
        _fileStore.Save(workspace, CreateCharterArtifact());
        _fileStore.Save(workspace, CreateDecisionArtifact());
        _fileStore.Save(workspace, CreatePlanArtifact());

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Memora:WorkspacesRootPath"] = _workspacesRootPath
                    })));

        using var client = LocalAuthTestClient.CreateAuthorizedClient(factory);

        var response = await client.GetAsync("/understanding?projectId=memora&taskDescription=Review%20understanding%20outputs&artifactId=PLN-001&traceabilityKind=Dependency");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Understanding Outputs", html, StringComparison.Ordinal);
        Assert.Contains("Context View", html, StringComparison.Ordinal);
        Assert.Contains("Traceability View", html, StringComparison.Ordinal);
        Assert.Contains("Component Summary", html, StringComparison.Ordinal);
        Assert.Contains("PLN-001 - Understanding outputs plan", html, StringComparison.Ordinal);
        Assert.Contains("approved artifacts are the default context grounding", html, StringComparison.Ordinal);
        Assert.Contains("PLN-001 depends on ADR-001", html, StringComparison.Ordinal);
        Assert.Contains("Goal", html, StringComparison.Ordinal);
        Assert.Contains("Current understanding stays grounded in shared retrieval logic.", html, StringComparison.Ordinal);
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
            ["milestone-4"],
            "user",
            "charter seed",
            ArtifactLinks.Empty,
            """
            ## Problem Statement
            Keep project understanding durable.

            ## Primary Users / Stakeholders
            Engineers and operators.

            ## Current Pain
            Project understanding is hard to inspect consistently.

            ## Desired Outcome
            Human-readable outputs stay grounded.

            ## Definition of Success
            Understanding views render from approved project data.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Problem Statement"] = "Keep project understanding durable.",
                ["Primary Users / Stakeholders"] = "Engineers and operators.",
                ["Current Pain"] = "Project understanding is hard to inspect consistently.",
                ["Desired Outcome"] = "Human-readable outputs stay grounded.",
                ["Definition of Success"] = "Understanding views render from approved project data."
            });

    private static ArchitectureDecisionArtifact CreateDecisionArtifact() =>
        new(
            "ADR-001",
            "memora",
            ArtifactStatus.Approved,
            "Understanding output decision",
            new DateTimeOffset(2026, 4, 17, 8, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 8, 45, 0, TimeSpan.Zero),
            1,
            ["milestone-4"],
            "user",
            "decision seed",
            ArtifactLinks.Empty,
            """
            ## Context
            Operators need traceable output views.

            ## Decision
            Reuse the shared traceability service.

            ## Alternatives Considered
            Rebuild understanding selection separately in the UI.

            ## Consequences
            Output views stay aligned with approved relationships.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Operators need traceable output views.",
                ["Decision"] = "Reuse the shared traceability service.",
                ["Alternatives Considered"] = "Rebuild understanding selection separately in the UI.",
                ["Consequences"] = "Output views stay aligned with approved relationships."
            },
            "2026-04-17");

    private static PlanArtifact CreatePlanArtifact() =>
        new(
            "PLN-001",
            "memora",
            ArtifactStatus.Approved,
            "Understanding outputs plan",
            new DateTimeOffset(2026, 4, 17, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 9, 20, 0, TimeSpan.Zero),
            1,
            ["milestone-4", "understanding"],
            "user",
            "plan seed",
            new ArtifactLinks(
                dependsOn: ["ADR-001"],
                affects: [],
                derivedFrom: [],
                supersedes: []),
            """
            ## Goal
            Build understanding outputs.

            ## Scope
            Keep the first slice thin and reviewable.

            ## Acceptance Criteria
            - understanding outputs render from current project files

            ## Notes
            Current understanding stays grounded in shared retrieval logic.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Goal"] = "Build understanding outputs.",
                ["Scope"] = "Keep the first slice thin and reviewable.",
                ["Acceptance Criteria"] = "- understanding outputs render from current project files",
                ["Notes"] = "Current understanding stays grounded in shared retrieval logic."
            },
            ArtifactPriority.High,
            true);
}
