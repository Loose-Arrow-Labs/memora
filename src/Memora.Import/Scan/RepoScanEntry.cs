namespace Memora.Import.Scan;

public sealed record RepoScanEntry(
    string RelativePath,
    string Extension,
    string TopLevelPath,
    long SizeBytes);

public sealed class RepoScanResult
{
    public RepoScanResult(
        string repoRootPath,
        IReadOnlyList<RepoScanEntry> entries,
        IReadOnlyList<string> excludedPaths)
    {
        RepoRootPath = repoRootPath ?? throw new ArgumentNullException(nameof(repoRootPath));
        Entries = entries?.ToArray() ?? throw new ArgumentNullException(nameof(entries));
        ExcludedPaths = excludedPaths?.ToArray() ?? throw new ArgumentNullException(nameof(excludedPaths));
    }

    public string RepoRootPath { get; }
    public IReadOnlyList<RepoScanEntry> Entries { get; }
    public IReadOnlyList<string> ExcludedPaths { get; }
}
