using System.Text;

namespace Memora.Storage.Persistence;

public static class AtomicFileWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static AtomicFileWriteResult WriteNewText(string targetPath, string contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(contents);

        var directory = Path.GetDirectoryName(targetPath) ??
                        throw new ArgumentException("Target path must include a directory.", nameof(targetPath));
        Directory.CreateDirectory(directory);
        if (File.Exists(targetPath))
        {
            return AtomicFileWriteResult.TargetAlreadyExists;
        }

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, Utf8NoBom))
            {
                writer.Write(contents);
            }

            File.Move(tempPath, targetPath, overwrite: false);
            return AtomicFileWriteResult.Created;
        }
        catch (IOException exception) when (File.Exists(targetPath) && IsTargetAlreadyExistsMoveFailure(exception))
        {
            return AtomicFileWriteResult.TargetAlreadyExists;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    internal static bool IsTargetAlreadyExistsMoveFailure(IOException exception)
    {
        const int errorFileExists = 80;
        const int errorAlreadyExists = 183;
        const int eExist = 17;

        var nativeErrorCode = exception.HResult & 0xFFFF;
        return nativeErrorCode is errorFileExists or errorAlreadyExists or eExist;
    }
}

public enum AtomicFileWriteResult
{
    Created,
    TargetAlreadyExists
}
