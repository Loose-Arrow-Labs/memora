using System.Diagnostics;
using Memora.Core.AgentInteraction;
using Memora.Core.Artifacts;

namespace Memora.Mcp.Server;

internal static class McpContractHelpers
{
    internal static string RequireValue(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
}

public sealed record McpErrorContract(string Code, string Description)
{
    public string Code { get; } = McpContractHelpers.RequireValue(Code, nameof(Code));
    public string Description { get; } = McpContractHelpers.RequireValue(Description, nameof(Description));
}

public sealed record McpToolDefinition(
    string Name,
    string Description,
    string RequestContract,
    string ResponseContract,
    IReadOnlyList<McpErrorContract> Errors)
{
    public string Name { get; } = McpContractHelpers.RequireValue(Name, nameof(Name));
    public string Description { get; } = McpContractHelpers.RequireValue(Description, nameof(Description));
    public string RequestContract { get; } = McpContractHelpers.RequireValue(RequestContract, nameof(RequestContract));
    public string ResponseContract { get; } = McpContractHelpers.RequireValue(ResponseContract, nameof(ResponseContract));
    public IReadOnlyList<McpErrorContract> Errors { get; } = Errors?.ToArray() ?? throw new ArgumentNullException(nameof(Errors));
}

public sealed record McpResourceDefinition(
    string UriTemplate,
    string Name,
    string Description,
    string ResponseContract,
    IReadOnlyList<McpErrorContract> Errors)
{
    public string UriTemplate { get; } = McpContractHelpers.RequireValue(UriTemplate, nameof(UriTemplate));
    public string Name { get; } = McpContractHelpers.RequireValue(Name, nameof(Name));
    public string Description { get; } = McpContractHelpers.RequireValue(Description, nameof(Description));
    public string ResponseContract { get; } = McpContractHelpers.RequireValue(ResponseContract, nameof(ResponseContract));
    public IReadOnlyList<McpErrorContract> Errors { get; } = Errors?.ToArray() ?? throw new ArgumentNullException(nameof(Errors));
}

public sealed record McpResourceReadResult<TPayload>(string Uri, TPayload? Payload, IReadOnlyList<AgentInteractionError> Errors)
{
    public string Uri { get; } = McpContractHelpers.RequireValue(Uri, nameof(Uri));
    public TPayload? Payload { get; } = Payload;
    public IReadOnlyList<AgentInteractionError> Errors { get; } = Errors?.ToArray() ?? throw new ArgumentNullException(nameof(Errors));
    public bool IsSuccess => Errors.Count == 0;
}

public sealed record McpToolCallResult(
    string Name,
    string? RequestContract,
    string? ResponseContract,
    object? Payload,
    IReadOnlyList<AgentInteractionError> Errors)
{
    public string Name { get; } = McpContractHelpers.RequireValue(Name, nameof(Name));
    public string? RequestContract { get; } = string.IsNullOrWhiteSpace(RequestContract) ? null : RequestContract.Trim();
    public string? ResponseContract { get; } = string.IsNullOrWhiteSpace(ResponseContract) ? null : ResponseContract.Trim();
    public object? Payload { get; } = Payload;
    public IReadOnlyList<AgentInteractionError> Errors { get; } = Errors?.ToArray() ?? throw new ArgumentNullException(nameof(Errors));
    public bool IsSuccess => Errors.Count == 0;
}

public sealed class MemoraMcpServer
{
    private const string ProjectResourcePrefix = "memora://projects/";

    private readonly IAgentInteractionService _service;
    private readonly IReadOnlyDictionary<string, McpToolBinding> _toolBindings;

    public MemoraMcpServer(IAgentInteractionService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));

        var toolBindings = new[]
        {
            new McpToolBinding(
                new McpToolDefinition(
                    "get_context",
                    "Retrieve grounded context for a project task through deterministic assembly.",
                    nameof(GetContextRequest),
                    nameof(GetContextResponse),
                    [new McpErrorContract("context.validation", "The context request did not satisfy shared validation rules.")]),
                typeof(GetContextRequest),
                request => GetContext((GetContextRequest)request)),
            new McpToolBinding(
                new McpToolDefinition(
                    "propose_artifact",
                    "Submit a new artifact proposal in non-canonical state.",
                    nameof(ProposeArtifactRequest),
                    nameof(ProposalResponse),
                    [new McpErrorContract("proposal.validation", "The proposal did not satisfy shared validation rules.")]),
                typeof(ProposeArtifactRequest),
                request => ProposeArtifact((ProposeArtifactRequest)request)),
            new McpToolBinding(
                new McpToolDefinition(
                    "propose_update",
                    "Submit a proposed update for an existing artifact.",
                    nameof(ProposeUpdateRequest),
                    nameof(ProposalResponse),
                    [new McpErrorContract("proposal.validation", "The update proposal did not satisfy shared validation rules.")]),
                typeof(ProposeUpdateRequest),
                request => ProposeUpdate((ProposeUpdateRequest)request)),
            new McpToolBinding(
                new McpToolDefinition(
                    "record_outcome",
                    "Record an execution outcome as a reviewable non-canonical artifact.",
                    nameof(RecordOutcomeRequest),
                    nameof(OutcomeResponse),
                    [new McpErrorContract("outcome.validation", "The outcome request did not satisfy shared validation rules.")]),
                typeof(RecordOutcomeRequest),
                request => RecordOutcome((RecordOutcomeRequest)request))
        };

