using Memora.Context.Extensions;

namespace Memora.Context.Tests.Extensions;

public sealed class OptionalRetrievalExtensionTests
{
    [Fact]
    public void Descriptor_DefaultsToOptionalNonCanonicalBoundary()
    {
        var descriptor = new OptionalRetrievalExtensionDescriptor(
            "Semantic candidate discovery",
            "external-provider",
            OptionalRetrievalExtensionKind.SemanticCandidateDiscovery);

        Assert.False(descriptor.EnabledByDefault);
        Assert.False(descriptor.CanProvideCanonicalTruth);
    }

    [Fact]
    public void Descriptor_RejectsEnabledByDefaultExtensions()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new OptionalRetrievalExtensionDescriptor(
                "Semantic candidate discovery",
                "external-provider",
                OptionalRetrievalExtensionKind.SemanticCandidateDiscovery,
                enabledByDefault: true));

        Assert.Contains("cannot be enabled by default", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Descriptor_RejectsCanonicalTruthClaims()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new OptionalRetrievalExtensionDescriptor(
                "Semantic candidate discovery",
                "external-provider",
                OptionalRetrievalExtensionKind.SemanticCandidateDiscovery,
                canProvideCanonicalTruth: true));

        Assert.Contains("cannot provide canonical truth", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExternalRequest_NormalizesFocusValues()
    {
        var request = new ExternalRetrievalRequest(
            " memora ",
            " Need optional candidates. ",
            focusArtifactIds: [" ADR-002 ", "ADR-001", "ADR-001"],
            focusTags: [" retrieval ", "retrieval"]);

        Assert.Equal("memora", request.ProjectId);
        Assert.Equal("Need optional candidates.", request.TaskDescription);
        Assert.Equal(["ADR-001", "ADR-002"], request.FocusArtifactIds);
        Assert.Equal(["retrieval"], request.FocusTags);
    }

    [Fact]
    public void ExternalCandidate_CarriesAdvisoryIdExplanationOnly()
    {
        var candidate = new ExternalRetrievalCandidate(
            " ADR-001 ",
            "External provider suggests reviewing this artifact.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["provider_trace"] = "trace-001",
                [""] = "ignored"
            });

        Assert.Equal("ADR-001", candidate.ArtifactId);
        Assert.Equal(["provider_trace"], candidate.Metadata.Keys);
    }

    [Fact]
    public void ExtensionContract_DoesNotChangeCoreRetrievalByItself()
    {
        IOptionalRetrievalExtension extension = new TestOptionalRetrievalExtension();

        var candidates = extension.DiscoverCandidates(new ExternalRetrievalRequest("memora", "Need optional context."));

        Assert.Equal(OptionalRetrievalExtensionKind.SemanticCandidateDiscovery, extension.Descriptor.Kind);
        Assert.False(extension.Descriptor.EnabledByDefault);
        Assert.Equal(["ADR-001"], candidates.Select(candidate => candidate.ArtifactId));
    }

    private sealed class TestOptionalRetrievalExtension : IOptionalRetrievalExtension
    {
        public OptionalRetrievalExtensionDescriptor Descriptor { get; } =
            new(
                "Semantic candidate discovery",
                "test-provider",
                OptionalRetrievalExtensionKind.SemanticCandidateDiscovery);

        public IReadOnlyList<ExternalRetrievalCandidate> DiscoverCandidates(ExternalRetrievalRequest request) =>
            [new ExternalRetrievalCandidate("ADR-001", $"Advisory candidate for {request.ProjectId}.")];
    }
}
