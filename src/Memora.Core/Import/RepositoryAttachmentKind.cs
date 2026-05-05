namespace Memora.Core.Import;

public enum RepositoryAttachmentKind
{
    LocalGit,
    GitHub
}

public static class RepositoryAttachmentKindExtensions
{
    public static string ToSchemaValue(this RepositoryAttachmentKind kind) =>
        kind switch
        {
            RepositoryAttachmentKind.LocalGit => "local_git",
            RepositoryAttachmentKind.GitHub => "github",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown repository attachment kind.")
        };

    public static bool TryParseSchemaValue(string? value, out RepositoryAttachmentKind kind)
    {
        kind = RepositoryAttachmentKind.LocalGit;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim())
        {
            case "local_git":
                kind = RepositoryAttachmentKind.LocalGit;
                return true;
            case "github":
                kind = RepositoryAttachmentKind.GitHub;
                return true;
            default:
                return false;
        }
    }
}
