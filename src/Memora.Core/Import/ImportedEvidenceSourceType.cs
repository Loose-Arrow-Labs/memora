namespace Memora.Core.Import;

public enum ImportedEvidenceSourceType
{
    LocalGitCommit,
    LocalGitBranch,
    LocalGitTag,
    LocalGitChangelogSignal
}

public static class ImportedEvidenceSourceTypeExtensions
{
    public static string ToSchemaValue(this ImportedEvidenceSourceType sourceType) =>
        sourceType switch
        {
            ImportedEvidenceSourceType.LocalGitCommit => "local_git_commit",
            ImportedEvidenceSourceType.LocalGitBranch => "local_git_branch",
            ImportedEvidenceSourceType.LocalGitTag => "local_git_tag",
            ImportedEvidenceSourceType.LocalGitChangelogSignal => "local_git_changelog_signal",
            _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, "Unknown imported evidence source type.")
        };

    public static bool TryParseSchemaValue(string? value, out ImportedEvidenceSourceType sourceType)
    {
        sourceType = ImportedEvidenceSourceType.LocalGitCommit;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim())
        {
            case "local_git_commit":
                sourceType = ImportedEvidenceSourceType.LocalGitCommit;
                return true;
            case "local_git_branch":
                sourceType = ImportedEvidenceSourceType.LocalGitBranch;
                return true;
            case "local_git_tag":
                sourceType = ImportedEvidenceSourceType.LocalGitTag;
                return true;
            case "local_git_changelog_signal":
                sourceType = ImportedEvidenceSourceType.LocalGitChangelogSignal;
                return true;
            default:
                return false;
        }
    }
}
