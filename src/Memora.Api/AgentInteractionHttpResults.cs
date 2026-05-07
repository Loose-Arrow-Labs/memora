using Memora.Core.AgentInteraction;
using Microsoft.AspNetCore.Http;

namespace Memora.Api;

internal static class AgentInteractionHttpResults
{
    public static IResult FromProjectResponse(ProjectLookupResponse response) => FromResponse(response);

    public static IResult FromContextResponse(GetContextResponse response) => FromResponse(response);

    public static IResult FromProposalResponse(ProposalResponse response) =>
        response.IsSuccess
            ? Results.Json(response, statusCode: StatusCodes.Status202Accepted)
            : FromResponse(response);

    public static IResult FromOutcomeResponse(OutcomeResponse response) =>
        response.IsSuccess
            ? Results.Json(response, statusCode: StatusCodes.Status202Accepted)
            : FromResponse(response);

    public static IResult FromReviewInboxResponse(ReviewInboxResponse response) => FromResponse(response);

    public static IResult FromReviewArtifactPreviewResponse(ReviewArtifactPreviewResponse response) => FromResponse(response);

    public static IResult FromReviewDecisionResponse(ReviewDecisionResponse response) => FromResponse(response);

    private static IResult FromResponse(AgentInteractionResponse response)
    {
        if (response.IsSuccess)
        {
            return Results.Ok(response);
        }

        var errorCode = response.Errors.FirstOrDefault()?.Code ?? string.Empty;

        if (errorCode.EndsWith(".not_found", StringComparison.Ordinal))
        {
            return Results.Json(response, statusCode: StatusCodes.Status404NotFound);
        }

        if (errorCode.EndsWith(".not_configured", StringComparison.Ordinal))
        {
            return Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.BadRequest(response);
    }
}
