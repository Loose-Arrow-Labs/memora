namespace Memora.Core.AgentInteraction;

public enum ExternalRuntimeContractSurface
{
    Mcp,
    OpenApi
}

public enum ExternalRuntimeOperationKind
{
    Read,
    Propose
}

public sealed record ExternalRuntimeOperationDefinition(
    string Name,
    ExternalRuntimeOperationKind Kind,
    string Purpose,
    string RequestContract,
    string ResponseContract,
    IReadOnlyList<ExternalRuntimeContractSurface> SupportedSurfaces,
    bool WritesCanonicalTruth = false)
{
    public string Name { get; } = AgentInteractionContractHelpers.RequireValue(Name, nameof(Name), "Operation name is required.");
    public ExternalRuntimeOperationKind Kind { get; } = Kind;
    public string Purpose { get; } = AgentInteractionContractHelpers.RequireValue(Purpose, nameof(Purpose), "Operation purpose is required.");
    public string RequestContract { get; } = AgentInteractionContractHelpers.RequireValue(RequestContract, nameof(RequestContract), "Request contract is required.");
    public string ResponseContract { get; } = AgentInteractionContractHelpers.RequireValue(ResponseContract, nameof(ResponseContract), "Response contract is required.");
    public IReadOnlyList<ExternalRuntimeContractSurface> SupportedSurfaces { get; } =
        SupportedSurfaces?.Distinct().ToArray() ?? throw new ArgumentNullException(nameof(SupportedSurfaces));
    public bool WritesCanonicalTruth { get; } = WritesCanonicalTruth;
}

public sealed record ExternalRuntimeConstraint(
    string Code,
    string Description)
{
    public string Code { get; } = AgentInteractionContractHelpers.RequireValue(Code, nameof(Code), "Constraint code is required.");
    public string Description { get; } = AgentInteractionContractHelpers.RequireValue(Description, nameof(Description), "Constraint description is required.");
}

public sealed record ExternalRuntimeContractDefinition(
    string Version,
    ExternalRuntimeContractSurface PrimarySurface,
    ExternalRuntimeContractSurface CompanionSurface,
    IReadOnlyList<ExternalRuntimeOperationDefinition> Operations,
    IReadOnlyList<ExternalRuntimeConstraint> Constraints)
{
    public string Version { get; } = AgentInteractionContractHelpers.RequireValue(Version, nameof(Version), "Version is required.");
    public ExternalRuntimeContractSurface PrimarySurface { get; } = PrimarySurface;
    public ExternalRuntimeContractSurface CompanionSurface { get; } = CompanionSurface;
    public IReadOnlyList<ExternalRuntimeOperationDefinition> Operations { get; } =
        Operations?.ToArray() ?? throw new ArgumentNullException(nameof(Operations));
    public IReadOnlyList<ExternalRuntimeConstraint> Constraints { get; } =
        Constraints?.ToArray() ?? throw new ArgumentNullException(nameof(Constraints));
}

public static class ExternalRuntimeContract
{
    public static ExternalRuntimeContractDefinition Current { get; } =
        new(
            "memora.runtime.v1",
            ExternalRuntimeContractSurface.Mcp,
            ExternalRuntimeContractSurface.OpenApi,
            [
                new(
                    "project_lookup",
                    ExternalRuntimeOperationKind.Read,
                    "Resolve a project identity and imported readiness state before runtime-facing context or proposal flows begin.",
                    "projectId",
                    nameof(ProjectLookupResponse),
                    [ExternalRuntimeContractSurface.Mcp, ExternalRuntimeContractSurface.OpenApi]),
                new(
                    "get_context",
                    ExternalRuntimeOperationKind.Read,
                    "Retrieve governed project context through deterministic assembly with explainable inclusion reasoning.",
                    nameof(GetContextRequest),
                    nameof(GetContextResponse),
                    [ExternalRuntimeContractSurface.Mcp, ExternalRuntimeContractSurface.OpenApi]),
                new(
                    "propose_artifact",
                    ExternalRuntimeOperationKind.Propose,
                    "Create a reviewable non-canonical artifact proposal.",
                    nameof(ProposeArtifactRequest),
                    nameof(ProposalResponse),
                    [ExternalRuntimeContractSurface.Mcp, ExternalRuntimeContractSurface.OpenApi]),
                new(
                    "propose_update",
                    ExternalRuntimeOperationKind.Propose,
                    "Create a reviewable non-canonical update proposal for an existing artifact.",
                    nameof(ProposeUpdateRequest),
                    nameof(ProposalResponse),
                    [ExternalRuntimeContractSurface.Mcp, ExternalRuntimeContractSurface.OpenApi]),
                new(
                    "record_outcome",
                    ExternalRuntimeOperationKind.Propose,
                    "Record a reviewable non-canonical outcome artifact without mutating approved truth.",
                    nameof(RecordOutcomeRequest),
                    nameof(OutcomeResponse),
                    [ExternalRuntimeContractSurface.Mcp, ExternalRuntimeContractSurface.OpenApi])
            ],
            [
                new("truth.filesystem", "Filesystem-backed approved artifacts remain canonical truth."),
                new("index.derived", "SQLite remains derived and rebuildable rather than canonical."),
                new("writes.proposal_only", "External runtimes may create reviewable proposals but may not directly write canonical artifacts."),
                new("approval.required", "Canonical state changes still require the normal approval-governed lifecycle."),
                new("retrieval.hybrid_boundary", "Advisory discovery may expose candidates, while grounded context assembly stays deterministic, explainable, lifecycle-aware, and non-semantic in core v1."),
                new("provider.agnostic", "Core behavior stays provider-agnostic and runtime-agnostic across external callers."),
                new("boundary.no_runtime_host", "Memora remains structured memory and governance rather than becoming an execution runtime.")
            ]);
}
