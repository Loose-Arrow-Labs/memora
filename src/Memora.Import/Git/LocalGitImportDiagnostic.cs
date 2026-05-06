namespace Memora.Import.Git;

public enum LocalGitImportDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record LocalGitImportDiagnostic(
    string Code,
    string Message,
    LocalGitImportDiagnosticSeverity Severity,
    string? Path = null)
{
    public string Code { get; } = RequireValue(Code, nameof(Code));
    public string Message { get; } = RequireValue(Message, nameof(Message));
    public LocalGitImportDiagnosticSeverity Severity { get; } = Severity;
    public string? Path { get; } = string.IsNullOrWhiteSpace(Path) ? null : Path.Trim();

    public static LocalGitImportDiagnostic Error(string code, string message, string? path = null) =>
        new(code, message, LocalGitImportDiagnosticSeverity.Error, path);

    public static LocalGitImportDiagnostic Warning(string code, string message, string? path = null) =>
        new(code, message, LocalGitImportDiagnosticSeverity.Warning, path);

    public static LocalGitImportDiagnostic Info(string code, string message, string? path = null) =>
        new(code, message, LocalGitImportDiagnosticSeverity.Info, path);

    private static string RequireValue(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
}
