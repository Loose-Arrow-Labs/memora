using Memora.Core.Artifacts;
using Memora.Storage.Parsing;
using Memora.Storage.Persistence;

namespace Memora.Storage.Tests.Persistence;

public sealed class ArtifactMarkdownWriterTests
{
    private readonly ArtifactMarkdownWriter _writer = new();
    private readonly ArtifactMarkdownParser _parser = new();

    [Fact]
    public void Write_TitleSingleDoubleQuotes_RoundTrips()
    {
        const string title = "it's \"complex\"";

        var markdown = _writer.Write(CreatePlanArtifact(title));

        Assert.Contains("title: \"it's \\\"complex\\\"\"", markdown, StringComparison.Ordinal);
        var artifact = ParsePlan(markdown);
        Assert.Equal(title, artifact.Title);
    }

    [Fact]
    public void Write_DoubleQuotedTitleBackslashes_PreservesLiterals()
    {
        const string title = @"it's \\server\share";

        var markdown = _writer.Write(CreatePlanArtifact(title));

        Assert.Contains(@"title: ""it's \\server\share""", markdown, StringComparison.Ordinal);
        var artifact = ParsePlan(markdown);
        Assert.Equal(title, artifact.Title);
    }

    [Fact]
    public void Write_TildeTitle_RoundTripsAsString()
    {
        const string title = "~";

        var markdown = _writer.Write(CreatePlanArtifact(title));

        Assert.Contains("title: '~'", markdown, StringComparison.Ordinal);
        var artifact = ParsePlan(markdown);
        Assert.Equal(title, artifact.Title);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("NULL")]
    [InlineData("~")]
    [InlineData("Yes")]
    [InlineData("No")]
    [InlineData("True")]
    [InlineData("False")]
    [InlineData("!important")]
    [InlineData("&anchor")]
    [InlineData("*alias")]
    [InlineData("> folded")]
    [InlineData("| literal")]
    [InlineData("? key")]
    [InlineData("@handle")]
    [InlineData("`template")]
    [InlineData("contains # comment")]
    [InlineData("key: value")]
    public void Write_ReservedYamlTitleScalars_QuotedRoundTrip(string title)
    {
        var markdown = _writer.Write(CreatePlanArtifact(title));

        var titleLine = markdown.Split('\n').Single(line => line.StartsWith("title: ", StringComparison.Ordinal));
        Assert.True(
            titleLine.StartsWith("title: '", StringComparison.Ordinal) ||
            titleLine.StartsWith("title: \"", StringComparison.Ordinal),
            $"Expected quoted title line, found '{titleLine}'.");
        var artifact = ParsePlan(markdown);
        Assert.Equal(title, artifact.Title);
    }

    private PlanArtifact ParsePlan(string markdown)
    {
        var result = _parser.Parse(markdown);
        Assert.True(result.Validation.IsValid, string.Join(Environment.NewLine, result.Validation.Issues.Select(issue => issue.Message)));
        return Assert.IsType<PlanArtifact>(result.Artifact);
    }

    private static PlanArtifact CreatePlanArtifact(string title) =>
        new(
            "PLN-001",
            "memora",
            ArtifactStatus.Draft,
            title,
            new DateTimeOffset(2026, 4, 14, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 14, 12, 30, 0, TimeSpan.Zero),
            1,
            ["storage", "core"],
            "user",
            "filesystem persistence",
            new ArtifactLinks(["CHR-001"], ["ADR-001"], [], []),
            """
            ## Goal
            Persist artifacts on disk.

            ## Scope
            Limit the change to filesystem storage.

            ## Acceptance Criteria
            - approved artifacts persist in canonical locations
            - draft artifacts persist in draft locations

            ## Notes
            Preserve revision traceability.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Goal"] = "Persist artifacts on disk.",
                ["Scope"] = "Limit the change to filesystem storage.",
                ["Acceptance Criteria"] = "- approved artifacts persist in canonical locations\n- draft artifacts persist in draft locations",
                ["Notes"] = "Preserve revision traceability."
            },
            ArtifactPriority.Normal,
            true);
}
