using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Memora.Import.Prerequisites;

public sealed class RuntimePrerequisiteChecker
{
    private static readonly Version MinGitVersion = new(2, 0);
    private static readonly Version MinGhVersion = new(2, 0);

    private readonly Func<string, string> _runVersionCommand;

    public RuntimePrerequisiteChecker()
        : this(RunVersionCommand)
    {
    }

    internal RuntimePrerequisiteChecker(Func<string, string> runVersionCommand)
    {
        _runVersionCommand = runVersionCommand ?? throw new ArgumentNullException(nameof(runVersionCommand));
    }

    public RuntimePrerequisiteResult Check()
    {
        var diagnostics = new List<RuntimePrerequisiteDiagnostic>();
        CheckTool("git", "git --version", MinGitVersion, "git.missing", "git.version.unsupported", diagnostics);
        CheckTool("gh", "gh --version", MinGhVersion, "gh.missing", "gh.version.unsupported", diagnostics);
        return new RuntimePrerequisiteResult(diagnostics);
    }

    private void CheckTool(
        string toolName,
        string versionArg,
        Version minVersion,
        string missingCode,
        string unsupportedCode,
        ICollection<RuntimePrerequisiteDiagnostic> diagnostics)
    {
        string output;
        try
        {
            output = _runVersionCommand(toolName);
        }
        catch (Exception exception) when (exception is InvalidOperationException or FileNotFoundException or System.ComponentModel.Win32Exception)
        {
            diagnostics.Add(new RuntimePrerequisiteDiagnostic(
                missingCode,
                $"Required tool '{toolName}' was not found. Install it and ensure it is on the system PATH.",
                toolName));
            return;
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            diagnostics.Add(new RuntimePrerequisiteDiagnostic(
                missingCode,
                $"Required tool '{toolName}' did not return version information.",
                toolName));
            return;
        }

        var match = Regex.Match(output, @"(\d+)\.(\d+)");
        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, out var major) ||
            !int.TryParse(match.Groups[2].Value, out var minor))
        {
            return;
        }

        var detected = new Version(major, minor);
        if (detected < minVersion)
        {
            diagnostics.Add(new RuntimePrerequisiteDiagnostic(
                unsupportedCode,
                $"Detected {toolName} version {detected} is below the minimum supported version {minVersion}.",
                toolName));
        }
    }

    private static string RunVersionCommand(string toolName)
    {
        var startInfo = new ProcessStartInfo(toolName, "--version")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException($"Unable to start '{toolName}'.");
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);
        return output;
    }
}
