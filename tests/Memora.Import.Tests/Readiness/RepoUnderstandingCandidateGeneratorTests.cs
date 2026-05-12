using Memora.Core.Import;
using Memora.Import.Readiness;
using Memora.Import.Scan;

namespace Memora.Import.Tests.Readiness;

public sealed class RepoUnderstandingCandidateGeneratorTests
{
    private static RepoScanResult EmptyScan() =>
        new("C:/repo", [], []);

    private static RepoScanResult ScanWithEntries(params string[] relativePaths)
    {
        var entries = relativePaths
            .Select(p =>
            {
                var topLevel = p.Contains('/') ? p[..p.IndexOf('/', StringComparison.Ordinal)] : p;
                return new RepoScanEntry(p, Path.GetExtension(p), topLevel, 100);
            })
            .ToArray();
        return new RepoScanResult("C:/repo", entries, []);
    }

    [Fact]
    public void Generate_EmptyScan_ReturnsNoCandidates()
    {
        var generator = new RepoUnderstandingCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.FastBaseline, EmptyScan());

        Assert.Empty(candidates);
    }

    [Fact]
    public void Generate_AgentsFile_ProducesDecisionCandidateWithProvenance()
    {
        var scan = ScanWithEntries("AGENTS.md", "src/app.cs");
        var generator = new RepoUnderstandingCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.FastBaseline, scan);

        var decision = candidates.FirstOrDefault(c => c.Kind == CandidateMemoryKind.Decision &&
                                                      c.Title.Contains("agent instructions", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(decision);
        Assert.Equal(CandidateMemorySource.EvidenceDerived, decision.Source);
        Assert.True(decision.Confidence > 0.5);
        Assert.NotEmpty(decision.ExtractionReason);
        Assert.NotEmpty(decision.Ambiguity);
    }

    [Fact]
    public void Generate_CiConfiguration_ProducesDecisionCandidate()
    {
        var scan = ScanWithEntries(".github/workflows/ci.yml", "src/app.cs");
        var generator = new RepoUnderstandingCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.FastBaseline, scan);

        Assert.Contains(candidates, c => c.Kind == CandidateMemoryKind.Decision &&
                                         c.Title.Contains("CI", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Generate_LayeredDirectories_ProducesDecisionCandidate()
    {
        var scan = ScanWithEntries("core/models.cs", "api/controllers.cs", "infrastructure/db.cs");
        var generator = new RepoUnderstandingCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.FastBaseline, scan);

        Assert.Contains(candidates, c => c.Kind == CandidateMemoryKind.Decision &&
                                         c.Title.Contains("Layered", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Generate_ApiDirectory_ProducesContractCandidate()
    {
        var scan = ScanWithEntries("src/Memora.Api/Program.cs", "src/app.cs");
        var generator = new RepoUnderstandingCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.FastBaseline, scan);

        Assert.Contains(candidates, c => c.Kind == CandidateMemoryKind.Contract);
    }

    [Fact]
    public void Generate_MultipleTopLevelModules_ProducesContractCandidate()
    {
        var scan = ScanWithEntries("core/model.cs", "storage/store.cs", "api/endpoint.cs");
        var generator = new RepoUnderstandingCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.FastBaseline, scan);

        Assert.Contains(candidates, c =>
            c.Kind == CandidateMemoryKind.Contract &&
            c.Title.Contains("module areas", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Generate_AllCandidatesAreNonCanonicalByDefault()
    {
        var scan = ScanWithEntries("AGENTS.md", "core/model.cs", "api/endpoint.cs", ".github/workflows/ci.yml");
        var generator = new RepoUnderstandingCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.FastBaseline, scan);

        Assert.All(candidates, c =>
        {
            Assert.NotEmpty(c.CandidateId);
            Assert.NotEmpty(c.ExtractionReason);
            Assert.NotEmpty(c.Ambiguity);
            Assert.True(c.Confidence > 0 && c.Confidence <= 1.0);
        });
    }

    [Fact]
    public void Generate_StrictGovernance_ProducesReviewRequiredDisposition()
    {
        var scan = ScanWithEntries("AGENTS.md");
        var generator = new RepoUnderstandingCandidateGenerator();

        var candidates = generator.Generate("project", ImportMode.StrictGovernance, scan);

        Assert.All(candidates, c => Assert.Equal(CandidateMemoryDisposition.ReviewRequired, c.Disposition));
    }

    [Fact]
    public void Generate_SameInputsProduceSameCandidateIds()
    {
        var scan = ScanWithEntries("AGENTS.md", "core/model.cs", "api/endpoint.cs");
        var generator = new RepoUnderstandingCandidateGenerator();

        var first = generator.Generate("project", ImportMode.FastBaseline, scan).Select(c => c.CandidateId).ToArray();
        var second = generator.Generate("project", ImportMode.FastBaseline, scan).Select(c => c.CandidateId).ToArray();

        Assert.Equal(first, second);
    }
}
