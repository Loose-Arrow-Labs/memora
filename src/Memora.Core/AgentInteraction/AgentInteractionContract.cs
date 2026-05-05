using Memora.Core.Artifacts;
using Memora.Core.Automation;
using Memora.Core.Import;

namespace Memora.Core.AgentInteraction;

public sealed record AgentInteractionError(string Code, string Message, string? Path = null)
{
    public string Code { get; } = AgentInteractionContractHelpers.RequireValue(Code, nameof(Code), "Error code is required.");
    public string Message { get; } = AgentInteractionContractHelpers.RequireValue(Message, nameof(Message), "Error message is required.");
    public string? Path { get; } = string.IsNullOrWhiteSpace(Path) ? null : Path.Trim();
}

public abstract record AgentInteractionResponse(IReadOnlyList<AgentInteractionError> Errors)
{
    public IReadOnlyList<AgentInteractionError> Errors { get; } = Errors ?? throw new ArgumentNullException(nameof(Errors));

    public bool IsSuccess => Errors.Count == 0;
}

public sealed record GetContextRequest
{
    public GetContextRequest(
        string projectId,
        string taskDescription,
        bool includeDraftArtifacts = false,
        bool includeLayer3History = false,
        IReadOnlyList<string>? focusArtifactIds = null,
        IReadOnlyList<string>? focusTags = null,
        int maxLayer2Artifacts = 10,
        int maxLayer3Artifacts = 10)
    {
        if (maxLayer2Artifacts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLayer2Artifacts), "Layer 2 artifact limit must be greater than zero.");
        }

        if (maxLayer3Artifacts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLayer3Artifacts), "Layer 3 artifact limit must be greater than zero.");
        }

        ProjectId = AgentInteractionContractHelpers.RequireValue(projectId, nameof(projectId), "Project id is required.");
        TaskDescription = AgentInteractionContractHelpers.RequireValue(taskDescription, nameof(taskDescription), "Task description is required.");
        IncludeDraftArtifacts = includeDraftArtifacts;
        IncludeLayer3History = includeLayer3History;
        FocusArtifactIds = AgentInteractionContractHelpers.NormalizeValues(focusArtifactIds);
        FocusTags = AgentInteractionContractHelpers.NormalizeValues(focusTags);
        MaxLayer2Artifacts = maxLayer2Artifacts;
        MaxLayer3Artifacts = maxLayer3Artifacts;
    }

    public string ProjectId { get; }

    public string TaskDescription { get; }

    public bool IncludeDraftArtifacts { get; }

    public bool IncludeLayer3History { get; }

    public IReadOnlyList<string> FocusArtifactIds { get; }

    public IReadOnlyList<string> FocusTags { get; }

    public int MaxLayer2Artifacts { get; }

    public int MaxLayer3Artifacts { get; }
}

public enum AgentContextLayerKind
{
    Layer1,
    Layer2,
    Layer3
}

public sealed record AgentContextInclusionReason(
    string Code,
    string Description,
    IReadOnlyList<string> RelatedArtifactIds)
{
    public string Code { get; } = AgentInteractionContractHelpers.RequireValue(Code, nameof(Code), "Reason code is required.");
    public string Description { get; } = AgentInteractionContractHelpers.RequireValue(Description, nameof(Description), "Reason description is required.");
    public IReadOnlyList<string> RelatedArtifactIds { get; } = AgentInteractionContractHelpers.NormalizeValues(RelatedArtifactIds);
}

public sealed record AgentContextArtifact(
    ArtifactDocument Artifact,
    IReadOnlyList<AgentContextInclusionReason> InclusionReasons)
{
    public ArtifactDocument Artifact { get; } = Artifact ?? throw new ArgumentNullException(nameof(Artifact));
    public IReadOnlyList<AgentContextInclusionReason> InclusionReasons { get; } =
        InclusionReasons?.ToArray() ?? throw new ArgumentNullException(nameof(InclusionReasons));
}

public sealed record AgentContextLayer(
    AgentContextLayerKind Kind,
    IReadOnlyList<AgentContextArtifact> Artifacts)
{
    public AgentContextLayerKind Kind { get; } = Kind;
    public IReadOnlyList<AgentContextArtifact> Artifacts { get; } =
        Artifacts?.ToArray() ?? throw new ArgumentNullException(nameof(Artifacts));
}

public sealed record AgentContextBundle(
    GetContextRequest Request,
    IReadOnlyList<AgentContextLayer> Layers)
{
    public GetContextRequest Request { get; } = Request ?? throw new ArgumentNullException(nameof(Request));
    public IReadOnlyList<AgentContextLayer> Layers { get; } =
        Layers?.ToArray() ?? throw new ArgumentNullException(nameof(Layers));
}

public sealed record GetContextResponse(AgentContextBundle? Bundle, IReadOnlyList<AgentInteractionError> Errors)
    : AgentInteractionResponse(Errors);

