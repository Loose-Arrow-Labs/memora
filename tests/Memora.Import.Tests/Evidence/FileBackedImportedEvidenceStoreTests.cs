using Memora.Core.Import;
using Memora.Import.Evidence;

namespace Memora.Import.Tests.Evidence;

public sealed class FileBackedImportedEvidenceStoreTests : IDisposable
{
    private readonly string _workspaceRootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-evidence-store-tests",
        Guid.NewGuid().ToString("N"));

    private readonly FileBackedImportedEvidenceStore _store = new();

    [Fact]
    public async Task Save_ConcurrentDuplicateEvidence_AllowsOnlyOneWriterAndLeavesNoTempFiles()
    {
        Directory.CreateDirectory(_workspaceRootPath);
        var request = new ProjectEvidenceWriteRequest(_workspaceRootPath, [CreateRecord("commit-001")]);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 16)
                .Select(_ => Task.Run(() => _store.Save(request))));

        Assert.Equal(1, results.Sum(result => result.CreatedCount));
        Assert.Equal(15, results.Sum(result => result.ExistingCount));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(_workspaceRootPath, "evidence"), "commit-001.json", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(_workspaceRootPath, "*.tmp", SearchOption.AllDirectories));
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspaceRootPath))
        {
            Directory.Delete(_workspaceRootPath, recursive: true);
        }
    }

    private static ImportedEvidenceRecord CreateRecord(string stableId) =>
        new(
            stableId,
            "memora",
            ImportedEvidenceSourceType.LocalGitCommit,
            "ATT-LOCAL",
            "local:/repo",
            "abc123",
            "Implement storage",
            "Changed persistence behavior.",
            new DateTimeOffset(2026, 5, 7, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 7, 8, 5, 0, TimeSpan.Zero),
            "git commit abc123",
            ImportedEvidenceTrustState.BaselineEvidence,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["changed_files"] = "src/Memora.Storage/Persistence/ArtifactFileStore.cs"
            });
}
