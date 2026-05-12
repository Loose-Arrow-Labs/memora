namespace Memora.Import.Scan;

public static class RepoIntakeBoundary
{
    // Directory names that are pruned before recursion (never traversed).
    public static readonly IReadOnlySet<string> ExcludedDirectoryNames = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "dist", "out", "build", "target",
        ".gradle", "node_modules", "vendor", "packages", ".cargo",
        "__pycache__", ".vs", ".idea",
        ".git", "evidence", "canonical", "drafts", "summaries", "indexes"
    };

    // File extensions that are excluded (binary, compiled, archive, credential formats).
    public static readonly IReadOnlySet<string> ExcludedExtensions = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        ".dll", ".exe", ".so", ".dylib", ".pdb",
        ".zip", ".tar", ".gz", ".bz2", ".xz", ".rar", ".7z",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg", ".webp",
        ".mp4", ".mov", ".avi", ".mkv",
        ".pdf",
        ".pem", ".key", ".p12", ".pfx", ".cer", ".crt"
    };

    // File name patterns that are excluded regardless of extension.
    private static readonly IReadOnlySet<string> ExcludedFileNames = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        ".env"
    };

    // Path segment substrings that mark a file as generated (excluded).
    private static readonly string[] GeneratedPathSegments =
    [
        "/generated/", "\\generated\\",
        ".g.cs", ".generated.", ".designer.cs"
    ];

    public static bool IsDirectoryExcluded(string directoryName) =>
        ExcludedDirectoryNames.Contains(directoryName);

    public static bool IsFileExcluded(string fileName, string extension)
    {
        if (ExcludedFileNames.Contains(fileName))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(extension) && ExcludedExtensions.Contains(extension))
        {
            return true;
        }

        return false;
    }

    public static bool IsPathGenerated(string relativePath)
    {
        foreach (var segment in GeneratedPathSegments)
        {
            if (relativePath.Contains(segment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
