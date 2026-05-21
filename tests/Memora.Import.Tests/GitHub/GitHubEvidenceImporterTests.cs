using System.Text.Json;
using Memora.Core.Import;
using Memora.Import.Evidence;
using Memora.Import.GitHub;
using Memora.Storage.Workspaces;

namespace Memora.Import.Tests.GitHub;

public sealed class GitHubEvidenceImporterTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-github-import-tests",
        Guid.NewGuid().ToString("N"));

    private readonly WorkspaceDiscovery _workspaceDiscovery = new();
    private readonly FileBackedImportedEvidenceStore _evidenceStore = new();

    [Fact]
    public void Import_WritesGitHubEvidenceWithStableProvenance()
    {
        var workspacePath = CreateWorkspaceWithGitHubAttachment("memora");
        var importer = CreateImporter(CreateSnapshot());

        var result = importer.Import(new GitHubEvidenceImportRequest("memora", ImportMode.EvidenceCanonical));

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Progress.TotalRecords);
        Assert.Equal(1, result.Progress.IssueCount);
        Assert.Equal(1, result.Progress.PullRequestCount);
        Assert.Equal(1, result.Progress.ReviewCount);
        Assert.Equal(1, result.Progress.ReviewCommentCount);
        Assert.Equal(1, result.Progress.CommitCount);
        Assert.Equal(1, result.Progress.ReleaseCount);
        Assert.Equal(1, result.Progress.DiscussionCount);

        var stored = _evidenceStore.ReadAll(workspacePath);
        Assert.Equal(7, stored.Count);
        Assert.Equal(7, stored.Select(record => record.StableId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(stored, record => Assert.Equal(ImportedEvidenceTrustState.CanonicalEvidence, record.TrustState));
        Assert.Contains(stored, record =>
            record.SourceType == ImportedEvidenceSourceType.GitHubIssue &&
            record.Metadata["url"] == "https://github.com/alucero270/memora/issues/209" &&
            record.Metadata["number"] == "209");
        Assert.Contains(stored, record =>
            record.SourceType == ImportedEvidenceSourceType.GitHubPullRequest &&
            record.Metadata["mergeCommitSha"] == "abc123");
        Assert.All(stored, record => Assert.StartsWith("https://github.com/alucero270/memora", record.Provenance, StringComparison.Ordinal));
    }

    [Fact]
    public void Import_ReRunPreventsDuplicateEvidenceFiles()
    {
        var workspacePath = CreateWorkspaceWithGitHubAttachment("memora");
        var importer = CreateImporter(CreateSnapshot());

        var first = importer.Import(new GitHubEvidenceImportRequest("memora", ImportMode.FastBaseline));
        var second = importer.Import(new GitHubEvidenceImportRequest("memora", ImportMode.FastBaseline));

        Assert.True(first.IsSuccess);
        Assert.Equal(7, first.Progress.CreatedRecords);
        Assert.Equal(0, first.Progress.ExistingRecords);
        Assert.Equal(0, second.Progress.CreatedRecords);
        Assert.Equal(7, second.Progress.ExistingRecords);
        Assert.Equal(7, Directory.EnumerateFiles(Path.Combine(workspacePath, "evidence"), "*.json", SearchOption.AllDirectories).Count());
    }

    [Fact]
    public void Import_PartialImportRemainsExplicit()
    {
        CreateWorkspaceWithGitHubAttachment("memora");
        var importer = CreateImporter(
            new GitHubEvidenceSnapshot(
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                IsPartial: true,
                [GitHubImportDiagnostic.Warning("github.import.partial", "Pagination was bounded.", "issues")]));

        var result = importer.Import(new GitHubEvidenceImportRequest("memora", ImportMode.FastBaseline));

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "github.import.partial");
    }

    [Theory]
    [InlineData("github.credentials.missing")]
    [InlineData("github.private_denied")]
    [InlineData("github.rate_limited")]
    public void Import_ProviderErrorsReturnClearDiagnostics(string diagnosticCode)
    {
        CreateWorkspaceWithGitHubAttachment("memora");
        var importer = CreateImporter(
            GitHubEvidenceClientResult.Failed(
                GitHubImportDiagnostic.Error(diagnosticCode, "Provider failed.", "github")));

        var result = importer.Import(new GitHubEvidenceImportRequest("memora", ImportMode.FastBaseline));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == diagnosticCode);
    }

    [Fact]
    public void Import_GitHubCliPayload_BadRecord_PersistsValidAndDiagnostic()
    {
        var workspacePath = CreateWorkspaceWithGitHubAttachment("memora");
        var runner = new FakeGhRunner(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["/repos/alucero270/memora/issues?state=all&per_page=10"] = "[]",
                ["/repos/alucero270/memora/pulls?state=all&per_page=10"] =
                    """
                    [
                      {
                        "number": 101,
                        "html_url": "https://github.com/alucero270/memora/pull/101",
                        "title": "Valid PR",
                        "state": "closed",
                        "merge_commit_sha": null,
                        "merged_at": null,
                        "created_at": "2026-05-01T12:00:00Z",
                        "updated_at": "2026-05-02T12:00:00Z"
                      },
                      {
                        "number": 102,
                        "html_url": "https://github.com/alucero270/memora/pull/102",
                        "state": "open",
                        "merge_commit_sha": null,
                        "created_at": "not-a-date",
                        "updated_at": "2026-05-03T12:00:00Z"
                      }
                    ]
                    """,
                ["/repos/alucero270/memora/pulls/comments?per_page=10"] = "[]",
                ["/repos/alucero270/memora/commits?per_page=10"] = "[]",
                ["/repos/alucero270/memora/releases?per_page=10"] = "[]",
                ["/repos/alucero270/memora/pulls/101/reviews?per_page=10"] = "[]"
            });
        var importer = new GitHubEvidenceImporter(
            _rootPath,
            _workspaceDiscovery,
            new GitHubCliEvidenceClient(runner.Run),
            _evidenceStore);

        var result = importer.Import(new GitHubEvidenceImportRequest("memora", ImportMode.EvidenceCanonical, maxItems: 10));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Progress.TotalRecords);
        Assert.Equal(1, result.Progress.PullRequestCount);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "github.response.field.missing" && diagnostic.Path == "pulls[1].title");
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Path?.Contains("merged_at", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Path?.Contains("body", StringComparison.Ordinal) == true);

        var stored = _evidenceStore.ReadAll(workspacePath);
        var record = Assert.Single(stored, record => record.SourceType == ImportedEvidenceSourceType.GitHubPullRequest);
        Assert.Equal("PR #101: Valid PR", record.Title);
        Assert.Equal(string.Empty, record.Metadata["mergeCommitSha"]);
        Assert.Equal(string.Empty, record.Metadata["mergedAtUtc"]);
    }

    [Theory]
    [InlineData("not-json", "github.response.invalid_json")]
    [InlineData("""{"message":"not an array"}""", "github.response.unexpected")]
    public void GitHubCliClient_MalformedPayloads_MarkSnapshotPartial(string issuesPayload, string diagnosticCode)
    {
        var runner = new FakeGhRunner(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["/repos/alucero270/memora/issues?state=all&per_page=10"] = issuesPayload,
                ["/repos/alucero270/memora/pulls?state=all&per_page=10"] = "[]",
                ["/repos/alucero270/memora/pulls/comments?per_page=10"] = "[]",
                ["/repos/alucero270/memora/commits?per_page=10"] = "[]",
                ["/repos/alucero270/memora/releases?per_page=10"] = "[]"
            });
        var client = new GitHubCliEvidenceClient(runner.Run);

        var result = client.Fetch("https://github.com/alucero270/memora.git", maxItems: 10);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Snapshot);
        Assert.True(result.Snapshot.IsPartial);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == diagnosticCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private GitHubEvidenceImporter CreateImporter(GitHubEvidenceSnapshot snapshot) =>
        CreateImporter(GitHubEvidenceClientResult.Succeeded(snapshot));

    private GitHubEvidenceImporter CreateImporter(GitHubEvidenceClientResult result) =>
        new(
            _rootPath,
            _workspaceDiscovery,
            new FakeGitHubEvidenceClient(result),
            _evidenceStore);

    private static GitHubEvidenceSnapshot CreateSnapshot() =>
        new(
            [
                new GitHubIssueEvidence(
                    209,
                    "https://github.com/alucero270/memora/issues/209",
                    "Define import modes",
                    "closed",
                    new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 5, 4, 8, 0, 0, TimeSpan.Zero))
            ],
            [
                new GitHubPullRequestEvidence(
                    238,
                    "https://github.com/alucero270/memora/pull/238",
                    "M10-01 docs",
                    "open",
                    "abc123",
                    null,
                    new DateTimeOffset(2026, 5, 5, 18, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 5, 5, 18, 5, 0, TimeSpan.Zero))
            ],
            [
                new GitHubReviewEvidence(
                    238,
                    "1001",
                    "https://github.com/alucero270/memora/pull/238#pullrequestreview-1001",
                    "APPROVED",
                    "reviewer",
                    new DateTimeOffset(2026, 5, 5, 18, 10, 0, TimeSpan.Zero))
            ],
            [
                new GitHubReviewCommentEvidence(
                    238,
                    "2001",
                    "https://github.com/alucero270/memora/pull/238#discussion_r2001",
                    "docs/import-and-workspace-strategy.md",
                    new DateTimeOffset(2026, 5, 5, 18, 11, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 5, 5, 18, 12, 0, TimeSpan.Zero))
            ],
            [
                new GitHubCommitEvidence(
                    "abc123",
                    "https://github.com/alucero270/memora/commit/abc123",
                    "docs(import): define first-run import modes",
                    "alucero270",
                    new DateTimeOffset(2026, 5, 5, 18, 0, 0, TimeSpan.Zero))
            ],
            [
                new GitHubReleaseEvidence(
                    "3001",
                    "https://github.com/alucero270/memora/releases/tag/v0.1.0",
                    "v0.1.0",
                    "v0.1.0",
                    new DateTimeOffset(2026, 5, 5, 18, 30, 0, TimeSpan.Zero))
            ],
            [
                new GitHubDiscussionEvidence(
                    "D_123",
                    "https://github.com/alucero270/memora/discussions/1",
                    "Import modes",
                    new DateTimeOffset(2026, 5, 5, 17, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 5, 5, 17, 30, 0, TimeSpan.Zero))
            ],
            IsPartial: false,
            []);

    private string CreateWorkspaceWithGitHubAttachment(string projectId)
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
                            attachmentId = "ATT-GITHUB",
                            projectId,
                            kind = "github",
                            repositoryIdentity = "github:https://github.com/alucero270/memora.git",
                            localPath = (string?)null,
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

    private sealed class FakeGitHubEvidenceClient : IGitHubEvidenceClient
    {
        private readonly GitHubEvidenceClientResult _result;

        public FakeGitHubEvidenceClient(GitHubEvidenceClientResult result)
        {
            _result = result;
        }

        public GitHubEvidenceClientResult Fetch(string remoteUrl, int maxItems) =>
            _result;
    }

    private sealed class FakeGhRunner
    {
        private readonly IReadOnlyDictionary<string, string> _apiResponses;

        public FakeGhRunner(IReadOnlyDictionary<string, string> apiResponses)
        {
            _apiResponses = apiResponses;
        }

        public GitHubCliEvidenceClient.GhCommandResult Run(IReadOnlyList<string> arguments, int timeoutMs)
        {
            if (arguments.SequenceEqual(["auth", "status"]))
            {
                return GitHubCliEvidenceClient.GhCommandResult.Succeeded(string.Empty);
            }

            if (arguments.Count == 2 &&
                string.Equals(arguments[0], "api", StringComparison.Ordinal) &&
                _apiResponses.TryGetValue(arguments[1], out var output))
            {
                return GitHubCliEvidenceClient.GhCommandResult.Succeeded(output);
            }

            return GitHubCliEvidenceClient.GhCommandResult.Succeeded("[]");
        }
    }
}