        Tools = toolBindings.Select(binding => binding.Definition).ToArray();
        _toolBindings = toolBindings.ToDictionary(binding => binding.Definition.Name, StringComparer.Ordinal);
    }

    public IReadOnlyList<McpToolDefinition> Tools { get; }

    public IReadOnlyList<McpResourceDefinition> Resources { get; } =
    [
        new(
            "memora://projects/{projectId}",
            "project",
            "Read project metadata and imported readiness state for a Memora workspace.",
            nameof(ProjectLookupResponse),
            [
                new McpErrorContract("mcp.resource.uri.invalid", "The resource URI does not match the published project resource template."),
                new McpErrorContract("project.not_found", "The requested project could not be found in the configured workspace root.")
            ])
    ];

    public ProjectLookupResponse ReadProject(string projectId)
    {
        try
        {
            return _service.GetProject(projectId);
        }
        catch (Exception exception)
        {
            return new ProjectLookupResponse(
                projectId,
                null,
                null,
                [CreateBoundaryExecutionError(exception, "project_id")]);
        }
    }

    public McpToolCallResult InvokeTool(string name, object? request)
    {
        if (!_toolBindings.TryGetValue(name, out var binding))
        {
            return new McpToolCallResult(
                name,
                null,
                null,
                null,
                [new AgentInteractionError("mcp.tool.not_found", $"Tool '{name}' is not part of the published Memora MCP surface.", "name")]);
        }

        if (request is null || !binding.RequestType.IsInstanceOfType(request))
        {
            return new McpToolCallResult(
                binding.Definition.Name,
                binding.Definition.RequestContract,
                binding.Definition.ResponseContract,
                null,
                [
                    new AgentInteractionError(
                        "mcp.tool.request.invalid",
                        $"Tool '{binding.Definition.Name}' requires request contract '{binding.Definition.RequestContract}'.",
                        "arguments")
                ]);
        }

        var response = binding.Invoke(request);
        return new McpToolCallResult(
            binding.Definition.Name,
            binding.Definition.RequestContract,
            binding.Definition.ResponseContract,
            response,
            response.Errors);
    }

    public McpResourceReadResult<ProjectLookupResponse> ReadResource(string uri)
    {
        if (!TryParseProjectResourceUri(uri, out var projectId))
        {
            return new McpResourceReadResult<ProjectLookupResponse>(
                uri,
                null,
                [new AgentInteractionError("mcp.resource.uri.invalid", "Resource URI must match memora://projects/{projectId}.", "uri")]);
        }

        var response = ReadProject(projectId);
        return new McpResourceReadResult<ProjectLookupResponse>(uri, response, response.Errors);
    }

    public GetContextResponse GetContext(GetContextRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return _service.GetContext(request);
        }
        catch (Exception exception)
        {
            return new GetContextResponse(
                null,
                [CreateBoundaryExecutionError(exception, "request")]);
        }
    }

    public ProposalResponse ProposeArtifact(ProposeArtifactRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return _service.ProposeArtifact(request);
        }
        catch (Exception exception)
        {
            return new ProposalResponse(
                request.ProjectId,
                request.ArtifactId,
                request.ArtifactType,
                ArtifactStatus.Proposed,
                0,
                [CreateBoundaryExecutionError(exception, "request")]);
        }
    }

    public ProposalResponse ProposeUpdate(ProposeUpdateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return _service.ProposeUpdate(request);
        }
        catch (Exception exception)
        {
            return new ProposalResponse(
                request.ProjectId,
                request.ArtifactId,
                ArtifactType.Plan,
                ArtifactStatus.Proposed,
                0,
                [CreateBoundaryExecutionError(exception, "request")]);
        }
    }

    public OutcomeResponse RecordOutcome(RecordOutcomeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return _service.RecordOutcome(request);
        }
        catch (Exception exception)
        {
            return new OutcomeResponse(
                request.ProjectId,
                request.ArtifactId,
                ArtifactStatus.Proposed,
                0,
                OutcomeKind.Mixed,
                [CreateBoundaryExecutionError(exception, "request")]);
        }
    }

    private static bool TryParseProjectResourceUri(string uri, out string projectId)
    {
        projectId = string.Empty;

        if (string.IsNullOrWhiteSpace(uri) || !uri.StartsWith(ProjectResourcePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var candidate = uri[ProjectResourcePrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Contains('/'))
        {
            return false;
        }

        projectId = candidate;
        return true;
    }

    private static AgentInteractionError CreateBoundaryExecutionError(Exception exception, string path)
    {
        var sanitized = AgentInteractionExceptionSanitizer.Sanitize(exception);
        Trace.TraceError("MCP boundary exception sanitized as {0}: {1}", sanitized.Code, exception);
        return sanitized.ToError(path);
    }

    private sealed record McpToolBinding(
        McpToolDefinition Definition,
        Type RequestType,
        Func<object, AgentInteractionResponse> Invoke);
}
