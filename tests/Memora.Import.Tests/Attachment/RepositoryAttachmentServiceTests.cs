using System.Text.Json;
using Memora.Core.Import;
using Memora.Import.Attachment;
using Memora.Import.Git;
using Memora.Storage.Workspaces;

namespace Memora.Import.Tests.Attachment;

public sealed class RepositoryAttachmentServiceTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-import-tests",
        Guid.NewGuid().ToString("N"));

    private readonly WorkspaceDiscovery _workspaceDiscovery = new();

    [Fact]
    public void Attach_LocalGit_PersistsRepositoryMetadataWithoutUsingSourceAsWorkspace()
    {
        CreateWorkspace("memora");
        var repoPath = CreateSourceRepository("memora-source");
        var service = CreateService(new FakeGitRepositoryInspector(
            new GitRepositoryInspection(
                repoPath,
                "trunk",
                "origin",
                "https://github.com/alucero270/memora.git")));

        var result = service.Attach(
            new RepositoryAttachmentRequest(
                "memora",
                RepositoryAttachmentKind.LocalGit,
                localPath: repoPath));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Attachment);
        Assert.Equal(RepositoryAttachmentKind.LocalGit, result.Attachment.Kind);
        Assert.Equal(Path.GetFullPath(repoPath), result.Attachment.LocalPath);
        Assert.Equal("trunk", result.Attachment.DefaultBranch);
        Assert.NotEqual(Path.GetFullPath(repoPath), GetWorkspacePath("memora"));

        var workspace = _workspaceDiscovery.Load(GetWorkspacePath("memora"));
        var attachment = Assert.Single(workspace.Metadata.RepositoryAttachments);
        Assert.Equal(result.Attachment.AttachmentId, attachment.AttachmentId);
        Assert.Equal("local:" + Path.GetFullPath(repoPath).Replace('\\', '/'), attachment.RepositoryIdentity);
        Assert.Equal("https://github.com/alucero270/memora.git", attachment.OriginUrl);
    }

    [Fact]
    public void Attach_GitHub_PersistsRemoteIdentityDefaultBranch()
    {
        CreateWorkspace("memora");
        var service = CreateService();

        var result = service.Attach(
            new RepositoryAttachmentRequest(
                "memora",
                RepositoryAttachmentKind.GitHub,
                remoteUrl: "git@github.com:alucero270/memora.git",
                defaultBranch: "main"));

        Assert.True(result.IsSuccess);

        var workspace = _workspaceDiscovery.Load(GetWorkspacePath("memora"));
        var attachment = Assert.Single(workspace.Metadata.RepositoryAttachments);
        Assert.Equal(RepositoryAttachmentKind.GitHub, attachment.Kind);
        Assert.Equal("github:https://github.com/alucero270/memora.git", attachment.RepositoryIdentity);
        Assert.Equal("https://github.com/alucero270/memora.git", attachment.RemoteUrl);
        Assert.Equal("main", attachment.DefaultBranch);
        Assert.Null(attachment.LocalPath);
    }

    [Fact]
    public void Attach_LocalGit_MissingRepository_ReturnsValidationError()
    {
        CreateWorkspace("memora");
        var service = CreateService();

        var result = service.Attach(
            new RepositoryAttachmentRequest(
                "memora",
                RepositoryAttachmentKind.LocalGit,
                localPath: Path.Combine(_rootPath, "missing")));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "attachment.repo.missing");
        Assert.Empty(_workspaceDiscovery.Load(GetWorkspacePath("memora")).Metadata.RepositoryAttachments);
    }

    [Fact]
    public void Attach_GitHub_UnsupportedRemote_ReturnsValidationError()
    {
        CreateWorkspace("memora");
        var service = CreateService();

        var result = service.Attach(
            new RepositoryAttachmentRequest(
                "memora",
                RepositoryAttachmentKind.GitHub,
                remoteUrl: "https://gitlab.com/example/memora.git"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "attachment.remote.unsupported");
    }

    [Fact]
    public void Attach_DuplicateRepository_ReturnsValidationError()
    {
        CreateWorkspace("memora");
        var service = CreateService();
        var request = new RepositoryAttachmentRequest(
            "memora",
            RepositoryAttachmentKind.GitHub,
            remoteUrl: "https://github.com/alucero270/memora",
            defaultBranch: "main");

        Assert.True(service.Attach(request).IsSuccess);
        var duplicate = service.Attach(request);

        Assert.False(duplicate.IsSuccess);
        Assert.Contains(duplicate.Errors, error => error.Code == "attachment.duplicate");
        Assert.Single(_workspaceDiscovery.Load(GetWorkspacePath("memora")).Metadata.RepositoryAttachments);
    }

    [Fact]
    public void Attach_InvalidWorkspace_ReturnsWorkspaceError()
    {
        var service = CreateService();

        var result = service.Attach(
            new RepositoryAttachmentRequest(
                "missing",
                RepositoryAttachmentKind.GitHub,
                remoteUrl: "https://github.com/alucero270/memora"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Code == "workspace.root.missing");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private RepositoryAttachmentService CreateService(IGitRepositoryInspector? inspector = null) =>
        new(
            _rootPath,
            _workspaceDiscovery,
            inspector ?? new FakeGitRepositoryInspector(null));

    private string CreateWorkspace(string projectId)
    {
        var workspaceRoot = GetWorkspacePath(projectId);
        Directory.CreateDirectory(workspaceRoot);
        File.WriteAllText(
            Path.Combine(workspaceRoot, "project.json"),
            JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["projectId"] = projectId,
                    ["name"] = "Memora",
                    ["status"] = "active"
                }));
        return workspaceRoot;
    }

    private string CreateSourceRepository(string name)
    {
        var path = Path.Combine(_rootPath, "source", name);
        Directory.CreateDirectory(path);
        return path;
    }

    private string GetWorkspacePath(string projectId) =>
        Path.Combine(_rootPath, projectId);

    private sealed class FakeGitRepositoryInspector : IGitRepositoryInspector
    {
        private readonly GitRepositoryInspection? _inspection;

        public FakeGitRepositoryInspector(GitRepositoryInspection? inspection)
        {
            _inspection = inspection;
        }

        public GitRepositoryInspectionResult Inspect(string localPath) =>
            _inspection is null
                ? GitRepositoryInspectionResult.Failed(
                    "attachment.git_metadata.missing",
                    $"Repository path '{localPath}' does not contain readable Git metadata.")
                : GitRepositoryInspectionResult.Succeeded(_inspection);
    }
}
