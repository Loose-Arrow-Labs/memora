using Memora.Core.Approval;
using Memora.Core.Artifacts;
using Memora.Core.Editing;
using Memora.Core.Planning;
using Memora.Core.Tests.Planning;

namespace Memora.Core.Tests.Workflow;

public sealed class HumanLoopWorkflowTests
{
    private readonly PlanningDraftGenerator _draftGenerator = new();
    private readonly ApprovalQueueBuilder _approvalQueueBuilder = new();
    private readonly DraftArtifactEditor _draftArtifactEditor = new();
    private readonly ArtifactApprovalWorkflow _approvalWorkflow = new();

    [Fact]
    public void PlanningToDraft_Approval_PromotesCanonicalOnly()
    {
        var intake = PlanningIntakeTestBuilder.CreateValidIntake();

        var generation = _draftGenerator.Generate(intake);

        Assert.True(generation.IsSuccess);
        var draftArtifacts = generation.DraftArtifacts
            .Append(generation.SessionSummary!)
            .ToArray();

        Assert.All(
            draftArtifacts,
            artifact => Assert.NotEqual(ArtifactStatus.Approved, artifact.Status));

        var initialQueue = _approvalQueueBuilder.Build(intake.Project.ProjectId, draftArtifacts);
        Assert.Equal(draftArtifacts.Length, initialQueue.Items.Count);

        var originalPlan = Assert.Single(draftArtifacts.OfType<PlanArtifact>());
        var editedDraftResult = _draftArtifactEditor.Edit(
            originalPlan,
            new DraftArtifactEditRequest(
                Title: "Define planning intake review flow",
                Reason: "Expanded after operator review.",
                Tags: ["planning", "review"],
                Sections: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Goal"] = "Connect planning intake to a reviewable operator workflow.",
                    ["Scope"] = "Keep this path in core validation and lifecycle behavior.",
                    ["Acceptance Criteria"] = "- drafts stay pending before approval\n- approved artifacts leave the pending queue",
                    ["Notes"] = "This edited draft is the candidate for approval."
                }),
            new DateTimeOffset(2026, 04, 16, 11, 00, 00, TimeSpan.Zero));

        Assert.True(editedDraftResult.IsSuccess);
        var editedPlan = Assert.IsType<PlanArtifact>(editedDraftResult.EditedArtifact);
        Assert.Equal(ArtifactStatus.Draft, editedPlan.Status);
        Assert.Equal(2, editedPlan.Revision);

        var beforeApprovalState = ReplaceArtifact(draftArtifacts, originalPlan, editedPlan);
        Assert.DoesNotContain(beforeApprovalState, artifact => artifact.Status == ArtifactStatus.Approved);

        var approvalResult = _approvalWorkflow.Approve(
            editedPlan,
            new DateTimeOffset(2026, 04, 16, 11, 15, 00, TimeSpan.Zero));

        Assert.True(approvalResult.IsSuccess);
        var approvedPlan = Assert.IsType<PlanArtifact>(approvalResult.ApprovedArtifact);
        Assert.Equal(ArtifactStatus.Approved, approvedPlan.Status);

        var afterApprovalState = ReplaceArtifact(beforeApprovalState, editedPlan, approvedPlan);
        var queueAfterApproval = _approvalQueueBuilder.Build(intake.Project.ProjectId, afterApprovalState);

        Assert.Contains(
            afterApprovalState,
            artifact => artifact.Id == approvedPlan.Id && artifact.Status == ArtifactStatus.Approved);
        Assert.DoesNotContain(
            queueAfterApproval.Items,
            item => item.ArtifactId == approvedPlan.Id);
        Assert.Contains(
            queueAfterApproval.Items,
            item => item.ArtifactId != approvedPlan.Id);
    }

    [Fact]
    public void RejectPending_RemovesQueue_NoCanonical()
    {
        var intake = PlanningIntakeTestBuilder.CreateValidIntake();
        var generation = _draftGenerator.Generate(intake);
        var questionDraft = Assert.Single(generation.DraftArtifacts.OfType<OpenQuestionArtifact>());

        var initialQueue = _approvalQueueBuilder.Build(intake.Project.ProjectId, generation.DraftArtifacts);
        Assert.Contains(initialQueue.Items, item => item.ArtifactId == questionDraft.Id);

        var rejectionResult = _approvalWorkflow.Reject(
            questionDraft,
            new DateTimeOffset(2026, 04, 16, 11, 30, 00, TimeSpan.Zero));

        Assert.True(rejectionResult.IsSuccess);
        var rejectedQuestion = Assert.IsType<OpenQuestionArtifact>(rejectionResult.RejectedArtifact);
        Assert.Equal(ArtifactStatus.Deprecated, rejectedQuestion.Status);
        Assert.Null(rejectionResult.ApprovedArtifact);

        var afterRejectState = ReplaceArtifact(generation.DraftArtifacts, questionDraft, rejectedQuestion);
        var queueAfterReject = _approvalQueueBuilder.Build(intake.Project.ProjectId, afterRejectState);

        Assert.DoesNotContain(
            afterRejectState,
            artifact => artifact.Id == questionDraft.Id && artifact.Status == ArtifactStatus.Approved);
        Assert.DoesNotContain(
            queueAfterReject.Items,
            item => item.ArtifactId == questionDraft.Id);
    }

    [Fact]
    public void InvalidEdit_ValidationBeforeApproval_KeepsDraft()
    {
        var intake = PlanningIntakeTestBuilder.CreateValidIntake();
        var generation = _draftGenerator.Generate(intake);
        var planDraft = Assert.Single(generation.DraftArtifacts.OfType<PlanArtifact>());

        var invalidEditResult = _draftArtifactEditor.Edit(
            planDraft,
            new DraftArtifactEditRequest(
                Sections: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Goal"] = "Still a valid goal.",
                    ["Scope"] = "Still a valid scope.",
                    ["Acceptance Criteria"] = "This is not a bullet list.",
                    ["Notes"] = "Validation should stop the workflow."
                }),
            new DateTimeOffset(2026, 04, 16, 11, 45, 00, TimeSpan.Zero));

        Assert.False(invalidEditResult.IsSuccess);
        Assert.Null(invalidEditResult.EditedArtifact);
        Assert.Contains(
            invalidEditResult.Validation.Issues,
            issue => issue.Code == "artifact.plan.acceptance_criteria.missing");

        var queueAfterInvalidEdit = _approvalQueueBuilder.Build(intake.Project.ProjectId, generation.DraftArtifacts);

        Assert.Contains(
            queueAfterInvalidEdit.Items,
            item => item.ArtifactId == planDraft.Id);
        Assert.DoesNotContain(
            generation.DraftArtifacts,
            artifact => artifact.Id == planDraft.Id && artifact.Status == ArtifactStatus.Approved);
    }

    private static IReadOnlyList<ArtifactDocument> ReplaceArtifact(
        IEnumerable<ArtifactDocument> artifacts,
        ArtifactDocument originalArtifact,
        ArtifactDocument replacementArtifact) =>
        artifacts
            .Where(artifact =>
                !string.Equals(artifact.Id, originalArtifact.Id, StringComparison.Ordinal) ||
                artifact.Revision != originalArtifact.Revision ||
                artifact.Status != originalArtifact.Status)
            .Append(replacementArtifact)
            .ToArray();
}
