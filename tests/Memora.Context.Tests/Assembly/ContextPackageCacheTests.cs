using Memora.Context.Assembly;
using Memora.Context.Models;
using Memora.Core.Artifacts;

namespace Memora.Context.Tests.Assembly;

public sealed class ContextPackageCacheTests
{
    private readonly ContextBundleBuilder _builder = new();
    private readonly ContextPackageCache _cache = new();

    [Fact]
    public void GetOrBuild_ReusesPackageForIdenticalRequestArtifacts()
    {
        var request = new ContextBundleRequest("memora", "Prepare cached context.");
        var artifacts = new[] { CreateCharterArtifact() };
        var buildCount = 0;

        var first = _cache.GetOrBuild(request, artifacts, BuildCountingPackage);
        var second = _cache.GetOrBuild(request, artifacts.Reverse().ToArray(), BuildCountingPackage);

        Assert.False(first.WasCacheHit);
        Assert.True(second.WasCacheHit);
        Assert.Same(first.Bundle, second.Bundle);
        Assert.Equal(1, buildCount);

        ContextBundle BuildCountingPackage(
            ContextBundleRequest contextRequest,
            IReadOnlyList<ArtifactDocument> contextArtifacts)
        {
            buildCount++;
            return _builder.Build(contextRequest, contextArtifacts);
        }
    }

    [Fact]
    public void GetOrBuild_MissesWhenArtifactFingerprintChanges()
    {
        var request = new ContextBundleRequest("memora", "Prepare cached context.");
        var buildCount = 0;

        _cache.GetOrBuild(request, [CreateCharterArtifact(title: "Memora charter")], BuildCountingPackage);
        var changed = _cache.GetOrBuild(request, [CreateCharterArtifact(title: "Updated Memora charter")], BuildCountingPackage);

        Assert.False(changed.WasCacheHit);
        Assert.Equal(2, buildCount);

        ContextBundle BuildCountingPackage(
            ContextBundleRequest contextRequest,
            IReadOnlyList<ArtifactDocument> contextArtifacts)
        {
            buildCount++;
            return _builder.Build(contextRequest, contextArtifacts);
        }
    }

    [Fact]
    public void GetOrBuild_MissesWhenRequestShapeChanges()
    {
        var artifacts = new[] { CreateCharterArtifact() };
        var buildCount = 0;

        _cache.GetOrBuild(new ContextBundleRequest("memora", "Prepare cached context."), artifacts, BuildCountingPackage);
        var changed = _cache.GetOrBuild(new ContextBundleRequest("memora", "Prepare cached context.", focusTags: ["cache"]), artifacts, BuildCountingPackage);

        Assert.False(changed.WasCacheHit);
        Assert.Equal(2, buildCount);

        ContextBundle BuildCountingPackage(
            ContextBundleRequest contextRequest,
            IReadOnlyList<ArtifactDocument> contextArtifacts)
        {
            buildCount++;
            return _builder.Build(contextRequest, contextArtifacts);
        }
    }

    private static ProjectCharterArtifact CreateCharterArtifact(string title = "Memora charter") =>
        new(
            "CHR-001",
            "memora",
            ArtifactStatus.Approved,
            title,
            new DateTimeOffset(2026, 4, 23, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 23, 8, 15, 0, TimeSpan.Zero),
            1,
            ["context", "cache"],
            "user",
            "cache test",
            ArtifactLinks.Empty,
            """
            ## Problem Statement
            Context packages should be reusable when filesystem truth has not changed.

            ## Primary Users / Stakeholders
            Operators.

            ## Current Pain
            Repeated requests rebuild identical context packages.

            ## Desired Outcome
            Cache hits preserve deterministic output shape.

            ## Definition of Success
            Cache misses occur when artifact state changes.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Problem Statement"] = "Context packages should be reusable when filesystem truth has not changed.",
                ["Primary Users / Stakeholders"] = "Operators.",
                ["Current Pain"] = "Repeated requests rebuild identical context packages.",
                ["Desired Outcome"] = "Cache hits preserve deterministic output shape.",
                ["Definition of Success"] = "Cache misses occur when artifact state changes."
            });
}
