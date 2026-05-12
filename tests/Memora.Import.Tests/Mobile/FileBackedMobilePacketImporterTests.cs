using System.Text.Json;
using Memora.Import.Mobile;

namespace Memora.Import.Tests.Mobile;

public sealed class FileBackedMobilePacketImporterTests : IDisposable
{
    private const string ValidPlanningNotePacket = """
        ---
        packet_version: 1
        packet_id: 11111111-2222-3333-4444-555555555555
        created_at: 2026-05-12T18:41:00Z
        source: mobile
        intent: planning_note
        lifecycle_target: planning_input
        canonical: false
        title: Caching strategy review
        ---

        ## Note

        Worth a closer look during planning.
        """;

    private readonly string _workspaceRoot;

    public FileBackedMobilePacketImporterTests()
    {
        _workspaceRoot = Path.Combine(Path.GetTempPath(), "memora-mobile-import-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspaceRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspaceRoot))
        {
            Directory.Delete(_workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public void ImportFromMarkdown_ValidPacket_PersistsUnderImportsMobile()
    {
        var importer = new FileBackedMobilePacketImporter(() => DateTimeOffset.Parse("2026-05-12T20:00:00Z"));

        var result = importer.ImportFromMarkdown(_workspaceRoot, ValidPlanningNotePacket);

        Assert.Equal(MobilePacketImportOutcome.Imported, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.PersistedPath);
        Assert.True(File.Exists(result.PersistedPath));

        var expectedPath = Path.Combine(_workspaceRoot, "imports", "mobile", "11111111-2222-3333-4444-555555555555.json");
        Assert.Equal(expectedPath, result.PersistedPath);

        using var document = JsonDocument.Parse(File.ReadAllText(result.PersistedPath!));
        var root = document.RootElement;
        Assert.Equal("11111111-2222-3333-4444-555555555555", root.GetProperty("packetId").GetString());
        Assert.Equal("planning_note", root.GetProperty("intent").GetString());
        Assert.Equal("planning_input", root.GetProperty("lifecycleTarget").GetString());
        Assert.False(root.GetProperty("canonical").GetBoolean());
        Assert.Equal("Caching strategy review", root.GetProperty("title").GetString());
        Assert.Contains("Worth a closer look", root.GetProperty("body").GetString());
        Assert.Equal("2026-05-12T20:00:00.0000000+00:00", root.GetProperty("importedAtUtc").GetString());
    }

    [Fact]
    public void ImportFromMarkdown_InvalidPacket_DoesNotPersistAndReportsDiagnostics()
    {
        var importer = new FileBackedMobilePacketImporter();
        var invalid = ValidPlanningNotePacket.Replace("canonical: false", "canonical: true", StringComparison.Ordinal);

        var result = importer.ImportFromMarkdown(_workspaceRoot, invalid);

        Assert.Equal(MobilePacketImportOutcome.Rejected, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Null(result.PersistedPath);
        Assert.Contains(result.Diagnostics, d => d.Code == "mobile_packet.envelope.canonical_must_be_false");
        Assert.False(Directory.Exists(Path.Combine(_workspaceRoot, "imports", "mobile")));
    }

    [Fact]
    public void ImportFromMarkdown_DuplicatePacketId_ReportsAlreadyImportedWithoutOverwriting()
    {
        var importer = new FileBackedMobilePacketImporter();

        var first = importer.ImportFromMarkdown(_workspaceRoot, ValidPlanningNotePacket);
        Assert.Equal(MobilePacketImportOutcome.Imported, first.Outcome);
        var firstContents = File.ReadAllText(first.PersistedPath!);

        var second = importer.ImportFromMarkdown(_workspaceRoot, ValidPlanningNotePacket);
        Assert.Equal(MobilePacketImportOutcome.AlreadyImported, second.Outcome);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.PersistedPath, second.PersistedPath);
        Assert.Equal(firstContents, File.ReadAllText(second.PersistedPath!));
    }

    [Fact]
    public void ImportFromFile_MissingFile_ReportsSourceMissingDiagnostic()
    {
        var importer = new FileBackedMobilePacketImporter();
        var missingPath = Path.Combine(_workspaceRoot, "does-not-exist.md");

        var result = importer.ImportFromFile(_workspaceRoot, missingPath);

        Assert.Equal(MobilePacketImportOutcome.Rejected, result.Outcome);
        Assert.Contains(result.Diagnostics, d => d.Code == "mobile_packet.source.missing");
    }

    [Fact]
    public void ImportFromFile_ValidPacketFile_RoundtripsThroughFilesystem()
    {
        var importer = new FileBackedMobilePacketImporter(() => DateTimeOffset.Parse("2026-05-12T20:00:00Z"));
        var packetPath = Path.Combine(_workspaceRoot, "incoming.md");
        File.WriteAllText(packetPath, ValidPlanningNotePacket);

        var result = importer.ImportFromFile(_workspaceRoot, packetPath);

        Assert.Equal(MobilePacketImportOutcome.Imported, result.Outcome);
        Assert.True(File.Exists(result.PersistedPath));

        using var document = JsonDocument.Parse(File.ReadAllText(result.PersistedPath!));
        Assert.Equal("incoming.md", document.RootElement.GetProperty("sourceLabel").GetString());
    }
}
