using System.Net;
using System.Text;
using Memora.Ui.Operator;

namespace Memora.Ui.Rendering;

public static class HierarchicalNavigationRenderer
{
    public static string RenderTreeSidebar(
        IReadOnlyList<OperatorProjectSummary> projects,
        HierarchicalSelection selection,
        HierarchicalCookieState cookieState)
    {
        var html = new StringBuilder();
        html.AppendLine("<aside class=\"tree-pane\" aria-label=\"Primary navigation\">");
        html.AppendLine("<nav class=\"tree-nav\">");

        if (projects.Count == 0)
        {
            html.AppendLine("<div class=\"tree-empty\">");
            html.AppendLine("<p class=\"tree-empty-title\">Memora</p>");
            html.AppendLine("<p class=\"tree-empty-hint\">No projects yet.</p>");
            html.AppendLine("<a class=\"button\" href=\"/get-started\">Add a project</a>");
            html.AppendLine("</div>");
            html.AppendLine("</nav>");
            html.AppendLine("</aside>");
            return html.ToString();
        }

        var memoraRootOpen = true;
        html.AppendLine($"<details class=\"tree-node tree-root\" data-tree-id=\"memora\"{(memoraRootOpen ? " open" : string.Empty)}>");
        html.AppendLine("<summary class=\"tree-summary\"><span class=\"tree-label\">Memora</span></summary>");
        html.AppendLine("<ul class=\"tree-list\">");

        foreach (var project in projects)
        {
            html.AppendLine(RenderProjectNode(project, selection, cookieState));
        }

        html.AppendLine("<li class=\"tree-add-action\"><a class=\"tree-add\" href=\"/get-started\">+ Add a project</a></li>");
        html.AppendLine("</ul>");
        html.AppendLine("</details>");
        html.AppendLine("</nav>");
        html.AppendLine("</aside>");
        return html.ToString();
    }

    private static string RenderProjectNode(
        OperatorProjectSummary project,
        HierarchicalSelection selection,
        HierarchicalCookieState cookieState)
    {
        var html = new StringBuilder();
        var projectId = project.ProjectId;
        var nodeId = $"project:{projectId}";
        var isSelectedProject = string.Equals(selection.ProjectId, projectId, StringComparison.Ordinal);
        var isOpen = isSelectedProject || cookieState.IsExpanded(nodeId);
        var openAttr = isOpen ? " open" : string.Empty;

        html.AppendLine("<li>");
        html.AppendLine($"<details class=\"tree-node tree-project\" data-tree-id=\"{Encode(nodeId)}\"{openAttr}>");
        html.AppendLine("<summary class=\"tree-summary\">");
        html.AppendLine($"<span class=\"tree-label\">{Encode(project.Name)}</span>");
        html.AppendLine($"<span class=\"tree-hint\">{Encode(project.ProjectId)}</span>");
        html.AppendLine("</summary>");
        html.AppendLine($"<a class=\"tree-link tree-node-target{(isSelectedProject && selection.Section == HierarchicalSection.None ? " is-selected" : string.Empty)}\" href=\"/projects/{Encode(projectId)}\">{Encode(project.Name)}</a>");

        html.AppendLine("<ul class=\"tree-list\">");
        html.AppendLine(RenderSectionNode(projectId, "Agent resources", HierarchicalSection.AgentResources, selection, cookieState));
        html.AppendLine(RenderSectionNode(projectId, "Artifacts", HierarchicalSection.Artifacts, selection, cookieState));
        html.AppendLine(RenderSectionNode(projectId, "Project root", HierarchicalSection.ProjectRoot, selection, cookieState));
        html.AppendLine("</ul>");

        html.AppendLine("</details>");
        html.AppendLine("</li>");
        return html.ToString();
    }

