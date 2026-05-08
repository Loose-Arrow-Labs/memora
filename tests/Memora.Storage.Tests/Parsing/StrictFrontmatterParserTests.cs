using System.Text;
using Memora.Storage.Parsing;

namespace Memora.Storage.Tests.Parsing;

public sealed class StrictFrontmatterParserTests
{
    [Fact]
    public void Parse_DeepFrontmatterBeyondLimit_ReturnsValidationIssue()
    {
        var result = StrictFrontmatterParser.Parse(CreateNestedFrontmatter(depth: 64));

        Assert.False(result.Validation.IsValid);
        Assert.Empty(result.Frontmatter);
        var issue = Assert.Single(result.Validation.Issues, issue => issue.Code == "frontmatter.parse.depth_exceeded");
        Assert.Contains("maximum depth of 32", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ThirtyDeepFrontmatter_ParsesSuccessfully()
    {
        var result = StrictFrontmatterParser.Parse(CreateNestedFrontmatter(depth: 30));

        Assert.True(result.Validation.IsValid);
        Assert.Empty(result.Validation.Issues);
        var current = result.Frontmatter;

        for (var level = 1; level <= 30; level++)
        {
            current = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(current[$"level{level:D2}"]);
        }

        Assert.Equal("ok", current["value"]);
    }

    private static string CreateNestedFrontmatter(int depth)
    {
        var builder = new StringBuilder();

        for (var level = 1; level <= depth; level++)
        {
            builder.Append(' ', (level - 1) * 2);
            builder.Append("level");
            builder.Append(level.ToString("D2"));
            builder.AppendLine(":");
        }

        builder.Append(' ', depth * 2);
        builder.AppendLine("value: ok");

        return builder.ToString();
    }
}
