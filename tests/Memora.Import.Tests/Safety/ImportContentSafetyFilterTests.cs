using System.Text.Json;
using Memora.Core.Import;
using Memora.Import.Evidence;
using Memora.Import.Git;
using Memora.Import.GitHub;
using Memora.Import.Safety;
using Memora.Storage.Workspaces;

namespace Memora.Import.Tests.Safety;

public sealed class ImportContentSafetyFilterTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-import-safety-tests",
        Guid.NewGuid().ToString("N"));

    private readonly WorkspaceDiscovery _workspaceDiscovery = new();
    private readonly FileBackedImportedEvidenceStore _evidenceStore = new();

    [Fact]
    public void LocalGitImport_RedactsOpenAiProjectKeyBeforePersistence()
    {
        var repoPath = CreateSourceRepository("local-source");
        var workspacePath = CreateWorkspace("memora", localPath: repoPath, github: false);
        var importer = new LocalGitEvidenceImporter(
            _rootPath,
            _workspaceDiscovery,
            new FakeLocalGitHistoryReader(
                LocalGitHistoryReadResult.Succeeded(
                    new LocalGitHistorySnapshot(
                        [
                            new LocalGitCommit(
                                "abc123",
                                new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero),
                                "Alex",
                                "alex@example.test",
                                "Remove sk-proj-abcdefghijklmnopqrstuvwxyz",
                                ["src/app.cs"])
                        ],
                        [],
                        [],
                        [],
                        IsPartial: false,
                        []))),
            _evidenceStore,
            new ImportContentSafetyFilter());

        var result = importer.Import(new LocalGitEvidenceImportRequest("memora", ImportMode.FastBaseline));

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "import.secret.redacted");
        var stored = Assert.Single(_evidenceStore.ReadAll(workspacePath));
        Assert.DoesNotContain("sk-proj-", stored.Title, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", stored.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void GitHubImport_RedactsGitHubTokenBeforePersistence()
    {
        var workspacePath = CreateWorkspace("memora", localPath: null, github: true);
        var importer = new GitHubEvidenceImporter(
            _rootPath,
            _workspaceDiscovery,
            new FakeGitHubEvidenceClient(
                GitHubEvidenceClientResult.Succeeded(
                    new GitHubEvidenceSnapshot(
                        [
                            new GitHubIssueEvidence(
                                42,
                                "https://github.com/alucero270/memora/issues/42",
                                "Token ghp_abcdefghijklmnopqrstuvwxyz123456",
                                "open",
                                new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero),
                                new DateTimeOffset(2026, 5, 5, 12, 30, 0, TimeSpan.Zero))
                        ],
                        [],
                        [],
                        [],
                        [],
                        [],
                        [],
                        IsPartial: false,
                        []))),
            _evidenceStore,
            new ImportContentSafetyFilter());

        var result = importer.Import(new GitHubEvidenceImportRequest("memora", ImportMode.FastBaseline));

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "import.secret.redacted");
        var stored = Assert.Single(_evidenceStore.ReadAll(workspacePath));
        Assert.DoesNotContain("ghp_", stored.Title, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", stored.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void GitHubImport_PrivateKeyBlocksOnlyOffendingRecord()
    {
        var workspacePath = CreateWorkspace("memora", localPath: null, github: true);
        var privateKey = """
            -----BEGIN PRIVATE KEY-----
            abcdef
            -----END PRIVATE KEY-----
            """;
        var importer = new GitHubEvidenceImporter(
            _rootPath,
            _workspaceDiscovery,
            new FakeGitHubEvidenceClient(
                GitHubEvidenceClientResult.Succeeded(
                    new GitHubEvidenceSnapshot(
                        [],
                        [],
                        [],
                        [],
                        [
                            new GitHubCommitEvidence(
                                "abc123",
                                "https://github.com/alucero270/memora/commit/abc123",
                                privateKey,
                                "alucero270",
                                new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero))
                        ],
                        [],
                        [],
                        IsPartial: false,
                        []))),
            _evidenceStore,
            new ImportContentSafetyFilter());

        var result = importer.Import(new GitHubEvidenceImportRequest("memora", ImportMode.FastBaseline));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "import.secret.blocked");
        Assert.Empty(_evidenceStore.ReadAll(workspacePath));
    }

    [Fact]
    public void GitHubImport_MixedBatch_BlocksOffendingRecordAndPersistsSafeRecords()
    {
        var workspacePath = CreateWorkspace("memora-mixed", localPath: null, github: true);
        var privateKey = """
            -----BEGIN PRIVATE KEY-----
            abcdef
            -----END PRIVATE KEY-----
            """;
        var importer = new GitHubEvidenceImporter(
            _rootPath,
            _workspaceDiscovery,
            new FakeGitHubEvidenceClient(
                GitHubEvidenceClientResult.Succeeded(
                    new GitHubEvidenceSnapshot(
                        [
                            new GitHubIssueEvidence(
                                1,
                                "https://github.com/alucero270/memora/issues/1",
                                "Safe issue title",
                                "open",
                                new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero),
                                new DateTimeOffset(2026, 5, 5, 12, 30, 0, TimeSpan.Zero))
                        ],
                        [],
                        [],
                        [],
                        [
                            new GitHubCommitEvidence(
                                "abc123",
                                "https://github.com/alucero270/memora/commit/abc123",
                                privateKey,
                                "alucero270",
                                new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero))
                        ],
                        [],
                        [],
                        IsPartial: false,
                        []))),
            _evidenceStore,
            new ImportContentSafetyFilter());

        var result = importer.Import(new GitHubEvidenceImportRequest("memora-mixed", ImportMode.FastBaseline));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "import.secret.blocked");
        var stored = _evidenceStore.ReadAll(workspacePath);
        Assert.Single(stored);
        Assert.Contains("Safe issue title", stored[0].Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Filter_MixedBatch_BlocksOffendingRecordOnly()
    {
        var filter = new ImportContentSafetyFilter();
        var privateKey = "-----BEGIN PRIVATE KEY-----\nabcdef\n-----END PRIVATE KEY-----";
        var safe = MakeRecord("EVD-SAFE", "Safe title", "Safe summary");
        var blocked = MakeRecord("EVD-BLOCKED", privateKey, "Summary");

        var result = filter.Filter([safe, blocked]);

        Assert.True(result.BlocksPersistence);
        Assert.Contains(result.Diagnostics, d => d.Code == "import.secret.blocked" && d.StableEvidenceId == "EVD-BLOCKED");
        var kept = Assert.Single(result.Records);
        Assert.Equal("EVD-SAFE", kept.StableId);
    }

    [Fact]
    public void Filter_NotCovered_BearerTokenPassesThrough()
    {
        var filter = new ImportContentSafetyFilter();
        var record = MakeRecord("EVD-1", "Authorization: Bearer mytoken123", "summary");

        var result = filter.Filter([record]);

        Assert.False(result.BlocksPersistence);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Filter_NotCovered_ApiKeyPassesThrough()
    {
        var filter = new ImportContentSafetyFilter();
        var record = MakeRecord("EVD-2", "api_key: supersecretvalue123", "summary");

        var result = filter.Filter([record]);

        Assert.False(result.BlocksPersistence);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Filter_NotCovered_SlackTokenPassesThrough()
    {
        var filter = new ImportContentSafetyFilter();
        var record = MakeRecord("EVD-3", "xoxb-1234567890-abcdefghij", "summary");

        var result = filter.Filter([record]);

        Assert.False(result.BlocksPersistence);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Filter_NotCovered_PublishableStripeKeyPassesThrough()
    {
        var filter = new ImportContentSafetyFilter();
        // Stripe publishable keys (pk_live_) are not covered; only the private-key
        // PEM block rule blocks persistence.
        var record = MakeRecord("EVD-4", "pk_live_abcdefghijklmnopqrstuvwxyz1234", "summary");

        var result = filter.Filter([record]);

        Assert.False(result.BlocksPersistence);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Filter_NotCovered_JwtPassesThrough()
    {
        var filter = new ImportContentSafetyFilter();
        var jwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U";
        var record = MakeRecord("EVD-5", jwt, "summary");

        var result = filter.Filter([record]);

        Assert.False(result.BlocksPersistence);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Filter_SafeContentPassesWithoutDiagnostics()
    {
        var filter = new ImportContentSafetyFilter();
        var record = new ImportedEvidenceRecord(
            "EVD-SAFE",
            "memora",
            ImportedEvidenceSourceType.LocalGitCommit,
            "ATT-1",
            "local:/repo",
            "abc123",
            "Implement import mode",
            "No secrets here.",
            new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 5, 12, 1, 0, TimeSpan.Zero),
            "local git commit abc123",
            ImportedEvidenceTrustState.BaselineEvidence,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["changedFiles"] = "src/app.cs"
            });

        var result = filter.Filter([record]);

        Assert.False(result.BlocksPersistence);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("Implement import mode", Assert.Single(result.Records).Title);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private string CreateWorkspace(string projectId, string? localPath, bool github)
    {
        Directory.CreateDirectory(_rootPath);
        var workspaceRoot = Path.Combine(_rootPath, projectId);
        Directory.CreateDirectory(workspaceRoot);
        var attachment = github
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["attachmentId"] = "ATT-GITHUB",
                ["projectId"] = projectId,
                ["kind"] = "github",
                ["repositoryIdentity"] = "github:https://github.com/alucero270/memora.git",
                ["localPath"] = null,
                ["remoteUrl"] = "https://github.com/alucero270/memora.git",
                ["defaultBranch"] = "main",
                ["originRemoteName"] = "origin",
                ["originUrl"] = "https://github.com/alucero270/memora.git",
                ["attachedAtUtc"] = "2026-05-05T18:00:00Z"
            }
            : new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["attachmentId"] = "ATT-LOCAL",
                ["projectId"] = projectId,
                ["kind"] = "local_git",
                ["repositoryIdentity"] = "local:" + Path.GetFullPath(localPath!).Replace('\\', '/'),
                ["localPath"] = Path.GetFullPath(localPath!),
                ["remoteUrl"] = "https://github.com/alucero270/memora.git",
                ["defaultBranch"] = "main",
                ["originRemoteName"] = "origin",
                ["originUrl"] = "https://github.com/alucero270/memora.git",
                ["attachedAtUtc"] = "2026-05-05T18:00:00Z"
            };

        File.WriteAllText(
            Path.Combine(workspaceRoot, "project.json"),
            JsonSerializer.Serialize(
                new
                {
                    projectId,
                    name = "Memora",
                    status = "active",
                    repositoryAttachments = new[] { attachment }
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

        public LocalGitHistoryReadResult Read(string repositoryPath, int maxCommits) => _result;
    }

    private sealed class FakeGitHubEvidenceClient : IGitHubEvidenceClient
    {
        private readonly GitHubEvidenceClientResult _result;

        public FakeGitHubEvidenceClient(GitHubEvidenceClientResult result)
        {
            _result = result;
        }

        public GitHubEvidenceClientResult Fetch(string remoteUrl, int maxItems) => _result;
    }

    private static ImportedEvidenceRecord MakeRecord(string stableId, string title, string summary) =>
        new(
            stableId,
            "memora",
            ImportedEvidenceSourceType.GitHubIssue,
            "ATT-1",
            "github:https://github.com/alucero270/memora.git",
            "https://github.com/alucero270/memora/issues/1",
            title,
            summary,
            new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 5, 12, 1, 0, TimeSpan.Zero),
            "github issue 1",
            ImportedEvidenceTrustState.BaselineEvidence);
}
