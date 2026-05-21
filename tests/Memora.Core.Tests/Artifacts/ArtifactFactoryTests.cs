using Memora.Core.Artifacts;
using Memora.Core.Validation;

namespace Memora.Core.Tests.Artifacts;

public sealed class ArtifactFactoryTests
{
    private readonly ArtifactFactory _factory = new();

    [Fact]
    public void ValidCharter_ParsesAndValidates()
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(ArtifactType.Charter);
        var sections = ArtifactTestBuilder.CreateSections(ArtifactType.Charter);

        var result = _factory.Create(frontmatter, ArtifactTestBuilder.CreateBody(sections), sections);

        Assert.True(result.Validation.IsValid);
        var artifact = Assert.IsType<ProjectCharterArtifact>(result.Artifact);
        Assert.Equal("CHR-001", artifact.Id);
        Assert.Single(artifact.Links.Affects);
        Assert.Equal(ArtifactRelationshipKind.Affects, artifact.Links.Affects[0].Kind);
        Assert.Equal("ADR-001", artifact.Links.Affects[0].TargetArtifactId);
    }

    [Fact]
    public void InvalidStatusEnum_FailsValidation()
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(ArtifactType.Charter);
        var sections = ArtifactTestBuilder.CreateSections(ArtifactType.Charter);
        frontmatter["status"] = "not_real";

        var result = _factory.Create(frontmatter, ArtifactTestBuilder.CreateBody(sections), sections);

        Assert.False(result.Validation.IsValid);
        Assert.Contains(result.Validation.Issues, issue => issue.Path == "status" && issue.Code == "artifact.status.invalid");
    }

    [Fact]
    public void InvalidTimestamp_FailsValidation()
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(ArtifactType.Charter);
        var sections = ArtifactTestBuilder.CreateSections(ArtifactType.Charter);
        frontmatter["created_at"] = "2026-04-14T14:00:00+02:00";

        var result = _factory.Create(frontmatter, ArtifactTestBuilder.CreateBody(sections), sections);

        Assert.False(result.Validation.IsValid);
        Assert.Contains(result.Validation.Issues, issue => issue.Path == "created_at" && issue.Code == "artifact.timestamp.invalid");
    }

    [Fact]
    public void UnknownFrontmatterKey_FailsValidation()
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(ArtifactType.Charter);
        var sections = ArtifactTestBuilder.CreateSections(ArtifactType.Charter);
        frontmatter["roadmap_phase"] = "future";

        var result = _factory.Create(frontmatter, ArtifactTestBuilder.CreateBody(sections), sections);

        Assert.False(result.Validation.IsValid);
        Assert.Contains(result.Validation.Issues, issue => issue.Path == "roadmap_phase" && issue.Code == "artifact.frontmatter.key.unknown");
    }

    [Fact]
    public void RevisionLessThanOne_FailsValidation()
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(ArtifactType.Charter);
        var sections = ArtifactTestBuilder.CreateSections(ArtifactType.Charter);
        frontmatter["revision"] = 0;

        var result = _factory.Create(frontmatter, ArtifactTestBuilder.CreateBody(sections), sections);

        Assert.False(result.Validation.IsValid);
        Assert.Contains(result.Validation.Issues, issue => issue.Path == "revision" && issue.Code == "artifact.revision.invalid");
    }

    [Fact]
    public void InvalidRelationshipKey_FailsValidation()
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(ArtifactType.Charter);
        var sections = ArtifactTestBuilder.CreateSections(ArtifactType.Charter);
        var links = (Dictionary<string, object?>)frontmatter["links"]!;
        links["related_to"] = new List<object?>();

        var result = _factory.Create(frontmatter, ArtifactTestBuilder.CreateBody(sections), sections);

        Assert.False(result.Validation.IsValid);
        Assert.Contains(result.Validation.Issues, issue => issue.Path == "links.related_to" && issue.Code == "artifact.links.key.invalid");
    }

    [Fact]
    public void InvalidRelationshipTarget_FailsValidation()
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(ArtifactType.Charter);
        var sections = ArtifactTestBuilder.CreateSections(ArtifactType.Charter);
        var links = (Dictionary<string, object?>)frontmatter["links"]!;
        links["affects"] = new List<object?> { "Architecture overview" };

        var result = _factory.Create(frontmatter, ArtifactTestBuilder.CreateBody(sections), sections);

        Assert.False(result.Validation.IsValid);
        Assert.Contains(result.Validation.Issues, issue => issue.Path == "links.affects[0]" && issue.Code == "artifact.links.value.invalid");
    }

    [Fact]
    public void PlanMissingAcceptanceCriteria_FailsValidation()
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(ArtifactType.Plan);
        var sections = ArtifactTestBuilder.CreateSections(ArtifactType.Plan);
        sections["Acceptance Criteria"] = "Needs measurable validation but no criteria are listed.";

        var result = _factory.Create(frontmatter, ArtifactTestBuilder.CreateBody(sections), sections);

        Assert.False(result.Validation.IsValid);
        Assert.Contains(result.Validation.Issues, issue => issue.Path == "body.sections.Acceptance Criteria" && issue.Code == "artifact.plan.acceptance_criteria.missing");
    }

    [Fact]
    public void SessionSummaryWithCanonicalTrue_FailsValidation()
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(ArtifactType.SessionSummary);
        var sections = ArtifactTestBuilder.CreateSections(ArtifactType.SessionSummary);
        frontmatter["canonical"] = true;

        var result = _factory.Create(frontmatter, ArtifactTestBuilder.CreateBody(sections), sections);

        Assert.False(result.Validation.IsValid);
        Assert.Contains(result.Validation.Issues, issue => issue.Path == "canonical" && issue.Code == "artifact.session_summary.canonical.invalid");
    }

    [Fact]
    public void NoteArtifact_MinimalFrontmatterArbitraryBody_Valid()
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(ArtifactType.Note);
        var sections = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Body"] = "Just a quick thought I want to keep around."
        };

        var result = _factory.Create(frontmatter, "Just a quick thought I want to keep around.", sections);

        Assert.True(result.Validation.IsValid);
        var note = Assert.IsType<NoteArtifact>(result.Artifact);
        Assert.Equal(ArtifactType.Note, note.Type);
        Assert.StartsWith("NTE-", note.Id);
    }

    [Fact]
    public void NoteArtifact_RejectsTypeSpecificFrontmatter()
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(ArtifactType.Note);
        frontmatter["decision_date"] = "2026-05-14";
        var sections = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Body"] = "Note body."
        };

        var result = _factory.Create(frontmatter, "Note body.", sections);

        Assert.False(result.Validation.IsValid);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == "artifact.frontmatter.key.unknown" && issue.Path == "decision_date");
    }

    [Fact]
    public void NoteArtifact_RejectsNonNotePrefixId()
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(ArtifactType.Note);
        frontmatter["id"] = "ADR-123";
        var sections = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Body"] = "Note body."
        };

        var result = _factory.Create(frontmatter, "Note body.", sections);

        Assert.False(result.Validation.IsValid);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == "artifact.id.invalid" && issue.Path == "id");
    }

    [Theory]
    [MemberData(nameof(AllArtifactTypes))]
    public void MissingRequiredSection_FailsValidation(ArtifactType artifactType)
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(artifactType);
        var sections = ArtifactTestBuilder.CreateSections(artifactType);
        var missingSection = Memora.Core.Validation.ArtifactBodyRules.GetRequiredSections(artifactType)[0];
        sections.Remove(missingSection);

        var result = _factory.Create(frontmatter, ArtifactTestBuilder.CreateBody(sections), sections);

        Assert.False(result.Validation.IsValid);
        Assert.Contains(result.Validation.Issues, issue => issue.Path == $"body.sections.{missingSection}" && issue.Code == "artifact.body.section.missing");
    }

    public static IEnumerable<object[]> AllArtifactTypes()
    {
        foreach (var artifactType in Enum.GetValues<ArtifactType>())
        {
            // The Note type is intentionally low-ceremony and has no required
            // body sections, so the "missing required section" theory does not
            // apply to it.
            if (Memora.Core.Validation.ArtifactBodyRules.GetRequiredSections(artifactType).Count == 0)
            {
                continue;
            }

            yield return [artifactType];
        }
    }
}
