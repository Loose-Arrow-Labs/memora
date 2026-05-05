using System.Diagnostics;

namespace Memora.Import.Git;

public sealed class ProcessGitRepositoryInspector : IGitRepositoryInspector
{
    public GitRepositoryInspectionResult Inspect(string localPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);

        if (!Directory.Exists(localPath))
        {
            return GitRepositoryInspectionResult.Failed(
                "attachment.repo.missing",
                $"Repository path '{localPath}' was not found.");
        }

        var insideWorkTree = RunGit(localPath, "rev-parse --is-inside-work-tree");
        if (!insideWorkTree.IsSuccess || !string.Equals(insideWorkTree.Output.Trim(), "true", StringComparison.OrdinalIgnoreCase))
        {
            return GitRepositoryInspectionResult.Failed(
                "attachment.git_metadata.missing",
                $"Repository path '{localPath}' does not contain readable Git metadata.");
        }

        var root = RunGit(localPath, "rev-parse --show-toplevel");
        if (!root.IsSuccess)
        {
            return GitRepositoryInspectionResult.Failed("attachment.git_command.failed", root.Error);
        }

        var branch = RunGit(localPath, "symbolic-ref --short HEAD");
        var defaultBranch = branch.IsSuccess && !string.IsNullOrWhiteSpace(branch.Output)
            ? branch.Output.Trim()
            : "main";

        var originUrl = RunGit(localPath, "remote get-url origin");

        return GitRepositoryInspectionResult.Succeeded(
            new GitRepositoryInspection(
                Path.GetFullPath(root.Output.Trim()),
                defaultBranch,
                originUrl.IsSuccess ? "origin" : null,
                originUrl.IsSuccess ? originUrl.Output.Trim() : null));
    }

    private static GitCommandResult RunGit(string workingDirectory, string arguments)
    {
        using var process = Process.Start(
            new ProcessStartInfo("git", $"-C \"{workingDirectory}\" {arguments}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

        if (process is null)
        {
            return GitCommandResult.Failed("Unable to start git.");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        if (!process.WaitForExit(10000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            return GitCommandResult.Failed("Git command timed out.");
        }

        return process.ExitCode == 0
            ? GitCommandResult.Succeeded(output)
            : GitCommandResult.Failed(string.IsNullOrWhiteSpace(error) ? output : error);
    }

    private sealed record GitCommandResult(bool IsSuccess, string Output, string Error)
    {
        public static GitCommandResult Succeeded(string output) => new(true, output, string.Empty);

        public static GitCommandResult Failed(string error) => new(false, string.Empty, error.Trim());
    }
}