public sealed record ProjectLookupResponse : AgentInteractionResponse
{
    public ProjectLookupResponse(
        string projectId,
        string? name,
        string? status,
        IReadOnlyList<AgentInteractionError> errors,
        IReadOnlyList<ProjectRepositoryAttachment>? repositoryAttachments = null)
        : base(errors)
    {
        ProjectId = AgentInteractionContractHelpers.RequireValue(projectId, nameof(projectId), "Project id is required.");
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        RepositoryAttachments = repositoryAttachments?.ToArray() ?? [];
    }

    public string ProjectId { get; }

    public string? Name { get; }

    public string? Status { get; }

    public IReadOnlyList<ProjectRepositoryAttachment> RepositoryAttachments { get; }
}

public sealed record ArtifactProposalContent
{
    public ArtifactProposalContent(
        string title,
        string provenance,
        string reason,
        IReadOnlyList<string>? tags,
        IReadOnlyDictionary<string, string> sections,
        AgentArtifactLinks? links = null,
        IReadOnlyDictionary<string, object?>? typeSpecificValues = null)
    {
        if (sections is null)
        {
            throw new ArgumentNullException(nameof(sections));
        }

        Title = AgentInteractionContractHelpers.RequireValue(title, nameof(title), "Title is required.");
        Provenance = AgentInteractionContractHelpers.RequireValue(provenance, nameof(provenance), "Provenance is required.");
        Reason = AgentInteractionContractHelpers.RequireValue(reason, nameof(reason), "Reason is required.");
        Tags = AgentInteractionContractHelpers.NormalizeValues(tags);
        Sections = new Dictionary<string, string>(sections, StringComparer.Ordinal);
        Links = links ?? AgentArtifactLinks.Empty;
        TypeSpecificValues = typeSpecificValues is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(typeSpecificValues, StringComparer.Ordinal);
    }

    public string Title { get; }

    public string Provenance { get; }

    public string Reason { get; }

    public IReadOnlyList<string> Tags { get; }

    public IReadOnlyDictionary<string, string> Sections { get; }

    public AgentArtifactLinks Links { get; }

    public IReadOnlyDictionary<string, object?> TypeSpecificValues { get; }
}

public sealed record AgentArtifactLinks(
    IReadOnlyList<string> DependsOn,
    IReadOnlyList<string> Affects,
    IReadOnlyList<string> DerivedFrom,
    IReadOnlyList<string> Supersedes)
{
    public IReadOnlyList<string> DependsOn { get; } = AgentInteractionContractHelpers.NormalizeValues(DependsOn);
    public IReadOnlyList<string> Affects { get; } = AgentInteractionContractHelpers.NormalizeValues(Affects);
    public IReadOnlyList<string> DerivedFrom { get; } = AgentInteractionContractHelpers.NormalizeValues(DerivedFrom);
    public IReadOnlyList<string> Supersedes { get; } = AgentInteractionContractHelpers.NormalizeValues(Supersedes);

    public ArtifactLinks ToArtifactLinks() => new(DependsOn, Affects, DerivedFrom, Supersedes);

    public static AgentArtifactLinks Empty { get; } = new([], [], [], []);
}

public sealed record ProposeArtifactRequest
{
    public ProposeArtifactRequest(
        string projectId,
        string artifactId,
        ArtifactType artifactType,
        ArtifactProposalContent content)
    {
        ProjectId = AgentInteractionContractHelpers.RequireValue(projectId, nameof(projectId), "Project id is required.");
        ArtifactId = AgentInteractionContractHelpers.RequireValue(artifactId, nameof(artifactId), "Artifact id is required.");
        ArtifactType = artifactType;
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public string ProjectId { get; }

    public string ArtifactId { get; }

    public ArtifactType ArtifactType { get; }

    public ArtifactProposalContent Content { get; }

    public ArtifactStatus RequestedStatus => ArtifactStatus.Proposed;
}

public sealed record ProposeUpdateRequest
{
    public ProposeUpdateRequest(
        string projectId,
        string artifactId,
        int expectedRevision,
        ArtifactProposalContent content)
    {
        if (expectedRevision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedRevision), "Expected revision must be greater than zero.");
        }

        ProjectId = AgentInteractionContractHelpers.RequireValue(projectId, nameof(projectId), "Project id is required.");
        ArtifactId = AgentInteractionContractHelpers.RequireValue(artifactId, nameof(artifactId), "Artifact id is required.");
        ExpectedRevision = expectedRevision;
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public string ProjectId { get; }

    public string ArtifactId { get; }

    public int ExpectedRevision { get; }

    public ArtifactProposalContent Content { get; }

    public ArtifactStatus RequestedStatus => ArtifactStatus.Proposed;
}

public sealed record ProposalResponse(
    string ProjectId,
    string ArtifactId,
    ArtifactType ArtifactType,
    ArtifactStatus ResultingStatus,
    int Revision,
    IReadOnlyList<AgentInteractionError> Errors)
    : AgentInteractionResponse(Errors)
{
    public string ProjectId { get; } = AgentInteractionContractHelpers.RequireValue(ProjectId, nameof(ProjectId), "Project id is required.");
    public string ArtifactId { get; } = AgentInteractionContractHelpers.RequireValue(ArtifactId, nameof(ArtifactId), "Artifact id is required.");
    public ArtifactType ArtifactType { get; } = ArtifactType;
    public ArtifactStatus ResultingStatus { get; } = ResultingStatus;
    public int Revision { get; } = Revision > 0
        ? Revision
        : Errors.Count == 0
            ? throw new ArgumentOutOfRangeException(nameof(Revision), "Revision must be greater than zero.")
            : 0;
}

