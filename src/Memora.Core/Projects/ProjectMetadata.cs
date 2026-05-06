using Memora.Core.Import;

namespace Memora.Core.Projects;

public sealed record ProjectMetadata
{
    public ProjectMetadata(
        string projectId,
        string name,
        string? status,
        IReadOnlyList<ProjectRepositoryAttachment>? repositoryAttachments = null)
    {
        ProjectId = RequireValue(projectId, nameof(projectId));
        Name = RequireValue(name, nameof(name));
        Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        RepositoryAttachments = repositoryAttachments?.ToArray() ?? [];
    }

    public string ProjectId { get; }

    public string Name { get; }

    public string? Status { get; }

    public IReadOnlyList<ProjectRepositoryAttachment> RepositoryAttachments { get; }

    private static string RequireValue(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
}
