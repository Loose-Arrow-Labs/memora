namespace Memora.Import.GitHub;

public enum GitHubImportDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record GitHubImportDiagnostic(
    string Code,
    string Message,
    GitHubImportDiagnosticSeverity Severity,
    string? Path = null)
{
    public string Code { get; } = RequireValue(Code, nameof(Code));
    public string Message { get; } = RequireValue(Message, nameof(Message));
    public GitHubImportDiagnosticSeverity Severity { get; } = Severity;
    public string? Path { get; } = string.IsNullOrWhiteSpace(Path) ? null : Path.Trim();

    public static GitHubImportDiagnostic Error(string code, string message, string? path = null) =>
        new(code, message, GitHubImportDiagnosticSeverity.Error, path);

    public static GitHubImportDiagnostic Warning(string code, string message, string? path = null) =>
        new(code, message, GitHubImportDiagnosticSeverity.Warning, path);

    public static GitHubImportDiagnostic Info(string code, string message, string? path = null) =>
        new(code, message, GitHubImportDiagnosticSeverity.Info, path);

    private static string RequireValue(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
}
