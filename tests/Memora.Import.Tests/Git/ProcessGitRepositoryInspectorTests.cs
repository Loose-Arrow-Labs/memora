using System.Diagnostics;
using Memora.Import.Git;

namespace Memora.Import.Tests.Git;

public sealed class ProcessGitRepositoryInspectorTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-process-git-inspector-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Inspect_SemicolonRepositoryPath_ReturnsToplevel()
    {
        var repoPath = CreateGitRepository("repo; --version");
        var inspector = new ProcessGitRepositoryInspector();

        var result = inspector.Inspect(repoPath);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(Path.GetFullPath(repoPath), result.Inspection!.WorkingTreeRootPath);
    }

    [Fact]
    public void Inspect_QuoteAndSemicolonRepositoryPath_ReturnsToplevelOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var repoPath = CreateGitRepository("repo\"; --version");
        var inspector = new ProcessGitRepositoryInspector();

        var result = inspector.Inspect(repoPath);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(Path.GetFullPath(repoPath), result.Inspection!.WorkingTreeRootPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private string CreateGitRepository(string directoryName)
    {
        var repoPath = Path.Combine(_rootPath, directoryName);
        Directory.CreateDirectory(repoPath);
        RunGit(repoPath, ["init"]);
        return repoPath;
    }

    private static void RunGit(string workingDirectory, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(workingDirectory);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        _ = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(10000), "git command timed out.");
        Assert.Equal(0, process.ExitCode);
    }
}
