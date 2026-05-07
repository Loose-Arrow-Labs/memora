namespace Memora.Storage.Persistence;

public sealed class ArtifactPersistenceException : IOException
{
    public ArtifactPersistenceException(string code, string artifactPath, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactPath);

        Code = code;
        ArtifactPath = artifactPath;
    }

    public string Code { get; }
    public string ArtifactPath { get; }
}
