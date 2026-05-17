using Memora.Context.Models;
using Memora.Context.Ranking;
using Memora.Core.Artifacts;

namespace Memora.Context.Tests.Ranking;

public sealed class DeterministicContextRankingEngineTests
{
    private readonly DeterministicContextRankingEngine _engine = new();

    [Fact]
    public void Rank_PrefersApprovedArtifactsOverDraftsWhenOtherFactorsMatch()
    {
        var request = new ContextBundleRequest("memora", "Need milestone 3 context.");
        var approved = CreateCandidate("PLN-001", ArtifactStatus.Approved, ContextArtifactOrigin.CanonicalApproved);
        var draft = CreateCandidate("PLN-002", ArtifactStatus.Draft, ContextArtifactOrigin.DraftProposal);

        var ranked = _engine.Rank(request, [draft, approved]);

        Assert.Equal("PLN-001", ranked[0].Artifact.Artifact.Id);
        Assert.True(ranked[0].Breakdown.CanonicalStatusPriority > ranked[1].Breakdown.CanonicalStatusPriority);
    }

    [Fact]
    public void Rank_UsesMilestoneTagsAsAnExplicitPriorityFactor()
    {
        var request = new ContextBundleRequest("memora", "Prepare Milestone 3 context bundle.");
        var matching = CreateCandidate("ADR-001", ArtifactStatus.Approved, ContextArtifactOrigin.CanonicalApproved, tags: ["milestone-3"]);
        var nonMatching = CreateCandidate("ADR-002", ArtifactStatus.Approved, ContextArtifactOrigin.CanonicalApproved, tags: ["milestone-2"]);

        var ranked = _engine.Rank(request, [nonMatching, matching]);

        Assert.Equal("ADR-001", ranked[0].Artifact.Artifact.Id);
        Assert.True(ranked[0].Breakdown.MilestoneRelevance > ranked[1].Breakdown.MilestoneRelevance);
    }

    [Fact]
    public void Rank_UsesDirectRelationshipsToFocusArtifacts()
    {
        var request = new ContextBundleRequest("memora", "Need supporting context.", focusArtifactIds: ["PLN-099"]);
        var related = CreateCandidate(
            "ADR-001",
            ArtifactStatus.Approved,
            ContextArtifactOrigin.CanonicalApproved,
            links: new ArtifactLinks(["PLN-099"], [], [], []));
        var unrelated = CreateCandidate("ADR-002", ArtifactStatus.Approved, ContextArtifactOrigin.CanonicalApproved);

        var ranked = _engine.Rank(request, [unrelated, related]);

        Assert.Equal("ADR-001", ranked[0].Artifact.Artifact.Id);
        Assert.True(ranked[0].Breakdown.RelationshipProximity > ranked[1].Breakdown.RelationshipProximity);
    }

    [Fact]
    public void Rank_TraversesBoundedTypedRelationshipPathsToFocusArtifacts()
    {
        var request = new ContextBundleRequest("memora", "Need supporting context.", focusArtifactIds: ["ADR-003"]);
        var indirectlyRelated = CreateCandidate(
            "ADR-001",
            ArtifactStatus.Approved,
            ContextArtifactOrigin.CanonicalApproved,
            links: new ArtifactLinks(["ADR-002"], [], [], []));
        var intermediate = CreateCandidate(
            "ADR-002",
            ArtifactStatus.Approved,
            ContextArtifactOrigin.CanonicalApproved,
            links: new ArtifactLinks(["ADR-003"], [], [], []));
        var unrelated = CreateCandidate("ADR-004", ArtifactStatus.Approved, ContextArtifactOrigin.CanonicalApproved);

        var ranked = _engine.Rank(request, [unrelated, indirectlyRelated, intermediate]);

        Assert.True(ranked.Single(result => result.Artifact.Artifact.Id == "ADR-001").Breakdown.RelationshipProximity > 0);
        Assert.Contains(
            ranked.Single(result => result.Artifact.Artifact.Id == "ADR-001").RelationshipPaths,
            path =>
                path.FocusArtifactId == "ADR-003" &&
                path.Depth == 2 &&
                path.Segments.Select(segment => segment.RelationshipKind).SequenceEqual([ArtifactRelationshipKind.DependsOn, ArtifactRelationshipKind.DependsOn]));
        Assert.Equal("ADR-004", ranked.Last().Artifact.Artifact.Id);
    }

