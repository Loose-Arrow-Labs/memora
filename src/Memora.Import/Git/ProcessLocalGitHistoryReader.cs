using System.Diagnostics;

namespace Memora.Import.Git;

public sealed class ProcessLocalGitHistoryReader : ILocalGitHistoryReader
{
    public LocalGitHistoryReadResult Read(string repositoryPath, int maxCommits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        if (!Directory.Exists(repositoryPath))
        {
            return LocalGitHistoryReadResult.Failed(
                LocalGitImportDiagnostic.Error(
                    "local_git.repo.unsupported",
                    $"Repository path '{repositoryPath}' was not found.",
                    "repository_path"));
        }

        var insideWorkTree = RunGit(repositoryPath, "rev-parse --is-inside-work-tree");
        if (!insideWorkTree.IsSuccess || !string.Equals(insideWorkTree.Output.Trim(), "true", StringComparison.OrdinalIgnoreCase))
        {
            return LocalGitHistoryReadResult.Failed(
                LocalGitImportDiagnostic.Error(
                    "local_git.metadata.missing",
                    $"Repository path '{repositoryPath}' does not contain readable Git metadata.",
                    "repository_path"));
        }

        var diagnostics = new List<LocalGitImportDiagnostic>();
        var commits = ReadCommits(repositoryPath, maxCommits, diagnostics);
        var branches = ReadBranches(repositoryPath, diagnostics);
        var tags = ReadTags(repositoryPath, diagnostics);
        var changelogSignals = ReadChangelogSignals(repositoryPath);

        var isPartial = commits.Count >= maxCommits;
        if (isPartial)
        {
            diagnostics.Add(
                LocalGitImportDiagnostic.Warning(
                    "local_git.import.partial",
                    $"Commit import reached the configured bound of {maxCommits} commits.",
                    "max_commits"));
        }

        return LocalGitHistoryReadResult.Succeeded(
            new LocalGitHistorySnapshot(
                commits,
                branches,
                tags,
                changelogSignals,
                isPartial,
                diagnostics));
    }

    private static IReadOnlyList<LocalGitCommit> ReadCommits(
        string repositoryPath,
        int maxCommits,
        List<LocalGitImportDiagnostic> diagnostics)
    {
        var result = RunGit(
            repositoryPath,
            $"log --max-count={maxCommits} --date=iso-strict --pretty=format:%H%x1f%aI%x1f%an%x1f%ae%x1f%s --name-only");
        if (!result.IsSuccess)
        {
            diagnostics.Add(LocalGitImportDiagnostic.Error("local_git.command.failed", result.Error, "git log"));
            return [];
        }

        var commits = new List<LocalGitCommit>();
        var currentHeader = Array.Empty<string>();
        var changedFiles = new List<string>();

        foreach (var line in result.Output.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.Contains('\u001f', StringComparison.Ordinal))
            {
                AddCurrentCommit(commits, currentHeader, changedFiles);
                currentHeader = trimmed.Split('\u001f');
                changedFiles = [];
                continue;
            }

            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                changedFiles.Add(trimmed.Trim());
            }
        }

        AddCurrentCommit(commits, currentHeader, changedFiles);
        return commits;
    }

    private static void AddCurrentCommit(
        ICollection<LocalGitCommit> commits,
        IReadOnlyList<string> header,
        IReadOnlyList<string> changedFiles)
    {
        if (header.Count < 5 || !DateTimeOffset.TryParse(header[1], out var committedAtUtc))
        {
            return;
        }

        commits.Add(
            new LocalGitCommit(
                header[0],
                committedAtUtc,
                header[2],
                header[3],
                header[4],
                changedFiles));
    }

    private static IReadOnlyList<LocalGitBranch> ReadBranches(
        string repositoryPath,
        List<LocalGitImportDiagnostic> diagnostics)
    {
        var result = RunGit(repositoryPath, "for-each-ref refs/heads refs/remotes --format=%(refname:short)%09%(objectname)");
        if (!result.IsSuccess)
        {
            diagnostics.Add(LocalGitImportDiagnostic.Warning("local_git.branches.partial", result.Error, "git for-each-ref branches"));
            return [];
        }

        return result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('\t'))
            .Where(parts => parts.Length == 2 && !parts[0].EndsWith("/HEAD", StringComparison.Ordinal))
            .Select(parts => new LocalGitBranch(parts[0], parts[1]))
            .OrderBy(branch => branch.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<LocalGitTag> ReadTags(
        string repositoryPath,
        List<LocalGitImportDiagnostic> diagnostics)
    {
        var result = RunGit(repositoryPath, "for-each-ref refs/tags --format=%(refname:short)%09%(objectname)%09%(creatordate:iso-strict)");
        if (!result.IsSuccess)
        {
            diagnostics.Add(LocalGitImportDiagnostic.Warning("local_git.tags.partial", result.Error, "git for-each-ref tags"));
            return [];
        }

        return result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('\t'))
            .Where(parts => parts.Length >= 2)
            .Select(parts => new LocalGitTag(
                parts[0],
                parts[1],
                parts.Length >= 3 && DateTimeOffset.TryParse(parts[2], out var taggedAtUtc) ? taggedAtUtc : null))
            .OrderBy(tag => tag.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<LocalGitChangelogSignal> ReadChangelogSignals(string repositoryPath)
    {
        var root = Path.GetFullPath(repositoryPath);
        return Directory
            .EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                var name = Path.GetFileName(path);
                return name.StartsWith("CHANGELOG", StringComparison.OrdinalIgnoreCase) ||
                       name.StartsWith("RELEASE", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new LocalGitChangelogSignal(
                Path.GetRelativePath(root, path).Replace('\\', '/'),
                "Repository contains a release or changelog signal."))
            .ToArray();
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

        if (!process.WaitForExit(15000))
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