    private static string RenderSectionNode(
        string projectId,
        string label,
        HierarchicalSection section,
        HierarchicalSelection selection,
        HierarchicalCookieState cookieState)
    {
        var nodeId = $"project:{projectId}:{SectionSlug(section)}";
        var isSelectedSection = string.Equals(selection.ProjectId, projectId, StringComparison.Ordinal) &&
                                selection.Section == section;
        var isOpen = isSelectedSection || cookieState.IsExpanded(nodeId);
        var openAttr = isOpen ? " open" : string.Empty;
        var sectionLanding = SectionLandingPath(projectId, section);
        var isSelectedLanding = isSelectedSection && selection.Leaf is null;

        var html = new StringBuilder();
        html.AppendLine("<li>");
        html.AppendLine($"<details class=\"tree-node tree-section\" data-tree-id=\"{Encode(nodeId)}\"{openAttr}>");
        html.AppendLine($"<summary class=\"tree-summary\"><span class=\"tree-label\">{Encode(label)}</span></summary>");
        html.AppendLine($"<a class=\"tree-link tree-node-target{(isSelectedLanding ? " is-selected" : string.Empty)}\" href=\"{Encode(sectionLanding)}\">{Encode(label)}</a>");

        html.AppendLine("<ul class=\"tree-list\">");
        foreach (var leaf in SectionLeaves(projectId, section))
        {
            var leafSelected = isSelectedSection && string.Equals(selection.Leaf, leaf.Slug, StringComparison.Ordinal);
            var selectedClass = leafSelected ? " is-selected" : string.Empty;
            html.AppendLine($"<li><a class=\"tree-link tree-leaf{selectedClass}\" href=\"{Encode(leaf.Url)}\">{Encode(leaf.Label)}</a></li>");
        }

        html.AppendLine("</ul>");
        html.AppendLine("</details>");
        html.AppendLine("</li>");
        return html.ToString();
    }

    private static IEnumerable<TreeLeaf> SectionLeaves(string projectId, HierarchicalSection section)
    {
        var encodedProjectId = Uri.EscapeDataString(projectId);

        return section switch
        {
            HierarchicalSection.Artifacts =>
            [
                new TreeLeaf("queue", "Queue", $"/projects/{encodedProjectId}/queue"),
                new TreeLeaf("proposals", "Proposals", $"/projects/{encodedProjectId}/proposals"),
                new TreeLeaf("artifacts", "All artifacts", $"/projects/{encodedProjectId}"),
                new TreeLeaf("trust", "Trust dashboard", $"/projects/{encodedProjectId}/trust"),
            ],
            HierarchicalSection.AgentResources =>
            [
                new TreeLeaf("context", "Context viewer", $"/context-viewer?projectId={encodedProjectId}"),
                new TreeLeaf("understanding", "Understanding output", $"/understanding?projectId={encodedProjectId}"),
            ],
            HierarchicalSection.ProjectRoot =>
            [
                new TreeLeaf("first-run", "First-run import", $"/projects/{encodedProjectId}/first-run-import"),
            ],
            _ => Array.Empty<TreeLeaf>()
        };
    }

    public static string SectionLandingPath(string projectId, HierarchicalSection section)
    {
        var encoded = Uri.EscapeDataString(projectId);
        return section switch
        {
            HierarchicalSection.AgentResources => $"/projects/{encoded}/agent-resources",
            HierarchicalSection.Artifacts => $"/projects/{encoded}",
            HierarchicalSection.ProjectRoot => $"/projects/{encoded}/project-root",
            _ => $"/projects/{encoded}"
        };
    }

    private static string SectionSlug(HierarchicalSection section) =>
        section switch
        {
            HierarchicalSection.AgentResources => "agent-resources",
            HierarchicalSection.Artifacts => "artifacts",
            HierarchicalSection.ProjectRoot => "project-root",
            _ => "section"
        };

    public static string RenderBreadcrumbs(
        IReadOnlyList<OperatorProjectSummary> projects,
        HierarchicalSelection selection)
    {
        var html = new StringBuilder();
        html.AppendLine("<nav class=\"breadcrumbs\" aria-label=\"Breadcrumb\">");
        html.AppendLine("<ol>");

        var trailingLeaf = !string.IsNullOrWhiteSpace(selection.Leaf);
        var trailingSection = !trailingLeaf && selection.Section != HierarchicalSection.None;
        var trailingProject = !trailingLeaf && !trailingSection && selection.ProjectId is not null;
        var trailingRoot = selection.ProjectId is null;

        AppendCrumb(html, "Memora", "/", trailingRoot);

        if (selection.ProjectId is not null)
        {
            var project = projects.FirstOrDefault(p =>
                string.Equals(p.ProjectId, selection.ProjectId, StringComparison.Ordinal));
            var projectLabel = project?.Name ?? selection.ProjectId;
            AppendCrumb(html, projectLabel, $"/projects/{Encode(selection.ProjectId)}", trailingProject);

            if (selection.Section != HierarchicalSection.None)
            {
                var sectionLabel = SectionLabel(selection.Section);
                var sectionPath = SectionLandingPath(selection.ProjectId, selection.Section);
                AppendCrumb(html, sectionLabel, sectionPath, trailingSection);
            }

            if (trailingLeaf)
            {
                var leaf = SectionLeaves(selection.ProjectId, selection.Section)
                    .FirstOrDefault(node => string.Equals(node.Slug, selection.Leaf, StringComparison.Ordinal));
                var leafLabel = leaf?.Label ?? selection.Leaf;
                html.AppendLine($"<li aria-current=\"page\">{Encode(leafLabel!)}</li>");
            }
        }

        html.AppendLine("</ol>");
        html.AppendLine("</nav>");
        return html.ToString();
    }

