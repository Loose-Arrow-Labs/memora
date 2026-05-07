using Memora.Core.AgentInteraction;
using Memora.Core.Artifacts;

namespace Memora.Api.Services;

internal sealed class UnavailableAgentInteractionService : IAgentInteractionService, IReviewInboxService
{
    public ProjectLookupResponse GetProject(string projectId) =>
        new(
            projectId,
            null,
            null,
            [new AgentInteractionError("project.not_found", $"Project '{projectId}' was not found.", "project_id")]);

    public GetContextResponse GetContext(GetContextRequest request) =>
        new(
            null,
            [new AgentInteractionError("context.not_configured", "Context assembly service is not configured.", "service")]);

    public ProposalResponse ProposeArtifact(ProposeArtifactRequest request) =>
        new(
            request.ProjectId,
            request.ArtifactId,
            request.ArtifactType,
            ArtifactStatus.Proposed,
            0,
            [new AgentInteractionError("proposal.not_configured", "Proposal submission service is not configured.", "service")]);

    public ProposalResponse ProposeUpdate(ProposeUpdateRequest request) =>
        new(
            request.ProjectId,
            request.ArtifactId,
            ArtifactType.Plan,
            ArtifactStatus.Proposed,
            0,
            [new AgentInteractionError("proposal.not_configured", "Proposal submission service is not configured.", "service")]);

    public OutcomeResponse RecordOutcome(RecordOutcomeRequest request) =>
        new(
            request.ProjectId,
            request.ArtifactId,
            ArtifactStatus.Proposed,
            0,
            OutcomeKind.Mixed,
            [new AgentInteractionError("outcome.not_configured", "Outcome recording service is not configured.", "service")]);

    public ReviewInboxResponse GetReviewInbox(string projectId) =>
        new(
            projectId,
            [],
            [new AgentInteractionError("review.not_configured", "Review inbox service is not configured.", "service")]);

    public ReviewArtifactPreviewResponse GetReviewArtifactPreview(string projectId, string relativePath) =>
        new(
            projectId,
            null,
            null,
            new Dictionary<string, string>(StringComparer.Ordinal),
            [new AgentInteractionError("review.not_configured", "Review inbox service is not configured.", "service")]);

    public ReviewDecisionResponse ApplyReviewDecision(string projectId, ReviewDecisionRequest request) =>
        new(
            projectId,
            request.Decision,
            null,
            null,
            [new AgentInteractionError("review.not_configured", "Review decision service is not configured.", "service")]);
}
