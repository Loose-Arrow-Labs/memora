using Microsoft.Data.Sqlite;
using Memora.Index.Schema;

namespace Memora.Index.Tests.Schema;

public sealed class SqliteIndexSchemaTests
{
    private readonly SqliteIndexSchema _schema = new();

    [Fact]
    public void EnsureCreated_CreatesTablesAndIndexes()
    {
        using var connection = CreateConnection();

        _schema.EnsureCreated(connection);

        Assert.Equal(
            ["artifact_relationships", "artifact_revisions", "artifacts", "projects", "schema_info"],
            GetTableNames(connection));

        Assert.Equal(
            ["singleton_id", "schema_version"],
            GetColumnNames(connection, "schema_info"));

        Assert.Equal(
            ["project_id", "name", "status", "workspace_root_path", "project_metadata_path"],
            GetColumnNames(connection, "projects"));

        Assert.Equal(
            ["project_id", "artifact_id", "artifact_type", "latest_status", "latest_revision", "title", "latest_updated_at_utc"],
            GetColumnNames(connection, "artifacts"));

        Assert.Equal(
            ["project_id", "artifact_id", "revision", "artifact_status", "title", "file_path", "created_at_utc", "updated_at_utc", "is_canonical"],
            GetColumnNames(connection, "artifact_revisions"));

        Assert.Equal(
            ["project_id", "source_artifact_id", "source_revision", "relationship_kind", "target_artifact_id"],
            GetColumnNames(connection, "artifact_relationships"));

        Assert.Contains("idx_artifacts_project_type_status", GetIndexNames(connection));
        Assert.Contains("idx_artifact_revisions_project_status", GetIndexNames(connection));
        Assert.Contains("idx_artifact_relationships_target", GetIndexNames(connection));
    }

    [Fact]
    public void EnsureCreated_WritesCurrentSchemaVersion()
    {
        using var connection = CreateConnection();

        _schema.EnsureCreated(connection);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT schema_version FROM schema_info WHERE singleton_id = 1;";

        var version = command.ExecuteScalar();

        Assert.Equal(SqliteIndexSchema.CurrentVersion, Convert.ToInt32(version));
    }

    [Fact]
    public void EnsureCreated_IsIdempotent()
    {
        using var connection = CreateConnection();

        _schema.EnsureCreated(connection);
        _schema.EnsureCreated(connection);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM schema_info;";

        var rowCount = command.ExecuteScalar();

        Assert.Equal(1L, rowCount);
    }

    private static SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static IReadOnlyList<string> GetTableNames(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT name
                              FROM sqlite_master
                              WHERE type = 'table'
                                AND name NOT LIKE 'sqlite_%'
                              ORDER BY name;
                              """;

        using var reader = command.ExecuteReader();
        var names = new List<string>();

        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static IReadOnlyList<string> GetColumnNames(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        using var reader = command.ExecuteReader();
        var names = new List<string>();

        while (reader.Read())
        {
            names.Add(reader.GetString(1));
        }

        return names;
    }

    private static IReadOnlyList<string> GetIndexNames(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
                              SELECT name
                              FROM sqlite_master
                              WHERE type = 'index'
                                AND name NOT LIKE 'sqlite_%'
                              ORDER BY name;
                              """;

        using var reader = command.ExecuteReader();
        var names = new List<string>();

        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}