public sealed record RecordOutcomeRequest
{
    public RecordOutcomeRequest(
        string projectId,
        string artifactId,
        ArtifactProposalContent content)
    {
        ProjectId = AgentInteractionContractHelpers.RequireValue(projectId, nameof(projectId), "Project id is required.");
        ArtifactId = AgentInteractionContractHelpers.RequireValue(artifactId, nameof(artifactId), "Artifact id is required.");
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public string ProjectId { get; }

    public string ArtifactId { get; }

    public ArtifactType ArtifactType => ArtifactType.Outcome;

    public ArtifactProposalContent Content { get; }

    public ArtifactStatus RequestedStatus => ArtifactStatus.Proposed;
}

public sealed record OutcomeResponse(
    string ProjectId,
    string ArtifactId,
    ArtifactStatus ResultingStatus,
    int Revision,
    OutcomeKind OutcomeKind,
    IReadOnlyList<AgentInteractionError> Errors)
    : AgentInteractionResponse(Errors)
{
    public string ProjectId { get; } = AgentInteractionContractHelpers.RequireValue(ProjectId, nameof(ProjectId), "Project id is required.");
    public string ArtifactId { get; } = AgentInteractionContractHelpers.RequireValue(ArtifactId, nameof(ArtifactId), "Artifact id is required.");
    public ArtifactStatus ResultingStatus { get; } = ResultingStatus;
    public int Revision { get; } = Revision > 0
        ? Revision
        : Errors.Count == 0
            ? throw new ArgumentOutOfRangeException(nameof(Revision), "Revision must be greater than zero.")
            : 0;
    public OutcomeKind OutcomeKind { get; } = OutcomeKind;
}

public sealed record PolicyGovernedSessionSummaryWriteRequest
{
    public PolicyGovernedSessionSummaryWriteRequest(
        string projectId,
        string artifactId,
        ArtifactProposalContent content,
        ControlledAutomationPolicy policy,
        ControlledAutomationTriggerEvent triggerEvent)
    {
        ProjectId = AgentInteractionContractHelpers.RequireValue(projectId, nameof(projectId), "Project id is required.");
        ArtifactId = AgentInteractionContractHelpers.RequireValue(artifactId, nameof(artifactId), "Artifact id is required.");
        Content = content ?? throw new ArgumentNullException(nameof(content));
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        TriggerEvent = triggerEvent ?? throw new ArgumentNullException(nameof(triggerEvent));
    }

    public string ProjectId { get; }

    public string ArtifactId { get; }

    public ArtifactProposalContent Content { get; }

    public ControlledAutomationPolicy Policy { get; }

    public ControlledAutomationTriggerEvent TriggerEvent { get; }

    public ArtifactType ArtifactType => ArtifactType.SessionSummary;
}

public sealed record PolicyGovernedWriteResponse(
    string ProjectId,
    string ArtifactId,
    ArtifactType ArtifactType,
    ArtifactStatus ResultingStatus,
    int Revision,
    AutomationStorageScope StorageScope,
    string? WrittenPath,
    IReadOnlyList<AgentInteractionError> Errors)
    : AgentInteractionResponse(Errors)
{
    public string ProjectId { get; } = AgentInteractionContractHelpers.RequireValue(ProjectId, nameof(ProjectId), "Project id is required.");
    public string ArtifactId { get; } = AgentInteractionContractHelpers.RequireValue(ArtifactId, nameof(ArtifactId), "Artifact id is required.");
    public ArtifactType ArtifactType { get; } = ArtifactType;
    public ArtifactStatus ResultingStatus { get; } = ResultingStatus;
    public int Revision { get; } = Revision > 0
        ? Revision
        : Errors.Count == 0
            ? throw new ArgumentOutOfRangeException(nameof(Revision), "Revision must be greater than zero.")
            : 0;
    public AutomationStorageScope StorageScope { get; } = StorageScope;
    public string? WrittenPath { get; } = string.IsNullOrWhiteSpace(WrittenPath) ? null : WrittenPath.Trim();
}

public interface IAgentInteractionService
{
    ProjectLookupResponse GetProject(string projectId);

    GetContextResponse GetContext(GetContextRequest request);

    ProposalResponse ProposeArtifact(ProposeArtifactRequest request);

    ProposalResponse ProposeUpdate(ProposeUpdateRequest request);

    OutcomeResponse RecordOutcome(RecordOutcomeRequest request);
}

internal static class AgentInteractionContractHelpers
{
    internal static string RequireValue(string value, string parameterName, string message) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(message, parameterName)
            : value.Trim();

    internal static IReadOnlyList<string> NormalizeValues(IReadOnlyList<string>? values) =>
        values is null
            ? []
            : values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
}
