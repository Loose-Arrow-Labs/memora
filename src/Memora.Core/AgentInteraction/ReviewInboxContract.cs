using Memora.Core.Artifacts;

namespace Memora.Core.AgentInteraction;

public sealed record ReviewInboxResponse(
    string ProjectId,
    IReadOnlyList<ReviewInboxItem> Items,
    IReadOnlyList<AgentInteractionError> Errors)
    : AgentInteractionResponse(Errors)
{
    public string ProjectId { get; } = AgentInteractionContractHelpers.RequireValue(ProjectId, nameof(ProjectId), "Project id is required.");
    public IReadOnlyList<ReviewInboxItem> Items { get; } = Items?.ToArray() ?? throw new ArgumentNullException(nameof(Items));
}

public sealed record ReviewArtifactPreviewResponse(
    string ProjectId,
    ReviewInboxItem? Item,
    string? Body,
    IReadOnlyDictionary<string, string> Sections,
    IReadOnlyList<AgentInteractionError> Errors)
    : AgentInteractionResponse(Errors)
{
    public string ProjectId { get; } = AgentInteractionContractHelpers.RequireValue(ProjectId, nameof(ProjectId), "Project id is required.");
    public IReadOnlyDictionary<string, string> Sections { get; } =
        Sections is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(Sections, StringComparer.Ordinal);
}

public sealed record ReviewDecisionRequest(
    string RelativePath,
    string Decision)
{
    public string RelativePath { get; } = AgentInteractionContractHelpers.RequireValue(RelativePath, nameof(RelativePath), "Relative path is required.");
    public string Decision { get; } = AgentInteractionContractHelpers.RequireValue(Decision, nameof(Decision), "Decision is required.").ToLowerInvariant();
}

public sealed record ReviewDecisionResponse(
    string ProjectId,
    string Decision,
    ReviewInboxItem? Item,
    string? Message,
    IReadOnlyList<AgentInteractionError> Errors)
    : AgentInteractionResponse(Errors)
{
    public string ProjectId { get; } = AgentInteractionContractHelpers.RequireValue(ProjectId, nameof(ProjectId), "Project id is required.");
    public string Decision { get; } = AgentInteractionContractHelpers.RequireValue(Decision, nameof(Decision), "Decision is required.").ToLowerInvariant();
    public string? Message { get; } = string.IsNullOrWhiteSpace(Message) ? null : Message.Trim();
}

public sealed record ReviewInboxItem(
    string ArtifactId,
    ArtifactType ArtifactType,
    ArtifactStatus Status,
    string Title,
    int Revision,
    string Provenance,
    string Reason,
    string RelativePath,
    string FilePath,
    string ValidationState,
    DateTimeOffset UpdatedAtUtc)
{
    public string ArtifactId { get; } = AgentInteractionContractHelpers.RequireValue(ArtifactId, nameof(ArtifactId), "Artifact id is required.");
    public string Title { get; } = AgentInteractionContractHelpers.RequireValue(Title, nameof(Title), "Title is required.");
    public string Provenance { get; } = AgentInteractionContractHelpers.RequireValue(Provenance, nameof(Provenance), "Provenance is required.");
    public string Reason { get; } = AgentInteractionContractHelpers.RequireValue(Reason, nameof(Reason), "Reason is required.");
    public string RelativePath { get; } = AgentInteractionContractHelpers.RequireValue(RelativePath, nameof(RelativePath), "Relative path is required.");
    public string FilePath { get; } = AgentInteractionContractHelpers.RequireValue(FilePath, nameof(FilePath), "File path is required.");
    public string ValidationState { get; } = AgentInteractionContractHelpers.RequireValue(ValidationState, nameof(ValidationState), "Validation state is required.");
}

public interface IReviewInboxService
{
    ReviewInboxResponse GetReviewInbox(string projectId);

    ReviewArtifactPreviewResponse GetReviewArtifactPreview(string projectId, string relativePath);

    ReviewDecisionResponse ApplyReviewDecision(string projectId, ReviewDecisionRequest request);
}
