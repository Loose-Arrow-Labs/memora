namespace Memora.Ui.ContextViewer;

using Memora.Core.AgentInteraction;

public sealed record ContextViewerRequest(
    string ProjectId,
    string TaskDescription,
    bool IncludeDraftArtifacts,
    bool IncludeLayer3History);

public sealed record ContextViewerPageModel(
    string? ProjectId,
    string? TaskDescription,
    bool IncludeDraftArtifacts,
    bool IncludeLayer3History,
    string? ErrorMessage,
    AgentContextBundle? Bundle,
    IReadOnlyList<AgentInteractionError> Errors);
