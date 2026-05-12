using Memora.Core.Artifacts;
using Memora.Import.Readiness;

namespace Memora.Import.Tests.Readiness;

public sealed class CandidateArtifactConverterTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 11, 12, 0, 0, TimeSpan.Zero);

    private static CandidateMemoryRecord MakeCandidate(
        CandidateMemoryKind kind,
        CandidateMemorySource source = CandidateMemorySource.EvidenceDerived,
        double confidence = 0.80,
        string[]? evidenceIds = null) =>
        new(
            CandidateId: "CAN-ABCDEF1234567890",
            Kind: kind,
            Source: source,
            Title: $"Test candidate ({kind})",
            Summary: "This is a test summary.",
            Confidence: confidence,
            Ambiguity: "Ambiguity note here.",
            ExtractionReason: "Detected from test scan.",
            Disposition: CandidateMemoryDisposition.ReviewRequired,
            EvidenceStableIds: evidenceIds ?? []);

    [Fact]
    public void Convert_NullProjectId_ThrowsArgumentException()
    {
        var converter = new CandidateArtifactConverter();
        var candidate = MakeCandidate(CandidateMemoryKind.Decision);

        Assert.Throws<ArgumentNullException>(() => converter.Convert(null!, candidate, Now));
    }

    [Fact]
    public void Convert_NullCandidate_ThrowsArgumentNullException()
    {
        var converter = new CandidateArtifactConverter();

        Assert.Throws<ArgumentNullException>(() => converter.Convert("project", null!, Now));
    }

    [Fact]
    public void Convert_DecisionKind_ProducesArchitectureDecisionArtifact()
    {
        var converter = new CandidateArtifactConverter();
        var candidate = MakeCandidate(CandidateMemoryKind.Decision);

        var artifact = converter.Convert("project", candidate, Now);

        Assert.IsType<ArchitectureDecisionArtifact>(artifact);
        Assert.Equal(ArtifactType.Decision, artifact.Type);
        Assert.Equal(ArtifactStatus.Draft, artifact.Status);
    }

    [Fact]
    public void Convert_OpenQuestionKind_ProducesOpenQuestionArtifact()
    {
        var converter = new CandidateArtifactConverter();
        var candidate = MakeCandidate(CandidateMemoryKind.OpenQuestion);

        var artifact = converter.Convert("project", candidate, Now);

        var qa = Assert.IsType<OpenQuestionArtifact>(artifact);
        Assert.Equal(QuestionStatus.Open, qa.QuestionStatus);
        Assert.Equal(ArtifactStatus.Draft, artifact.Status);
    }

    [Fact]
    public void Convert_RepoStructureKind_ProducesRepoStructureArtifact()
    {
        var converter = new CandidateArtifactConverter();
        var candidate = MakeCandidate(CandidateMemoryKind.RepoStructure);

        var artifact = converter.Convert("project", candidate, Now);

        var rs = Assert.IsType<RepoStructureArtifact>(artifact);
        Assert.Equal(SnapshotSource.Generated, rs.SnapshotSource);
    }

    [Fact]
    public void Convert_OutcomeKind_ProducesOutcomeArtifact()
    {
        var converter = new CandidateArtifactConverter();
        var candidate = MakeCandidate(CandidateMemoryKind.Outcome);

        var artifact = converter.Convert("project", candidate, Now);

        Assert.IsType<OutcomeArtifact>(artifact);
    }

    [Fact]
    public void Convert_ContributionStyleKind_ProducesWorkflowConstraintArtifact()
    {
        var converter = new CandidateArtifactConverter();
        var candidate = MakeCandidate(CandidateMemoryKind.ContributionStyle);

        var artifact = converter.Convert("project", candidate, Now);

        var ca = Assert.IsType<ConstraintArtifact>(artifact);
        Assert.Equal(ConstraintKind.Workflow, ca.ConstraintKind);
        Assert.Equal(ArtifactStatus.Draft, artifact.Status);
    }

    [Fact]
    public void Convert_RiskKind_ProducesHighSeverityOperationalConstraint()
    {
        var converter = new CandidateArtifactConverter();
        var candidate = MakeCandidate(CandidateMemoryKind.Risk);

        var artifact = converter.Convert("project", candidate, Now);

        var ca = Assert.IsType<ConstraintArtifact>(artifact);
        Assert.Equal(ConstraintKind.Operational, ca.ConstraintKind);
        Assert.Equal(ConstraintSeverity.High, ca.Severity);
    }

    [Fact]
    public void Convert_ContractKind_ProducesWorkflowConstraintArtifact()
    {
        var converter = new CandidateArtifactConverter();
        var candidate = MakeCandidate(CandidateMemoryKind.Contract);

        var artifact = converter.Convert("project", candidate, Now);

        var ca = Assert.IsType<ConstraintArtifact>(artifact);
        Assert.Equal(ConstraintKind.Workflow, ca.ConstraintKind);
    }

    [Fact]
    public void Convert_ArtifactPreservesNonCanonicalStatus()
    {
        var converter = new CandidateArtifactConverter();
        var candidate = MakeCandidate(CandidateMemoryKind.Decision);

        var artifact = converter.Convert("project", candidate, Now);

        Assert.Equal(ArtifactStatus.Draft, artifact.Status);
        Assert.NotEqual(ArtifactStatus.Approved, artifact.Status);
    }

    [Fact]
    public void Convert_ArtifactProvenanceMatchesExtractionReason()
    {
        var converter = new CandidateArtifactConverter();
        var candidate = MakeCandidate(CandidateMemoryKind.Decision);

        var artifact = converter.Convert("project", candidate, Now);

        Assert.Equal(candidate.ExtractionReason, artifact.Provenance);
    }

    [Fact]
    public void Convert_ArtifactBodyIncludesConfidenceAndAmbiguity()
    {
        var converter = new CandidateArtifactConverter();
        var candidate = MakeCandidate(CandidateMemoryKind.Decision, confidence: 0.75);

        var artifact = converter.Convert("project", candidate, Now);

        Assert.Contains("75%", artifact.Body, StringComparison.Ordinal);
        Assert.Contains(candidate.Ambiguity, artifact.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Convert_ArtifactSectionsIncludeAmbiguityAndExtractionReason()
    {
        var converter = new CandidateArtifactConverter();
        var candidate = MakeCandidate(CandidateMemoryKind.Decision);

        var artifact = converter.Convert("project", candidate, Now);

        Assert.True(artifact.Sections.ContainsKey("ambiguity"));
        Assert.True(artifact.Sections.ContainsKey("extraction_reason"));
        Assert.True(artifact.Sections.ContainsKey("source_classification"));
    }

    [Fact]
    public void Convert_EvidenceIdsIncludedInSections()
    {
        var converter = new CandidateArtifactConverter();
        var candidate = MakeCandidate(CandidateMemoryKind.Decision, evidenceIds: ["ev-001", "ev-002"]);

        var artifact = converter.Convert("project", candidate, Now);

        Assert.True(artifact.Sections.ContainsKey("evidence_ids"));
        Assert.Contains("ev-001", artifact.Sections["evidence_ids"], StringComparison.Ordinal);
    }

    [Fact]
    public void Convert_TagsIncludeSourceAndKind()
    {
        var converter = new CandidateArtifactConverter();
        var candidate = MakeCandidate(CandidateMemoryKind.Decision, CandidateMemorySource.Inferred);

        var artifact = converter.Convert("project", candidate, Now);

        Assert.Contains("source:inferred", artifact.Tags);
        Assert.Contains("kind:decision", artifact.Tags);
    }

    [Fact]
    public void ConvertAll_EmptyList_ReturnsEmpty()
    {
        var converter = new CandidateArtifactConverter();

        var artifacts = converter.ConvertAll("project", [], Now);

        Assert.Empty(artifacts);
    }

    [Fact]
    public void ConvertAll_MultipleKinds_AllAreNonCanonical()
    {
        var converter = new CandidateArtifactConverter();
        var candidates = new[]
        {
            MakeCandidate(CandidateMemoryKind.Decision),
            MakeCandidate(CandidateMemoryKind.Contract),
            MakeCandidate(CandidateMemoryKind.OpenQuestion)
        };

        var artifacts = converter.ConvertAll("project", candidates, Now);

        Assert.All(artifacts, a => Assert.Equal(ArtifactStatus.Draft, a.Status));
        Assert.Equal(3, artifacts.Count);
    }

    [Fact]
    public void ConvertAll_ArtifactsIdMatchesCandidateId()
    {
        var converter = new CandidateArtifactConverter();
        var candidate = MakeCandidate(CandidateMemoryKind.Decision);

        var artifacts = converter.ConvertAll("project", [candidate], Now);

        Assert.Equal(candidate.CandidateId, artifacts[0].Id);
    }
}
