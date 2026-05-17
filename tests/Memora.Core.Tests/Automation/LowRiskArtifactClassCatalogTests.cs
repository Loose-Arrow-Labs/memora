using Memora.Core.Artifacts;
using Memora.Core.Automation;

namespace Memora.Core.Tests.Automation;

public sealed class LowRiskArtifactClassCatalogTests
{
    [Fact]
    public void Definitions_AreBoundedNonCanonical()
    {
        var definitions = LowRiskArtifactClassCatalog.GetDefinitions();

        Assert.Collection(
            definitions,
            sessionSummary =>
            {
                Assert.Equal(ArtifactType.SessionSummary, sessionSummary.ArtifactType);
                Assert.Equal(AutomationStorageScope.Summary, sessionSummary.StorageScope);
                Assert.False(sessionSummary.AllowsCanonicalDirectWrite);
                Assert.False(sessionSummary.AllowsApprovalBypassInV1);
                Assert.Contains("canonical must remain false", sessionSummary.RequiredGuardrails);
            },
            repoStructure =>
            {
                Assert.Equal(ArtifactType.RepoStructure, repoStructure.ArtifactType);
                Assert.Equal(AutomationStorageScope.Draft, repoStructure.StorageScope);
                Assert.False(repoStructure.AllowsCanonicalDirectWrite);
                Assert.False(repoStructure.AllowsApprovalBypassInV1);
                Assert.Contains("approved repo-structure artifacts still require lifecycle governance", repoStructure.RequiredGuardrails);
            });
    }

    [Theory]
    [InlineData(ArtifactType.Charter)]
    [InlineData(ArtifactType.Plan)]
    [InlineData(ArtifactType.Decision)]
    [InlineData(ArtifactType.Constraint)]
    [InlineData(ArtifactType.Question)]
    [InlineData(ArtifactType.Outcome)]
    public void GovernanceCriticalArtifactTypes_AreNotLowRiskCandidates(ArtifactType artifactType)
    {
        Assert.False(LowRiskArtifactClassCatalog.IsLowRiskCandidate(artifactType));
        Assert.False(LowRiskArtifactClassCatalog.TryGetDefinition(artifactType, out _));
    }
}

