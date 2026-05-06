using Memora.Core.Import;

namespace Memora.Import.Attachment;

public sealed record RepositoryAttachmentRequest
{
    public RepositoryAttachmentRequest(
        string projectId,
        RepositoryAttachmentKind kind,
        string? localPath = null,
        string? remoteUrl = null,
        string? defaultBranch = null)
    {
        ProjectId = RequireValue(projectId, nameof(projectId));
        Kind = kind;
        LocalPath = NormalizeOptional(localPath);
        RemoteUrl = NormalizeOptional(remoteUrl);
        DefaultBranch = NormalizeOptional(defaultBranch);
    }

    public string ProjectId { get; }

    public RepositoryAttachmentKind Kind { get; }

    public string? LocalPath { get; }

    public string? RemoteUrl { get; }

    public string? DefaultBranch { get; }

    private static string RequireValue(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
