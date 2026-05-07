using Memora.Core.AgentInteraction;
using Memora.Core.Artifacts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Memora.Api;

internal static class AgentInteractionHttpExceptionBoundary
{
    public static IResult GetProject(
        string projectId,
        IAgentInteractionService service,
        ILogger logger)
    {
        try
        {
            return AgentInteractionHttpResults.FromProjectResponse(service.GetProject(projectId));
        }
        catch (Exception exception)
        {
            var error = CreateSanitizedError(exception, logger, "project_id");
            return ServerError(new ProjectLookupResponse(projectId, null, null, [error]));
        }
    }

    public static IResult GetContext(
        GetContextRequest request,
        IAgentInteractionService service,
        ILogger logger)
    {
        try
        {
            return AgentInteractionHttpResults.FromContextResponse(service.GetContext(request));
        }
        catch (Exception exception)
        {
            var error = CreateSanitizedError(exception, logger, "request");
            return ServerError(new GetContextResponse(null, [error]));
        }
    }

    public static IResult ProposeArtifact(
        ProposeArtifactRequest request,
        IAgentInteractionService service,
        ILogger logger)
    {
        try
        {
            return AgentInteractionHttpResults.FromProposalResponse(service.ProposeArtifact(request));
        }
        catch (Exception exception)
        {
            var error = CreateSanitizedError(exception, logger, "request");
            return ServerError(
                new ProposalResponse(
                    request.ProjectId,
                    request.ArtifactId,
                    request.ArtifactType,
                    ArtifactStatus.Proposed,
                    0,
                    [error]));
        }
    }

    public static IResult ProposeUpdate(
        ProposeUpdateRequest request,
        IAgentInteractionService service,
        ILogger logger)
    {
        try
        {
            return AgentInteractionHttpResults.FromProposalResponse(service.ProposeUpdate(request));
        }
        catch (Exception exception)
        {
            var error = CreateSanitizedError(exception, logger, "request");
            return ServerError(
                new ProposalResponse(
                    request.ProjectId,
                    request.ArtifactId,
                    ArtifactType.Plan,
                    ArtifactStatus.Proposed,
                    0,
                    [error]));
        }
    }

    public static IResult RecordOutcome(
        RecordOutcomeRequest request,
        IAgentInteractionService service,
        ILogger logger)
    {
        try
        {
            return AgentInteractionHttpResults.FromOutcomeResponse(service.RecordOutcome(request));
        }
        catch (Exception exception)
        {
            var error = CreateSanitizedError(exception, logger, "request");
            return ServerError(
                new OutcomeResponse(
                    request.ProjectId,
                    request.ArtifactId,
                    ArtifactStatus.Proposed,
                    0,
                    OutcomeKind.Mixed,
                    [error]));
        }
    }

    private static AgentInteractionError CreateSanitizedError(Exception exception, ILogger logger, string path)
    {
        var sanitized = AgentInteractionExceptionSanitizer.Sanitize(exception);
        logger.LogError(exception, "API boundary exception sanitized as {DiagnosticCode}.", sanitized.Code);
        return sanitized.ToError(path);
    }

    private static IResult ServerError(AgentInteractionResponse response) =>
        Results.Json(response, statusCode: StatusCodes.Status500InternalServerError);
}
