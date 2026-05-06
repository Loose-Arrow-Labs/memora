namespace Memora.Core.Import;

public sealed record ProjectRepositoryAttachment
{
    public ProjectRepositoryAttachment(
        string attachmentId,
        string projectId,
        RepositoryAttachmentKind kind,
        string repositoryIdentity,
        string? localPath,
        string? remoteUrl,
        string defaultBranch,
        string? originRemoteName,
        string? originUrl,
        DateTimeOffset attachedAtUtc)
    {
        AttachmentId = RequireValue(attachmentId, nameof(attachmentId));
        ProjectId = RequireValue(projectId, nameof(projectId));
        Kind = kind;
        RepositoryIdentity = RequireValue(repositoryIdentity, nameof(repositoryIdentity));
        LocalPath = NormalizeOptional(localPath);
        RemoteUrl = NormalizeOptional(remoteUrl);
        DefaultBranch = RequireValue(defaultBranch, nameof(defaultBranch));
        OriginRemoteName = NormalizeOptional(originRemoteName);
        OriginUrl = NormalizeOptional(originUrl);
        AttachedAtUtc = attachedAtUtc;
    }

    public string AttachmentId { get; }

    public string ProjectId { get; }

    public RepositoryAttachmentKind Kind { get; }

    public string RepositoryIdentity { get; }

    public string? LocalPath { get; }

    public string? RemoteUrl { get; }

    public string DefaultBranch { get; }

    public string? OriginRemoteName { get; }

    public string? OriginUrl { get; }

    public DateTimeOffset AttachedAtUtc { get; }

    private static string RequireValue(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
