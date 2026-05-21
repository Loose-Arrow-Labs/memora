using Memora.Context.Assembly;
using Memora.Context.Models;
using Memora.Core.Artifacts;

namespace Memora.Context.Tests.Assembly;

public sealed class ContextBundleBuilderTests
{
    private readonly ContextBundleBuilder _builder = new();

    [Fact]
    public void Build_ReturnsStableThreeLayerBundle()
    {
        var request = new ContextBundleRequest(
            "memora",
            "Prepare milestone 3 context.",
            includeLayer3History: true,
            focusArtifactIds: ["ADR-010"]);

        var bundle = _builder.Build(
            request,
            [
                CreateRepoArtifact(),
                CreateInactivePlanArtifact(),
                CreateDecisionArtifact("ADR-010", ArtifactStatus.Approved, new ArtifactLinks(["PLN-001"], [], [], [])),
                CreateQuestionArtifact(),
                CreateActivePlanArtifact(),
                CreateCharterArtifact(),
                CreateSessionSummaryArtifact()
            ]);

        Assert.Equal(
            [ContextLayerKind.Layer1, ContextLayerKind.Layer2, ContextLayerKind.Layer3],
            bundle.Layers.Select(layer => layer.Kind));
        Assert.Equal(["CHR-001", "PLN-001", "REP-001"], bundle.Layers[0].Artifacts.Select(entry => entry.Artifact.Id));
        Assert.Equal(["ADR-010", "QST-001"], bundle.Layers[1].Artifacts.Select(entry => entry.Artifact.Id));
        Assert.Equal(["PLN-002", "SUM-001"], bundle.Layers[2].Artifacts.Select(entry => entry.Artifact.Id));
    }

    [Fact]
    public void Build_ExcludesDraftArtifactsByDefault()
    {
        var request = new ContextBundleRequest("memora", "Prepare milestone 3 context.");

        var bundle = _builder.Build(
            request,
            [
                CreateCharterArtifact(),
                CreateActivePlanArtifact(),
                CreateDecisionArtifact("ADR-001", ArtifactStatus.Draft, ArtifactLinks.Empty)
            ]);

        Assert.DoesNotContain(bundle.Artifacts, artifact => artifact.Artifact.Id == "ADR-001");
    }

    [Fact]
    public void Build_IncludesDraftArtifactsWhenExplicitlyAllowed()
    {
        var request = new ContextBundleRequest("memora", "Prepare milestone 3 context.", includeDraftArtifacts: true);

        var bundle = _builder.Build(
            request,
            [
                CreateCharterArtifact(),
                CreateActivePlanArtifact(),
                CreateDecisionArtifact("ADR-001", ArtifactStatus.Draft, ArtifactLinks.Empty)
            ]);

        var draftArtifact = Assert.Single(bundle.Artifacts, artifact => artifact.Artifact.Id == "ADR-001");
        Assert.Equal(ContextArtifactOrigin.DraftProposal, draftArtifact.Origin);
        Assert.Contains(draftArtifact.InclusionReasons, reason => reason.Code == "draft-explicitly-allowed");
    }

    [Fact]
    public void Build_SelectedArtifacts_HaveInclusionReasons()
    {
        var request = new ContextBundleRequest("memora", "Prepare milestone 3 context.", focusArtifactIds: ["ADR-010"]);

        var bundle = _builder.Build(
            request,
            [
                CreateCharterArtifact(),
                CreateActivePlanArtifact(),
                CreateDecisionArtifact("ADR-010", ArtifactStatus.Approved, ArtifactLinks.Empty)
            ]);

        Assert.All(bundle.Artifacts, artifact => Assert.NotEmpty(artifact.InclusionReasons));
        Assert.Contains(
            bundle.Artifacts.Single(artifact => artifact.Artifact.Id == "ADR-010").InclusionReasons,
            reason => reason.Code == "explicit-focus-artifact");
    }

    [Fact]
    public void Build_IsDeterministicForIdenticalInputs()
    {
        var request = new ContextBundleRequest(
            "memora",
            "Prepare milestone 3 context.",
            includeLayer3History: true,
            focusArtifactIds: ["ADR-010"]);

        var artifacts = new ArtifactDocument[]
        {
            CreateQuestionArtifact(),
            CreateDecisionArtifact("ADR-010", ArtifactStatus.Approved, new ArtifactLinks(["PLN-001"], [], [], [])),
            CreateActivePlanArtifact(),
            CreateCharterArtifact(),
            CreateRepoArtifact(),
            CreateSessionSummaryArtifact(),
            CreateInactivePlanArtifact()
        };

        var first = _builder.Build(request, artifacts);
        var second = _builder.Build(request, artifacts.Reverse().ToArray());

        Assert.Equal(
            first.Layers.Select(layer => string.Join(",", layer.Artifacts.Select(artifact => artifact.Artifact.Id))),
            second.Layers.Select(layer => string.Join(",", layer.Artifacts.Select(artifact => artifact.Artifact.Id))));
    }

