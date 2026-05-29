using Memora.Core.Approval;
using Memora.Core.Artifacts;
using Memora.Core.Editing;
using Memora.Core.Planning;
using Memora.Core.Tests.Artifacts;
using Memora.Core.Tests.Planning;
using Memora.Core.Validation;

namespace Memora.Core.Tests.Approval;

public sealed class ArtifactApprovalWorkflowTests
{
    private readonly ArtifactApprovalWorkflow _workflow = new();
    private readonly PlanningDraftGenerator _draftGenerator = new();
    private readonly DraftArtifactEditor _editor = new();
    private readonly ArtifactFactory _artifactFactory = new();

    [Fact]
    public void Approve_DraftArtifact_PromotesToApproved()
    {
        var generation = _draftGenerator.Generate(PlanningIntakeTestBuilder.CreateValidIntake());
        var draftArtifact = Assert.IsType<PlanArtifact>(generation.DraftArtifacts[0]);

        var result = _workflow.Approve(
            draftArtifact,
            new DateTimeOffset(2026, 04, 16, 10, 00, 00, TimeSpan.Zero));

        Assert.True(result.IsSuccess);
        var approved = Assert.IsType<PlanArtifact>(result.ApprovedArtifact);
        Assert.Equal(ArtifactStatus.Approved, approved.Status);
        Assert.Equal(draftArtifact.Id, approved.Id);
        Assert.Equal(draftArtifact.Revision, approved.Revision);
        Assert.Null(result.SupersededArtifact);
        Assert.Null(result.RejectedArtifact);
    }

    [Fact]
    public void Approve_EditedDraft_SupersedesApproved()
    {
        var currentApproved = CreatePlanArtifact(ArtifactStatus.Approved, revision: 1, updatedAt: "2026-04-16T09:00:00Z");
        var editedDraftResult = _editor.Edit(
            currentApproved with { Status = ArtifactStatus.Draft },
            new DraftArtifactEditRequest(
                Title: "Revised canonical plan",
                Sections: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Goal"] = "Updated goal.",
                    ["Scope"] = "Updated scope.",
                    ["Acceptance Criteria"] = "- still valid\n- ready for approval",
                    ["Notes"] = "Reviewed before promotion."
                }),
            new DateTimeOffset(2026, 04, 16, 09, 30, 00, TimeSpan.Zero));

        var editedDraft = Assert.IsType<PlanArtifact>(editedDraftResult.EditedArtifact);

        var result = _workflow.Approve(
            editedDraft,
            new DateTimeOffset(2026, 04, 16, 10, 00, 00, TimeSpan.Zero),
            currentApproved);

