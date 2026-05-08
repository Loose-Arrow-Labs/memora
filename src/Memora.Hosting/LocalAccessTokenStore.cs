using System.Security.Cryptography;
using System.Text;

namespace Memora.Hosting;

public sealed class LocalAccessTokenStore
{
    private readonly object _syncRoot = new();

    public LocalAccessTokenStore(string workspacesRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacesRootPath);
        TokenDirectoryPath = Path.Combine(Path.GetFullPath(workspacesRootPath), LocalAccessDefaults.TokenDirectoryName);
        TokenPath = Path.Combine(TokenDirectoryPath, LocalAccessDefaults.TokenFileName);
    }

    public string TokenDirectoryPath { get; }
    public string TokenPath { get; }

    public string GetOrCreateToken()
    {
        lock (_syncRoot)
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (File.Exists(TokenPath))
                {
                    var existing = File.ReadAllText(TokenPath).Trim();
                    if (!string.IsNullOrWhiteSpace(existing))
                    {
                        return existing;
                    }
                }

                Directory.CreateDirectory(TokenDirectoryPath);
                RestrictDirectoryAccess(TokenDirectoryPath);

                var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                try
                {
                    using (var stream = new FileStream(TokenPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(stream, Encoding.UTF8))
                    {
                        writer.WriteLine(token);
                    }

                    RestrictFileAccess(TokenPath);
                    return token;
                }
                catch (IOException) when (File.Exists(TokenPath))
                {
                    Thread.Sleep(10);
                }
            }

            throw new IOException($"Unable to create or read local access token at '{TokenPath}'.");
        }
    }

    public bool IsValidToken(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var token = GetOrCreateToken();
        var expectedBytes = Encoding.UTF8.GetBytes(token);
        var candidateBytes = Encoding.UTF8.GetBytes(candidate.Trim());
        return expectedBytes.Length == candidateBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, candidateBytes);
    }

    private static void RestrictDirectoryAccess(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void RestrictFileAccess(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
