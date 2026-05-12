using Memora.Import.Scan;

namespace Memora.Import.Tests.Scan;

public sealed class DeterministicRepoScannerTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-scan-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Scan_EmptyDirectory_ReturnsNoEntries()
    {
        Directory.CreateDirectory(_rootPath);
        var scanner = new DeterministicRepoScanner();

        var result = scanner.Scan(_rootPath);

        Assert.Empty(result.Entries);
    }

    [Fact]
    public void Scan_MissingDirectory_ReturnsEmpty()
    {
        var scanner = new DeterministicRepoScanner();

        var result = scanner.Scan(Path.Combine(_rootPath, "does-not-exist"));

        Assert.Empty(result.Entries);
        Assert.Empty(result.ExcludedPaths);
    }

    [Fact]
    public void Scan_SourceAndDocFiles_AreIncluded()
    {
        CreateFile("src/app.cs", "source");
        CreateFile("README.md", "docs");
        CreateFile(".github/workflows/ci.yml", "ci");
        var scanner = new DeterministicRepoScanner();

        var result = scanner.Scan(_rootPath);

        var paths = result.Entries.Select(e => e.RelativePath).ToArray();
        Assert.Contains(".github/workflows/ci.yml", paths);
        Assert.Contains("README.md", paths);
        Assert.Contains("src/app.cs", paths);
    }

    [Fact]
    public void Scan_BinAndNodeModules_AreExcluded()
    {
        CreateFile("src/app.cs", "source");
        CreateFile("bin/Debug/app.dll", "binary");
        CreateFile("node_modules/pkg/index.js", "dep");
        var scanner = new DeterministicRepoScanner();

        var result = scanner.Scan(_rootPath);

        var paths = result.Entries.Select(e => e.RelativePath).ToArray();
        Assert.Contains("src/app.cs", paths);
        Assert.DoesNotContain("bin/Debug/app.dll", paths);
        Assert.DoesNotContain("node_modules/pkg/index.js", paths);
        Assert.Contains(result.ExcludedPaths, p => p.StartsWith("bin/", StringComparison.Ordinal));
        Assert.Contains(result.ExcludedPaths, p => p.StartsWith("node_modules/", StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_BinaryExtensions_AreExcluded()
    {
        CreateFile("app.exe", string.Empty);
        CreateFile("lib.dll", string.Empty);
        CreateFile("archive.zip", string.Empty);
        CreateFile("README.md", "docs");
        var scanner = new DeterministicRepoScanner();

        var result = scanner.Scan(_rootPath);

        var paths = result.Entries.Select(e => e.RelativePath).ToArray();
        Assert.Contains("README.md", paths);
        Assert.DoesNotContain("app.exe", paths);
        Assert.DoesNotContain("lib.dll", paths);
        Assert.DoesNotContain("archive.zip", paths);
    }

    [Fact]
    public void Scan_DotEnvFile_IsExcluded()
    {
        CreateFile(".env", "SECRET=value");
        CreateFile(".env.example", "SECRET=example");
        var scanner = new DeterministicRepoScanner();

        var result = scanner.Scan(_rootPath);

        var paths = result.Entries.Select(e => e.RelativePath).ToArray();
        Assert.DoesNotContain(".env", paths);
        Assert.Contains(".env.example", paths);
    }

    [Fact]
    public void Scan_OrderingIsStableAcrossRepeatedRuns()
    {
        CreateFile("z/file.cs", string.Empty);
        CreateFile("a/file.cs", string.Empty);
        CreateFile("m/file.cs", string.Empty);
        var scanner = new DeterministicRepoScanner();

        var first = scanner.Scan(_rootPath).Entries.Select(e => e.RelativePath).ToArray();
        var second = scanner.Scan(_rootPath).Entries.Select(e => e.RelativePath).ToArray();

        Assert.Equal(first, second);
        Assert.Equal(["a/file.cs", "m/file.cs", "z/file.cs"], first);
    }

    [Fact]
    public void Scan_EntriesHaveCorrectTopLevelPath()
    {
        CreateFile("src/core/models.cs", string.Empty);
        CreateFile("docs/guide.md", string.Empty);
        var scanner = new DeterministicRepoScanner();

        var result = scanner.Scan(_rootPath);

        Assert.Contains(result.Entries, e => e.RelativePath == "src/core/models.cs" && e.TopLevelPath == "src");
        Assert.Contains(result.Entries, e => e.RelativePath == "docs/guide.md" && e.TopLevelPath == "docs");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private void CreateFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }
}
