namespace Memora.Import.GitHub;

public sealed record GitHubAccount(string Login, string? Name);

public sealed record GitHubRepositoryEntry(
    string OwnerLogin,
    string Name,
    string FullName,
    bool IsPrivate,
    string HtmlUrl,
    string CloneUrl,
    string DefaultBranch,
    DateTimeOffset? UpdatedAtUtc);

public sealed record GitHubAccountValidationResult(
    GitHubAccount? Account,
    GitHubImportDiagnostic? Error)
{
    public bool IsSuccess => Account is not null && Error is null;

    public static GitHubAccountValidationResult Succeeded(GitHubAccount account) =>
        new(account, null);

    public static GitHubAccountValidationResult Failed(GitHubImportDiagnostic error) =>
        new(null, error);
}

public sealed record GitHubRepositoryListResult(
    IReadOnlyList<GitHubRepositoryEntry> Repositories,
    GitHubImportDiagnostic? Error)
{
    public IReadOnlyList<GitHubRepositoryEntry> Repositories { get; } =
        Repositories?.ToArray() ?? throw new ArgumentNullException(nameof(Repositories));

    public bool IsSuccess => Error is null;

    public static GitHubRepositoryListResult Succeeded(IReadOnlyList<GitHubRepositoryEntry> repositories) =>
        new(repositories, null);

    public static GitHubRepositoryListResult Failed(GitHubImportDiagnostic error) =>
        new([], error);
}
