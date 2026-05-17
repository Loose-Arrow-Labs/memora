using System.Text.Json;
using Memora.Core.Import;
using Memora.Import.Evidence;
using Memora.Import.Git;
using Memora.Storage.Workspaces;

namespace Memora.Import.Tests.Git;

public sealed class LocalGitEvidenceImporterTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-local-git-import-tests",
        Guid.NewGuid().ToString("N"));

    private readonly WorkspaceDiscovery _workspaceDiscovery = new();
    private readonly FileBackedImportedEvidenceStore _evidenceStore = new();

    [Fact]
    public void Import_GitEvidence_DeterministicNoDuplicates()
    {
        var repoPath = CreateSourceRepository("memora-source");
        var workspacePath = CreateWorkspaceWithLocalAttachment("memora", repoPath);
        var importer = CreateImporter(CreateHistorySnapshot());

        var first = importer.Import(new LocalGitEvidenceImportRequest("memora", ImportMode.FastBaseline));
        var second = importer.Import(new LocalGitEvidenceImportRequest("memora", ImportMode.FastBaseline));

        Assert.True(first.IsSuccess);
        Assert.Equal(5, first.Progress.TotalRecords);
        Assert.Equal(5, first.Progress.CreatedRecords);
        Assert.Equal(0, first.Progress.ExistingRecords);
        Assert.Equal(0, second.Progress.CreatedRecords);
        Assert.Equal(5, second.Progress.ExistingRecords);
        Assert.Collection(
            first.Records,
            record => Assert.Equal("Implement importer", record.Title),
            record => Assert.Equal("Initial import model", record.Title),
            record => Assert.Equal(ImportedEvidenceSourceType.LocalGitBranch, record.SourceType),
            record => Assert.Equal(ImportedEvidenceSourceType.LocalGitTag, record.SourceType),
            record => Assert.Equal(ImportedEvidenceSourceType.LocalGitChangelogSignal, record.SourceType));

        var files = Directory
            .EnumerateFiles(Path.Combine(workspacePath, "evidence"), "*.json", SearchOption.AllDirectories)
            .ToArray();
        Assert.Equal(5, files.Length);

        var stored = _evidenceStore.ReadAll(workspacePath);
        Assert.Equal(5, stored.Count);
        Assert.Equal(5, stored.Select(record => record.StableId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(stored, record => Assert.Equal(ImportedEvidenceTrustState.BaselineEvidence, record.TrustState));
        Assert.Contains(stored, record =>
            record.SourceType == ImportedEvidenceSourceType.LocalGitCommit &&
            record.Metadata["changedFiles"].Contains("src/Memora.Import/Git/LocalGitEvidenceImporter.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_StrictGovernance_StoresReviewableEvidence()
    {
        var repoPath = CreateSourceRepository("memora-source");
        var workspacePath = CreateWorkspaceWithLocalAttachment("memora", repoPath);
        var importer = CreateImporter(
            new LocalGitHistorySnapshot(
                [
                    new LocalGitCommit(
                        "abc123",
                        new DateTimeOffset(2026, 5, 4, 12, 0, 0, TimeSpan.Zero),
                        "Alex",
                        "alex@example.test",
                        "Add governance mode",
                        ["docs/import.md"])
                ],
                [],
                [],
                [],
                IsPartial: false,
                []));

        var result = importer.Import(new LocalGitEvidenceImportRequest("memora", ImportMode.StrictGovernance));

        Assert.True(result.IsSuccess);
        var record = Assert.Single(_evidenceStore.ReadAll(workspacePath));
        Assert.Equal(ImportedEvidenceTrustState.ReviewableEvidence, record.TrustState);
    }

    [Fact]
    public void Import_DiagnosticsDistinguishUnsupportedRepository()
    {
        CreateWorkspaceWithLocalAttachment("memora", Path.Combine(_rootPath, "missing-source"));
        var importer = CreateImporter(CreateHistorySnapshot());

        var result = importer.Import(new LocalGitEvidenceImportRequest("memora", ImportMode.FastBaseline));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "local_git.repo.unsupported");
    }

    [Theory]
    [InlineData("local_git.metadata.missing")]
    [InlineData("local_git.command.failed")]
    public void Import_PropagatesGitReadErrorDiagnostics(string diagnosticCode)
    {
        var repoPath = CreateSourceRepository("memora-source");
        CreateWorkspaceWithLocalAttachment("memora", repoPath);
        var importer = CreateImporter(
            LocalGitHistoryReadResult.Failed(
                LocalGitImportDiagnostic.Error(diagnosticCode, "Git read failed.", "repository")));

        var result = importer.Import(new LocalGitEvidenceImportRequest("memora", ImportMode.FastBaseline));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == diagnosticCode);
    }

    [Fact]
    public void Import_PreservesPartialImportWarningWhenHistoryIsBounded()
    {
        var repoPath = CreateSourceRepository("memora-source");
        CreateWorkspaceWithLocalAttachment("memora", repoPath);
        var importer = CreateImporter(
            new LocalGitHistorySnapshot(
                [],
                [],
                [],
                [],
                IsPartial: true,
                [LocalGitImportDiagnostic.Warning("local_git.import.partial", "Commit import reached the configured bound.", "max_commits")]));

        var result = importer.Import(new LocalGitEvidenceImportRequest("memora", ImportMode.FastBaseline, maxCommits: 1));

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "local_git.import.partial");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private LocalGitEvidenceImporter CreateImporter(LocalGitHistorySnapshot snapshot) =>
        CreateImporter(LocalGitHistoryReadResult.Succeeded(snapshot));

    private LocalGitEvidenceImporter CreateImporter(LocalGitHistoryReadResult result) =>
        new(
            _rootPath,
            _workspaceDiscovery,
            new FakeLocalGitHistoryReader(result),
            _evidenceStore);

    private static LocalGitHistorySnapshot CreateHistorySnapshot() =>
        new(
            [
                new LocalGitCommit(
                    "2222222",
                    new DateTimeOffset(2026, 5, 4, 14, 0, 0, TimeSpan.Zero),
                    "Alex",
                    "alex@example.test",
                    "Implement importer",
                    ["src/Memora.Import/Git/LocalGitEvidenceImporter.cs", "tests/Memora.Import.Tests/Git/LocalGitEvidenceImporterTests.cs"]),
                new LocalGitCommit(
                    "1111111",
                    new DateTimeOffset(2026, 5, 3, 9, 0, 0, TimeSpan.Zero),
                    "Alex",
                    "alex@example.test",
                    "Initial import model",
                    ["src/Memora.Core/Import/ImportedEvidenceRecord.cs"])
            ],
            [new LocalGitBranch("main", "2222222")],
            [new LocalGitTag("v0.1.0", "1111111", new DateTimeOffset(2026, 5, 3, 10, 0, 0, TimeSpan.Zero))],
            [new LocalGitChangelogSignal("CHANGELOG.md", "Repository contains a changelog.")],
            IsPartial: false,
            []);

    private string CreateWorkspaceWithLocalAttachment(string projectId, string repoPath)
    {
        Directory.CreateDirectory(_rootPath);
        var workspaceRoot = Path.Combine(_rootPath, projectId);
        Directory.CreateDirectory(workspaceRoot);
        File.WriteAllText(
            Path.Combine(workspaceRoot, "project.json"),
            JsonSerializer.Serialize(
                new
                {
                    projectId,
                    name = "Memora",
                    status = "active",
                    repositoryAttachments = new[]
                    {
                        new
                        {
                            attachmentId = "ATT-LOCAL",
                            projectId,
                            kind = "local_git",
                            repositoryIdentity = "local:" + Path.GetFullPath(repoPath).Replace('\\', '/'),
                            localPath = Path.GetFullPath(repoPath),
                            remoteUrl = "https://github.com/alucero270/memora.git",
                            defaultBranch = "main",
                            originRemoteName = "origin",
                            originUrl = "https://github.com/alucero270/memora.git",
                            attachedAtUtc = "2026-05-05T18:00:00Z"
                        }
                    }
                }));
        return workspaceRoot;
    }

    private string CreateSourceRepository(string name)
    {
        var repoPath = Path.Combine(_rootPath, "source", name);
        Directory.CreateDirectory(repoPath);
        return repoPath;
    }

    private sealed class FakeLocalGitHistoryReader : ILocalGitHistoryReader
    {
        private readonly LocalGitHistoryReadResult _result;

        public FakeLocalGitHistoryReader(LocalGitHistoryReadResult result)
        {
            _result = result;
        }

        public LocalGitHistoryReadResult Read(string repositoryPath, int maxCommits) =>
            _result;
    }
}
