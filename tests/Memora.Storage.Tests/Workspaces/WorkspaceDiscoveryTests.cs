using System.Text.Json;
using Memora.Storage.Workspaces;

namespace Memora.Storage.Tests.Workspaces;

public sealed class WorkspaceDiscoveryTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-storage-tests",
        Guid.NewGuid().ToString("N"));

    private readonly WorkspaceDiscovery _discovery = new();

    [Fact]
    public void Load_ComputesExplicitWorkspacePaths()
    {
        var workspaceRootPath = CreateWorkspace("alpha-workspace", "alpha", "Alpha Project", "active");

        var workspace = _discovery.Load(workspaceRootPath);

        Assert.Equal("alpha", workspace.Metadata.ProjectId);
        Assert.Equal("Alpha Project", workspace.Metadata.Name);
        Assert.Equal("active", workspace.Metadata.Status);
        Assert.Equal(Path.Combine(workspaceRootPath, "project.json"), workspace.ProjectMetadataPath);
        Assert.Equal(Path.Combine(workspaceRootPath, "canonical"), workspace.CanonicalRootPath);
        Assert.Equal(Path.Combine(workspaceRootPath, "drafts"), workspace.DraftsRootPath);
        Assert.Equal(Path.Combine(workspaceRootPath, "summaries"), workspace.SummariesRootPath);
        Assert.Equal(Path.Combine(workspaceRootPath, "evidence"), workspace.EvidenceRootPath);
        Assert.Equal(Path.Combine(workspace.CanonicalRootPath, "charters"), workspace.CanonicalChartersPath);
        Assert.Equal(Path.Combine(workspace.CanonicalRootPath, "decisions"), workspace.CanonicalDecisionsPath);
        Assert.Equal(Path.Combine(workspace.CanonicalRootPath, "plans"), workspace.CanonicalPlansPath);
        Assert.Equal(Path.Combine(workspace.CanonicalRootPath, "constraints"), workspace.CanonicalConstraintsPath);
        Assert.Equal(Path.Combine(workspace.CanonicalRootPath, "questions"), workspace.CanonicalQuestionsPath);
        Assert.Equal(Path.Combine(workspace.CanonicalRootPath, "outcomes"), workspace.CanonicalOutcomesPath);
        Assert.Equal(Path.Combine(workspace.CanonicalRootPath, "repo"), workspace.CanonicalRepoPath);
    }

    [Fact]
    public void Load_ReadsRepositoryAttachmentsFromProjectMetadata()
    {
        var workspaceRootPath = CreateWorkspaceDirectory("attached-workspace");
        WriteProjectMetadata(
            Path.Combine(workspaceRootPath, "project.json"),
            new Dictionary<string, object?>
            {
                ["projectId"] = "attached",
                ["name"] = "Attached Project",
                ["status"] = "active",
                ["repositoryAttachments"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["attachmentId"] = "ATT-123",
                        ["projectId"] = "attached",
                        ["kind"] = "github",
                        ["repositoryIdentity"] = "github:https://github.com/alucero270/memora.git",
                        ["remoteUrl"] = "https://github.com/alucero270/memora.git",
                        ["defaultBranch"] = "main",
                        ["originRemoteName"] = "origin",
                        ["originUrl"] = "https://github.com/alucero270/memora.git",
                        ["attachedAtUtc"] = "2026-05-05T18:00:00Z"
                    }
                }
            });

        var workspace = _discovery.Load(workspaceRootPath);

        var attachment = Assert.Single(workspace.Metadata.RepositoryAttachments);
        Assert.Equal("ATT-123", attachment.AttachmentId);
        Assert.Equal("attached", attachment.ProjectId);
        Assert.Equal("github:https://github.com/alucero270/memora.git", attachment.RepositoryIdentity);
        Assert.Equal("main", attachment.DefaultBranch);
    }

    [Fact]
    public void Discover_LoadsMultipleProjectsInDeterministicOrder()
    {
        CreateWorkspace("zeta-workspace", "zeta", "Zeta Project");
        CreateWorkspace("alpha-workspace", "alpha", "Alpha Project");

        var workspaces = _discovery.Discover(_rootPath);

        Assert.Collection(
            workspaces,
            workspace => Assert.Equal("alpha", workspace.ProjectId),
            workspace => Assert.Equal("zeta", workspace.ProjectId));
    }

    [Fact]
    public void Discover_RejectsDuplicateProjectIds()
    {
        CreateWorkspace("project-one", "shared", "Shared Project One");
        CreateWorkspace("project-two", "shared", "Shared Project Two");

        var exception = Assert.Throws<InvalidDataException>(() => _discovery.Discover(_rootPath));

        Assert.Contains("duplicate project ids", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shared", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_RejectsMissingRequiredProjectMetadata()
    {
        var workspaceRootPath = CreateWorkspaceDirectory("invalid-workspace");
        WriteProjectMetadata(
            Path.Combine(workspaceRootPath, "project.json"),
            new Dictionary<string, object?>
            {
                ["projectId"] = "invalid"
            });

        var exception = Assert.Throws<InvalidDataException>(() => _discovery.Load(workspaceRootPath));

        Assert.Contains("name", exception.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private string CreateWorkspace(string directoryName, string projectId, string name, string? status = null)
    {
        var workspaceRootPath = CreateWorkspaceDirectory(directoryName);
        WriteProjectMetadata(
            Path.Combine(workspaceRootPath, "project.json"),
            new Dictionary<string, object?>
            {
                ["projectId"] = projectId,
                ["name"] = name,
                ["status"] = status
            });

        return workspaceRootPath;
    }

    private string CreateWorkspaceDirectory(string directoryName)
    {
        var workspaceRootPath = Path.Combine(_rootPath, directoryName);
        Directory.CreateDirectory(Path.Combine(workspaceRootPath, "canonical"));
        Directory.CreateDirectory(Path.Combine(workspaceRootPath, "drafts"));
        Directory.CreateDirectory(Path.Combine(workspaceRootPath, "summaries"));
        return workspaceRootPath;
    }

    private static void WriteProjectMetadata(string metadataPath, IReadOnlyDictionary<string, object?> metadata)
    {
        var json = JsonSerializer.Serialize(metadata);
        File.WriteAllText(metadataPath, json);
    }
}
