namespace Memora.Core.Artifacts;

public abstract record ArtifactDocument(
    string Id,
    string ProjectId,
    ArtifactType Type,
    ArtifactStatus Status,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int Revision,
    IReadOnlyList<string> Tags,
    string Provenance,
    string Reason,
    ArtifactLinks Links,
    string Body,
    IReadOnlyDictionary<string, string> Sections);

public sealed record ProjectCharterArtifact(
    string Id,
    string ProjectId,
    ArtifactStatus Status,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int Revision,
    IReadOnlyList<string> Tags,
    string Provenance,
    string Reason,
    ArtifactLinks Links,
    string Body,
    IReadOnlyDictionary<string, string> Sections)
    : ArtifactDocument(Id, ProjectId, ArtifactType.Charter, Status, Title, CreatedAtUtc, UpdatedAtUtc, Revision, Tags, Provenance, Reason, Links, Body, Sections);

public sealed record PlanArtifact(
    string Id,
    string ProjectId,
    ArtifactStatus Status,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int Revision,
    IReadOnlyList<string> Tags,
    string Provenance,
    string Reason,
    ArtifactLinks Links,
    string Body,
    IReadOnlyDictionary<string, string> Sections,
    ArtifactPriority Priority,
    bool Active)
    : ArtifactDocument(Id, ProjectId, ArtifactType.Plan, Status, Title, CreatedAtUtc, UpdatedAtUtc, Revision, Tags, Provenance, Reason, Links, Body, Sections);

public sealed record ArchitectureDecisionArtifact(
    string Id,
    string ProjectId,
    ArtifactStatus Status,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int Revision,
    IReadOnlyList<string> Tags,
    string Provenance,
    string Reason,
    ArtifactLinks Links,
    string Body,
    IReadOnlyDictionary<string, string> Sections,
    string DecisionDate)
    : ArtifactDocument(Id, ProjectId, ArtifactType.Decision, Status, Title, CreatedAtUtc, UpdatedAtUtc, Revision, Tags, Provenance, Reason, Links, Body, Sections);

public sealed record ConstraintArtifact(
    string Id,
    string ProjectId,
    ArtifactStatus Status,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int Revision,
    IReadOnlyList<string> Tags,
    string Provenance,
    string Reason,
    ArtifactLinks Links,
    string Body,
    IReadOnlyDictionary<string, string> Sections,
    ConstraintKind ConstraintKind,
    ConstraintSeverity Severity)
    : ArtifactDocument(Id, ProjectId, ArtifactType.Constraint, Status, Title, CreatedAtUtc, UpdatedAtUtc, Revision, Tags, Provenance, Reason, Links, Body, Sections);

public sealed record OpenQuestionArtifact(
    string Id,
    string ProjectId,
    ArtifactStatus Status,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int Revision,
    IReadOnlyList<string> Tags,
    string Provenance,
    string Reason,
    ArtifactLinks Links,
    string Body,
    IReadOnlyDictionary<string, string> Sections,
    QuestionStatus QuestionStatus,
    ArtifactPriority Priority)
    : ArtifactDocument(Id, ProjectId, ArtifactType.Question, Status, Title, CreatedAtUtc, UpdatedAtUtc, Revision, Tags, Provenance, Reason, Links, Body, Sections);

public sealed record OutcomeArtifact(
    string Id,
    string ProjectId,
    ArtifactStatus Status,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int Revision,
    IReadOnlyList<string> Tags,
    string Provenance,
    string Reason,
    ArtifactLinks Links,
    string Body,
    IReadOnlyDictionary<string, string> Sections,
    OutcomeKind Outcome)
    : ArtifactDocument(Id, ProjectId, ArtifactType.Outcome, Status, Title, CreatedAtUtc, UpdatedAtUtc, Revision, Tags, Provenance, Reason, Links, Body, Sections);

public sealed record RepoStructureArtifact(
    string Id,
    string ProjectId,
    ArtifactStatus Status,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int Revision,
    IReadOnlyList<string> Tags,
    string Provenance,
    string Reason,
    ArtifactLinks Links,
    string Body,
    IReadOnlyDictionary<string, string> Sections,
    SnapshotSource SnapshotSource)
    : ArtifactDocument(Id, ProjectId, ArtifactType.RepoStructure, Status, Title, CreatedAtUtc, UpdatedAtUtc, Revision, Tags, Provenance, Reason, Links, Body, Sections);

public sealed record NoteArtifact(
    string Id,
    string ProjectId,
    ArtifactStatus Status,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int Revision,
    IReadOnlyList<string> Tags,
    string Provenance,
    string Reason,
    ArtifactLinks Links,
    string Body,
    IReadOnlyDictionary<string, string> Sections)
    : ArtifactDocument(Id, ProjectId, ArtifactType.Note, Status, Title, CreatedAtUtc, UpdatedAtUtc, Revision, Tags, Provenance, Reason, Links, Body, Sections);

public sealed record SessionSummaryArtifact(
    string Id,
    string ProjectId,
    ArtifactStatus Status,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int Revision,
    IReadOnlyList<string> Tags,
    string Provenance,
    string Reason,
    ArtifactLinks Links,
    string Body,
    IReadOnlyDictionary<string, string> Sections,
    SessionType SessionType,
    bool Canonical)
    : ArtifactDocument(Id, ProjectId, ArtifactType.SessionSummary, Status, Title, CreatedAtUtc, UpdatedAtUtc, Revision, Tags, Provenance, Reason, Links, Body, Sections);