    [Fact]
    public void Build_Layer2Cap_KeepsTopRanked()
    {
        var request = new ContextBundleRequest(
            "memora",
            "Prepare milestone 3 context.",
            maxLayer2Artifacts: 2);

        var bundle = _builder.Build(
            request,
            [
                CreateCharterArtifact(),
                CreateActivePlanArtifact(),
                CreateLayer2DecisionArtifact("ADR-001", new DateTimeOffset(2026, 4, 17, 9, 5, 0, TimeSpan.Zero)),
                CreateLayer2DecisionArtifact("ADR-002", new DateTimeOffset(2026, 4, 17, 9, 15, 0, TimeSpan.Zero)),
                CreateLayer2DecisionArtifact("ADR-003", new DateTimeOffset(2026, 4, 17, 9, 25, 0, TimeSpan.Zero)),
                CreateLayer2DecisionArtifact("ADR-004", new DateTimeOffset(2026, 4, 17, 9, 35, 0, TimeSpan.Zero))
            ]);

        Assert.Equal(2, bundle.Layers[1].Artifacts.Count);
        Assert.Equal(["ADR-004", "ADR-003"], bundle.Layers[1].Artifacts.Select(entry => entry.Artifact.Id));
    }

    [Fact]
    public void Build_Layer3Cap_WithHistory_Respected()
    {
        var request = new ContextBundleRequest(
            "memora",
            "Prepare milestone 3 context.",
            includeLayer3History: true,
            maxLayer3Artifacts: 2);

        var bundle = _builder.Build(
            request,
            [
                CreateCharterArtifact(),
                CreateActivePlanArtifact(),
                CreateSessionSummaryArtifact("SUM-001", new DateTimeOffset(2026, 4, 17, 9, 5, 0, TimeSpan.Zero)),
                CreateSessionSummaryArtifact("SUM-002", new DateTimeOffset(2026, 4, 17, 9, 15, 0, TimeSpan.Zero)),
                CreateSessionSummaryArtifact("SUM-003", new DateTimeOffset(2026, 4, 17, 9, 25, 0, TimeSpan.Zero)),
                CreateSessionSummaryArtifact("SUM-004", new DateTimeOffset(2026, 4, 17, 9, 35, 0, TimeSpan.Zero))
            ]);

        Assert.Equal(2, bundle.Layers[2].Artifacts.Count);
        Assert.Equal(["SUM-004", "SUM-003"], bundle.Layers[2].Artifacts.Select(entry => entry.Artifact.Id));
    }

