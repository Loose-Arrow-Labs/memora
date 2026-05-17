using Microsoft.Data.Sqlite;
using Memora.Core.Artifacts;
using Memora.Core.Projects;
using Memora.Index.Rebuild;
using Memora.Index.Traceability;
using Memora.Storage.Persistence;
using Memora.Storage.Workspaces;

namespace Memora.Index.Tests.Traceability;

public sealed class TraceabilityQueryServiceTests : IDisposable
{
    private readonly string _workspacesRootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-traceability-tests",
        Guid.NewGuid().ToString("N"));

    private readonly ArtifactFileStore _fileStore = new();
    private readonly SqliteIndexRebuilder _rebuilder = new();
    private readonly TraceabilityQueryService _service = new();

    [Fact]
    public void Query_Direct_ReturnsIncomingOutgoingPaths()
    {
        var workspace = CreateWorkspace("alpha-workspace", "alpha", "Alpha");
        SeedTraceabilityWorkspace(workspace);

        using var connection = CreateConnection();
        var rebuildResult = _rebuilder.Rebuild(connection, _workspacesRootPath);
        Assert.True(rebuildResult.Success);

        var result = _service.Query(connection, new TraceabilityQuery("alpha", "ADR-001", TraceabilityQueryKind.Direct));

        Assert.True(result.Success);
        Assert.Collection(
            result.Paths,
            path => AssertPath(
                path,
                ["ADR-001", "CHR-001"],
                [new TraceabilityPathSegment("ADR-001", ArtifactRelationshipKind.DependsOn, "CHR-001", TraceabilityTraversalDirection.Outgoing)]),
            path => AssertPath(
                path,
                ["ADR-001", "CNS-001"],
                [new TraceabilityPathSegment("ADR-001", ArtifactRelationshipKind.Affects, "CNS-001", TraceabilityTraversalDirection.Outgoing)]),
            path => AssertPath(
                path,
                ["ADR-001", "PLN-001"],
                [new TraceabilityPathSegment("PLN-001", ArtifactRelationshipKind.DependsOn, "ADR-001", TraceabilityTraversalDirection.Incoming)]),
            path => AssertPath(
                path,
                ["ADR-001", "CHR-001"],
                [new TraceabilityPathSegment("CHR-001", ArtifactRelationshipKind.Affects, "ADR-001", TraceabilityTraversalDirection.Incoming)]));
    }

    [Fact]
    public void Query_Dependency_ReturnsDeterministicTransitivePaths()
    {
        var workspace = CreateWorkspace("alpha-workspace", "alpha", "Alpha");
        SeedTraceabilityWorkspace(workspace);

        using var connection = CreateConnection();
        var rebuildResult = _rebuilder.Rebuild(connection, _workspacesRootPath);
        Assert.True(rebuildResult.Success);

        var result = _service.Query(connection, new TraceabilityQuery("alpha", "PLN-001", TraceabilityQueryKind.Dependency));

        Assert.True(result.Success);
        Assert.Collection(
            result.Paths,
            path => AssertPath(
                path,
                ["PLN-001", "ADR-001"],
                [new TraceabilityPathSegment("PLN-001", ArtifactRelationshipKind.DependsOn, "ADR-001", TraceabilityTraversalDirection.Outgoing)]),
            path => AssertPath(
                path,
                ["PLN-001", "ADR-001", "CHR-001"],
                [
                    new TraceabilityPathSegment("PLN-001", ArtifactRelationshipKind.DependsOn, "ADR-001", TraceabilityTraversalDirection.Outgoing),
                    new TraceabilityPathSegment("ADR-001", ArtifactRelationshipKind.DependsOn, "CHR-001", TraceabilityTraversalDirection.Outgoing)
                ]));
    }

    [Fact]
    public void Query_Impact_ReturnsExplicitReversePaths()
    {
        var workspace = CreateWorkspace("alpha-workspace", "alpha", "Alpha");
        SeedTraceabilityWorkspace(workspace);

        using var connection = CreateConnection();
        var rebuildResult = _rebuilder.Rebuild(connection, _workspacesRootPath);
        Assert.True(rebuildResult.Success);

        var result = _service.Query(connection, new TraceabilityQuery("alpha", "ADR-001", TraceabilityQueryKind.Impact));

        Assert.True(result.Success);
        Assert.Collection(
            result.Paths,
            path => AssertPath(
                path,
                ["ADR-001", "CNS-001"],
                [new TraceabilityPathSegment("ADR-001", ArtifactRelationshipKind.Affects, "CNS-001", TraceabilityTraversalDirection.Outgoing)]),
            path => AssertPath(
                path,
                ["ADR-001", "PLN-001"],
                [new TraceabilityPathSegment("PLN-001", ArtifactRelationshipKind.DependsOn, "ADR-001", TraceabilityTraversalDirection.Incoming)]));
    }

    [Fact]
    public void Query_InvalidRequest_ReturnsClearValidationErrors()
    {
        using var connection = CreateConnection();

        var missingFields = _service.Query(connection, new TraceabilityQuery("", "", TraceabilityQueryKind.Direct));

        Assert.False(missingFields.Success);
        Assert.Equal(
            [
                new TraceabilityQueryError("traceability.query.project_id.required", "Project id is required.", "projectId"),
                new TraceabilityQueryError("traceability.query.artifact_id.required", "Artifact id is required.", "artifactId")
            ],
            missingFields.Errors);

        var workspace = CreateWorkspace("alpha-workspace", "alpha", "Alpha");
        SeedTraceabilityWorkspace(workspace);
        var rebuildResult = _rebuilder.Rebuild(connection, _workspacesRootPath);
        Assert.True(rebuildResult.Success);

        var missingArtifact = _service.Query(connection, new TraceabilityQuery("alpha", "ADR-999", TraceabilityQueryKind.Direct));

        Assert.False(missingArtifact.Success);
        Assert.Equal(
            [new TraceabilityQueryError(
                "traceability.query.artifact.not_found",
                "Approved artifact 'ADR-999' was not found in project 'alpha'.",
                "artifactId")],
            missingArtifact.Errors);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspacesRootPath))
        {
            Directory.Delete(_workspacesRootPath, recursive: true);
        }
    }

    private ProjectWorkspace CreateWorkspace(string directoryName, string projectId, string name)
    {
        var workspaceRootPath = Path.Combine(_workspacesRootPath, directoryName);
        Directory.CreateDirectory(workspaceRootPath);
        File.WriteAllText(
            Path.Combine(workspaceRootPath, "project.json"),
            $$"""
              {
                "projectId": "{{projectId}}",
                "name": "{{name}}",
                "status": "active"
              }
              """);
        return new ProjectWorkspace(new ProjectMetadata(projectId, name, "active"), workspaceRootPath);
    }

    private void SeedTraceabilityWorkspace(ProjectWorkspace workspace)
    {
        _fileStore.Save(workspace, CreateCharterArtifact(workspace.ProjectId));
        _fileStore.Save(workspace, CreateDecisionArtifact(workspace.ProjectId));
        _fileStore.Save(workspace, CreateConstraintArtifact(workspace.ProjectId));
        _fileStore.Save(workspace, CreatePlanArtifact(workspace.ProjectId));
    }

    private static SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static ProjectCharterArtifact CreateCharterArtifact(string projectId) =>
        new(
            "CHR-001",
            projectId,
            ArtifactStatus.Approved,
            "Charter",
            new DateTimeOffset(2026, 4, 20, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 20, 9, 5, 0, TimeSpan.Zero),
            1,
            ["charter"],
            "user",
            "fixture",
            new ArtifactLinks([], ["ADR-001"], [], []),
            """
            ## Problem Statement
            Need deterministic traceability.

            ## Primary Users / Stakeholders
            Engineers.

            ## Current Pain
            Relationship paths are opaque.

            ## Desired Outcome
            Queries stay explicit.

            ## Definition of Success
            Deterministic traceability paths are returned.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Problem Statement"] = "Need deterministic traceability.",
                ["Primary Users / Stakeholders"] = "Engineers.",
                ["Current Pain"] = "Relationship paths are opaque.",
                ["Desired Outcome"] = "Queries stay explicit.",
                ["Definition of Success"] = "Deterministic traceability paths are returned."
            });

    private static ArchitectureDecisionArtifact CreateDecisionArtifact(string projectId) =>
        new(
            "ADR-001",
            projectId,
            ArtifactStatus.Approved,
            "Decision",
            new DateTimeOffset(2026, 4, 20, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 20, 10, 5, 0, TimeSpan.Zero),
            1,
            ["decision"],
            "user",
            "fixture",
            new ArtifactLinks(["CHR-001"], ["CNS-001"], [], []),
            """
            ## Context
            Traceability depends on stable links.

            ## Decision
            Keep relationship traversal deterministic.

            ## Alternatives Considered
            Ad hoc graph exploration.

            ## Consequences
            Results are explainable.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Traceability depends on stable links.",
                ["Decision"] = "Keep relationship traversal deterministic.",
                ["Alternatives Considered"] = "Ad hoc graph exploration.",
                ["Consequences"] = "Results are explainable."
            },
            "2026-04-20");

    private static ConstraintArtifact CreateConstraintArtifact(string projectId) =>
        new(
            "CNS-001",
            projectId,
            ArtifactStatus.Approved,
            "Constraint",
            new DateTimeOffset(2026, 4, 20, 11, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 20, 11, 5, 0, TimeSpan.Zero),
            1,
            ["constraint"],
            "user",
            "fixture",
            ArtifactLinks.Empty,
            """
            ## Constraint
            Keep traceability deterministic.

            ## Why It Exists
            Operators need explainable paths.

            ## Implications
            Traversal behavior must be stable.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Constraint"] = "Keep traceability deterministic.",
                ["Why It Exists"] = "Operators need explainable paths.",
                ["Implications"] = "Traversal behavior must be stable."
            },
            ConstraintKind.Workflow,
            ConstraintSeverity.Normal);

    private static PlanArtifact CreatePlanArtifact(string projectId) =>
        new(
            "PLN-001",
            projectId,
            ArtifactStatus.Approved,
            "Plan",
            new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 20, 12, 5, 0, TimeSpan.Zero),
            1,
            ["plan"],
            "user",
            "fixture",
            new ArtifactLinks(["ADR-001"], [], [], []),
            """
            ## Goal
            Provide deterministic traceability.

            ## Scope
            Traverse approved relationships only.

            ## Acceptance Criteria
            - dependency paths are explicit

            ## Notes
            Keep the model small.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Goal"] = "Provide deterministic traceability.",
                ["Scope"] = "Traverse approved relationships only.",
                ["Acceptance Criteria"] = "- dependency paths are explicit",
                ["Notes"] = "Keep the model small."
            },
            ArtifactPriority.Normal,
            true);

    private static void AssertPath(
        TraceabilityPath path,
        IReadOnlyList<string> expectedArtifactIds,
        IReadOnlyList<TraceabilityPathSegment> expectedSegments)
    {
        Assert.Equal(expectedArtifactIds, path.ArtifactIds);
        Assert.Equal(expectedSegments.Count, path.Segments.Count);

        for (var index = 0; index < expectedSegments.Count; index++)
        {
            Assert.Equal(expectedSegments[index], path.Segments[index]);
        }
    }
}
