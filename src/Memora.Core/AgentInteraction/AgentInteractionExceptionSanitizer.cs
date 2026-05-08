namespace Memora.Core.AgentInteraction;

public sealed record SanitizedAgentInteractionException(string Code, string Message)
{
    public AgentInteractionError ToError(string? path) => new(Code, Message, path);
}

public static class AgentInteractionExceptionSanitizer
{
    public static SanitizedAgentInteractionException Sanitize(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            InvalidDataException when exception.Message.Contains("trustState", StringComparison.OrdinalIgnoreCase) =>
                new("evidence.record.invalid_trust_state", "Stored evidence record data is invalid."),
            InvalidDataException when exception.Message.Contains("sourceType", StringComparison.OrdinalIgnoreCase) =>
                new("evidence.record.invalid_source_type", "Stored evidence record data is invalid."),
            InvalidDataException =>
                new("data.invalid", "Stored data is invalid."),
            UnauthorizedAccessException =>
                new("filesystem.access_denied", "A required local file could not be accessed."),
            IOException =>
                new("filesystem.io_failed", "A required local file operation failed."),
            _ =>
                new("internal.unhandled_exception", "The request could not be completed because an internal error occurred.")
        };
    }
}
