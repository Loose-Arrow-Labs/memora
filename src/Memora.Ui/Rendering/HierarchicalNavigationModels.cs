namespace Memora.Ui.Rendering;

public enum HierarchicalSection
{
    None,
    AgentResources,
    Artifacts,
    ProjectRoot
}

public sealed record HierarchicalSelection(
    string? ProjectId,
    HierarchicalSection Section,
    string? Leaf,
    string CurrentPath)
{
    public static HierarchicalSelection ForRoot() => new(null, HierarchicalSection.None, null, "/");

    public static HierarchicalSelection ForProject(string projectId, string currentPath) =>
        new(projectId, HierarchicalSection.None, null, currentPath);

    public static HierarchicalSelection ForSection(
        string projectId,
        HierarchicalSection section,
        string currentPath) =>
        new(projectId, section, null, currentPath);

    public static HierarchicalSelection ForLeaf(
        string projectId,
        HierarchicalSection section,
        string leaf,
        string currentPath) =>
        new(projectId, section, leaf, currentPath);
}

public sealed record HierarchicalCookieState(IReadOnlyList<string> ExpandedNodeIds)
{
    public IReadOnlyList<string> ExpandedNodeIds { get; } =
        ExpandedNodeIds?.ToArray() ?? throw new ArgumentNullException(nameof(ExpandedNodeIds));

    public const string CookieName = "memora.tree.expanded";

    public static HierarchicalCookieState Empty { get; } = new(Array.Empty<string>());

    public bool IsExpanded(string nodeId) =>
        ExpandedNodeIds.Any(id => string.Equals(id, nodeId, StringComparison.Ordinal));

    public static HierarchicalCookieState Parse(string? cookieValue)
    {
        if (string.IsNullOrWhiteSpace(cookieValue))
        {
            return Empty;
        }

        var ids = cookieValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(id => id.Length > 0 && id.Length <= 200)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new HierarchicalCookieState(ids);
    }
}
