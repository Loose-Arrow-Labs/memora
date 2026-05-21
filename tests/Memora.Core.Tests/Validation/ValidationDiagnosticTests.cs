using Memora.Core.Planning;
using Memora.Core.Validation;

namespace Memora.Core.Tests.Validation;

public sealed class ValidationDiagnosticTests
{
    [Fact]
    public void ValidationIssue_Diagnostic_ContainsCodePath()
    {
        var issue = new ArtifactValidationIssue(
            "artifact.title.required",
            "Artifact title is required.",
            "title");

        Assert.Equal(
            "Artifact title is required. (code: artifact.title.required; path: title)",
            issue.DiagnosticMessage);
    }

    [Fact]
    public void ArtifactValidationIssue_DiagnosticMessage_HandlesMissingPath()
    {
        var issue = new ArtifactValidationIssue(
            "frontmatter.parse",
            "Frontmatter could not be parsed.");

        Assert.Equal(
            "Frontmatter could not be parsed. (code: frontmatter.parse; path: not provided)",
            issue.DiagnosticMessage);
    }

    [Fact]
    public void PlanningIntakeIssue_Diagnostic_UsesSameShape()
    {
        var issue = new PlanningIntakeValidationIssue(
            "planning_intake.project_id.required",
            "Project id is required.",
            "project.project_id");

        Assert.Equal(
            "Project id is required. (code: planning_intake.project_id.required; path: project.project_id)",
            issue.DiagnosticMessage);
    }
}