    [Fact]
    public void Rank_UsesRecencyBeforeDirectMatchForOtherwiseEqualArtifacts()
    {
        var request = new ContextBundleRequest("memora", "Need context assembly details.");
        var newer = CreateCandidate(
            "ADR-001",
            ArtifactStatus.Approved,
            ContextArtifactOrigin.CanonicalApproved,
            title: "Context assembly",
            updatedAtUtc: new DateTimeOffset(2026, 4, 17, 12, 0, 0, TimeSpan.Zero));
        var olderButStrongerMatch = CreateCandidate(
            "ADR-002",
            ArtifactStatus.Approved,
            ContextArtifactOrigin.CanonicalApproved,
            title: "Context assembly details",
            updatedAtUtc: new DateTimeOffset(2026, 4, 17, 11, 0, 0, TimeSpan.Zero));

        var ranked = _engine.Rank(request, [olderButStrongerMatch, newer]);

        Assert.Equal("ADR-001", ranked[0].Artifact.Artifact.Id);
        Assert.True(ranked[0].Breakdown.RecencyPriority > ranked[1].Breakdown.RecencyPriority);
        Assert.True(ranked[1].Breakdown.DirectMatchStrength > ranked[0].Breakdown.DirectMatchStrength);
    }

    [Fact]
    public void Rank_UsesArtifactIdAsStableFinalTieBreaker()
    {
        var request = new ContextBundleRequest("memora", "Need context.");
        var first = CreateCandidate("ADR-001", ArtifactStatus.Approved, ContextArtifactOrigin.CanonicalApproved);
        var second = CreateCandidate("ADR-002", ArtifactStatus.Approved, ContextArtifactOrigin.CanonicalApproved);

        var ranked = _engine.Rank(request, [second, first]);

        Assert.Equal(["ADR-001", "ADR-002"], ranked.Select(result => result.Artifact.Artifact.Id));
    }

    [Fact]
    public void Rank_IdenticalInputs_PreservesBreakdownsRegardlessOfOrder()
    {
        var request = new ContextBundleRequest(
            "memora",
            "Prepare milestone 3 context for ADR-010.",
            focusArtifactIds: ["ADR-010"],
            focusTags: ["milestone-3", "context"]);
        var artifacts = new[]
        {
            CreateCandidate("ADR-010", ArtifactStatus.Approved, ContextArtifactOrigin.CanonicalApproved, tags: ["milestone-3", "context"]),
            CreateCandidate("ADR-020", ArtifactStatus.Approved, ContextArtifactOrigin.CanonicalApproved, tags: ["milestone-3"]),
            CreateCandidate("ADR-030", ArtifactStatus.Approved, ContextArtifactOrigin.CanonicalApproved, tags: ["context"])
        };

        var first = _engine.Rank(request, artifacts);
        var second = _engine.Rank(request, artifacts.Reverse().ToArray());

        Assert.Equal(first.Select(result => result.Artifact.Artifact.Id), second.Select(result => result.Artifact.Artifact.Id));
        Assert.Equal(first.Select(result => result.Breakdown), second.Select(result => result.Breakdown));
    }

    private static ContextBundleArtifact CreateCandidate(
        string id,
        ArtifactStatus status,
        ContextArtifactOrigin origin,
        string? title = null,
        IReadOnlyList<string>? tags = null,
        ArtifactLinks? links = null,
        DateTimeOffset? updatedAtUtc = null)
    {
        var artifact = new ArchitectureDecisionArtifact(
            id,
            "memora",
            status,
            title ?? "Context ranking decision",
            new DateTimeOffset(2026, 4, 17, 9, 0, 0, TimeSpan.Zero),
            updatedAtUtc ?? new DateTimeOffset(2026, 4, 17, 10, 0, 0, TimeSpan.Zero),
            1,
            tags ?? ["context"],
            "user",
            "ranking test",
            links ?? ArtifactLinks.Empty,
            """
            ## Context
            Deterministic context assembly is required.

            ## Decision
            Rank artifacts using explicit factors.

            ## Alternatives Considered
            Probabilistic ranking.

            ## Consequences
            Ordering stays explainable.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Deterministic context assembly is required.",
                ["Decision"] = "Rank artifacts using explicit factors.",
                ["Alternatives Considered"] = "Probabilistic ranking.",
                ["Consequences"] = "Ordering stays explainable."
            },
            "2026-04-17");

        return new ContextBundleArtifact(artifact, origin);
    }
}
