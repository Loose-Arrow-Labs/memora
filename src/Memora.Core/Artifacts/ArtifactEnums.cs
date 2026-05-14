using System.Collections.ObjectModel;

namespace Memora.Core.Artifacts;

public enum ArtifactType
{
    Charter,
    Plan,
    Decision,
    Constraint,
    Question,
    Outcome,
    RepoStructure,
    SessionSummary,
    // Low-ceremony catch-all type with no required body sections and no
    // type-specific frontmatter. Use this when a thought, observation, or
    // pending finding does not yet fit one of the typed artifacts. The eight
    // typed schemas above remain opt-in for users who want their structure.
    Note
}

public enum ArtifactStatus
{
    Proposed,
    Draft,
    Approved,
    Superseded,
    Deprecated
}

public enum ArtifactPriority
{
    Low,
    Normal,
    High
}

public enum ConstraintKind
{
    Technical,
    Product,
    Workflow,
    Operational,
    Integration
}

public enum ConstraintSeverity
{
    Low,
    Normal,
    High,
    Critical
}

public enum QuestionStatus
{
    Open,
    Resolved,
    Deferred
}

public enum OutcomeKind
{
    Success,
    Failure,
    Mixed
}

public enum SnapshotSource
{
    Manual,
    Generated
}

public enum SessionType
{
    Planning,
    Review,
    Execution,
    Retrospective
}

public static class ArtifactSchemaValues
{
    private static readonly IReadOnlyDictionary<string, ArtifactType> ArtifactTypes =
        new ReadOnlyDictionary<string, ArtifactType>(new Dictionary<string, ArtifactType>(StringComparer.Ordinal)
        {
            ["charter"] = ArtifactType.Charter,
            ["plan"] = ArtifactType.Plan,
            ["decision"] = ArtifactType.Decision,
            ["constraint"] = ArtifactType.Constraint,
            ["question"] = ArtifactType.Question,
            ["outcome"] = ArtifactType.Outcome,
            ["repo_structure"] = ArtifactType.RepoStructure,
            ["session_summary"] = ArtifactType.SessionSummary,
            ["note"] = ArtifactType.Note
        });

    private static readonly IReadOnlyDictionary<ArtifactType, string> ArtifactTypeNames = CreateReverseMap(ArtifactTypes);

    private static readonly IReadOnlyDictionary<string, ArtifactStatus> ArtifactStatuses =
        new ReadOnlyDictionary<string, ArtifactStatus>(new Dictionary<string, ArtifactStatus>(StringComparer.Ordinal)
        {
            ["proposed"] = ArtifactStatus.Proposed,
            ["draft"] = ArtifactStatus.Draft,
            ["approved"] = ArtifactStatus.Approved,
            ["superseded"] = ArtifactStatus.Superseded,
            ["deprecated"] = ArtifactStatus.Deprecated
        });

    private static readonly IReadOnlyDictionary<ArtifactStatus, string> ArtifactStatusNames = CreateReverseMap(ArtifactStatuses);

    private static readonly IReadOnlyDictionary<string, ArtifactPriority> ArtifactPriorities =
        new ReadOnlyDictionary<string, ArtifactPriority>(new Dictionary<string, ArtifactPriority>(StringComparer.Ordinal)
        {
            ["low"] = ArtifactPriority.Low,
            ["normal"] = ArtifactPriority.Normal,
            ["high"] = ArtifactPriority.High
        });

    private static readonly IReadOnlyDictionary<ArtifactPriority, string> ArtifactPriorityNames = CreateReverseMap(ArtifactPriorities);

    private static readonly IReadOnlyDictionary<string, ConstraintKind> ConstraintKinds =
        new ReadOnlyDictionary<string, ConstraintKind>(new Dictionary<string, ConstraintKind>(StringComparer.Ordinal)
        {
            ["technical"] = ConstraintKind.Technical,
            ["product"] = ConstraintKind.Product,
            ["workflow"] = ConstraintKind.Workflow,
            ["operational"] = ConstraintKind.Operational,
            ["integration"] = ConstraintKind.Integration
        });

    private static readonly IReadOnlyDictionary<ConstraintKind, string> ConstraintKindNames = CreateReverseMap(ConstraintKinds);

    private static readonly IReadOnlyDictionary<string, ConstraintSeverity> ConstraintSeverities =
        new ReadOnlyDictionary<string, ConstraintSeverity>(new Dictionary<string, ConstraintSeverity>(StringComparer.Ordinal)
        {
            ["low"] = ConstraintSeverity.Low,
            ["normal"] = ConstraintSeverity.Normal,
            ["high"] = ConstraintSeverity.High,
            ["critical"] = ConstraintSeverity.Critical
        });

    private static readonly IReadOnlyDictionary<ConstraintSeverity, string> ConstraintSeverityNames = CreateReverseMap(ConstraintSeverities);

