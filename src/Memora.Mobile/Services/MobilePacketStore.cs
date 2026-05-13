using System.Globalization;
using System.Text.Json;
using Memora.Mobile.Models;

namespace Memora.Mobile.Services;

public sealed record SavedMobilePacket(
    string PacketId,
    string CreatedAt,
    MobileCaptureIntent Intent,
    string Title,
    string Body,
    string TagsCsv,
    string TargetProjectHint,
    string DeviceLabel,
    string ProposedArtifactType,
    string SavedAt);

public sealed class MobilePacketStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _root;

    public MobilePacketStore()
        : this(Path.Combine(FileSystem.AppDataDirectory, "saved-packets"))
    {
    }

    public MobilePacketStore(string root)
    {
        _root = root;
        Directory.CreateDirectory(_root);
    }

    public string Root => _root;

    public IReadOnlyList<SavedMobilePacket> LoadAll()
    {
        if (!Directory.Exists(_root))
        {
            return [];
        }

        var packets = new List<SavedMobilePacket>();
        foreach (var path in Directory.EnumerateFiles(_root, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(path);
                var packet = JsonSerializer.Deserialize<SavedMobilePacket>(json, JsonOptions);
                if (packet is not null)
                {
                    packets.Add(packet);
                }
            }
            catch (JsonException)
            {
                // skip corrupt entries
            }
            catch (IOException)
            {
                // skip unreadable entries
            }
        }

        return packets
            .OrderByDescending(p => p.SavedAt, StringComparer.Ordinal)
            .ToArray();
    }

    public void Save(SavedMobilePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var path = Path.Combine(_root, packet.PacketId + ".json");
        var tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(tempPath, JsonSerializer.Serialize(packet, JsonOptions));
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Move(tempPath, path);
    }

    public void Delete(string packetId)
    {
        if (string.IsNullOrWhiteSpace(packetId)) return;
        var path = Path.Combine(_root, packetId + ".json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static string NowIsoUtc() =>
        DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}
