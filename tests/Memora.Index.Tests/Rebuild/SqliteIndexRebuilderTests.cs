using Microsoft.Data.Sqlite;
using Memora.Core.Artifacts;
using Memora.Core.Projects;
using Memora.Index.Relationships;
using Memora.Index.Rebuild;
using Memora.Storage.Persistence;
using Memora.Storage.Workspaces;

namespace Memora.Index.Tests.Rebuild;

public sealed class SqliteIndexRebuilderTests : IDisposable
{
    private readonly string _workspacesRootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-index-rebuild-tests",
        Guid.NewGuid().ToString("N"));

    private readonly SqliteIndexRebuilder _rebuilder = new();
    private readonly ArtifactRelationshipIndex _relationshipIndex = new();
    private readonly ArtifactFileStore _fileStore = new();

    [Fact]
    public void Rebuild_PopulatesDerivedIndexFromWorkspaceFiles()
    {
        var alphaWorkspace = CreateWorkspace("alpha-workspace", "alpha", "Alpha");
        var betaWorkspace = CreateWorkspace("beta-workspace", "beta", "Beta");

        _fileStore.Save(alphaWorkspace, CreateCharterArtifact("alpha", revision: 1));
        _fileStore.Save(alphaWorkspace, CreatePlanArtifact("alpha", ArtifactStatus.Approved, revision: 1));
        _fileStore.Save(alphaWorkspace, CreatePlanArtifact("alpha", ArtifactStatus.Draft, revision: 2));
        _fileStore.Save(alphaWorkspace, CreateDecisionArtifact("alpha", revision: 1));
        _fileStore.Save(alphaWorkspace, CreateSessionSummaryArtifact("alpha", revision: 1));
        _fileStore.Save(betaWorkspace, CreateCharterArtifact("beta", revision: 1));

        using var connection = CreateConnection();

        var result = _rebuilder.Rebuild(connection, _workspacesRootPath);

        Assert.True(result.Success);
        Assert.Equal(IndexRebuildStatus.Succeeded, result.Status);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.ProjectCount);
        Assert.Equal(5, result.ArtifactCount);
        Assert.Equal(6, result.RevisionCount);
        Assert.Equal(2, result.RelationshipCount);
        Assert.Equal(2, result.FilesystemProjectCount);
        Assert.Equal(6, result.FilesystemArtifactFileCount);
        Assert.Contains("Rebuilt derived SQLite index from filesystem truth", result.Summary, StringComparison.Ordinal);
        Assert.Equal(2L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM projects;"));
        Assert.Equal(5L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM artifacts;"));
        Assert.Equal(6L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM artifact_revisions;"));
        Assert.Equal(2L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM artifact_relationships;"));
        Assert.Equal(
            "draft|2",
            ExecuteScalar<string>(
                connection,
                "SELECT latest_status || '|' || latest_revision FROM artifacts WHERE project_id = 'alpha' AND artifact_id = 'PLN-001';"));
        Assert.Equal(
            1L,
            ExecuteScalar<long>(
                connection,
                "SELECT is_canonical FROM artifact_revisions WHERE project_id = 'alpha' AND artifact_id = 'PLN-001' AND revision = 1;"));
        Assert.Equal(
            0L,
            ExecuteScalar<long>(
                connection,
                "SELECT is_canonical FROM artifact_revisions WHERE project_id = 'alpha' AND artifact_id = 'PLN-001' AND revision = 2;"));

        var outgoing = _relationshipIndex.GetOutgoingRelationships(connection, "alpha", "PLN-001");
        Assert.Equal(
            [
                new PersistedArtifactRelationship("alpha", "PLN-001", 1, ArtifactRelationshipKind.Affects, "ADR-001"),
                new PersistedArtifactRelationship("alpha", "PLN-001", 1, ArtifactRelationshipKind.DependsOn, "CHR-001")
            ],
            outgoing);

        var incoming = _relationshipIndex.GetIncomingRelationships(connection, "alpha", "CHR-001");
        Assert.Equal(
            [new PersistedArtifactRelationship("alpha", "PLN-001", 1, ArtifactRelationshipKind.DependsOn, "CHR-001")],
            incoming);
    }

    [Fact]
    public void Rebuild_ReplacesExistingIndexContents()
    {
        var workspace = CreateWorkspace("alpha-workspace", "alpha", "Alpha");
        _fileStore.Save(workspace, CreateDecisionArtifact("alpha", revision: 1));

        using var connection = CreateConnection();
        ExecuteNonQuery(
            connection,
            """
            CREATE TABLE IF NOT EXISTS schema_info (singleton_id INTEGER PRIMARY KEY, schema_version INTEGER NOT NULL);
            INSERT OR REPLACE INTO schema_info (singleton_id, schema_version) VALUES (1, 1);
            CREATE TABLE IF NOT EXISTS projects (project_id TEXT PRIMARY KEY, name TEXT NOT NULL, status TEXT NULL, workspace_root_path TEXT NOT NULL, project_metadata_path TEXT NOT NULL);
            INSERT INTO projects (project_id, name, status, workspace_root_path, project_metadata_path)
            VALUES ('stale', 'Stale', NULL, 'C:\stale', 'C:\stale\project.json');
            """);

        var result = _rebuilder.Rebuild(connection, _workspacesRootPath);

        Assert.True(result.Success);
        Assert.Equal(IndexRebuildStatus.Succeeded, result.Status);
        Assert.Equal(1L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM projects;"));
        Assert.Equal(0L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM projects WHERE project_id = 'stale';"));
    }

    [Fact]
    public void Rebuild_InvalidArtifact_DiagnosticsPreserveRows()
    {
        var workspace = CreateWorkspace("alpha-workspace", "alpha", "Alpha");
        _fileStore.Save(workspace, CreateDecisionArtifact("alpha", revision: 1));

        using var connection = CreateConnection();
        var firstResult = _rebuilder.Rebuild(connection, _workspacesRootPath);
        Assert.True(firstResult.Success);

        Directory.CreateDirectory(Path.Combine(workspace.DraftsRootPath, "plan"));
        File.WriteAllText(
            Path.Combine(workspace.DraftsRootPath, "plan", "broken.r0001.md"),
            """
            ---
            id PLN-999
            type: plan
            ---
            ## Goal
            broken
            """);

        var secondResult = _rebuilder.Rebuild(connection, _workspacesRootPath);

        Assert.False(secondResult.Success);
        Assert.Equal(IndexRebuildStatus.Failed, secondResult.Status);
        Assert.NotEmpty(secondResult.Diagnostics);
        var diagnostic = Assert.Single(secondResult.Diagnostics, diagnostic => diagnostic.Code == "frontmatter.parse");
        Assert.Contains("source: filesystem truth", diagnostic.DiagnosticMessage, StringComparison.Ordinal);
        Assert.Contains("index: derived SQLite index", diagnostic.DiagnosticMessage, StringComparison.Ordinal);
        Assert.Contains("broken.r0001.md", diagnostic.DiagnosticMessage, StringComparison.Ordinal);
        Assert.Equal(1, secondResult.FilesystemProjectCount);
        Assert.Equal(2, secondResult.FilesystemArtifactFileCount);
        Assert.Contains("existing derived SQLite index rows were preserved", secondResult.Summary, StringComparison.Ordinal);
        Assert.Equal(1L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM projects;"));
        Assert.Equal(1L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM artifacts;"));
        Assert.Equal(1L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM artifact_revisions;"));
        Assert.Equal(0L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM artifact_relationships;"));
    }

    [Fact]
    public void Rebuild_InvalidRelationship_DiagnosticsPreserveRows()
    {
        var workspace = CreateWorkspace("alpha-workspace", "alpha", "Alpha");
        _fileStore.Save(workspace, CreateDecisionArtifact("alpha", revision: 1));

        using var connection = CreateConnection();
        var firstResult = _rebuilder.Rebuild(connection, _workspacesRootPath);
        Assert.True(firstResult.Success);

        _fileStore.Save(workspace, CreatePlanArtifact("alpha", ArtifactStatus.Approved, revision: 1) with
        {
            Links = new ArtifactLinks(["CHR-999"], [], [], [])
        });

        var result = _rebuilder.Rebuild(connection, _workspacesRootPath);

        Assert.False(result.Success);
        Assert.Equal(IndexRebuildStatus.Failed, result.Status);
        var diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == "index.relationship.target.invalid");
        Assert.Contains("Approved artifact 'PLN-001' references missing approved target 'CHR-999'.", diagnostic.DiagnosticMessage, StringComparison.Ordinal);
        Assert.Contains("source: filesystem truth", diagnostic.DiagnosticMessage, StringComparison.Ordinal);
        Assert.Contains("index: derived SQLite index", diagnostic.DiagnosticMessage, StringComparison.Ordinal);
        Assert.Equal(1L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM projects;"));
        Assert.Equal(1L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM artifacts;"));
        Assert.Equal(1L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM artifact_revisions;"));
        Assert.Equal(0L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM artifact_relationships;"));
    }

    [Fact]
    public void Rebuild_BadArtifact_PreservesExistingIndex()
    {
        var alphaWorkspace = CreateWorkspace("alpha-workspace", "alpha", "Alpha");
        var betaWorkspace = CreateWorkspace("beta-workspace", "beta", "Beta");
        _fileStore.Save(alphaWorkspace, CreateDecisionArtifact("alpha", revision: 1));
        _fileStore.Save(betaWorkspace, CreateDecisionArtifact("beta", revision: 1));

        using var connection = CreateConnection();
        var firstResult = _rebuilder.Rebuild(connection, _workspacesRootPath);
        Assert.True(firstResult.Success);
        Assert.Equal(2L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM projects;"));
        Assert.Equal(2L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM artifacts;"));

        Directory.CreateDirectory(Path.Combine(betaWorkspace.DraftsRootPath, "plan"));
        File.WriteAllText(
            Path.Combine(betaWorkspace.DraftsRootPath, "plan", "broken.r0001.md"),
            """
            ---
            id PLN-999
            type: plan
            ---
            ## Goal
            broken
            """);

        var secondResult = _rebuilder.Rebuild(connection, _workspacesRootPath);

        Assert.False(secondResult.Success);
        Assert.Equal(IndexRebuildStatus.Failed, secondResult.Status);
        Assert.Contains(secondResult.Diagnostics, diagnostic => diagnostic.Code == "frontmatter.parse");
        Assert.Equal(2, secondResult.FilesystemProjectCount);
        Assert.Equal(3, secondResult.FilesystemArtifactFileCount);
        Assert.Equal(2L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM projects;"));
        Assert.Equal(2L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM artifacts;"));
        Assert.Equal(2L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM artifact_revisions;"));
        Assert.Equal(1L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM artifacts WHERE project_id = 'alpha';"));
        Assert.Equal(1L, ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM artifacts WHERE project_id = 'beta';"));
    }

    [Fact]
    public void Rebuild_FromSampleWorkspaceFixture_PopulatesExpectedRows()
    {
        using var connection = CreateConnection();
        var sampleWorkspacesRootPath = GetSampleWorkspacesRootPath();
        var artifactFileCount = Directory
            .EnumerateFiles(Path.Combine(sampleWorkspacesRootPath, "demo-project"), "*.md", SearchOption.AllDirectories)
            .Count();

        var result = _rebuilder.Rebuild(connection, sampleWorkspacesRootPath);

        Assert.True(result.Success);
        Assert.Equal(IndexRebuildStatus.Succeeded, result.Status);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(1, result.ProjectCount);
        Assert.Equal(artifactFileCount, result.RevisionCount);
        Assert.Equal(artifactFileCount, result.FilesystemArtifactFileCount);
        Assert.Equal("demo-project", ExecuteScalar<string>(connection, "SELECT project_id FROM projects LIMIT 1;"));
        Assert.Equal(
            artifactFileCount,
            ExecuteScalar<long>(connection, "SELECT COUNT(*) FROM artifact_revisions;"));
        Assert.Equal(
            0L,
            ExecuteScalar<long>(
                connection,
                """
                SELECT COUNT(*)
                FROM artifacts a
                LEFT JOIN artifact_revisions r
                    ON r.project_id = a.project_id
                    AND r.artifact_id = a.artifact_id
                WHERE r.artifact_id IS NULL;
                """));
        Assert.Equal(
            ExecuteScalar<long>(
                connection,
                """
                SELECT COUNT(*)
                FROM (
                    SELECT project_id, artifact_id
                    FROM artifact_revisions
                    WHERE artifact_status = 'approved'
                    GROUP BY project_id, artifact_id
                );
                """),
            ExecuteScalar<long>(
                connection,
                """
                SELECT COUNT(*)
                FROM artifact_revisions
                WHERE artifact_status = 'approved'
                    AND is_canonical = 1;
                """));
        Assert.Equal(
            0L,
            ExecuteScalar<long>(
                connection,
                """
                SELECT COUNT(*)
                FROM (
                    SELECT project_id, artifact_id
                    FROM artifact_revisions
                    WHERE is_canonical = 1
                    GROUP BY project_id, artifact_id
                    HAVING COUNT(*) > 1
                );
                """));
        Assert.Equal(
            0L,
            ExecuteScalar<long>(
                connection,
                """
                SELECT COUNT(*)
                FROM artifact_relationships rel
                LEFT JOIN artifact_revisions source_revision
                    ON source_revision.project_id = rel.project_id
                    AND source_revision.artifact_id = rel.source_artifact_id
                    AND source_revision.revision = rel.source_revision
                LEFT JOIN artifacts target_artifact
                    ON target_artifact.project_id = rel.project_id
                    AND target_artifact.artifact_id = rel.target_artifact_id
                WHERE source_revision.artifact_id IS NULL
                    OR target_artifact.artifact_id IS NULL;
                """));
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

    private static SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static T ExecuteScalar<T>(SqliteConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return (T)command.ExecuteScalar()!;
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }

    private static string GetSampleWorkspacesRootPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Memora.sln")))
            {
                return Path.Combine(current.FullName, "samples", "workspaces");
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing Memora.sln.");
    }

    private static PlanArtifact CreatePlanArtifact(string projectId, ArtifactStatus status, int revision) =>
        new(
            "PLN-001",
            projectId,
            status,
            "Index rebuild plan",
            new DateTimeOffset(2026, 4, 14, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 14, 12, revision, 0, TimeSpan.Zero),
            revision,
            ["index"],
            "user",
            "rebuild validation",
            new ArtifactLinks(["CHR-001"], ["ADR-001"], [], []),
            """
            ## Goal
            Rebuild the index.

            ## Scope
            Index workspace files only.

            ## Acceptance Criteria
            - rebuild parses files
            - rebuild writes sqlite rows

            ## Notes
            Keep the process deterministic.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Goal"] = "Rebuild the index.",
                ["Scope"] = "Index workspace files only.",
                ["Acceptance Criteria"] = "- rebuild parses files\n- rebuild writes sqlite rows",
                ["Notes"] = "Keep the process deterministic."
            },
            ArtifactPriority.Normal,
            true);

    private static ArchitectureDecisionArtifact CreateDecisionArtifact(string projectId, int revision) =>
        new(
            "ADR-001",
            projectId,
            ArtifactStatus.Approved,
            "Rebuild from files",
            new DateTimeOffset(2026, 4, 14, 13, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 14, 13, 5, 0, TimeSpan.Zero),
            revision,
            ["index"],
            "user",
            "sqlite rebuild",
            ArtifactLinks.Empty,
            """
            ## Context
            The index must be rebuildable.

            ## Decision
            Rebuild from filesystem truth.

            ## Alternatives Considered
            Manual repair.

            ## Consequences
            Rebuild remains deterministic.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "The index must be rebuildable.",
                ["Decision"] = "Rebuild from filesystem truth.",
                ["Alternatives Considered"] = "Manual repair.",
                ["Consequences"] = "Rebuild remains deterministic."
            },
            "2026-04-14");

    private static SessionSummaryArtifact CreateSessionSummaryArtifact(string projectId, int revision) =>
        new(
            "SUM-001",
            projectId,
            ArtifactStatus.Draft,
            "Rebuild session",
            new DateTimeOffset(2026, 4, 14, 14, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 14, 14, 10, 0, TimeSpan.Zero),
            revision,
            ["summary"],
            "agent",
            "session summary",
            ArtifactLinks.Empty,
            """
            ## Summary
            Rebuild work completed.

            ## Artifacts Created
            - ADR-001

            ## Artifacts Updated
            - PLN-001

            ## Open Threads
            - verify diagnostics
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Summary"] = "Rebuild work completed.",
                ["Artifacts Created"] = "- ADR-001",
                ["Artifacts Updated"] = "- PLN-001",
                ["Open Threads"] = "- verify diagnostics"
            },
            SessionType.Execution,
            false);

    private static ProjectCharterArtifact CreateCharterArtifact(string projectId, int revision) =>
        new(
            "CHR-001",
            projectId,
            ArtifactStatus.Approved,
            "Beta charter",
            new DateTimeOffset(2026, 4, 14, 11, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 14, 11, 5, 0, TimeSpan.Zero),
            revision,
            ["charter"],
            "user",
            "workspace seed",
            ArtifactLinks.Empty,
            """
            ## Problem Statement
            Durable project context is needed.

            ## Primary Users / Stakeholders
            Engineers.

            ## Current Pain
            Context drifts between sessions.

            ## Desired Outcome
            Shared truth stays structured.

            ## Definition of Success
            Files rebuild the index deterministically.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Problem Statement"] = "Durable project context is needed.",
                ["Primary Users / Stakeholders"] = "Engineers.",
                ["Current Pain"] = "Context drifts between sessions.",
                ["Desired Outcome"] = "Shared truth stays structured.",
                ["Definition of Success"] = "Files rebuild the index deterministically."
            });
}
