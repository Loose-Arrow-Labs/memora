using System.Text.Json;
using Memora.Core.Import;

namespace Memora.Import.Evidence;

public sealed class FileBackedImportedEvidenceStore : IImportedEvidenceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public EvidencePersistenceResult Save(ProjectEvidenceWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var evidenceRoot = Path.Combine(request.WorkspaceRootPath, "evidence");
        Directory.CreateDirectory(evidenceRoot);

        var createdCount = 0;
        var existingCount = 0;
        var writtenPaths = new List<string>();

        foreach (var record in request.Records.OrderBy(record => record.StableId, StringComparer.Ordinal))
        {
            var sourceDirectory = Path.Combine(evidenceRoot, record.SourceType.ToSchemaValue());
            Directory.CreateDirectory(sourceDirectory);
            var targetPath = Path.Combine(sourceDirectory, $"{record.StableId}.json");

            if (File.Exists(targetPath))
            {
                existingCount++;
                continue;
            }

            File.WriteAllText(targetPath, JsonSerializer.Serialize(ToSerializable(record), JsonOptions));
            createdCount++;
            writtenPaths.Add(targetPath);
        }

        return new EvidencePersistenceResult(createdCount, existingCount, writtenPaths);
    }

    public IReadOnlyList<ImportedEvidenceRecord> ReadAll(string workspaceRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRootPath);

        var evidenceRoot = Path.Combine(Path.GetFullPath(workspaceRootPath), "evidence");
        if (!Directory.Exists(evidenceRoot))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(evidenceRoot, "*.json", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(ReadRecord)
            .OrderBy(record => record.StableId, StringComparer.Ordinal)
            .ToArray();
    }

    private static ImportedEvidenceRecord ReadRecord(string filePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(filePath));
        var root = document.RootElement;

        if (!ImportedEvidenceSourceTypeExtensions.TryParseSchemaValue(ReadString(root, "sourceType"), out var sourceType))
        {
            throw new InvalidDataException($"Evidence record '{filePath}' has invalid sourceType.");
        }

        if (!ImportedEvidenceTrustStateExtensions.TryParseSchemaValue(ReadString(root, "trustState"), out var trustState))
        {
            throw new InvalidDataException($"Evidence record '{filePath}' has invalid trustState.");
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.TryGetProperty("metadata", out var metadataElement) &&
            metadataElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in metadataElement.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                metadata[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return new ImportedEvidenceRecord(
            ReadString(root, "stableId"),
            ReadString(root, "projectId"),
            sourceType,
            ReadString(root, "sourceAttachmentId"),
            ReadString(root, "sourceRepositoryIdentity"),
            ReadString(root, "sourceReference"),
            ReadString(root, "title"),
            ReadString(root, "summary"),
            DateTimeOffset.Parse(ReadString(root, "observedAtUtc")),
            DateTimeOffset.Parse(ReadString(root, "importedAtUtc")),
            ReadString(root, "provenance"),
            trustState,
            metadata);
    }

    private static SerializableEvidenceRecord ToSerializable(ImportedEvidenceRecord record) =>
        new(
            record.StableId,
            record.ProjectId,
            record.SourceType.ToSchemaValue(),
            record.SourceAttachmentId,
            record.SourceRepositoryIdentity,
            record.SourceReference,
            record.Title,
            record.Summary,
            record.ObservedAtUtc.ToString("O"),
            record.ImportedAtUtc.ToString("O"),
            record.Provenance,
            record.TrustState.ToSchemaValue(),
            record.Metadata);

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Evidence record property '{propertyName}' is required.");
        }

        return property.GetString() ?? string.Empty;
    }

    private sealed record SerializableEvidenceRecord(
        string StableId,
        string ProjectId,
        string SourceType,
        string SourceAttachmentId,
        string SourceRepositoryIdentity,
        string SourceReference,
        string Title,
        string Summary,
        string ObservedAtUtc,
        string ImportedAtUtc,
        string Provenance,
        string TrustState,
        IReadOnlyDictionary<string, string> Metadata);
}
