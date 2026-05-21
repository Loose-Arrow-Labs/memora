using Memora.Core.Artifacts;
using Memora.Core.Editing;
using Memora.Core.Revisions;
using Memora.Core.Tests.Artifacts;
using Memora.Core.Validation;

namespace Memora.Core.Tests.Revisions;

public sealed class ArtifactRevisionDiffBuilderTests
{
    private readonly ArtifactRevisionDiffBuilder _builder = new();
    private readonly DraftArtifactEditor _editor = new();
    private readonly ArtifactFactory _artifactFactory = new();

    [Fact]
    public void Build_EditedDraft_ReturnsDeterministicChanges()
    {
        var currentApproved = CreatePlanArtifact(ArtifactStatus.Approved, revision: 1);
        var editResult = _editor.Edit(
            currentApproved with { Status = ArtifactStatus.Draft },
            new DraftArtifactEditRequest(
                Title: "Updated approved plan",
                Reason: "Operator refined the milestone slice.",
                Tags: ["approval", "planning"],
                Sections: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Goal"] = "Updated goal text.",
                    ["Scope"] = "Existing scope.",
                    ["Acceptance Criteria"] = "- a\n- b",
                    ["Notes"] = "Edited notes."
                },
                TypeSpecificValues: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["priority"] = ArtifactPriority.High.ToSchemaValue(),
                    ["active"] = false
                }),
            new DateTimeOffset(2026, 04, 16, 10, 00, 00, TimeSpan.Zero));

        var candidate = Assert.IsType<PlanArtifact>(editResult.EditedArtifact);

        var result = _builder.Build(currentApproved, candidate);

        Assert.True(result.IsSuccess);
        var diff = Assert.IsType<ArtifactRevisionDiff>(result.Diff);
        Assert.Equal(currentApproved.Revision, diff.CurrentApprovedArtifact.Revision);
        Assert.Equal(candidate.Revision, diff.CandidateArtifact.Revision);
        Assert.Equal(9, diff.ChangeCount);
        Assert.Collection(
            diff.ChangedAreas,
            area => Assert.Equal(ArtifactFieldChangeArea.Metadata, area),
            area => Assert.Equal(ArtifactFieldChangeArea.Sections, area),
            area => Assert.Equal(ArtifactFieldChangeArea.TypeSpecific, area));
        Assert.Collection(
            diff.Changes.Select(change => change.Path),
            path => Assert.Equal("reason", path),
            path => Assert.Equal("sections.Acceptance Criteria", path),
            path => Assert.Equal("sections.Goal", path),
            path => Assert.Equal("sections.Notes", path),
            path => Assert.Equal("sections.Scope", path),
            path => Assert.Equal("tags", path),
            path => Assert.Equal("title", path),
            path => Assert.Equal("type_specific.active", path),
            path => Assert.Equal("type_specific.priority", path));

        var titleChange = Assert.Single(diff.Changes, change => change.Path == "title");
        Assert.Equal("plan title", titleChange.BeforeValue);
        Assert.Equal("Updated approved plan", titleChange.AfterValue);
        Assert.Equal(ArtifactFieldChangeKind.Modified, titleChange.Kind);
        Assert.Equal(ArtifactFieldChangeArea.Metadata, titleChange.Area);
        Assert.Equal("Title", titleChange.DisplayPath);

        var sectionChange = Assert.Single(diff.Changes, change => change.Path == "sections.Goal");
        Assert.Equal(ArtifactFieldChangeArea.Sections, sectionChange.Area);
        Assert.Equal("Section: Goal", sectionChange.DisplayPath);

        var typeSpecificChange = Assert.Single(diff.Changes, change => change.Path == "type_specific.priority");
        Assert.Equal(ArtifactFieldChangeArea.TypeSpecific, typeSpecificChange.Area);
        Assert.Equal("Type-specific: priority", typeSpecificChange.DisplayPath);
    }

    [Fact]
    public void Build_WorkflowOnlyRevision_NoContentChanges()
    {
        var currentApproved = CreatePlanArtifact(ArtifactStatus.Approved, revision: 1);
        var candidate = currentApproved with
        {
            Status = ArtifactStatus.Draft,
            Revision = 2,
            UpdatedAtUtc = new DateTimeOffset(2026, 04, 16, 10, 15, 00, TimeSpan.Zero)
        };

        var result = _builder.Build(currentApproved, candidate);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diff!.Changes);
    }

    [Fact]
    public void Build_WithMismatchedArtifactIdentity_FailsValidation()
    {
        var currentApproved = CreatePlanArtifact(ArtifactStatus.Approved, revision: 1);
        var candidate = currentApproved with
        {
            Id = "PLN-999",
            Status = ArtifactStatus.Draft,
            Revision = 2
        };

        var result = _builder.Build(currentApproved, candidate);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == "revision_diff.id.mismatch");
    }

    [Fact]
    public void Build_WithNonApprovedCurrentArtifact_FailsValidation()
    {
        var currentDraft = CreatePlanArtifact(ArtifactStatus.Draft, revision: 1);
        var candidate = currentDraft with
        {
            Revision = 2
        };

        var result = _builder.Build(currentDraft, candidate);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == "revision_diff.current.status.invalid");
    }

    private PlanArtifact CreatePlanArtifact(
        ArtifactStatus status,
        int revision)
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(ArtifactType.Plan);
        var sections = ArtifactTestBuilder.CreateSections(ArtifactType.Plan);
        frontmatter["status"] = status.ToSchemaValue();
        frontmatter["revision"] = revision;

        var result = _artifactFactory.Create(frontmatter, ArtifactTestBuilder.CreateBody(sections), sections);
        return Assert.IsType<PlanArtifact>(result.Artifact);
    }
}
