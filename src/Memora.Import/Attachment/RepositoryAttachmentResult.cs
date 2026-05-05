using Memora.Core.Import;

namespace Memora.Import.Attachment;

public sealed record RepositoryAttachmentError(string Code, string Message, string? Path = null)
{
    public string Code { get; } = RequireValue(Code, nameof(Code));
    public string Message { get; } = RequireValue(Message, nameof(Message));
    public string? Path { get; } = string.IsNullOrWhiteSpace(Path) ? null : Path.Trim();

    private static string RequireValue(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
}

public sealed record RepositoryAttachmentResult(
    ProjectRepositoryAttachment? Attachment,
    IReadOnlyList<RepositoryAttachmentError> Errors)
{
    public ProjectRepositoryAttachment? Attachment { get; } = Attachment;
    public IReadOnlyList<RepositoryAttachmentError> Errors { get; } =
        Errors?.ToArray() ?? throw new ArgumentNullException(nameof(Errors));

    public bool IsSuccess => Errors.Count == 0;

    public static RepositoryAttachmentResult Failed(params RepositoryAttachmentError[] errors) =>
        new(null, errors);

    public static RepositoryAttachmentResult Succeeded(ProjectRepositoryAttachment attachment) =>
        new(attachment, []);
}
