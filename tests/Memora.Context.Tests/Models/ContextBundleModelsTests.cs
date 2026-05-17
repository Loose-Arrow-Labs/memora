using Memora.Context.Models;
using Memora.Core.Artifacts;

namespace Memora.Context.Tests.Models;

public sealed class ContextBundleModelsTests
{
    [Fact]
    public void Request_NormalizesFocusInputsDeterministically()
    {
        var request = new ContextBundleRequest(
            "memora",
            "Assemble context for ranking work.",
            focusArtifactIds: ["ADR-002", "ADR-001", "ADR-001", " "],
            focusTags: ["ranking", "context", "ranking", "  "]);

        Assert.Equal(["ADR-001", "ADR-002"], request.FocusArtifactIds);
        Assert.Equal(["context", "ranking"], request.FocusTags);
        Assert.Equal(10, request.MaxLayer2Artifacts);
        Assert.Equal(10, request.MaxLayer3Artifacts);
    }

    [Fact]
    public void Request_BlankProjectId_IsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new ContextBundleRequest(" ", "Need context."));

        Assert.Contains("Project id is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BundleArtifact_CanonicalOriginRequiresApprovedArtifact()
    {
        var draftPlan = CreatePlanArtifact(ArtifactStatus.Draft, revision: 1);

        var exception = Assert.Throws<ArgumentException>(
            () => new ContextBundleArtifact(draftPlan, ContextArtifactOrigin.CanonicalApproved));

        Assert.Contains("approved artifacts", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BundleArtifact_SessionSummaryOriginRequiresSummaryArtifact()
    {
        var approvedPlan = CreatePlanArtifact(ArtifactStatus.Approved, revision: 1);

        var exception = Assert.Throws<ArgumentException>(
            () => new ContextBundleArtifact(approvedPlan, ContextArtifactOrigin.SessionSummary));

        Assert.Contains("session summary artifact", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Bundle_RejectsArtifactsFromDifferentProject()
    {
        var request = new ContextBundleRequest("memora", "Need context.");
        var otherProjectArtifact = new ContextBundleArtifact(
            CreatePlanArtifact(ArtifactStatus.Approved, revision: 1) with { ProjectId = "other" },
            ContextArtifactOrigin.CanonicalApproved);

        var exception = Assert.Throws<ArgumentException>(
            () => new ContextBundle(
                request,
                [new ContextBundleLayer(ContextLayerKind.Layer1, [otherProjectArtifact])]));

        Assert.Contains("belongs to project 'other'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Bundle_RejectsDuplicateArtifactRevisionsAcrossLayers()
    {
        var request = new ContextBundleRequest("memora", "Need context.");
        var approvedPlan = new ContextBundleArtifact(
            CreatePlanArtifact(ArtifactStatus.Approved, revision: 2),
            ContextArtifactOrigin.CanonicalApproved);

        var exception = Assert.Throws<ArgumentException>(
            () => new ContextBundle(
                request,
                [
                    new ContextBundleLayer(ContextLayerKind.Layer1, [approvedPlan]),
                    new ContextBundleLayer(ContextLayerKind.Layer2, [approvedPlan])
                ]));

        Assert.Contains("PLN-001.r0002", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Bundle_TracksArtifactCountAcrossLayers()
    {
        var request = new ContextBundleRequest("memora", "Need context.");
        var bundle = new ContextBundle(
            request,
            [
                new ContextBundleLayer(
                    ContextLayerKind.Layer1,
                    [new ContextBundleArtifact(CreatePlanArtifact(ArtifactStatus.Approved, revision: 1), ContextArtifactOrigin.CanonicalApproved)]),
                new ContextBundleLayer(
                    ContextLayerKind.Layer2,
                    [new ContextBundleArtifact(CreateSessionSummaryArtifact(), ContextArtifactOrigin.SessionSummary)])
            ]);

        Assert.Equal(2, bundle.ArtifactCount);
        Assert.Equal(2, bundle.Artifacts.Count);
    }

    private static PlanArtifact CreatePlanArtifact(ArtifactStatus status, int revision) =>
        new(
            "PLN-001",
            "memora",
            status,
            "Milestone 3 plan",
            new DateTimeOffset(2026, 4, 17, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 9, 15, 0, TimeSpan.Zero),
            revision,
            ["context", "ranking"],
            "user",
            "context modeling",
            ArtifactLinks.Empty,
            """
            ## Goal
            Assemble context bundles.

            ## Scope
            Keep the slice deterministic.

            ## Acceptance Criteria
            - bundle models stay strongly typed
            - validation keeps the shape honest

            ## Notes
            Ground the bundle in approved artifacts.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Goal"] = "Assemble context bundles.",
                ["Scope"] = "Keep the slice deterministic.",
                ["Acceptance Criteria"] = "- bundle models stay strongly typed\n- validation keeps the shape honest",
                ["Notes"] = "Ground the bundle in approved artifacts."
            },
            ArtifactPriority.High,
            true);

    private static SessionSummaryArtifact CreateSessionSummaryArtifact() =>
        new(
            "SUM-001",
            "memora",
            ArtifactStatus.Draft,
            "Context modeling session",
            new DateTimeOffset(2026, 4, 17, 9, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 9, 45, 0, TimeSpan.Zero),
            1,
            ["summary"],
            "agent",
            "implementation recap",
            ArtifactLinks.Empty,
            """
            ## Summary
            Added context bundle models.

            ## Artifacts Created
            - PLN-001

            ## Artifacts Updated
            - none

            ## Open Threads
            - add ranking next
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Summary"] = "Added context bundle models.",
                ["Artifacts Created"] = "- PLN-001",
                ["Artifacts Updated"] = "- none",
                ["Open Threads"] = "- add ranking next"
            },
            SessionType.Execution,
            false);
}
