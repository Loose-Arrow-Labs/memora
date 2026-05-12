using System.Text;
using System.Text.Json;
using Memora.Core.Mobile;
using Memora.Storage.Persistence;

namespace Memora.Import.Mobile;

public sealed class FileBackedMobilePacketImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly Func<DateTimeOffset> _utcNow;

    public FileBackedMobilePacketImporter()
        : this(() => DateTimeOffset.UtcNow)
    {
    }

    public FileBackedMobilePacketImporter(Func<DateTimeOffset> utcNow)
    {
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    public MobilePacketImportResult ImportFromFile(string workspaceRootPath, string packetFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(packetFilePath);

        if (!File.Exists(packetFilePath))
        {
            return new MobilePacketImportResult(
                MobilePacketImportOutcome.Rejected,
                PacketId: null,
                PersistedPath: null,
                Diagnostics:
                [
                    new MobilePacketParseDiagnostic(
                        "mobile_packet.source.missing",
                        $"Mobile packet source file '{packetFilePath}' was not found.",
                        packetFilePath)
                ]);
        }

        var markdown = File.ReadAllText(packetFilePath, Encoding.UTF8);
        return ImportFromMarkdown(workspaceRootPath, markdown, sourceLabel: Path.GetFileName(packetFilePath));
    }

    public MobilePacketImportResult ImportFromMarkdown(string workspaceRootPath, string markdown, string? sourceLabel = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRootPath);
        ArgumentNullException.ThrowIfNull(markdown);

        var parseResult = MobilePacketParser.Parse(markdown);
        if (!parseResult.IsSuccess || parseResult.Packet is null)
        {
            return new MobilePacketImportResult(
                MobilePacketImportOutcome.Rejected,
                PacketId: null,
                PersistedPath: null,
                Diagnostics: parseResult.Diagnostics);
        }

        var packet = parseResult.Packet;
        var importedAtUtc = _utcNow();

        var record = new PersistedMobilePacket(
            PacketVersion: packet.PacketVersion,
            PacketId: packet.PacketId,
            CreatedAt: packet.CreatedAtUtc.ToString("O"),
            Source: packet.Source,
            Intent: packet.Intent.ToSchemaValue(),
            LifecycleTarget: packet.LifecycleTarget.ToSchemaValue(),
            Canonical: packet.Canonical,
            Title: packet.Title,
            DeviceLabel: packet.DeviceLabel,
            TargetProjectHint: packet.TargetProjectHint,
            Tags: packet.Tags,
            ProposedArtifactType: packet.ProposedArtifactType,
            Body: packet.Body,
            ImportedAtUtc: importedAtUtc.ToString("O"),
            SourceLabel: sourceLabel);

        var importsRoot = Path.Combine(workspaceRootPath, "imports", "mobile");
        var targetPath = Path.Combine(importsRoot, $"{packet.PacketId}.json");
        var serialized = JsonSerializer.Serialize(record, JsonOptions);
        var writeOutcome = AtomicFileWriter.WriteNewText(targetPath, serialized);

        return writeOutcome switch
        {
            AtomicFileWriteResult.Created => new MobilePacketImportResult(
                MobilePacketImportOutcome.Imported,
                packet.PacketId,
                targetPath,
                []),
            AtomicFileWriteResult.TargetAlreadyExists => new MobilePacketImportResult(
                MobilePacketImportOutcome.AlreadyImported,
                packet.PacketId,
                targetPath,
                []),
            _ => throw new InvalidOperationException($"Unhandled atomic write result: {writeOutcome}.")
        };
    }

    private sealed record PersistedMobilePacket(
        int PacketVersion,
        string PacketId,
        string CreatedAt,
        string Source,
        string Intent,
        string LifecycleTarget,
        bool Canonical,
        string? Title,
        string? DeviceLabel,
        string? TargetProjectHint,
        IReadOnlyList<string> Tags,
        string? ProposedArtifactType,
        string Body,
        string ImportedAtUtc,
        string? SourceLabel);
}