        Assert.True(result.IsSuccess);
        var approved = Assert.IsType<PlanArtifact>(result.ApprovedArtifact);
        var superseded = Assert.IsType<PlanArtifact>(result.SupersededArtifact);
        Assert.Equal(ArtifactStatus.Approved, approved.Status);
        Assert.Equal(2, approved.Revision);
        Assert.Equal("Revised canonical plan", approved.Title);
        Assert.Equal(ArtifactStatus.Superseded, superseded.Status);
        Assert.Equal(2, superseded.Revision);
    }

    [Fact]
    public void Approve_ProposedArtifact_IsRejectedByValidation()
    {
        var proposed = CreatePlanArtifact(ArtifactStatus.Proposed, revision: 1);

        var result = _workflow.Approve(
            proposed,
            new DateTimeOffset(2026, 04, 16, 10, 15, 00, TimeSpan.Zero));

        Assert.False(result.IsSuccess);
        Assert.Null(result.ApprovedArtifact);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == "approval.approve.status.invalid");
    }

    [Fact]
    public void Reject_Draft_DeprecatesWithoutCanonical()
    {
        var draft = CreatePlanArtifact(ArtifactStatus.Draft, revision: 1);

        var result = _workflow.Reject(
            draft,
            new DateTimeOffset(2026, 04, 16, 10, 30, 00, TimeSpan.Zero));

        Assert.True(result.IsSuccess);
        var rejected = Assert.IsType<PlanArtifact>(result.RejectedArtifact);
        Assert.Equal(ArtifactStatus.Deprecated, rejected.Status);
        Assert.Null(result.ApprovedArtifact);
        Assert.Null(result.SupersededArtifact);
    }

    [Fact]
    public void Reject_ProposedArtifact_DeprecatesIt()
    {
        var proposed = CreatePlanArtifact(ArtifactStatus.Proposed, revision: 1);

        var result = _workflow.Reject(
            proposed,
            new DateTimeOffset(2026, 04, 16, 10, 45, 00, TimeSpan.Zero));

        Assert.True(result.IsSuccess);
        var rejected = Assert.IsType<PlanArtifact>(result.RejectedArtifact);
        Assert.Equal(ArtifactStatus.Deprecated, rejected.Status);
    }

    [Fact]
    public void Promote_ProposedArtifact_TransitionsToDraftWithSameRevision()
    {
        var proposed = CreatePlanArtifact(ArtifactStatus.Proposed, revision: 1, updatedAt: "2026-04-16T09:00:00Z");

        var result = _workflow.Promote(
            proposed,
            new DateTimeOffset(2026, 04, 16, 10, 45, 00, TimeSpan.Zero));

        Assert.True(result.IsSuccess);
        var promoted = Assert.IsType<PlanArtifact>(result.PromotedArtifact);
        Assert.Equal(ArtifactStatus.Draft, promoted.Status);
        Assert.Equal(proposed.Id, promoted.Id);
        Assert.Equal(proposed.Revision, promoted.Revision);
        Assert.True(promoted.UpdatedAtUtc > proposed.UpdatedAtUtc);
        Assert.Null(result.ApprovedArtifact);
        Assert.Null(result.RejectedArtifact);
        Assert.Null(result.SupersededArtifact);
    }

    [Fact]
    public void Promote_DraftArtifact_IsRejectedByValidation()
    {
        var draft = CreatePlanArtifact(ArtifactStatus.Draft, revision: 1);

        var result = _workflow.Promote(
            draft,
            new DateTimeOffset(2026, 04, 16, 10, 50, 00, TimeSpan.Zero));

        Assert.False(result.IsSuccess);
        Assert.Null(result.PromotedArtifact);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == "approval.promote.status.invalid");
    }

    [Fact]
    public void Promote_ApprovedArtifact_IsRejectedByValidation()
    {
        var approved = CreatePlanArtifact(ArtifactStatus.Approved, revision: 1);

        var result = _workflow.Promote(
            approved,
            new DateTimeOffset(2026, 04, 16, 10, 55, 00, TimeSpan.Zero));

        Assert.False(result.IsSuccess);
        Assert.Null(result.PromotedArtifact);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == "approval.promote.status.invalid");
    }

    [Fact]
    public void Approve_MismatchedApproved_FailsValidation()
    {
        var generation = _draftGenerator.Generate(PlanningIntakeTestBuilder.CreateValidIntake());
        var draftArtifact = Assert.IsType<PlanArtifact>(generation.DraftArtifacts[0]);
        var currentApproved = CreatePlanArtifact(ArtifactStatus.Approved, revision: 1) with
        {
            Id = "PLN-999"
        };

        var result = _workflow.Approve(
            draftArtifact,
            new DateTimeOffset(2026, 04, 16, 11, 00, 00, TimeSpan.Zero),
            currentApproved);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == "approval.current.id.mismatch");
    }

    [Fact]
    public void Approve_RebuildValidationFailure_ReturnsValidationResult()
    {
        var invalidDraft = CreatePlanArtifact(ArtifactStatus.Draft, revision: 1) with
        {
            Sections = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Goal"] = "Keep this artifact constructible.",
                ["Scope"] = "Simulate a persisted artifact that fails current rebuild rules.",
                ["Acceptance Criteria"] = "   ",
                ["Notes"] = "The public workflow should return validation, not throw."
            }
        };

        var exception = Record.Exception(() => _workflow.Approve(
            invalidDraft,
            new DateTimeOffset(2026, 04, 16, 11, 15, 00, TimeSpan.Zero)));

        Assert.Null(exception);
        var result = _workflow.Approve(
            invalidDraft,
            new DateTimeOffset(2026, 04, 16, 11, 15, 00, TimeSpan.Zero));
        Assert.False(result.IsSuccess);
        Assert.Null(result.ApprovedArtifact);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == "approval.rebuild.invalid");
    }

    [Fact]
    public void Reject_RebuildValidationFailure_ReturnsValidationResult()
    {
        var invalidDraft = CreatePlanArtifact(ArtifactStatus.Draft, revision: 1) with
        {
            Sections = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Goal"] = "Keep this artifact constructible.",
                ["Scope"] = "Simulate a persisted artifact that fails current rebuild rules.",
                ["Acceptance Criteria"] = "   ",
                ["Notes"] = "The public workflow should return validation, not throw."
            }
        };

        var result = _workflow.Reject(
            invalidDraft,
            new DateTimeOffset(2026, 04, 16, 11, 30, 00, TimeSpan.Zero));

        Assert.False(result.IsSuccess);
        Assert.Null(result.RejectedArtifact);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == "approval.rebuild.invalid");
    }

    private PlanArtifact CreatePlanArtifact(
        ArtifactStatus status,
        int revision,
        string updatedAt = "2026-04-16T09:00:00Z")
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(ArtifactType.Plan);
        var sections = ArtifactTestBuilder.CreateSections(ArtifactType.Plan);
        frontmatter["status"] = status.ToSchemaValue();
        frontmatter["revision"] = revision;
        frontmatter["created_at"] = updatedAt;
        frontmatter["updated_at"] = updatedAt;

        var result = _artifactFactory.Create(frontmatter, ArtifactTestBuilder.CreateBody(sections), sections);
        return Assert.IsType<PlanArtifact>(result.Artifact);
    }
}
