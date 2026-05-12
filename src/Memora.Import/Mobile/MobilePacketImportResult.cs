using Memora.Core.Mobile;

namespace Memora.Import.Mobile;

public enum MobilePacketImportOutcome
{
    Imported,
    AlreadyImported,
    Rejected
}

public sealed record MobilePacketImportResult(
    MobilePacketImportOutcome Outcome,
    string? PacketId,
    string? PersistedPath,
    IReadOnlyList<MobilePacketParseDiagnostic> Diagnostics)
{
    public IReadOnlyList<MobilePacketParseDiagnostic> Diagnostics { get; } =
        Diagnostics?.ToArray() ?? throw new ArgumentNullException(nameof(Diagnostics));

    public bool IsSuccess => Outcome is MobilePacketImportOutcome.Imported or MobilePacketImportOutcome.AlreadyImported;
}
