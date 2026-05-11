namespace Memora.Import.Scan;

public sealed class DeterministicRepoScanner
{
    private readonly Func<string, IEnumerable<string>> _enumerateFiles;
    private readonly Func<string, long> _getFileSize;

    public DeterministicRepoScanner()
        : this(EnumerateFiles, GetFileSize)
    {
    }

    internal DeterministicRepoScanner(
        Func<string, IEnumerable<string>> enumerateFiles,
        Func<string, long> getFileSize)
    {
        _enumerateFiles = enumerateFiles ?? throw new ArgumentNullException(nameof(enumerateFiles));
        _getFileSize = getFileSize ?? throw new ArgumentNullException(nameof(getFileSize));
    }

    public RepoScanResult Scan(string repoRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRootPath);

        var root = Path.GetFullPath(repoRootPath);
        if (!Directory.Exists(root))
        {
            return new RepoScanResult(root, [], []);
        }

        var entries = new List<RepoScanEntry>();
        var excludedPaths = new List<string>();

        ScanDirectory(root, root, entries, excludedPaths);

        var orderedEntries = entries
            .OrderBy(e => e.RelativePath, StringComparer.Ordinal)
            .ToArray();

        return new RepoScanResult(root, orderedEntries, excludedPaths);
    }

    private void ScanDirectory(
        string directoryPath,
        string rootPath,
        ICollection<RepoScanEntry> entries,
        ICollection<string> excludedPaths)
    {
        string[] subDirectories;
        string[] files;

        try
        {
            subDirectories = Directory.GetDirectories(directoryPath);
            files = Directory.GetFiles(directoryPath);
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }

        foreach (var file in files.OrderBy(f => f, StringComparer.Ordinal))
        {
            var fileName = Path.GetFileName(file);
            var extension = Path.GetExtension(file);
            var relativePath = NormalizeRelativePath(Path.GetRelativePath(rootPath, file));

            if (RepoIntakeBoundary.IsFileExcluded(fileName, extension) ||
                RepoIntakeBoundary.IsPathGenerated(relativePath))
            {
                excludedPaths.Add(relativePath);
                continue;
            }

            long sizeBytes;
            try
            {
                sizeBytes = _getFileSize(file);
            }
            catch (IOException)
            {
                continue;
            }

            var topLevel = GetTopLevelPath(relativePath);
            entries.Add(new RepoScanEntry(relativePath, extension, topLevel, sizeBytes));
        }

        foreach (var subDir in subDirectories.OrderBy(d => d, StringComparer.Ordinal))
        {
            var dirName = Path.GetFileName(subDir);
            if (RepoIntakeBoundary.IsDirectoryExcluded(dirName))
            {
                var relativeDir = NormalizeRelativePath(Path.GetRelativePath(rootPath, subDir));
                excludedPaths.Add(relativeDir + "/");
                continue;
            }

            ScanDirectory(subDir, rootPath, entries, excludedPaths);
        }
    }

    private static string NormalizeRelativePath(string path) =>
        path.Replace('\\', '/');

    private static string GetTopLevelPath(string relativePath)
    {
        var slashIndex = relativePath.IndexOf('/', StringComparison.Ordinal);
        return slashIndex < 0 ? relativePath : relativePath[..slashIndex];
    }

    private static IEnumerable<string> EnumerateFiles(string path) =>
        Directory.GetFiles(path);

    private static long GetFileSize(string path) =>
        new FileInfo(path).Length;
}
