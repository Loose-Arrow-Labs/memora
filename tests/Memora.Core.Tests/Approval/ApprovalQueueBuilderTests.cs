using Memora.Core.Approval;
using Memora.Core.Artifacts;
using Memora.Core.Planning;
using Memora.Core.Tests.Artifacts;
using Memora.Core.Tests.Planning;
using Memora.Core.Validation;

namespace Memora.Core.Tests.Approval;

public sealed class ApprovalQueueBuilderTests
{
    private readonly ApprovalQueueBuilder _builder = new();
    private readonly PlanningDraftGenerator _draftGenerator = new();
    private readonly ArtifactFactory _artifactFactory = new();

    [Fact]
    public void Build_IncludesOnlyPendingArtifactsForRequestedProject()
    {
        var draftGeneration = _draftGenerator.Generate(PlanningIntakeTestBuilder.CreateValidIntake());
        var artifacts = draftGeneration.DraftArtifacts
            .Append(draftGeneration.SessionSummary!)
            .Append(CreateArtifact("PLN-999", "memora", ArtifactStatus.Approved, "Approved plan"))
            .Append(CreateArtifact("PLN-998", "other-project", ArtifactStatus.Draft, "Other project draft"));

        var queue = _builder.Build("memora", artifacts);

        Assert.Equal("memora", queue.ProjectId);
        Assert.Equal(5, queue.Items.Count);
        Assert.All(queue.Items, item =>
        {
            Assert.Equal("memora", item.ProjectId);
            Assert.True(
                item.PendingStatus is ArtifactStatus.Proposed or ArtifactStatus.Draft,
                $"Expected pending review status but found '{item.PendingStatus}'.");
        });
        Assert.DoesNotContain(queue.Items, item => item.ArtifactId == "PLN-999");
        Assert.DoesNotContain(queue.Items, item => item.ArtifactId == "PLN-998");
    }

    [Fact]
    public void Build_OrdersProposedBeforeDraftByTimestampId()
    {
        var artifacts = new ArtifactDocument[]
        {
            CreateArtifact("PLN-200", "memora", ArtifactStatus.Draft, "Draft later", "2026-04-16T09:05:00Z"),
            CreateArtifact("PLN-100", "memora", ArtifactStatus.Proposed, "Proposal first", "2026-04-16T09:00:00Z"),
            CreateArtifact("PLN-101", "memora", ArtifactStatus.Proposed, "Proposal second", "2026-04-16T09:00:00Z"),
            CreateArtifact("PLN-150", "memora", ArtifactStatus.Draft, "Draft earlier", "2026-04-16T09:00:00Z")
        };

        var queue = _builder.Build("memora", artifacts);

        Assert.Equal(
            ["PLN-100", "PLN-101", "PLN-150", "PLN-200"],
            queue.Items.Select(item => item.ArtifactId).ToArray());
    }

    [Fact]
    public void Build_ReturnsEmptyQueueWhenProjectHasNoPendingArtifacts()
    {
        var artifacts = new[]
        {
            CreateArtifact("PLN-300", "memora", ArtifactStatus.Approved, "Approved artifact"),
            CreateArtifact("PLN-301", "memora", ArtifactStatus.Superseded, "Superseded artifact")
        };

        var queue = _builder.Build("memora", artifacts);

        Assert.Empty(queue.Items);
    }

    [Fact]
    public void QueueItem_PreservesMetadataNeededForLaterApprovalWorkflows()
    {
        var artifact = CreateArtifact("PLN-401", "memora", ArtifactStatus.Proposed, "Review me", "2026-04-16T08:30:00Z");

        var queue = _builder.Build("memora", [artifact]);

        var item = Assert.Single(queue.Items);
        Assert.Equal("PLN-401", item.ArtifactId);
        Assert.Equal(ArtifactType.Plan, item.ArtifactType);
        Assert.Equal(ArtifactStatus.Proposed, item.PendingStatus);
        Assert.Equal("Review me", item.Title);
        Assert.Equal("user", item.Provenance);
        Assert.Equal("queue test", item.Reason);
        Assert.Equal(artifact.UpdatedAtUtc, item.PendingSinceUtc);
    }

    private ArtifactDocument CreateArtifact(
        string id,
        string projectId,
        ArtifactStatus status,
        string title,
        string updatedAt = "2026-04-16T08:00:00Z")
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(ArtifactType.Plan);
        var sections = ArtifactTestBuilder.CreateSections(ArtifactType.Plan);

        frontmatter["id"] = id;
        frontmatter["project_id"] = projectId;
        frontmatter["status"] = status.ToSchemaValue();
        frontmatter["title"] = title;
        frontmatter["created_at"] = updatedAt;
        frontmatter["updated_at"] = updatedAt;
        frontmatter["reason"] = "queue test";

        var result = _artifactFactory.Create(frontmatter, ArtifactTestBuilder.CreateBody(sections), sections);

        var artifact = Assert.IsType<PlanArtifact>(result.Artifact);
        Assert.True(result.Validation.IsValid);
        return artifact;
    }
}
