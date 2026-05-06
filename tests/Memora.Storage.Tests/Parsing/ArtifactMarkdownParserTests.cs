using Memora.Core.Artifacts;
using Memora.Storage.Parsing;

namespace Memora.Storage.Tests.Parsing;

public sealed class ArtifactMarkdownParserTests
{
    private readonly ArtifactMarkdownParser _parser = new();

    [Theory]
    [InlineData("samples/workspaces/demo-project/drafts/decision/ADR-007.r0001.md", ArtifactType.Decision, ArtifactStatus.Draft)]
    [InlineData("samples/workspaces/demo-project/drafts/decision/ADR-008.r0001.md", ArtifactType.Decision, ArtifactStatus.Draft)]
    [InlineData("samples/workspaces/demo-project/drafts/decision/ADR-009.r0001.md", ArtifactType.Decision, ArtifactStatus.Draft)]
    [InlineData("samples/workspaces/demo-project/drafts/decision/ADR-010.r0001.md", ArtifactType.Decision, ArtifactStatus.Draft)]
    [InlineData("samples/workspaces/demo-project/drafts/decision/ADR-011.r0001.md", ArtifactType.Decision, ArtifactStatus.Draft)]
    [InlineData("samples/workspaces/demo-project/drafts/constraint/CNS-005.r0001.md", ArtifactType.Constraint, ArtifactStatus.Draft)]
    [InlineData("samples/workspaces/demo-project/drafts/constraint/CNS-006.r0001.md", ArtifactType.Constraint, ArtifactStatus.Draft)]
    [InlineData("samples/workspaces/demo-project/drafts/constraint/CNS-007.r0001.md", ArtifactType.Constraint, ArtifactStatus.Draft)]
    [InlineData("samples/workspaces/demo-project/drafts/outcome/OUT-001.r0001.md", ArtifactType.Outcome, ArtifactStatus.Draft)]
    [InlineData("samples/workspaces/demo-project/drafts/outcome/OUT-002.r0001.md", ArtifactType.Outcome, ArtifactStatus.Draft)]
    [InlineData("samples/workspaces/demo-project/drafts/plan/PLN-003.r0001.md", ArtifactType.Plan, ArtifactStatus.Draft)]
    [InlineData("samples/workspaces/demo-project/drafts/plan/PLN-004.r0001.md", ArtifactType.Plan, ArtifactStatus.Draft)]
    [InlineData("samples/workspaces/demo-project/drafts/question/QST-004.r0001.md", ArtifactType.Question, ArtifactStatus.Draft)]
    [InlineData("samples/workspaces/demo-project/drafts/question/QST-007.r0001.md", ArtifactType.Question, ArtifactStatus.Draft)]
    public void DemoProjectDraftArtifacts_ParseAndValidate(string relativePath, ArtifactType expectedType, ArtifactStatus expectedStatus)
    {
        var markdown = File.ReadAllText(GetRepositoryPath(relativePath));

        var result = _parser.Parse(markdown);

        Assert.True(result.Validation.IsValid);
        var artifact = Assert.IsAssignableFrom<ArtifactDocument>(result.Artifact);
        Assert.Equal("demo-project", artifact.ProjectId);
        Assert.Equal(expectedType, artifact.Type);
        Assert.Equal(expectedStatus, artifact.Status);
    }

    [Fact]
    public void ValidCharterMarkdown_ParsesAndValidates()
    {
        const string markdown = """
                                 ---
                                 id: CHR-001
                                 project_id: memora
                                 type: charter
                                 status: draft
                                 title: Memora charter
                                 created_at: 2026-04-14T12:00:00Z
                                 updated_at: 2026-04-14T12:30:00Z
                                 revision: 1
                                 tags: []
                                 provenance: user
                                 reason: schema foundation
                                 links:
                                   depends_on: []
                                   affects:
                                     - ADR-001
                                   derived_from: []
                                   supersedes: []
                                 ---
                                 ## Problem Statement
                                 Durable project memory is missing.

                                 ## Primary Users / Stakeholders
                                 Product and engineering teams.

                                 ## Current Pain
                                 Context is lost between iterations.

                                 ## Desired Outcome
                                 Artifact state remains structured and reviewable.

                                 ## Definition of Success
                                 Teams can validate artifacts deterministically.
                                 """;

        var result = _parser.Parse(markdown);

        Assert.True(result.Validation.IsValid);
        var artifact = Assert.IsType<ProjectCharterArtifact>(result.Artifact);
        Assert.Equal("memora", artifact.ProjectId);
        Assert.Single(artifact.Links.Affects);
        Assert.Equal(ArtifactRelationshipKind.Affects, artifact.Links.Affects[0].Kind);
        Assert.Equal("ADR-001", artifact.Links.Affects[0].TargetArtifactId);
    }

    [Fact]
    public void InvalidFrontmatter_IsRejectedCleanly()
    {
        const string markdown = """
                                 ---
                                 id: CHR-001
                                 project_id memora
                                 type: charter
                                 ---
                                 ## Problem Statement
                                 Missing colon above.
                                 """;

        var result = _parser.Parse(markdown);

        Assert.False(result.Validation.IsValid);
        Assert.Null(result.Artifact);
        Assert.Contains(result.Validation.Issues, issue => issue.Code == "frontmatter.parse");
    }

    [Fact]
    public void InvalidRelationshipTargetInMarkdown_IsRejectedCleanly()
    {
        const string markdown = """
                                 ---
                                 id: CHR-001
                                 project_id: memora
                                 type: charter
                                 status: draft
                                 title: Memora charter
                                 created_at: 2026-04-14T12:00:00Z
                                 updated_at: 2026-04-14T12:30:00Z
                                 revision: 1
                                 tags: []
                                 provenance: user
                                 reason: schema foundation
                                 links:
                                   depends_on: []
                                   affects:
                                     - Architecture overview
                                   derived_from: []
                                   supersedes: []
                                 ---
                                 ## Problem Statement
                                 Durable project memory is missing.

                                 ## Primary Users / Stakeholders
                                 Product and engineering teams.

                                 ## Current Pain
                                 Context is lost between iterations.

                                 ## Desired Outcome
                                 Artifact state remains structured and reviewable.

                                 ## Definition of Success
                                 Teams can validate artifacts deterministically.
                                 """;

        var result = _parser.Parse(markdown);

        Assert.False(result.Validation.IsValid);
        Assert.Null(result.Artifact);
        Assert.Contains(result.Validation.Issues, issue => issue.Path == "links.affects[0]" && issue.Code == "artifact.links.value.invalid");
    }

    private static string GetRepositoryPath(string relativePath) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relativePath));
}
