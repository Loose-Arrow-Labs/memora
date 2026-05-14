using Memora.Context.Models;
using Memora.Context.Ranking;
using Memora.Core.Artifacts;

namespace Memora.Context.Reasoning;

public sealed class ContextInclusionReasoner
{
    // Request tokens carry a base weight (taskDescription=3, focusTags=4,
    // focusArtifactIds=5). Artifact text weights are title=6, tags=5,
    // headings=3, body=1. A "strong" match requires the product to exceed
    // a title or tag hit on a task-description token: 3 * 5 = 15.
    private const int StrongKeywordMatchThreshold = 15;

    public IReadOnlyList<ContextInclusionReason> ExplainInclusion(
        ContextBundleRequest request,
        ContextLayerKind layer,
        RankedContextArtifact rankedArtifact)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rankedArtifact);

        var artifact = rankedArtifact.Artifact.Artifact;
        var reasons = new List<ContextInclusionReason>();

        switch (rankedArtifact.Artifact.Origin)
        {
            case ContextArtifactOrigin.CanonicalApproved:
                reasons.Add(new ContextInclusionReason(
                    "approved-default",
                    "Included because approved artifacts are the default context grounding in v1."));
                break;
            case ContextArtifactOrigin.DraftProposal when request.IncludeDraftArtifacts:
                reasons.Add(new ContextInclusionReason(
                    "draft-explicitly-allowed",
                    "Included because the request explicitly allows draft or proposed artifacts."));
                break;
            case ContextArtifactOrigin.DraftProposal:
                throw new InvalidOperationException(
                    $"Draft artifact '{artifact.Id}' cannot be included when drafts are not allowed.");
            case ContextArtifactOrigin.SessionSummary:
                reasons.Add(new ContextInclusionReason(
                    "noncanonical-history",
                    "Included as supporting non-canonical history rather than canonical truth."));
                break;
        }

        if (TryCreateLayerAnchorReason(layer, artifact) is { } layerReason)
        {
            reasons.Add(layerReason);
        }

        if (request.FocusArtifactIds.Contains(artifact.Id, StringComparer.Ordinal))
        {
            reasons.Add(new ContextInclusionReason(
                "explicit-focus-artifact",
                "Included because the request explicitly named this artifact.",
                [artifact.Id]));
        }

        var relatedFocusArtifactIds = rankedArtifact.RelationshipPaths
            .Where(path => path.Depth == 1)
            .Select(path => path.FocusArtifactId)
            .Concat(artifact.Links.Relationships
                .Select(relationship => relationship.TargetArtifactId)
                .Where(targetArtifactId => request.FocusArtifactIds.Contains(targetArtifactId, StringComparer.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(targetArtifactId => targetArtifactId, StringComparer.Ordinal)
            .ToArray();

        if (relatedFocusArtifactIds.Length > 0)
        {
            reasons.Add(new ContextInclusionReason(
                "related-focus-artifact",
                "Included because it has an explicit stored relationship with a focused artifact.",
                relatedFocusArtifactIds));
        }

        var traversedFocusArtifactIds = rankedArtifact.RelationshipPaths
            .Where(path => path.Depth > 1)
            .SelectMany(path => path.ArtifactIds.Skip(1))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(artifactId => artifactId, StringComparer.Ordinal)
            .ToArray();

        if (traversedFocusArtifactIds.Length > 0)
        {
            reasons.Add(new ContextInclusionReason(
                "traversed-focus-artifact",
                "Included because bounded explicit relationship traversal connects it to a focused artifact.",
                traversedFocusArtifactIds));
        }

        if (rankedArtifact.Breakdown.MilestoneRelevance > 0)
        {
            reasons.Add(new ContextInclusionReason(
                "milestone-relevance",
                "Included because it matches the same milestone markers as the request."));
        }

        if (rankedArtifact.Breakdown.DirectMatchStrength > 0)
        {
            // Honest label: token overlap is what the deterministic ranker actually
            // measures — keyword presence weighted by where the term appears
            // (title > tags > headings > body). It is not a semantic match. A
            // separate stronger label is emitted when the overlap is high enough
            // that at least one request keyword hits a title or tag, which is
            // closer to "this artifact is plausibly about the request".
            reasons.Add(new ContextInclusionReason(
                "request-keyword-overlap",
                "Included because at least one request keyword appears in the artifact title, tags, sections, or body."));

            if (rankedArtifact.Breakdown.DirectMatchStrength >= StrongKeywordMatchThreshold)
            {
                reasons.Add(new ContextInclusionReason(
                    "request-keyword-strong-match",
                    "Included because a request keyword appears in the artifact title or tags, not only in the body."));
            }
        }

        return reasons;
    }

    private static ContextInclusionReason? TryCreateLayerAnchorReason(ContextLayerKind layer, ArtifactDocument artifact) =>
        (layer, artifact.Type) switch
        {
            (ContextLayerKind.Layer1, ArtifactType.Charter) => new ContextInclusionReason(
                "layer1-charter-anchor",
                "Included as the project charter anchor for Layer 1 context."),
            (ContextLayerKind.Layer1, ArtifactType.Plan) when artifact is PlanArtifact { Active: true } => new ContextInclusionReason(
                "layer1-active-plan-anchor",
                "Included as the active plan anchor for Layer 1 context."),
            (ContextLayerKind.Layer1, ArtifactType.RepoStructure) => new ContextInclusionReason(
                "layer1-repo-anchor",
                "Included as the repository snapshot anchor for Layer 1 context."),
            (ContextLayerKind.Layer3, ArtifactType.SessionSummary) => new ContextInclusionReason(
                "layer3-supporting-history",
                "Included as supporting history for on-demand Layer 3 context."),
            _ => null
        };
}
