using Microsoft.Data.Sqlite;
using Memora.Core.Artifacts;
using Memora.Index.Schema;
using Memora.Storage.Parsing;
using Memora.Storage.Workspaces;

namespace Memora.Index.Rebuild;

public sealed class SqliteIndexRebuilder
{
    private readonly WorkspaceDiscovery _workspaceDiscovery = new();
    private readonly ArtifactMarkdownParser _markdownParser = new();
    private readonly SqliteIndexSchema _schema = new();

    public IndexRebuildResult Rebuild(SqliteConnection connection, string workspacesRootPath)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacesRootPath);

        _schema.EnsureCreated(connection);

        var diagnostics = new List<IndexRebuildDiagnostic>();
        var workspaces = _workspaceDiscovery.Discover(workspacesRootPath);
        var projectRows = workspaces
            .Select(workspace => new ProjectRow(
                workspace.ProjectId,
                workspace.Metadata.Name,
                workspace.Metadata.Status,
                workspace.RootPath,
                workspace.ProjectMetadataPath))
            .ToList();

        var parsedArtifacts = new List<ParsedArtifactRecord>();
        var revisionKeys = new HashSet<(string ProjectId, string ArtifactId, int Revision)>();
        var artifactFileCount = 0;

        foreach (var workspace in workspaces)
        {
            foreach (var filePath in EnumerateArtifactFiles(workspace))
            {
                artifactFileCount++;
                var markdown = File.ReadAllText(filePath);
                var parseResult = _markdownParser.Parse(markdown);

                if (!parseResult.Validation.IsValid || parseResult.Artifact is null)
                {
                    foreach (var issue in parseResult.Validation.Issues)
                    {
                        diagnostics.Add(new IndexRebuildDiagnostic(filePath, issue.Code, issue.Message, issue.Path ?? string.Empty));
                    }

                    continue;
                }

                var artifact = parseResult.Artifact;
                if (!string.Equals(artifact.ProjectId, workspace.ProjectId, StringComparison.Ordinal))
                {
                    diagnostics.Add(new IndexRebuildDiagnostic(
                        filePath,
                        "index.rebuild.project_id.mismatch",
                        $"Artifact project '{artifact.ProjectId}' does not match workspace project '{workspace.ProjectId}'.",
                        "project_id"));
                    continue;
                }

                var revisionKey = (artifact.ProjectId, artifact.Id, artifact.Revision);
                if (!revisionKeys.Add(revisionKey))
                {
                    diagnostics.Add(new IndexRebuildDiagnostic(
                        filePath,
                        "index.rebuild.revision.duplicate",
                        $"Duplicate artifact revision '{artifact.Id}' revision {artifact.Revision} was found for project '{artifact.ProjectId}'.",
                        "revision"));
                    continue;
                }

                var location = ClassifyLocation(workspace, filePath);
                parsedArtifacts.Add(new ParsedArtifactRecord(
                    artifact,
                    Path.GetFullPath(filePath),
                    location == ArtifactLocation.Canonical));
            }
        }

        var revisionRows = parsedArtifacts
            .Select(record => new ArtifactRevisionRow(
                record.Artifact.ProjectId,
                record.Artifact.Id,
                record.Artifact.Revision,
                record.Artifact.Type.ToSchemaValue(),
                record.Artifact.Status.ToSchemaValue(),
                record.Artifact.Title,
                record.FilePath,
                record.Artifact.CreatedAtUtc.ToString("O"),
                record.Artifact.UpdatedAtUtc.ToString("O"),
                record.IsCanonical))
            .ToList();

        var approvedArtifactIds = parsedArtifacts
            .Where(record => record.IsCanonical && record.Artifact.Status == ArtifactStatus.Approved)
            .Select(record => (record.Artifact.ProjectId, record.Artifact.Id))
            .ToHashSet();

        var relationshipRows = new List<ArtifactRelationshipRow>();
        foreach (var record in parsedArtifacts.Where(record => record.IsCanonical && record.Artifact.Status == ArtifactStatus.Approved))
        {
            foreach (var relationship in record.Artifact.Links.Relationships)
            {
                if (!approvedArtifactIds.Contains((record.Artifact.ProjectId, relationship.TargetArtifactId)))
                {
                    diagnostics.Add(new IndexRebuildDiagnostic(
                        record.FilePath,
                        "index.relationship.target.invalid",
                        $"Approved artifact '{record.Artifact.Id}' references missing approved target '{relationship.TargetArtifactId}'.",
                        $"links.{ArtifactLinks.ToFrontmatterKey(relationship.Kind)}"));
                    continue;
                }

                relationshipRows.Add(new ArtifactRelationshipRow(
                    record.Artifact.ProjectId,
                    record.Artifact.Id,
                    record.Artifact.Revision,
                    ArtifactLinks.ToFrontmatterKey(relationship.Kind),
                    relationship.TargetArtifactId));
            }
        }

        var artifactRows = revisionRows
            .GroupBy(row => (row.ProjectId, row.ArtifactId))
            .Select(group =>
            {
                var latest = group
                    .OrderByDescending(row => row.Revision)
                    .ThenByDescending(row => row.UpdatedAtUtc, StringComparer.Ordinal)
                    .First();

                return new ArtifactRow(
                    latest.ProjectId,
                    latest.ArtifactId,
                    latest.ArtifactType,
                    latest.ArtifactStatus,
                    latest.Revision,
                    latest.Title,
                    latest.UpdatedAtUtc);
            })
            .ToList();

        var status = diagnostics.Count == 0
            ? IndexRebuildStatus.Succeeded
            : IndexRebuildStatus.Failed;

        ReplaceIndexContents(connection, projectRows, artifactRows, revisionRows, relationshipRows, status == IndexRebuildStatus.Succeeded);

        return status == IndexRebuildStatus.Succeeded
            ? new IndexRebuildResult(projectRows.Count, artifactRows.Count, revisionRows.Count, relationshipRows.Count, diagnostics, projectRows.Count, artifactFileCount, status)
            : new IndexRebuildResult(0, 0, 0, 0, diagnostics, projectRows.Count, artifactFileCount, status);
    }

    private static IReadOnlyList<string> EnumerateArtifactFiles(ProjectWorkspace workspace)
    {
        var files = new List<string>();
        AddMarkdownFiles(files, workspace.CanonicalRootPath);
        AddMarkdownFiles(files, workspace.DraftsRootPath);
        AddMarkdownFiles(files, workspace.SummariesRootPath);
        files.Sort(StringComparer.Ordinal);
        return files;
    }

    private static void AddMarkdownFiles(ICollection<string> files, string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(rootPath, "*.md", SearchOption.AllDirectories))
        {
            files.Add(filePath);
        }
    }

    private static ArtifactLocation ClassifyLocation(ProjectWorkspace workspace, string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);

        if (IsUnderRoot(fullPath, workspace.CanonicalRootPath))
        {
            return ArtifactLocation.Canonical;
        }

        if (IsUnderRoot(fullPath, workspace.DraftsRootPath))
        {
            return ArtifactLocation.Draft;
        }

        return ArtifactLocation.Summary;
    }

    private static bool IsUnderRoot(string fullPath, string rootPath)
    {
        var normalizedRootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath)) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(normalizedRootPath, StringComparison.OrdinalIgnoreCase);
    }

    private static void ReplaceIndexContents(
        SqliteConnection connection,
        IReadOnlyList<ProjectRow> projectRows,
        IReadOnlyList<ArtifactRow> artifactRows,
        IReadOnlyList<ArtifactRevisionRow> revisionRows,
        IReadOnlyList<ArtifactRelationshipRow> relationshipRows,
        bool commitRows)
    {
        using var transaction = connection.BeginTransaction();

        ExecuteNonQuery(connection, transaction, "DELETE FROM artifact_relationships;");
        ExecuteNonQuery(connection, transaction, "DELETE FROM artifact_revisions;");
        ExecuteNonQuery(connection, transaction, "DELETE FROM artifacts;");
        ExecuteNonQuery(connection, transaction, "DELETE FROM projects;");

        if (commitRows)
        {
            InsertProjects(connection, transaction, projectRows);
            InsertArtifacts(connection, transaction, artifactRows);
            InsertArtifactRevisions(connection, transaction, revisionRows);
            InsertArtifactRelationships(connection, transaction, relationshipRows);

            transaction.Commit();
            return;
        }

        transaction.Rollback();
    }

    private static void InsertProjects(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<ProjectRow> rows)
    {
        foreach (var row in rows)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO projects (project_id, name, status, workspace_root_path, project_metadata_path)
                VALUES ($project_id, $name, $status, $workspace_root_path, $project_metadata_path);
                """;
            command.Parameters.AddWithValue("$project_id", row.ProjectId);
            command.Parameters.AddWithValue("$name", row.Name);
            command.Parameters.AddWithValue("$status", (object?)row.Status ?? DBNull.Value);
            command.Parameters.AddWithValue("$workspace_root_path", row.WorkspaceRootPath);
            command.Parameters.AddWithValue("$project_metadata_path", row.ProjectMetadataPath);
            command.ExecuteNonQuery();
        }
    }

    private static void InsertArtifacts(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<ArtifactRow> rows)
    {
        foreach (var row in rows)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO artifacts (project_id, artifact_id, artifact_type, latest_status, latest_revision, title, latest_updated_at_utc)
                VALUES ($project_id, $artifact_id, $artifact_type, $latest_status, $latest_revision, $title, $latest_updated_at_utc);
                """;
            command.Parameters.AddWithValue("$project_id", row.ProjectId);
            command.Parameters.AddWithValue("$artifact_id", row.ArtifactId);
            command.Parameters.AddWithValue("$artifact_type", row.ArtifactType);
            command.Parameters.AddWithValue("$latest_status", row.LatestStatus);
            command.Parameters.AddWithValue("$latest_revision", row.LatestRevision);
            command.Parameters.AddWithValue("$title", row.Title);
            command.Parameters.AddWithValue("$latest_updated_at_utc", row.LatestUpdatedAtUtc);
            command.ExecuteNonQuery();
        }
    }

    private static void InsertArtifactRevisions(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<ArtifactRevisionRow> rows)
    {
        foreach (var row in rows)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO artifact_revisions (project_id, artifact_id, revision, artifact_status, title, file_path, created_at_utc, updated_at_utc, is_canonical)
                VALUES ($project_id, $artifact_id, $revision, $artifact_status, $title, $file_path, $created_at_utc, $updated_at_utc, $is_canonical);
                """;
            command.Parameters.AddWithValue("$project_id", row.ProjectId);
            command.Parameters.AddWithValue("$artifact_id", row.ArtifactId);
            command.Parameters.AddWithValue("$revision", row.Revision);
            command.Parameters.AddWithValue("$artifact_status", row.ArtifactStatus);
            command.Parameters.AddWithValue("$title", row.Title);
            command.Parameters.AddWithValue("$file_path", row.FilePath);
            command.Parameters.AddWithValue("$created_at_utc", row.CreatedAtUtc);
            command.Parameters.AddWithValue("$updated_at_utc", row.UpdatedAtUtc);
            command.Parameters.AddWithValue("$is_canonical", row.IsCanonical ? 1 : 0);
            command.ExecuteNonQuery();
        }
    }

    private static void InsertArtifactRelationships(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<ArtifactRelationshipRow> rows)
    {
        foreach (var row in rows)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO artifact_relationships (project_id, source_artifact_id, source_revision, relationship_kind, target_artifact_id)
                VALUES ($project_id, $source_artifact_id, $source_revision, $relationship_kind, $target_artifact_id);
                """;
            command.Parameters.AddWithValue("$project_id", row.ProjectId);
            command.Parameters.AddWithValue("$source_artifact_id", row.SourceArtifactId);
            command.Parameters.AddWithValue("$source_revision", row.SourceRevision);
            command.Parameters.AddWithValue("$relationship_kind", row.RelationshipKind);
            command.Parameters.AddWithValue("$target_artifact_id", row.TargetArtifactId);
            command.ExecuteNonQuery();
        }
    }

    private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction transaction, string commandText)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }

    private sealed record ProjectRow(
        string ProjectId,
        string Name,
        string? Status,
        string WorkspaceRootPath,
        string ProjectMetadataPath);

    private sealed record ArtifactRow(
        string ProjectId,
        string ArtifactId,
        string ArtifactType,
        string LatestStatus,
        int LatestRevision,
        string Title,
        string LatestUpdatedAtUtc);

    private sealed record ArtifactRevisionRow(
        string ProjectId,
        string ArtifactId,
        int Revision,
        string ArtifactType,
        string ArtifactStatus,
        string Title,
        string FilePath,
        string CreatedAtUtc,
        string UpdatedAtUtc,
        bool IsCanonical);

    private sealed record ArtifactRelationshipRow(
        string ProjectId,
        string SourceArtifactId,
        int SourceRevision,
        string RelationshipKind,
        string TargetArtifactId);

    private sealed record ParsedArtifactRecord(
        ArtifactDocument Artifact,
        string FilePath,
        bool IsCanonical);

    private enum ArtifactLocation
    {
        Canonical,
        Draft,
        Summary
    }
}
