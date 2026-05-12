using Memora.Import.Prerequisites;

namespace Memora.Import.Tests.Prerequisites;

public sealed class RuntimePrerequisiteCheckerTests
{
    [Fact]
    public void Check_BothToolsPresent_ReturnsReady()
    {
        var checker = new RuntimePrerequisiteChecker(tool => tool switch
        {
            "git" => "git version 2.43.0",
            "gh" => "gh version 2.40.1 (2024-01-01)",
            _ => string.Empty
        });

        var result = checker.Check();

        Assert.True(result.IsReady);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Check_GitMissing_ReturnsDiagnostic()
    {
        var checker = new RuntimePrerequisiteChecker(tool =>
            tool == "git"
                ? throw new InvalidOperationException("not found")
                : "gh version 2.40.1");

        var result = checker.Check();

        Assert.False(result.IsReady);
        Assert.Contains(result.Diagnostics, d => d.Code == "git.missing" && d.Tool == "git");
    }

    [Fact]
    public void Check_GhMissing_ReturnsDiagnostic()
    {
        var checker = new RuntimePrerequisiteChecker(tool =>
            tool == "gh"
                ? throw new InvalidOperationException("not found")
                : "git version 2.43.0");

        var result = checker.Check();

        Assert.False(result.IsReady);
        Assert.Contains(result.Diagnostics, d => d.Code == "gh.missing" && d.Tool == "gh");
    }

    [Fact]
    public void Check_BothMissing_ReturnsTwoDiagnostics()
    {
        var checker = new RuntimePrerequisiteChecker(_ => throw new InvalidOperationException("not found"));

        var result = checker.Check();

        Assert.False(result.IsReady);
        Assert.Equal(2, result.Diagnostics.Count);
        Assert.Contains(result.Diagnostics, d => d.Code == "git.missing");
        Assert.Contains(result.Diagnostics, d => d.Code == "gh.missing");
    }

    [Fact]
    public void Check_GitVersionBelowMinimum_ReturnsDiagnostic()
    {
        var checker = new RuntimePrerequisiteChecker(tool => tool switch
        {
            "git" => "git version 1.8.0",
            _ => "gh version 2.40.1"
        });

        var result = checker.Check();

        Assert.False(result.IsReady);
        Assert.Contains(result.Diagnostics, d => d.Code == "git.version.unsupported");
    }

    [Fact]
    public void Check_GhVersionBelowMinimum_ReturnsDiagnostic()
    {
        var checker = new RuntimePrerequisiteChecker(tool => tool switch
        {
            "gh" => "gh version 1.14.0",
            _ => "git version 2.43.0"
        });

        var result = checker.Check();

        Assert.False(result.IsReady);
        Assert.Contains(result.Diagnostics, d => d.Code == "gh.version.unsupported");
    }

    [Fact]
    public void Check_ToolReturnsNoVersionOutput_ReturnsMissingDiagnostic()
    {
        var checker = new RuntimePrerequisiteChecker(tool => tool switch
        {
            "git" => "git version 2.43.0",
            _ => string.Empty
        });

        var result = checker.Check();

        Assert.False(result.IsReady);
        Assert.Contains(result.Diagnostics, d => d.Code == "gh.missing");
    }
}
