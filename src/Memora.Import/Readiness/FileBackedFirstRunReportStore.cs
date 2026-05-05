using System.Text.Json;

namespace Memora.Import.Readiness;

public sealed class FileBackedFirstRunReportStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string Save(string workspaceRootPath, FirstRunMemoryGenerationResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRootPath);
        ArgumentNullException.ThrowIfNull(result);

        var summariesRoot = Path.Combine(Path.GetFullPath(workspaceRootPath), "summaries");
        Directory.CreateDirectory(summariesRoot);
        var path = Path.Combine(summariesRoot, "first-run-readiness.json");
        File.WriteAllText(path, JsonSerializer.Serialize(result, JsonOptions));
        return path;
    }

    public FirstRunMemoryGenerationResult? Load(string workspaceRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRootPath);

        var path = Path.Combine(Path.GetFullPath(workspaceRootPath), "summaries", "first-run-readiness.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<FirstRunMemoryGenerationResult>(File.ReadAllText(path), JsonOptions)
            : null;
    }
}
