using Memora.Core.AgentInteraction;
using Memora.Core.Artifacts;

namespace Memora.Core.Tests.AgentInteraction;

public sealed class AgentInteractionContractTests
{
    [Fact]
    public void GetContextRequest_UsesSharedContextBundleRequestShape()
    {
        var request = new GetContextRequest(
            "memora",
            "Prepare milestone 3 context.",
            includeDraftArtifacts: true,
            includeLayer3History: true,
            focusArtifactIds: ["ADR-001"],
            focusTags: ["milestone-3"]);

        Assert.Equal("memora", request.ProjectId);
        Assert.True(request.IncludeDraftArtifacts);
        Assert.True(request.IncludeLayer3History);
        Assert.Equal(["ADR-001"], request.FocusArtifactIds);
        Assert.Equal(["milestone-3"], request.FocusTags);
    }

    [Fact]
    public void ProposalRequests_AreExplicitlyProposalOnly()
    {
        var content = CreateContent();

        var newArtifactRequest = new ProposeArtifactRequest("memora", "ADR-001", ArtifactType.Decision, content);
        var updateRequest = new ProposeUpdateRequest("memora", "ADR-001", 2, content);
        var outcomeRequest = new RecordOutcomeRequest("memora", "OUT-001", content);

        Assert.Equal(ArtifactStatus.Proposed, newArtifactRequest.RequestedStatus);
        Assert.Equal(ArtifactStatus.Proposed, updateRequest.RequestedStatus);
        Assert.Equal(ArtifactStatus.Proposed, outcomeRequest.RequestedStatus);
        Assert.Equal(ArtifactType.Outcome, outcomeRequest.ArtifactType);
    }

    [Fact]
    public void ProposalContent_NormalizesTagsPreservesSections()
    {
        var content = new ArtifactProposalContent(
            "Context decision",
            "agent",
            "Need a reviewable proposal.",
            ["context", "milestone-3", "context"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Need deterministic context.",
                ["Decision"] = "Keep the contract explicit."
            });

        Assert.Equal(["context", "milestone-3"], content.Tags);
        Assert.Equal("Need deterministic context.", content.Sections["Context"]);
    }

    [Fact]
    public void ProposalRequest_BlankArtifactId_IsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new ProposeArtifactRequest("memora", " ", ArtifactType.Decision, CreateContent()));

        Assert.Contains("Artifact id is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UpdateRequest_InvalidExpectedRevision_IsRejected()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ProposeUpdateRequest("memora", "ADR-001", 0, CreateContent()));

        Assert.Contains("Expected revision must be greater than zero", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Responses_SurfaceErrorsSuccessState()
    {
        var success = new ProposalResponse("memora", "ADR-001", ArtifactType.Decision, ArtifactStatus.Proposed, 1, []);
        var failure = new GetContextResponse(
            null,
            [new AgentInteractionError("context.validation", "Project id is required.", "project_id")]);

        Assert.True(success.IsSuccess);
        Assert.False(failure.IsSuccess);
        Assert.Equal("context.validation", failure.Errors[0].Code);
    }

    [Fact]
    public void ExternalRuntimeContract_PublishesProposalOnlyOperations()
    {
        var contract = ExternalRuntimeContract.Current;

        Assert.Equal("memora.runtime.v1", contract.Version);
        Assert.Equal(ExternalRuntimeContractSurface.Mcp, contract.PrimarySurface);
        Assert.Equal(ExternalRuntimeContractSurface.OpenApi, contract.CompanionSurface);
        Assert.Contains(contract.Operations, operation => operation.Name == "get_context");
        Assert.Contains(contract.Operations, operation => operation.Name == "propose_artifact");
        Assert.Contains(contract.Operations, operation => operation.Name == "propose_update");
        Assert.Contains(contract.Operations, operation => operation.Name == "record_outcome");
        Assert.DoesNotContain(contract.Operations, operation => operation.WritesCanonicalTruth);
        Assert.Contains(contract.Constraints, constraint => constraint.Code == "writes.proposal_only");
        Assert.Contains(contract.Constraints, constraint => constraint.Code == "boundary.no_runtime_host");
    }

    [Fact]
    public void ProjectStateView_UsesExistingGetContextContractShape()
    {
        var contract = ExternalRuntimeContract.Current;

        Assert.Contains(
            contract.Operations,
            operation => operation.Name == "get_context" &&
                         operation.RequestContract == nameof(GetContextRequest) &&
                         operation.ResponseContract == nameof(GetContextResponse));
        Assert.DoesNotContain(contract.Operations, operation => operation.Name == "get_project_state");
    }

    private static ArtifactProposalContent CreateContent() =>
        new(
            "Context decision",
            "agent",
            "Need a reviewable proposal.",
            ["context"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Need deterministic context.",
                ["Decision"] = "Keep the contract explicit."
            },
            AgentArtifactLinks.Empty,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["decision_date"] = "2026-04-17"
            });
}