    private static void AppendCrumb(StringBuilder html, string label, string href, bool isCurrent)
    {
        if (isCurrent)
        {
            html.AppendLine($"<li aria-current=\"page\">{Encode(label)}</li>");
        }
        else
        {
            html.AppendLine($"<li><a href=\"{Encode(href)}\">{Encode(label)}</a></li>");
        }
    }

    private static string SectionLabel(HierarchicalSection section) =>
        section switch
        {
            HierarchicalSection.AgentResources => "Agent resources",
            HierarchicalSection.Artifacts => "Artifacts",
            HierarchicalSection.ProjectRoot => "Project root",
            _ => "Section"
        };

    public static string RenderSectionLanding(
        OperatorProjectSummary project,
        HierarchicalSection section,
        bool isAddProjectAvailable)
    {
        var html = new StringBuilder();
        var sectionLabel = SectionLabel(section);
        html.AppendLine("<section class=\"hero compact\">");
        html.AppendLine("<p class=\"eyebrow\">" + Encode(sectionLabel) + "</p>");
        html.AppendLine($"<h1>{Encode(project.Name)} · {Encode(sectionLabel)}</h1>");
        html.AppendLine(SectionLandingLede(section));
        html.AppendLine("</section>");

        html.AppendLine("<section class=\"panel note\">");
        html.AppendLine("<h2>What lives here</h2>");
        html.AppendLine(SectionLandingBody(project.ProjectId, section));
        if (!isAddProjectAvailable)
        {
            html.AppendLine("<p class=\"muted\">No \"Add a project\" route is wired on this branch yet; the home Get started CTA arrives with PR #376.</p>");
        }

        html.AppendLine("</section>");
        return html.ToString();
    }

    private static string SectionLandingLede(HierarchicalSection section) =>
        section switch
        {
            HierarchicalSection.AgentResources =>
                "<p class=\"lede\">Material an agent reads when working on this project: context bundles, AGENTS.md, MCP tool catalog, readiness reports. The full content set is defined in PBR-19; this landing surfaces the existing context and understanding routes today.</p>",
            HierarchicalSection.ProjectRoot =>
                "<p class=\"lede\">The attached source repository and its imported evidence: commits, pull requests, releases. Full content layout is defined in PBR-20; this landing routes to the existing first-run import status for now.</p>",
            _ =>
                "<p class=\"lede\">Project memory artifacts: decisions, plans, constraints, outcomes, charters, questions, and session summaries.</p>"
        };

    private static string SectionLandingBody(string projectId, HierarchicalSection section)
    {
        var encoded = Uri.EscapeDataString(projectId);
        return section switch
        {
            HierarchicalSection.AgentResources =>
                $"<ul class=\"list\"><li><a href=\"/context-viewer?projectId={encoded}\">Context viewer</a> — deterministic context bundle for a task description.</li><li><a href=\"/understanding?projectId={encoded}\">Understanding output</a> — read-only context, traceability, and component views.</li></ul>",
            HierarchicalSection.ProjectRoot =>
                $"<ul class=\"list\"><li><a href=\"/projects/{encoded}/first-run-import\">First-run import status</a> — repository identity, imported evidence counts, candidate memory, readiness.</li></ul>",
            _ =>
                $"<ul class=\"list\"><li><a href=\"/projects/{encoded}/queue\">Approval queue</a></li><li><a href=\"/projects/{encoded}/proposals\">Proposal review</a></li><li><a href=\"/projects/{encoded}\">All artifacts</a></li><li><a href=\"/projects/{encoded}/trust\">Trust dashboard</a></li></ul>"
        };
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private sealed record TreeLeaf(string Slug, string Label, string Url);
}
