namespace Memora.Import.Git;

public sealed record GitRepositoryInspection(
    string WorkingTreeRootPath,
    string DefaultBranch,
    string? OriginRemoteName,
    string? OriginUrl)
{
    public string WorkingTreeRootPath { get; } = RequireValue(WorkingTreeRootPath, nameof(WorkingTreeRootPath));
    public string DefaultBranch { get; } = RequireValue(DefaultBranch, nameof(DefaultBranch));
    public string? OriginRemoteName { get; } = string.IsNullOrWhiteSpace(OriginRemoteName) ? null : OriginRemoteName.Trim();
    public string? OriginUrl { get; } = string.IsNullOrWhiteSpace(OriginUrl) ? null : OriginUrl.Trim();

    private static string RequireValue(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
}

public sealed record GitRepositoryInspectionResult(
    GitRepositoryInspection? Inspection,
    string? ErrorCode,
    string? ErrorMessage)
{
    public bool IsSuccess => Inspection is not null && ErrorCode is null;

    public static GitRepositoryInspectionResult Succeeded(GitRepositoryInspection inspection) =>
        new(inspection, null, null);

    public static GitRepositoryInspectionResult Failed(string errorCode, string errorMessage) =>
        new(null, errorCode, errorMessage);
}

public interface IGitRepositoryInspector
{
    GitRepositoryInspectionResult Inspect(string localPath);
}
