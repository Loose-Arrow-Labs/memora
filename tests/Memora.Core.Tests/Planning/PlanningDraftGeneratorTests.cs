using Memora.Core.Artifacts;
using Memora.Core.Planning;

namespace Memora.Core.Tests.Planning;

public sealed class PlanningDraftGeneratorTests
{
    private readonly PlanningDraftGenerator _generator = new();

    [Fact]
    public void ValidIntake_GeneratesDraftArtifactsSummary()
    {
        var intake = PlanningIntakeTestBuilder.CreateValidIntake();

        var result = _generator.Generate(intake);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.ArtifactValidationIssues);
        Assert.Equal(4, result.DraftArtifacts.Count);
        Assert.Collection(
            result.DraftArtifacts,
            artifact =>
            {
                var plan = Assert.IsType<PlanArtifact>(artifact);
                Assert.Equal(ArtifactStatus.Draft, plan.Status);
                Assert.Equal("PLN-2026041510300001", plan.Id);
                Assert.Equal("planning_import:Operator planning notes", plan.Provenance);
            },
            artifact =>
            {
                var decision = Assert.IsType<ArchitectureDecisionArtifact>(artifact);
                Assert.Equal(ArtifactStatus.Draft, decision.Status);
                Assert.Equal("ADR-2026041510300001", decision.Id);
            },
            artifact =>
            {
                var constraint = Assert.IsType<ConstraintArtifact>(artifact);
                Assert.Equal(ArtifactStatus.Draft, constraint.Status);
                Assert.Equal("CNS-2026041510300001", constraint.Id);
            },
            artifact =>
            {
                var question = Assert.IsType<OpenQuestionArtifact>(artifact);
                Assert.Equal(ArtifactStatus.Draft, question.Status);
                Assert.Equal("QST-2026041510300001", question.Id);
            });

        var summary = Assert.IsType<SessionSummaryArtifact>(result.SessionSummary);
        Assert.Equal(ArtifactStatus.Draft, summary.Status);
        Assert.False(summary.Canonical);
        Assert.Equal(SessionType.Planning, summary.SessionType);
        Assert.Contains("PLN-2026041510300001", summary.Sections["Artifacts Created"]);
    }

    [Fact]
    public void IdenticalInput_ProducesDeterministicDraftIdsAndBodies()
    {
        var intake = PlanningIntakeTestBuilder.CreateValidIntake() with
        {
            Plans =
            [
                ..PlanningIntakeTestBuilder.CreateValidIntake().Plans,
                new PlanDraftSeed(
                    "Add approval queue model",
                    "Represent queued draft items before approval actions exist.",
                    "Limit this to queue state only.",
                    ["Queue entries can be listed deterministically."],
                    "Needed after draft generation.",
                    "Milestone 2 sequencing",
                    ArtifactPriority.Normal,
                    true,
                    ["approval"])
            ]
        };

        var first = _generator.Generate(intake);
        var second = _generator.Generate(intake);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(
            first.DraftArtifacts.Select(artifact => artifact.Id),
            second.DraftArtifacts.Select(artifact => artifact.Id));
        Assert.Equal("PLN-2026041510300002", first.DraftArtifacts.OfType<PlanArtifact>().Last().Id);
        Assert.Equal(
            first.SessionSummary!.Sections["Artifacts Created"],
            second.SessionSummary!.Sections["Artifacts Created"]);
    }

    [Fact]
    public void InvalidIntake_ValidationBlocksArtifactGeneration()
    {
        var intake = PlanningIntakeTestBuilder.CreateValidIntake() with
        {
            Project = new PlanningProjectScope("", ""),
            Plans = []
        };

        var result = _generator.Generate(intake);

        Assert.False(result.IsSuccess);
        Assert.Null(result.SessionSummary);
        Assert.Empty(result.DraftArtifacts);
        Assert.Contains(result.IntakeValidation.Issues, issue => issue.Code == "planning_intake.project_id.required");
    }

    [Fact]
    public void GeneratedQuestionPreservesResolutionForNonOpenStatus()
    {
        var intake = PlanningIntakeTestBuilder.CreateValidIntake();
        intake = intake with
        {
            Questions =
            [
                intake.Questions[0] with
                {
                    Status = QuestionStatus.Resolved,
                    Resolution = "Use generator-owned ids until persistence coordination exists."
                }
            ]
        };

        var result = _generator.Generate(intake);

        Assert.True(result.IsSuccess);
        var question = Assert.Single(result.DraftArtifacts.OfType<OpenQuestionArtifact>());
        Assert.Equal(QuestionStatus.Resolved, question.QuestionStatus);
        Assert.Equal("Use generator-owned ids until persistence coordination exists.", question.Sections["Resolution"]);
    }
}
