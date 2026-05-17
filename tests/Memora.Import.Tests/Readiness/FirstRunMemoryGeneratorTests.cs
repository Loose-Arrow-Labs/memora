using Memora.Core.Import;
using Memora.Import.Readiness;

namespace Memora.Import.Tests.Readiness;

public sealed class FirstRunMemoryGeneratorTests
{
    private readonly FirstRunMemoryGenerator _generator = new();

    [Fact]
    public void Generate_CreatesRepoStructureCandidateFromChangedFiles()
    {
        var result = _generator.Generate(
            "memora",
            ImportMode.FastBaseline,
            [
                CreateCommitEvidence(
                    "EVD-1",
                    "feat(import): add importer",
                    "src/Memora.Import/Git/LocalGitEvidenceImporter.cs\ntests/Memora.Import.Tests/Git/LocalGitEvidenceImporterTests.cs")
            ]);

        Assert.Contains(result.Candidates, candidate =>
            candidate.Kind == CandidateMemoryKind.RepoStructure &&
            candidate.Source == CandidateMemorySource.EvidenceDerived &&
            candidate.Title == "Top-level area: src" &&
            candidate.Disposition == CandidateMemoryDisposition.BaselineMemory &&
            candidate.EvidenceStableIds.Contains("EVD-1", StringComparer.Ordinal));
    }

    [Fact]
    public void Generate_CreatesBuildTestCommandCandidates()
    {
        var result = _generator.Generate(
            "memora",
            ImportMode.FastBaseline,
            [
                CreateCommitEvidence(
                    "EVD-1",
                    "feat(build): add solution",
                    "Memora.sln\nsrc/Memora.Core/Memora.Core.csproj\ntests/Memora.Core.Tests/Memora.Core.Tests.csproj")
            ]);

        Assert.Contains(result.Candidates, candidate =>
            candidate.Kind == CandidateMemoryKind.BuildCommand &&
            candidate.Source == CandidateMemorySource.EvidenceDerived &&
            candidate.Title == "Build with dotnet build" &&
            candidate.Disposition == CandidateMemoryDisposition.BaselineMemory);
        Assert.Contains(result.Candidates, candidate =>
            candidate.Kind == CandidateMemoryKind.TestCommand &&
            candidate.Source == CandidateMemorySource.EvidenceDerived &&
            candidate.Title == "Test with dotnet test" &&
            candidate.Disposition == CandidateMemoryDisposition.BaselineMemory);
    }

    [Fact]
    public void Generate_CreatesContributionStyleCandidateAsReviewRequired()
    {
        var result = _generator.Generate(
            "memora",
            ImportMode.FastBaseline,
            [
                CreateCommitEvidence("EVD-1", "feat(import): add importer", "src/import.cs"),
                CreateCommitEvidence("EVD-2", "fix(storage): handle duplicate import", "src/storage.cs")
            ]);

        var candidate = Assert.Single(result.Candidates, candidate => candidate.Kind == CandidateMemoryKind.ContributionStyle);
        Assert.Equal("Use conventional commit-style prefixes", candidate.Title);
        Assert.Equal(CandidateMemorySource.Inferred, candidate.Source);
        Assert.Equal(CandidateMemoryDisposition.ReviewRequired, candidate.Disposition);
        Assert.Equal(2, candidate.EvidenceStableIds.Count);
        Assert.Contains("Matched conventional commit-style prefixes", candidate.ExtractionReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ReadinessReportWarnsWhenContextAndTestsAreMissing()
    {
        var result = _generator.Generate(
            "memora",
            ImportMode.StrictGovernance,
            [
                CreateCommitEvidence("EVD-1", "fix(import): address risky module", "src/import.cs")
            ]);

        Assert.False(result.ReadinessReport.ReadyForAgentUse);
        Assert.Contains("README evidence has not been imported yet.", result.ReadinessReport.MissingContext);
        Assert.Contains("Agent operating instructions such as AGENTS.md have not been imported yet.", result.ReadinessReport.MissingContext);
        Assert.Contains("No deterministic test command candidate was found.", result.ReadinessReport.MissingTests);
        Assert.Contains("src", result.ReadinessReport.RiskyModules);
        Assert.Contains(result.ReadinessReport.AdvisoryDiscoveryGaps, gap => gap.Contains("Advisory discovery could inspect build configuration", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_BulkApprovalGroupsInferredMeaningForReview()
    {
        var result = _generator.Generate(
            "memora",
            ImportMode.BulkApproval,
            [
                CreateIssueEvidence("EVD-1", "Question: should imports be deterministic?", "GitHub issue #12 is open.")
            ]);

        var candidate = Assert.Single(result.Candidates, candidate => candidate.Kind == CandidateMemoryKind.OpenQuestion);
        Assert.Equal(CandidateMemorySource.Inferred, candidate.Source);
        Assert.Equal(CandidateMemoryDisposition.GroupedBaselineReview, candidate.Disposition);
        Assert.Equal("EVD-1", Assert.Single(candidate.EvidenceStableIds));
    }

    private static ImportedEvidenceRecord CreateCommitEvidence(
        string stableId,
        string title,
        string changedFiles) =>
        new(
            stableId,
            "memora",
            ImportedEvidenceSourceType.LocalGitCommit,
            "ATT-LOCAL",
            "local:/repo",
            stableId,
            title,
            title,
            new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 5, 12, 1, 0, TimeSpan.Zero),
            $"local git commit {stableId}",
            ImportedEvidenceTrustState.BaselineEvidence,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["changedFiles"] = changedFiles
            });

    private static ImportedEvidenceRecord CreateIssueEvidence(
        string stableId,
        string title,
        string summary) =>
        new(
            stableId,
            "memora",
            ImportedEvidenceSourceType.GitHubIssue,
            "ATT-GITHUB",
            "github:https://github.com/alucero270/memora.git",
            "12",
            title,
            summary,
            new DateTimeOffset(2026, 5, 5, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 5, 12, 1, 0, TimeSpan.Zero),
            "https://github.com/alucero270/memora/issues/12",
            ImportedEvidenceTrustState.BaselineEvidence);
}
