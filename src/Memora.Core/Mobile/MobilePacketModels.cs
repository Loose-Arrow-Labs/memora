namespace Memora.Core.Mobile;

public enum MobilePacketIntent
{
    Question,
    DecisionDraft,
    PlanningNote,
    ProposalDraft
}

public enum MobilePacketLifecycleTarget
{
    PlanningInput,
    ProposalDraft
}

public static class MobilePacketIntentExtensions
{
    public static string ToSchemaValue(this MobilePacketIntent intent) =>
        intent switch
        {
            MobilePacketIntent.Question => "question",
            MobilePacketIntent.DecisionDraft => "decision_draft",
            MobilePacketIntent.PlanningNote => "planning_note",
            MobilePacketIntent.ProposalDraft => "proposal_draft",
            _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unknown mobile packet intent.")
        };

    public static bool TryParseSchemaValue(string? value, out MobilePacketIntent intent)
    {
        intent = MobilePacketIntent.Question;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim())
        {
            case "question":
                intent = MobilePacketIntent.Question;
                return true;
            case "decision_draft":
                intent = MobilePacketIntent.DecisionDraft;
                return true;
            case "planning_note":
                intent = MobilePacketIntent.PlanningNote;
                return true;
            case "proposal_draft":
                intent = MobilePacketIntent.ProposalDraft;
                return true;
            default:
                return false;
        }
    }

    public static MobilePacketLifecycleTarget ExpectedLifecycleTarget(this MobilePacketIntent intent) =>
        intent switch
        {
            MobilePacketIntent.Question => MobilePacketLifecycleTarget.PlanningInput,
            MobilePacketIntent.PlanningNote => MobilePacketLifecycleTarget.PlanningInput,
            MobilePacketIntent.DecisionDraft => MobilePacketLifecycleTarget.ProposalDraft,
            MobilePacketIntent.ProposalDraft => MobilePacketLifecycleTarget.ProposalDraft,
            _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unknown mobile packet intent.")
        };
}

public static class MobilePacketLifecycleTargetExtensions
{
    public static string ToSchemaValue(this MobilePacketLifecycleTarget target) =>
        target switch
        {
            MobilePacketLifecycleTarget.PlanningInput => "planning_input",
            MobilePacketLifecycleTarget.ProposalDraft => "proposal_draft",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown mobile packet lifecycle target.")
        };

    public static bool TryParseSchemaValue(string? value, out MobilePacketLifecycleTarget target)
    {
        target = MobilePacketLifecycleTarget.PlanningInput;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim())
        {
            case "planning_input":
                target = MobilePacketLifecycleTarget.PlanningInput;
                return true;
            case "proposal_draft":
                target = MobilePacketLifecycleTarget.ProposalDraft;
                return true;
            default:
                return false;
        }
    }
}

public sealed record MobilePacket
{
    public MobilePacket(
        int packetVersion,
        string packetId,
        DateTimeOffset createdAtUtc,
        MobilePacketIntent intent,
        MobilePacketLifecycleTarget lifecycleTarget,
        string body,
        string? title = null,
        string? deviceLabel = null,
        string? targetProjectHint = null,
        IReadOnlyList<string>? tags = null,
        string? proposedArtifactType = null)
    {
        if (packetVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(packetVersion), "Packet version must be greater than zero.");
        }

        PacketVersion = packetVersion;
        PacketId = RequireValue(packetId, nameof(packetId));
        CreatedAtUtc = createdAtUtc;
        Intent = intent;
        LifecycleTarget = lifecycleTarget;
        Body = RequireValue(body, nameof(body));
        Title = NormalizeOptional(title);
        DeviceLabel = NormalizeOptional(deviceLabel);
        TargetProjectHint = NormalizeOptional(targetProjectHint);
        Tags = NormalizeTags(tags);
        ProposedArtifactType = NormalizeOptional(proposedArtifactType);
    }

    public int PacketVersion { get; }

    public string PacketId { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public MobilePacketIntent Intent { get; }

    public MobilePacketLifecycleTarget LifecycleTarget { get; }

    public string Body { get; }

    public string? Title { get; }

    public string? DeviceLabel { get; }

    public string? TargetProjectHint { get; }

    public IReadOnlyList<string> Tags { get; }

    public string? ProposedArtifactType { get; }

    public bool Canonical => false;

    public string Source => "mobile";

    private static string RequireValue(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string>? tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>(tags.Count);
        foreach (var raw in tags)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var trimmed = raw.Trim();
            if (seen.Add(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }
}

public sealed record MobilePacketParseDiagnostic(string Code, string Message, string? Path = null)
{
    public string Code { get; } = string.IsNullOrWhiteSpace(Code)
        ? throw new ArgumentException("Diagnostic code is required.", nameof(Code))
        : Code.Trim();

    public string Message { get; } = string.IsNullOrWhiteSpace(Message)
        ? throw new ArgumentException("Diagnostic message is required.", nameof(Message))
        : Message.Trim();

    public string? Path { get; } = string.IsNullOrWhiteSpace(Path) ? null : Path.Trim();
}

public sealed record MobilePacketParseResult(MobilePacket? Packet, IReadOnlyList<MobilePacketParseDiagnostic> Diagnostics)
{
    public IReadOnlyList<MobilePacketParseDiagnostic> Diagnostics { get; } =
        Diagnostics?.ToArray() ?? throw new ArgumentNullException(nameof(Diagnostics));

    public MobilePacket? Packet { get; } = Packet;

    public bool IsSuccess => Packet is not null && Diagnostics.Count == 0;
}
