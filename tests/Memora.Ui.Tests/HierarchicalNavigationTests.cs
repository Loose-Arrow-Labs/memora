using Memora.Ui.Operator;
using Memora.Ui.Rendering;

namespace Memora.Ui.Tests;

public sealed class HierarchicalNavigationTests : IClassFixture<OperatorShellFactory>
{
    private readonly OperatorShellFactory _factory;

    public HierarchicalNavigationTests(OperatorShellFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Home_RendersTreeWithMemoraRootAndProjectNodes()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);

        var html = await client.GetStringAsync("/");

        Assert.Contains("class=\"tree-pane\"", html, StringComparison.Ordinal);
        Assert.Contains("data-tree-id=\"memora\"", html, StringComparison.Ordinal);
        Assert.Contains("data-tree-id=\"project:demo-project\"", html, StringComparison.Ordinal);
        Assert.Contains(">Demo Project</a>", html, StringComparison.Ordinal);
        Assert.Contains(">Agent resources</a>", html, StringComparison.Ordinal);
        Assert.Contains(">Artifacts</a>", html, StringComparison.Ordinal);
        Assert.Contains(">Project root</a>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<summary class=\"tree-summary\">" + Environment.NewLine + "<a", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Home_BreadcrumbsContainOnlyMemoraAsCurrent()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);

        var html = await client.GetStringAsync("/");
        var breadcrumbsStart = html.IndexOf("class=\"breadcrumbs\"", StringComparison.Ordinal);
        Assert.True(breadcrumbsStart >= 0);
        var breadcrumbsEnd = html.IndexOf("</nav>", breadcrumbsStart, StringComparison.Ordinal);
        var breadcrumbsBlock = html.Substring(breadcrumbsStart, breadcrumbsEnd - breadcrumbsStart);

        Assert.Contains("aria-current=\"page\">Memora</li>", breadcrumbsBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Demo Project", breadcrumbsBlock, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProjectPage_TreeHighlightsCurrentProject()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);

        var html = await client.GetStringAsync("/projects/demo-project");

        Assert.Contains("is-selected\" href=\"/projects/demo-project\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProjectPage_BreadcrumbsEndOnProject()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);

        var html = await client.GetStringAsync("/projects/demo-project");
        var breadcrumbsStart = html.IndexOf("class=\"breadcrumbs\"", StringComparison.Ordinal);
        var breadcrumbsEnd = html.IndexOf("</nav>", breadcrumbsStart, StringComparison.Ordinal);
        var breadcrumbsBlock = html.Substring(breadcrumbsStart, breadcrumbsEnd - breadcrumbsStart);

        Assert.Contains("aria-current=\"page\">Demo Project</li>", breadcrumbsBlock, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AgentResourcesRoute_RendersSectionLanding()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);

        var html = await client.GetStringAsync("/projects/demo-project/agent-resources");

        Assert.Contains("Demo Project · Agent resources", html, StringComparison.Ordinal);
        Assert.Contains("PBR-19", html, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\">Agent resources</li>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("No \"Add a project\" route is wired", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProjectRootRoute_RendersSectionLanding()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);

        var html = await client.GetStringAsync("/projects/demo-project/project-root");

        Assert.Contains("Demo Project · Project root", html, StringComparison.Ordinal);
        Assert.Contains("PBR-20", html, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\">Project root</li>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TreeStateScript_IsEmbedded()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);

        var html = await client.GetStringAsync("/");

        Assert.Contains("memora.tree.expanded", html, StringComparison.Ordinal);
        Assert.Contains("addEventListener('toggle'", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStarted_PreservesExpandedTreeCookie()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);
        client.DefaultRequestHeaders.Add(
            "Cookie",
            $"{HierarchicalCookieState.CookieName}=project:demo-project:artifacts");

        var html = await client.GetStringAsync("/get-started");

        Assert.Contains(
            "data-tree-id=\"project:demo-project:artifacts\" open",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueuePage_TreeHighlightsQueueLeafUnderArtifacts()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);

        var html = await client.GetStringAsync("/projects/demo-project/queue");

        Assert.Contains("class=\"tree-link tree-leaf is-selected\" href=\"/projects/demo-project/queue\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\">Queue</li>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyWorkspaces_TreeShowsAddProjectCta()
    {
        var emptyRoot = Path.Combine(Path.GetTempPath(), "Memora.Ui.Tests.Empty", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyRoot);
        try
        {
            var html = HierarchicalNavigationRenderer.RenderTreeSidebar(
                Array.Empty<OperatorProjectSummary>(),
                HierarchicalSelection.ForRoot(),
                HierarchicalCookieState.Empty);

            Assert.Contains("class=\"tree-pane\"", html, StringComparison.Ordinal);
            Assert.Contains("No projects yet", html, StringComparison.Ordinal);
            Assert.Contains("href=\"/get-started\"", html, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(emptyRoot, recursive: true);
        }
    }

    [Fact]
    public void CookieState_ParsesAndIgnoresJunkEntries()
    {
        var cookieValue = "memora,project:demo-project,project:demo-project:artifacts," + new string('x', 250);

        var parsed = HierarchicalCookieState.Parse(cookieValue);

        Assert.True(parsed.IsExpanded("memora"));
        Assert.True(parsed.IsExpanded("project:demo-project"));
        Assert.True(parsed.IsExpanded("project:demo-project:artifacts"));
        Assert.False(parsed.IsExpanded(new string('x', 250)));
    }
}