    private static ArchitectureDecisionArtifact CreateLayer2DecisionArtifact(string id, DateTimeOffset updatedAtUtc) =>
        new(
            id,
            "memora",
            ArtifactStatus.Approved,
            $"Decision {id}",
            new DateTimeOffset(2026, 4, 17, 9, 0, 0, TimeSpan.Zero),
            updatedAtUtc,
            1,
            ["milestone-3", "context"],
            "user",
            "layer 2 cap seed",
            ArtifactLinks.Empty,
            """
            ## Context
            Layer 2 cap fixture.

            ## Decision
            Cover layer 2 capping.

            ## Alternatives Considered
            None.

            ## Consequences
            Cap behavior is verified.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Layer 2 cap fixture.",
                ["Decision"] = "Cover layer 2 capping.",
                ["Alternatives Considered"] = "None.",
                ["Consequences"] = "Cap behavior is verified."
            },
            "2026-04-17");

    private static SessionSummaryArtifact CreateSessionSummaryArtifact(string id, DateTimeOffset updatedAtUtc) =>
        new(
            id,
            "memora",
            ArtifactStatus.Draft,
            $"Summary {id}",
            new DateTimeOffset(2026, 4, 17, 9, 0, 0, TimeSpan.Zero),
            updatedAtUtc,
            1,
            ["summary"],
            "agent",
            "layer 3 cap seed",
            ArtifactLinks.Empty,
            """
            ## Summary
            Layer 3 cap fixture.

            ## Artifacts Created
            - none

            ## Artifacts Updated
            - none

            ## Open Threads
            - none
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Summary"] = "Layer 3 cap fixture.",
                ["Artifacts Created"] = "- none",
                ["Artifacts Updated"] = "- none",
                ["Open Threads"] = "- none"
            },
            SessionType.Execution,
            false);

    private static ProjectCharterArtifact CreateCharterArtifact() =>
        new(
            "CHR-001",
            "memora",
            ArtifactStatus.Approved,
            "Memora charter",
            new DateTimeOffset(2026, 4, 17, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 8, 15, 0, TimeSpan.Zero),
            1,
            ["milestone-3"],
            "user",
            "charter seed",
            ArtifactLinks.Empty,
            """
            ## Problem Statement
            Keep project memory structured.

            ## Primary Users / Stakeholders
            Engineers.

            ## Current Pain
            Context drifts between sessions.

            ## Desired Outcome
            Context stays deterministic.

            ## Definition of Success
            The builder stays grounded in approved artifacts.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Problem Statement"] = "Keep project memory structured.",
                ["Primary Users / Stakeholders"] = "Engineers.",
                ["Current Pain"] = "Context drifts between sessions.",
                ["Desired Outcome"] = "Context stays deterministic.",
                ["Definition of Success"] = "The builder stays grounded in approved artifacts."
            });

    private static PlanArtifact CreateActivePlanArtifact() =>
        CreatePlanArtifact("PLN-001", active: true, ArtifactStatus.Approved, "Active plan anchor");

    private static PlanArtifact CreateInactivePlanArtifact() =>
        CreatePlanArtifact("PLN-002", active: false, ArtifactStatus.Approved, "History plan");

    private static PlanArtifact CreatePlanArtifact(string id, bool active, ArtifactStatus status, string title) =>
        new(
            id,
            "memora",
            status,
            title,
            new DateTimeOffset(2026, 4, 17, 8, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, active ? 9 : 7, 0, 0, TimeSpan.Zero),
            1,
            ["milestone-3", "context"],
            "user",
            "plan seed",
            ArtifactLinks.Empty,
            """
            ## Goal
            Assemble deterministic context.

            ## Scope
            Keep the builder reviewable.

            ## Acceptance Criteria
            - bundles expose layer ordering

            ## Notes
            Preserve explicit inclusion reasoning.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Goal"] = "Assemble deterministic context.",
                ["Scope"] = "Keep the builder reviewable.",
                ["Acceptance Criteria"] = "- bundles expose layer ordering",
                ["Notes"] = "Preserve explicit inclusion reasoning."
            },
            ArtifactPriority.High,
            active);

    private static RepoStructureArtifact CreateRepoArtifact() =>
        new(
            "REP-001",
            "memora",
            ArtifactStatus.Approved,
            "Repository snapshot",
            new DateTimeOffset(2026, 4, 17, 8, 45, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 9, 10, 0, TimeSpan.Zero),
            1,
            ["milestone-3", "repo"],
            "user",
            "repo snapshot",
            ArtifactLinks.Empty,
            """
            ## Summary
            Repository structure snapshot.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Summary"] = "Repository structure snapshot."
            },
            SnapshotSource.Manual);

    private static ArchitectureDecisionArtifact CreateDecisionArtifact(string id, ArtifactStatus status, ArtifactLinks links) =>
        new(
            id,
            "memora",
            status,
            "Context assembly decision",
            new DateTimeOffset(2026, 4, 17, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 9, 20, 0, TimeSpan.Zero),
            1,
            ["milestone-3", "context"],
            "user",
            "decision seed",
            links,
            """
            ## Context
            Context assembly must stay deterministic.

            ## Decision
            Use layered bundles with ranking and explicit reasons.

            ## Alternatives Considered
            Unstructured retrieval.

            ## Consequences
            Operators can inspect the bundle.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Context assembly must stay deterministic.",
                ["Decision"] = "Use layered bundles with ranking and explicit reasons.",
                ["Alternatives Considered"] = "Unstructured retrieval.",
                ["Consequences"] = "Operators can inspect the bundle."
            },
            "2026-04-17");

    private static OpenQuestionArtifact CreateQuestionArtifact() =>
        new(
            "QST-001",
            "memora",
            ArtifactStatus.Approved,
            "Open context question",
            new DateTimeOffset(2026, 4, 17, 9, 5, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 9, 25, 0, TimeSpan.Zero),
            1,
            ["milestone-3", "question"],
            "user",
            "question seed",
            ArtifactLinks.Empty,
            """
            ## Question
            Which supporting artifacts should stay in Layer 2?

            ## Context
            The builder must stay deterministic.

            ## Impact
            Selection order affects explainability.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Question"] = "Which supporting artifacts should stay in Layer 2?",
                ["Context"] = "The builder must stay deterministic.",
                ["Impact"] = "Selection order affects explainability."
            },
            QuestionStatus.Open,
            ArtifactPriority.Normal);

    private static SessionSummaryArtifact CreateSessionSummaryArtifact() =>
        new(
            "SUM-001",
            "memora",
            ArtifactStatus.Draft,
            "Context history summary",
            new DateTimeOffset(2026, 4, 17, 9, 10, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 17, 9, 30, 0, TimeSpan.Zero),
            1,
            ["summary", "milestone-3"],
            "agent",
            "history seed",
            ArtifactLinks.Empty,
            """
            ## Summary
            Earlier context assembly work completed.

            ## Artifacts Created
            - ADR-010

            ## Artifacts Updated
            - PLN-002

            ## Open Threads
            - verify layer 3 ordering
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Summary"] = "Earlier context assembly work completed.",
                ["Artifacts Created"] = "- ADR-010",
                ["Artifacts Updated"] = "- PLN-002",
                ["Open Threads"] = "- verify layer 3 ordering"
            },
            SessionType.Execution,
            false);
}