    private static readonly IReadOnlyDictionary<string, QuestionStatus> QuestionStatuses =
        new ReadOnlyDictionary<string, QuestionStatus>(new Dictionary<string, QuestionStatus>(StringComparer.Ordinal)
        {
            ["open"] = QuestionStatus.Open,
            ["resolved"] = QuestionStatus.Resolved,
            ["deferred"] = QuestionStatus.Deferred
        });

    private static readonly IReadOnlyDictionary<QuestionStatus, string> QuestionStatusNames = CreateReverseMap(QuestionStatuses);

    private static readonly IReadOnlyDictionary<string, OutcomeKind> OutcomeKinds =
        new ReadOnlyDictionary<string, OutcomeKind>(new Dictionary<string, OutcomeKind>(StringComparer.Ordinal)
        {
            ["success"] = OutcomeKind.Success,
            ["failure"] = OutcomeKind.Failure,
            ["mixed"] = OutcomeKind.Mixed
        });

    private static readonly IReadOnlyDictionary<OutcomeKind, string> OutcomeKindNames = CreateReverseMap(OutcomeKinds);

    private static readonly IReadOnlyDictionary<string, SnapshotSource> SnapshotSources =
        new ReadOnlyDictionary<string, SnapshotSource>(new Dictionary<string, SnapshotSource>(StringComparer.Ordinal)
        {
            ["manual"] = SnapshotSource.Manual,
            ["generated"] = SnapshotSource.Generated
        });

    private static readonly IReadOnlyDictionary<SnapshotSource, string> SnapshotSourceNames = CreateReverseMap(SnapshotSources);

    private static readonly IReadOnlyDictionary<string, SessionType> SessionTypes =
        new ReadOnlyDictionary<string, SessionType>(new Dictionary<string, SessionType>(StringComparer.Ordinal)
        {
            ["planning"] = SessionType.Planning,
            ["review"] = SessionType.Review,
            ["execution"] = SessionType.Execution,
            ["retrospective"] = SessionType.Retrospective
        });

    private static readonly IReadOnlyDictionary<SessionType, string> SessionTypeNames = CreateReverseMap(SessionTypes);

    public static bool TryParseArtifactType(string? value, out ArtifactType artifactType) => TryParse(ArtifactTypes, value, out artifactType);
    public static bool TryParseArtifactStatus(string? value, out ArtifactStatus artifactStatus) => TryParse(ArtifactStatuses, value, out artifactStatus);
    public static bool TryParseArtifactPriority(string? value, out ArtifactPriority artifactPriority) => TryParse(ArtifactPriorities, value, out artifactPriority);
    public static bool TryParseConstraintKind(string? value, out ConstraintKind constraintKind) => TryParse(ConstraintKinds, value, out constraintKind);
    public static bool TryParseConstraintSeverity(string? value, out ConstraintSeverity constraintSeverity) => TryParse(ConstraintSeverities, value, out constraintSeverity);
    public static bool TryParseQuestionStatus(string? value, out QuestionStatus questionStatus) => TryParse(QuestionStatuses, value, out questionStatus);
    public static bool TryParseOutcomeKind(string? value, out OutcomeKind outcomeKind) => TryParse(OutcomeKinds, value, out outcomeKind);
    public static bool TryParseSnapshotSource(string? value, out SnapshotSource snapshotSource) => TryParse(SnapshotSources, value, out snapshotSource);
    public static bool TryParseSessionType(string? value, out SessionType sessionType) => TryParse(SessionTypes, value, out sessionType);

    public static string ToSchemaValue(this ArtifactType value) => ArtifactTypeNames[value];
    public static string ToSchemaValue(this ArtifactStatus value) => ArtifactStatusNames[value];
    public static string ToSchemaValue(this ArtifactPriority value) => ArtifactPriorityNames[value];
    public static string ToSchemaValue(this ConstraintKind value) => ConstraintKindNames[value];
    public static string ToSchemaValue(this ConstraintSeverity value) => ConstraintSeverityNames[value];
    public static string ToSchemaValue(this QuestionStatus value) => QuestionStatusNames[value];
    public static string ToSchemaValue(this OutcomeKind value) => OutcomeKindNames[value];
    public static string ToSchemaValue(this SnapshotSource value) => SnapshotSourceNames[value];
    public static string ToSchemaValue(this SessionType value) => SessionTypeNames[value];

    private static bool TryParse<TEnum>(IReadOnlyDictionary<string, TEnum> map, string? value, out TEnum parsed)
        where TEnum : struct
    {
        if (value is not null && map.TryGetValue(value, out parsed))
        {
            return true;
        }

        parsed = default;
        return false;
    }

    private static IReadOnlyDictionary<TEnum, string> CreateReverseMap<TEnum>(IReadOnlyDictionary<string, TEnum> values)
        where TEnum : struct
    {
        var reverse = values.ToDictionary(pair => pair.Value, pair => pair.Key);
        return new ReadOnlyDictionary<TEnum, string>(reverse);
    }
}
