using Memora.Context.Models;
using Memora.Context.Ranking;
using Memora.Context.Reasoning;
using Memora.Core.Artifacts;

namespace Memora.Context.Tests.Reasoning;

public sealed class ContextInclusionReasonerTests
{
    private readonly ContextInclusionReasoner _reasoner = new();

    [Fact]
    public void ExplainInclusion_AddsApprovedDefaultAndLayerAnchorReasons()
    {
        var request = new ContextBundleRequest("memora", "Need Layer 1 context.");
        var rankedArtifact = CreateRankedArtifact(
            new ContextBundleArtifact(CreateActivePlanArtifact(ArtifactStatus.Approved), ContextArtifactOrigin.CanonicalApproved),
            milestoneRelevance: 0,
            directMatchStrength: 0);

        var reasons = _reasoner.ExplainInclusion(request, ContextLayerKind.Layer1, rankedArtifact);

        Assert.Equal(["approved-default", "layer1-active-plan-anchor"], reasons.Select(reason => reason.Code));
    }

    [Fact]
    public void ExplainInclusion_RejectsDraftArtifactsWhenDraftsAreNotAllowed()
    {
        var request = new ContextBundleRequest("memora", "Need context.");
        var rankedArtifact = CreateRankedArtifact(
            new ContextBundleArtifact(CreateActivePlanArtifact(ArtifactStatus.Draft), ContextArtifactOrigin.DraftProposal),
            milestoneRelevance: 0,
            directMatchStrength: 0);

        var exception = Assert.Throws<InvalidOperationException>(
            () => _reasoner.ExplainInclusion(request, ContextLayerKind.Layer2, rankedArtifact));

        Assert.Contains("cannot be included", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplainInclusion_AddsDraftAllowanceReasonWhenRequestAllowsDrafts()
    {
        var request = new ContextBundleRequest("memora", "Need context.", includeDraftArtifacts: true);
        var rankedArtifact = CreateRankedArtifact(
            new ContextBundleArtifact(CreateActivePlanArtifact(ArtifactStatus.Draft), ContextArtifactOrigin.DraftProposal),
            milestoneRelevance: 0,
            directMatchStrength: 0);

        var reasons = _reasoner.ExplainInclusion(request, ContextLayerKind.Layer2, rankedArtifact);

        Assert.Equal("draft-explicitly-allowed", reasons[0].Code);
    }

    [Fact]
    public void ExplainInclusion_AddsExplicitFocusAndRelationshipReasons()
    {
        var request = new ContextBundleRequest("memora", "Need context.", focusArtifactIds: ["ADR-100", "PLN-001"]);
        var artifact = new ContextBundleArtifact(
            CreateDecisionArtifact("ADR-100", ArtifactStatus.Approved, new ArtifactLinks(["PLN-001"], [], [], [])),
            ContextArtifactOrigin.CanonicalApproved);
        var rankedArtifact = CreateRankedArtifact(artifact, milestoneRelevance: 0, directMatchStrength: 0);

        var reasons = _reasoner.ExplainInclusion(request, ContextLayerKind.Layer2, rankedArtifact);

        Assert.Contains(reasons, reason => reason.Code == "explicit-focus-artifact");
        Assert.Contains(reasons, reason => reason.Code == "related-focus-artifact" && reason.RelatedArtifactIds.SequenceEqual(["PLN-001"]));
    }

    [Fact]
    public void ExplainInclusion_AddsMilestoneAndKeywordOverlapReasonsFromRankingBreakdown()
    {
        var request = new ContextBundleRequest("memora", "Prepare milestone 3 context.");
        var rankedArtifact = CreateRankedArtifact(
            new ContextBundleArtifact(CreateDecisionArtifact("ADR-001", ArtifactStatus.Approved, ArtifactLinks.Empty), ContextArtifactOrigin.CanonicalApproved),
            milestoneRelevance: 1,
            directMatchStrength: 14);

        var reasons = _reasoner.ExplainInclusion(request, ContextLayerKind.Layer2, rankedArtifact);

        Assert.Contains(reasons, reason => reason.Code == "milestone-relevance");
        Assert.Contains(reasons, reason => reason.Code == "request-keyword-overlap");
        Assert.DoesNotContain(reasons, reason => reason.Code == "request-keyword-strong-match");
    }

    [Fact]
    public void ExplainInclusion_AddsStrongKeywordMatchWhenScoreExceedsThreshold()
    {
        var request = new ContextBundleRequest("memora", "Caching strategy for context packages.");
        var rankedArtifact = CreateRankedArtifact(
            new ContextBundleArtifact(CreateDecisionArtifact("ADR-001", ArtifactStatus.Approved, ArtifactLinks.Empty), ContextArtifactOrigin.CanonicalApproved),
            milestoneRelevance: 0,
            directMatchStrength: 18);

        var reasons = _reasoner.ExplainInclusion(request, ContextLayerKind.Layer2, rankedArtifact);

        Assert.Contains(reasons, reason => reason.Code == "request-keyword-overlap");
        Assert.Contains(reasons, reason => reason.Code == "request-keyword-strong-match");
    }

    [Fact]
    public void ExplainInclusion_OmitsKeywordOverlapReasonWhenScoreIsZero()
    {
        var request = new ContextBundleRequest("memora", "Anything.");
        var rankedArtifact = CreateRankedArtifact(
            new ContextBundleArtifact(CreateDecisionArtifact("ADR-001", ArtifactStatus.Approved, ArtifactLinks.Empty), ContextArtifactOrigin.CanonicalApproved),
            milestoneRelevance: 0,
            directMatchStrength: 0);

        var reasons = _reasoner.ExplainInclusion(request, ContextLayerKind.Layer2, rankedArtifact);

        Assert.DoesNotContain(reasons, reason => reason.Code is "request-keyword-overlap" or "request-keyword-strong-match" or "direct-task-match");
    }

    [Fact]
    public void ExplainInclusion_AddsTraversalReasonForBoundedRelationshipPaths()
    {
        var request = new ContextBundleRequest("memora", "Need context.", focusArtifactIds: ["ADR-003"]);
        var artifact = new ContextBundleArtifact(
            CreateDecisionArtifact("ADR-001", ArtifactStatus.Approved, new ArtifactLinks(["ADR-002"], [], [], [])),
            ContextArtifactOrigin.CanonicalApproved);
        var rankedArtifact = CreateRankedArtifact(
            artifact,
            milestoneRelevance: 0,
            directMatchStrength: 0,
            relationshipPaths:
            [
                new ContextRelationshipTraversalPath(
                    "ADR-003",
                    ["ADR-001", "ADR-002", "ADR-003"],
                    [
                        new ContextRelationshipTraversalSegment("ADR-001", ArtifactRelationshipKind.DependsOn, "ADR-002", ContextRelationshipTraversalDirection.Outgoing),
                        new ContextRelationshipTraversalSegment("ADR-002", ArtifactRelationshipKind.DependsOn, "ADR-003", ContextRelationshipTraversalDirection.Outgoing)
                    ])
            ]);

        var reasons = _reasoner.ExplainInclusion(request, ContextLayerKind.Layer2, rankedArtifact);

        var reason = Assert.Single(reasons, reason => reason.Code == "traversed-focus-artifact");
        Assert.Equal(["ADR-002", "ADR-003"], reason.RelatedArtifactIds);
    }

    [Fact]
    public void ExplainInclusion_UsesStableReasonOrdering()
    {
        var request = new ContextBundleRequest("memora", "Prepare milestone 3 context.", focusArtifactIds: ["ADR-001"]);
        var rankedArtifact = CreateRankedArtifact(
            new ContextBundleArtifact(CreateDecisionArtifact("ADR-001", ArtifactStatus.Approved, ArtifactLinks.Empty), ContextArtifactOrigin.CanonicalApproved),
            milestoneRelevance: 1,
            directMatchStrength: 14);

        var reasons = _reasoner.ExplainInclusion(request, ContextLayerKind.Layer2, rankedArtifact);

        Assert.Equal(
            ["approved-default", "explicit-focus-artifact", "milestone-relevance", "request-keyword-overlap"],
            reasons.Select(reason => reason.Code));
    }

    private static RankedContextArtifact CreateRankedArtifact(
        ContextBundleArtifact artifact,
        int milestoneRelevance,
        int directMatchStrength,
        IReadOnlyList<ContextRelationshipTraversalPath>? relationshipPaths = null) =>
        new(
            artifact,
            new ContextRankingBreakdown(
                TypePriority: 7,
                CanonicalStatusPriority: artifact.Origin == ContextArtifactOrigin.CanonicalApproved ? 3 : 2,
                MilestoneRelevance: milestoneRelevance,
                RelationshipProximity: relationshipPaths?.Count ?? 0,
                RecencyPriority: 1,
                DirectMatchStrength: directMatchStrength),
            relationshipPaths);

    private static PlanArtifact CreateActivePlanArtifact(ArtifactStatus status) =>
        new(
            "PLN-001",
            "memora",
            status,
            "Milestone 3 plan",
            new DateTimeOffset(2026, 4, 17, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 9, 15, 0, TimeSpan.Zero),
            1,
            ["milestone-3"],
            "user",
            "context reasoning",
            ArtifactLinks.Empty,
            """
            ## Goal
            Assemble context bundles.

            ## Scope
            Keep inclusion reasoning explicit.

            ## Acceptance Criteria
            - inclusion reasons stay deterministic

            ## Notes
            Ground context in approved artifacts by default.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Goal"] = "Assemble context bundles.",
                ["Scope"] = "Keep inclusion reasoning explicit.",
                ["Acceptance Criteria"] = "- inclusion reasons stay deterministic",
                ["Notes"] = "Ground context in approved artifacts by default."
            },
            ArtifactPriority.High,
            true);

    private static ArchitectureDecisionArtifact CreateDecisionArtifact(string id, ArtifactStatus status, ArtifactLinks links) =>
        new(
            id,
            "memora",
            status,
            "Context reasoning decision",
            new DateTimeOffset(2026, 4, 17, 9, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 9, 45, 0, TimeSpan.Zero),
            1,
            ["milestone-3"],
            "user",
            "reasoning tests",
            links,
            """
            ## Context
            Context inclusion must be explainable.

            ## Decision
            Carry explicit reasons with each included artifact.

            ## Alternatives Considered
            Implicit selection with no rationale.

            ## Consequences
            Operators can inspect why each artifact was selected.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Context inclusion must be explainable.",
                ["Decision"] = "Carry explicit reasons with each included artifact.",
                ["Alternatives Considered"] = "Implicit selection with no rationale.",
                ["Consequences"] = "Operators can inspect why each artifact was selected."
            },
            "2026-04-17");
}
