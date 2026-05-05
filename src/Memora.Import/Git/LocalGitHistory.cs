namespace Memora.Import.Git;

public sealed record LocalGitCommit(
    string Sha,
    DateTimeOffset CommittedAtUtc,
    string AuthorName,
    string AuthorEmail,
    string Subject,
    IReadOnlyList<string> ChangedFiles)
{
    public IReadOnlyList<string> ChangedFiles { get; } =
        ChangedFiles?.Where(file => !string.IsNullOrWhiteSpace(file)).Select(file => file.Trim()).Distinct(StringComparer.Ordinal).OrderBy(file => file, StringComparer.Ordinal).ToArray()
        ?? throw new ArgumentNullException(nameof(ChangedFiles));
}

public sealed record LocalGitBranch(string Name, string TargetSha);

public sealed record LocalGitTag(string Name, string TargetSha, DateTimeOffset? TaggedAtUtc);

public sealed record LocalGitChangelogSignal(string Path, string Summary);

public sealed record LocalGitHistorySnapshot(
    IReadOnlyList<LocalGitCommit> Commits,
    IReadOnlyList<LocalGitBranch> Branches,
    IReadOnlyList<LocalGitTag> Tags,
    IReadOnlyList<LocalGitChangelogSignal> ChangelogSignals,
    bool IsPartial,
    IReadOnlyList<LocalGitImportDiagnostic> Diagnostics)
{
    public IReadOnlyList<LocalGitCommit> Commits { get; } = Commits?.ToArray() ?? throw new ArgumentNullException(nameof(Commits));
    public IReadOnlyList<LocalGitBranch> Branches { get; } = Branches?.ToArray() ?? throw new ArgumentNullException(nameof(Branches));
    public IReadOnlyList<LocalGitTag> Tags { get; } = Tags?.ToArray() ?? throw new ArgumentNullException(nameof(Tags));
    public IReadOnlyList<LocalGitChangelogSignal> ChangelogSignals { get; } = ChangelogSignals?.ToArray() ?? throw new ArgumentNullException(nameof(ChangelogSignals));
    public IReadOnlyList<LocalGitImportDiagnostic> Diagnostics { get; } = Diagnostics?.ToArray() ?? throw new ArgumentNullException(nameof(Diagnostics));
}

public sealed record LocalGitHistoryReadResult(
    LocalGitHistorySnapshot? Snapshot,
    IReadOnlyList<LocalGitImportDiagnostic> Diagnostics)
{
    public IReadOnlyList<LocalGitImportDiagnostic> Diagnostics { get; } =
        Diagnostics?.ToArray() ?? throw new ArgumentNullException(nameof(Diagnostics));

    public bool IsSuccess => Snapshot is not null && !Diagnostics.Any(diagnostic => diagnostic.Severity == LocalGitImportDiagnosticSeverity.Error);

    public static LocalGitHistoryReadResult Succeeded(LocalGitHistorySnapshot snapshot) =>
        new(snapshot, snapshot.Diagnostics);

    public static LocalGitHistoryReadResult Failed(params LocalGitImportDiagnostic[] diagnostics) =>
        new(null, diagnostics);
}

public interface ILocalGitHistoryReader
{
    LocalGitHistoryReadResult Read(string repositoryPath, int maxCommits);
}
