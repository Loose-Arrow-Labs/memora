using Memora.Core.Artifacts;
using Memora.Core.Editing;
using Memora.Core.Planning;
using Memora.Core.Tests.Artifacts;
using Memora.Core.Tests.Planning;
using Memora.Core.Validation;

namespace Memora.Core.Tests.Editing;

public sealed class DraftArtifactEditorTests
{
    private readonly DraftArtifactEditor _editor = new();
    private readonly PlanningDraftGenerator _generator = new();
    private readonly ArtifactFactory _artifactFactory = new();

    [Fact]
    public void Edit_DraftPlan_UpdatesFieldsIncrementsRevision()
    {
        var generation = _generator.Generate(PlanningIntakeTestBuilder.CreateValidIntake());
        var original = Assert.IsType<PlanArtifact>(generation.DraftArtifacts[0]);

        var result = _editor.Edit(
            original,
            new DraftArtifactEditRequest(
                Title: "Define planning intake and queue contract",
                Reason: "Expanded scope after review.",
                Tags: ["planning", "queue"],
                Sections: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Goal"] = "Capture planning input and queue-ready draft behavior.",
                    ["Scope"] = "Keep the slice in core models only.",
                    ["Acceptance Criteria"] = "- edited drafts stay non-canonical\n- revision increments",
                    ["Notes"] = "Reviewed by operator before approval."
                },
                TypeSpecificValues: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["priority"] = ArtifactPriority.High.ToSchemaValue(),
                    ["active"] = false
                }),
            new DateTimeOffset(2026, 04, 16, 09, 00, 00, TimeSpan.Zero));

        Assert.True(result.IsSuccess);
        var edited = Assert.IsType<PlanArtifact>(result.EditedArtifact);
        Assert.Equal(1, result.OriginalArtifact.Revision);
        Assert.Equal(2, edited.Revision);
        Assert.Equal(original.Id, edited.Id);
        Assert.Equal("Define planning intake and queue contract", edited.Title);
        Assert.Equal("Expanded scope after review.", edited.Reason);
        Assert.Equal(ArtifactPriority.High, edited.Priority);
        Assert.False(edited.Active);
        Assert.Equal("Capture planning input and queue-ready draft behavior.", edited.Sections["Goal"]);
        Assert.Equal(["planning", "queue"], edited.Tags);
        Assert.Equal(original.Title, result.OriginalArtifact.Title);
    }

    [Fact]
    public void Edit_ApprovedArtifact_FailsValidation()
    {
        var approvedArtifact = CreateArtifact(ArtifactType.Plan) with
        {
            Status = ArtifactStatus.Approved
        };

        var result = _editor.Edit(
            approvedArtifact,
            new DraftArtifactEditRequest(Title: "Should not edit canonical artifact"),
            new DateTimeOffset(2026, 04, 16, 09, 15, 00, TimeSpan.Zero));

        Assert.False(result.IsSuccess);
        Assert.Null(result.EditedArtifact);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == "draft_edit.status.invalid");
    }

    [Fact]
    public void Edit_InvalidSections_FailsArtifactValidation()
    {
        var generation = _generator.Generate(PlanningIntakeTestBuilder.CreateValidIntake());
        var original = Assert.IsType<PlanArtifact>(generation.DraftArtifacts[0]);

        var result = _editor.Edit(
            original,
            new DraftArtifactEditRequest(
                Sections: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Goal"] = "Still a goal.",
                    ["Scope"] = "Still scoped.",
                    ["Acceptance Criteria"] = "No bullet items here.",
                    ["Notes"] = "This should fail plan validation."
                }),
            new DateTimeOffset(2026, 04, 16, 09, 30, 00, TimeSpan.Zero));

        Assert.False(result.IsSuccess);
        Assert.Null(result.EditedArtifact);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == "artifact.plan.acceptance_criteria.missing");
    }

    [Fact]
    public void Edit_InvalidTypeSpecificKey_FailsValidation()
    {
        var generation = _generator.Generate(PlanningIntakeTestBuilder.CreateValidIntake());
        var original = Assert.IsType<SessionSummaryArtifact>(generation.SessionSummary);

        var result = _editor.Edit(
            original,
            new DraftArtifactEditRequest(
                TypeSpecificValues: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["canonical"] = true
                }),
            new DateTimeOffset(2026, 04, 16, 09, 45, 00, TimeSpan.Zero));

        Assert.False(result.IsSuccess);
        Assert.Null(result.EditedArtifact);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == "draft_edit.type_specific_key.invalid");
    }

    private ArtifactDocument CreateArtifact(ArtifactType artifactType)
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(artifactType);
        var sections = ArtifactTestBuilder.CreateSections(artifactType);
        var result = _artifactFactory.Create(frontmatter, ArtifactTestBuilder.CreateBody(sections), sections);

        Assert.True(result.Validation.IsValid);
        return Assert.IsAssignableFrom<ArtifactDocument>(result.Artifact);
    }
}
