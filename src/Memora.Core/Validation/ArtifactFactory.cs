using Memora.Core.Artifacts;

namespace Memora.Core.Validation;

public sealed class ArtifactFactory
{
    public ArtifactCreationResult Create(
        IReadOnlyDictionary<string, object?> frontmatter,
        string body,
        IReadOnlyDictionary<string, string> sections)
    {
        ArgumentNullException.ThrowIfNull(frontmatter);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(sections);

        var collector = new ValidationCollector();

        var id = ReadRequiredString(frontmatter, "id", collector);
        var projectId = ReadRequiredString(frontmatter, "project_id", collector);
        var title = ReadRequiredString(frontmatter, "title", collector);
        var provenance = ReadRequiredString(frontmatter, "provenance", collector);
        var reason = ReadRequiredString(frontmatter, "reason", collector);
        var type = ReadArtifactType(frontmatter, collector);
        var status = ReadArtifactStatus(frontmatter, collector);
        var createdAt = ReadUtcTimestamp(frontmatter, "created_at", collector);
        var updatedAt = ReadUtcTimestamp(frontmatter, "updated_at", collector);
        var revision = ReadRevision(frontmatter, collector);
        var tags = ReadStringList(frontmatter, "tags", collector);
        var links = ReadLinks(frontmatter, collector);

        if (type is not null)
        {
            ValidateAllowedFrontmatterKeys(frontmatter, type.Value, collector);
            ValidateBodySections(type.Value, sections, collector);
        }

        if (type is not null && !string.IsNullOrWhiteSpace(id) && !ArtifactIdValidator.IsValidForType(id, type.Value))
        {
            collector.Add(
                "artifact.id.invalid",
                $"Artifact id '{id}' does not match the required {ArtifactIdValidator.GetPrefix(type.Value)} prefix format.",
                "id");
        }

        ArtifactDocument? artifact = null;

        if (type is not null &&
            status is not null &&
            createdAt is not null &&
            updatedAt is not null &&
            revision is not null &&
            tags is not null &&
            links is not null &&
            !string.IsNullOrWhiteSpace(id) &&
            !string.IsNullOrWhiteSpace(projectId) &&
            !string.IsNullOrWhiteSpace(title) &&
            !string.IsNullOrWhiteSpace(provenance) &&
            !string.IsNullOrWhiteSpace(reason))
        {
            artifact = CreateArtifact(
                frontmatter,
                body,
                sections,
                id,
                projectId,
                title,
                provenance,
                reason,
                type.Value,
                status.Value,
                createdAt.Value,
                updatedAt.Value,
                revision.Value,
                tags,
                links,
                collector);
        }

        return new ArtifactCreationResult(
            collector.HasIssues ? null : artifact,
            collector.ToResult());
    }

    private static ArtifactDocument? CreateArtifact(
        IReadOnlyDictionary<string, object?> frontmatter,
        string body,
        IReadOnlyDictionary<string, string> sections,
        string id,
        string projectId,
        string title,
        string provenance,
        string reason,
        ArtifactType type,
        ArtifactStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int revision,
        IReadOnlyList<string> tags,
        ArtifactLinks links,
        ValidationCollector collector)
    {
        return type switch
        {
            ArtifactType.Charter => new ProjectCharterArtifact(id, projectId, status, title, createdAt, updatedAt, revision, tags, provenance, reason, links, body, sections),
            ArtifactType.Plan => CreatePlanArtifact(frontmatter, body, sections, id, projectId, status, title, createdAt, updatedAt, revision, tags, provenance, reason, links, collector),
            ArtifactType.Decision => CreateDecisionArtifact(frontmatter, body, sections, id, projectId, status, title, createdAt, updatedAt, revision, tags, provenance, reason, links, collector),
            ArtifactType.Constraint => CreateConstraintArtifact(frontmatter, body, sections, id, projectId, status, title, createdAt, updatedAt, revision, tags, provenance, reason, links, collector),
            ArtifactType.Question => CreateQuestionArtifact(frontmatter, body, sections, id, projectId, status, title, createdAt, updatedAt, revision, tags, provenance, reason, links, collector),
            ArtifactType.Outcome => CreateOutcomeArtifact(frontmatter, body, sections, id, projectId, status, title, createdAt, updatedAt, revision, tags, provenance, reason, links, collector),
            ArtifactType.RepoStructure => CreateRepoStructureArtifact(frontmatter, body, sections, id, projectId, status, title, createdAt, updatedAt, revision, tags, provenance, reason, links, collector),
            ArtifactType.SessionSummary => CreateSessionSummaryArtifact(frontmatter, body, sections, id, projectId, status, title, createdAt, updatedAt, revision, tags, provenance, reason, links, collector),
            ArtifactType.Note => new NoteArtifact(id, projectId, status, title, createdAt, updatedAt, revision, tags, provenance, reason, links, body, sections),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported artifact type.")
        };
    }

    private static PlanArtifact? CreatePlanArtifact(
        IReadOnlyDictionary<string, object?> frontmatter,
        string body,
        IReadOnlyDictionary<string, string> sections,
        string id,
        string projectId,
        ArtifactStatus status,
        string title,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int revision,
        IReadOnlyList<string> tags,
        string provenance,
        string reason,
        ArtifactLinks links,
        ValidationCollector collector)
    {
        var priority = ReadArtifactPriority(frontmatter, collector);
        var active = ReadBoolean(frontmatter, "active", collector);

        if (priority is null || active is null)
        {
            return null;
        }

        if (!sections.TryGetValue("Acceptance Criteria", out var acceptanceCriteria) || !HasAcceptanceCriteria(acceptanceCriteria))
        {
            collector.Add(
                "artifact.plan.acceptance_criteria.missing",
                "Plan artifacts must include at least one acceptance criterion in the 'Acceptance Criteria' section.",
                "body.sections.Acceptance Criteria");
        }

        return new PlanArtifact(id, projectId, status, title, createdAt, updatedAt, revision, tags, provenance, reason, links, body, sections, priority.Value, active.Value);
    }

    private static ArchitectureDecisionArtifact? CreateDecisionArtifact(
        IReadOnlyDictionary<string, object?> frontmatter,
        string body,
        IReadOnlyDictionary<string, string> sections,
        string id,
        string projectId,
        ArtifactStatus status,
        string title,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int revision,
        IReadOnlyList<string> tags,
        string provenance,
        string reason,
        ArtifactLinks links,
        ValidationCollector collector)
    {
        var decisionDate = ReadRequiredString(frontmatter, "decision_date", collector);

        if (string.IsNullOrWhiteSpace(decisionDate))
        {
            return null;
        }

        return new ArchitectureDecisionArtifact(id, projectId, status, title, createdAt, updatedAt, revision, tags, provenance, reason, links, body, sections, decisionDate);
    }

    private static ConstraintArtifact? CreateConstraintArtifact(
        IReadOnlyDictionary<string, object?> frontmatter,
        string body,
        IReadOnlyDictionary<string, string> sections,
        string id,
        string projectId,
        ArtifactStatus status,
        string title,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int revision,
        IReadOnlyList<string> tags,
        string provenance,
        string reason,
        ArtifactLinks links,
        ValidationCollector collector)
    {
        var constraintKind = ReadConstraintKind(frontmatter, collector);
        var severity = ReadConstraintSeverity(frontmatter, collector);

        if (constraintKind is null || severity is null)
        {
            return null;
        }

        return new ConstraintArtifact(id, projectId, status, title, createdAt, updatedAt, revision, tags, provenance, reason, links, body, sections, constraintKind.Value, severity.Value);
    }

    private static OpenQuestionArtifact? CreateQuestionArtifact(
        IReadOnlyDictionary<string, object?> frontmatter,
        string body,
        IReadOnlyDictionary<string, string> sections,
        string id,
        string projectId,
        ArtifactStatus status,
        string title,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int revision,
        IReadOnlyList<string> tags,
        string provenance,
        string reason,
        ArtifactLinks links,
        ValidationCollector collector)
    {
        var questionStatus = ReadQuestionStatus(frontmatter, collector);
        var priority = ReadArtifactPriority(frontmatter, collector);

        if (questionStatus is null || priority is null)
        {
            return null;
        }

        if (questionStatus is not QuestionStatus.Open &&
            (!sections.TryGetValue("Resolution", out var resolution) || string.IsNullOrWhiteSpace(resolution)))
        {
            collector.Add(
                "artifact.question.resolution.required",
                "Open questions must include resolution content when the question status is not open.",
                "body.sections.Resolution");
        }

        return new OpenQuestionArtifact(id, projectId, status, title, createdAt, updatedAt, revision, tags, provenance, reason, links, body, sections, questionStatus.Value, priority.Value);
    }

    private static OutcomeArtifact? CreateOutcomeArtifact(
        IReadOnlyDictionary<string, object?> frontmatter,
        string body,
        IReadOnlyDictionary<string, string> sections,
        string id,
        string projectId,
        ArtifactStatus status,
        string title,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int revision,
        IReadOnlyList<string> tags,
        string provenance,
        string reason,
        ArtifactLinks links,
        ValidationCollector collector)
    {
        var outcome = ReadOutcomeKind(frontmatter, collector);

        if (outcome is null)
        {
            return null;
        }

        return new OutcomeArtifact(id, projectId, status, title, createdAt, updatedAt, revision, tags, provenance, reason, links, body, sections, outcome.Value);
    }

    private static RepoStructureArtifact? CreateRepoStructureArtifact(
        IReadOnlyDictionary<string, object?> frontmatter,
        string body,
        IReadOnlyDictionary<string, string> sections,
        string id,
        string projectId,
        ArtifactStatus status,
        string title,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int revision,
        IReadOnlyList<string> tags,
        string provenance,
        string reason,
        ArtifactLinks links,
        ValidationCollector collector)
    {
        var snapshotSource = ReadSnapshotSource(frontmatter, collector);

        if (snapshotSource is null)
        {
            return null;
        }

        return new RepoStructureArtifact(id, projectId, status, title, createdAt, updatedAt, revision, tags, provenance, reason, links, body, sections, snapshotSource.Value);
    }

    private static SessionSummaryArtifact? CreateSessionSummaryArtifact(
        IReadOnlyDictionary<string, object?> frontmatter,
        string body,
        IReadOnlyDictionary<string, string> sections,
        string id,
        string projectId,
        ArtifactStatus status,
        string title,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        int revision,
        IReadOnlyList<string> tags,
        string provenance,
        string reason,
        ArtifactLinks links,
        ValidationCollector collector)
    {
        var sessionType = ReadSessionType(frontmatter, collector);
        var canonical = ReadBoolean(frontmatter, "canonical", collector);

        if (sessionType is null || canonical is null)
        {
            return null;
        }

        if (canonical.Value)
        {
            collector.Add(
                "artifact.session_summary.canonical.invalid",
                "Session summary artifacts must declare canonical: false.",
                "canonical");
        }

        return new SessionSummaryArtifact(id, projectId, status, title, createdAt, updatedAt, revision, tags, provenance, reason, links, body, sections, sessionType.Value, canonical.Value);
    }

    private static void ValidateAllowedFrontmatterKeys(IReadOnlyDictionary<string, object?> frontmatter, ArtifactType artifactType, ValidationCollector collector)
    {
        var allowedKeys = ArtifactFrontmatterRules.GetAllowedFrontmatterKeys(artifactType);

        foreach (var key in frontmatter.Keys)
        {
            if (!allowedKeys.Contains(key))
            {
                collector.Add(
                    "artifact.frontmatter.key.unknown",
                    $"Frontmatter key '{key}' is not allowed for artifact type '{artifactType.ToSchemaValue()}'.",
                    key);
            }
        }
    }

    private static void ValidateBodySections(ArtifactType artifactType, IReadOnlyDictionary<string, string> sections, ValidationCollector collector)
    {
        foreach (var requiredSection in ArtifactBodyRules.GetRequiredSections(artifactType))
        {
            if (!sections.ContainsKey(requiredSection))
            {
                collector.Add(
                    "artifact.body.section.missing",
                    $"Artifact body must include the '## {requiredSection}' section.",
                    $"body.sections.{requiredSection}");
            }
        }
    }

    private static string ReadRequiredString(IReadOnlyDictionary<string, object?> values, string key, ValidationCollector collector)
    {
        if (!values.TryGetValue(key, out var rawValue))
        {
            collector.Add("artifact.frontmatter.missing", $"Frontmatter key '{key}' is required.", key);
            return string.Empty;
        }

        if (rawValue is not string stringValue || string.IsNullOrWhiteSpace(stringValue))
        {
            collector.Add("artifact.frontmatter.invalid", $"Frontmatter key '{key}' must be a non-empty string.", key);
            return string.Empty;
        }

        return stringValue;
    }

    private static string? ReadOptionalString(IReadOnlyDictionary<string, object?> values, string key, ValidationCollector collector)
    {
        if (!values.TryGetValue(key, out var rawValue))
        {
            collector.Add("artifact.frontmatter.missing", $"Frontmatter key '{key}' is required.", key);
            return null;
        }

        if (rawValue is not string stringValue || string.IsNullOrWhiteSpace(stringValue))
        {
            collector.Add("artifact.frontmatter.invalid", $"Frontmatter key '{key}' must be a non-empty string.", key);
            return null;
        }

        return stringValue;
    }

    private static ArtifactType? ReadArtifactType(IReadOnlyDictionary<string, object?> values, ValidationCollector collector)
    {
        var rawValue = ReadOptionalString(values, "type", collector);

        if (rawValue is null)
        {
            return null;
        }

        if (ArtifactSchemaValues.TryParseArtifactType(rawValue, out var artifactType))
        {
            return artifactType;
        }

        collector.Add("artifact.type.invalid", $"Artifact type '{rawValue}' is not supported.", "type");
        return null;
    }

    private static ArtifactStatus? ReadArtifactStatus(IReadOnlyDictionary<string, object?> values, ValidationCollector collector)
    {
        var rawValue = ReadOptionalString(values, "status", collector);

        if (rawValue is null)
        {
            return null;
        }

        if (ArtifactSchemaValues.TryParseArtifactStatus(rawValue, out var artifactStatus))
        {
            return artifactStatus;
        }

        collector.Add("artifact.status.invalid", $"Artifact status '{rawValue}' is not supported.", "status");
        return null;
    }

    private static ArtifactPriority? ReadArtifactPriority(IReadOnlyDictionary<string, object?> values, ValidationCollector collector)
    {
        var rawValue = ReadOptionalString(values, "priority", collector);

        if (rawValue is null)
        {
            return null;
        }

        if (ArtifactSchemaValues.TryParseArtifactPriority(rawValue, out var artifactPriority))
        {
            return artifactPriority;
        }

        collector.Add("artifact.priority.invalid", $"Artifact priority '{rawValue}' is not supported.", "priority");
        return null;
    }

    private static ConstraintKind? ReadConstraintKind(IReadOnlyDictionary<string, object?> values, ValidationCollector collector)
    {
        var rawValue = ReadOptionalString(values, "constraint_kind", collector);

        if (rawValue is null)
        {
            return null;
        }

        if (ArtifactSchemaValues.TryParseConstraintKind(rawValue, out var constraintKind))
        {
            return constraintKind;
        }

        collector.Add("artifact.constraint_kind.invalid", $"Constraint kind '{rawValue}' is not supported.", "constraint_kind");
        return null;
    }

    private static ConstraintSeverity? ReadConstraintSeverity(IReadOnlyDictionary<string, object?> values, ValidationCollector collector)
    {
        var rawValue = ReadOptionalString(values, "severity", collector);

        if (rawValue is null)
        {
            return null;
        }

        if (ArtifactSchemaValues.TryParseConstraintSeverity(rawValue, out var severity))
        {
            return severity;
        }

        collector.Add("artifact.severity.invalid", $"Constraint severity '{rawValue}' is not supported.", "severity");
        return null;
    }

    private static QuestionStatus? ReadQuestionStatus(IReadOnlyDictionary<string, object?> values, ValidationCollector collector)
    {
        var rawValue = ReadOptionalString(values, "question_status", collector);

        if (rawValue is null)
        {
            return null;
        }

        if (ArtifactSchemaValues.TryParseQuestionStatus(rawValue, out var questionStatus))
        {
            return questionStatus;
        }

        collector.Add("artifact.question_status.invalid", $"Question status '{rawValue}' is not supported.", "question_status");
        return null;
    }

    private static OutcomeKind? ReadOutcomeKind(IReadOnlyDictionary<string, object?> values, ValidationCollector collector)
    {
        var rawValue = ReadOptionalString(values, "outcome", collector);

        if (rawValue is null)
        {
            return null;
        }

        if (ArtifactSchemaValues.TryParseOutcomeKind(rawValue, out var outcome))
        {
            return outcome;
        }

        collector.Add("artifact.outcome.invalid", $"Outcome '{rawValue}' is not supported.", "outcome");
        return null;
    }

    private static SnapshotSource? ReadSnapshotSource(IReadOnlyDictionary<string, object?> values, ValidationCollector collector)
    {
        var rawValue = ReadOptionalString(values, "snapshot_source", collector);

        if (rawValue is null)
        {
            return null;
        }

        if (ArtifactSchemaValues.TryParseSnapshotSource(rawValue, out var snapshotSource))
        {
            return snapshotSource;
        }

        collector.Add("artifact.snapshot_source.invalid", $"Snapshot source '{rawValue}' is not supported.", "snapshot_source");
        return null;
    }

    private static SessionType? ReadSessionType(IReadOnlyDictionary<string, object?> values, ValidationCollector collector)
    {
        var rawValue = ReadOptionalString(values, "session_type", collector);

        if (rawValue is null)
        {
            return null;
        }

        if (ArtifactSchemaValues.TryParseSessionType(rawValue, out var sessionType))
        {
            return sessionType;
        }

        collector.Add("artifact.session_type.invalid", $"Session type '{rawValue}' is not supported.", "session_type");
        return null;
    }

    private static DateTimeOffset? ReadUtcTimestamp(IReadOnlyDictionary<string, object?> values, string key, ValidationCollector collector)
    {
        var rawValue = ReadOptionalString(values, key, collector);

        if (rawValue is null)
        {
            return null;
        }

        if (TimestampValidator.TryParseUtc(rawValue, out var timestamp))
        {
            return timestamp;
        }

        collector.Add("artifact.timestamp.invalid", $"Frontmatter key '{key}' must be a valid ISO-8601 UTC timestamp.", key);
        return null;
    }

    private static int? ReadRevision(IReadOnlyDictionary<string, object?> values, ValidationCollector collector)
    {
        if (!values.TryGetValue("revision", out var rawValue))
        {
            collector.Add("artifact.frontmatter.missing", "Frontmatter key 'revision' is required.", "revision");
            return null;
        }

        if (rawValue is not int revision)
        {
            collector.Add("artifact.revision.invalid", "Frontmatter key 'revision' must be an integer.", "revision");
            return null;
        }

        if (revision < 1)
        {
            collector.Add("artifact.revision.invalid", "Artifact revision must be greater than or equal to 1.", "revision");
            return null;
        }

        return revision;
    }

    private static bool? ReadBoolean(IReadOnlyDictionary<string, object?> values, string key, ValidationCollector collector)
    {
        if (!values.TryGetValue(key, out var rawValue))
        {
            collector.Add("artifact.frontmatter.missing", $"Frontmatter key '{key}' is required.", key);
            return null;
        }

        if (rawValue is not bool boolValue)
        {
            collector.Add("artifact.frontmatter.invalid", $"Frontmatter key '{key}' must be a boolean.", key);
            return null;
        }

        return boolValue;
    }

    private static IReadOnlyList<string>? ReadStringList(IReadOnlyDictionary<string, object?> values, string key, ValidationCollector collector)
    {
        if (!values.TryGetValue(key, out var rawValue))
        {
            collector.Add("artifact.frontmatter.missing", $"Frontmatter key '{key}' is required.", key);
            return null;
        }

        if (rawValue is not IReadOnlyList<object?> listValue)
        {
            collector.Add("artifact.frontmatter.invalid", $"Frontmatter key '{key}' must be a list of strings.", key);
            return null;
        }

        var results = new List<string>(listValue.Count);

        for (var index = 0; index < listValue.Count; index++)
        {
            if (listValue[index] is not string stringValue || string.IsNullOrWhiteSpace(stringValue))
            {
                collector.Add("artifact.frontmatter.invalid", $"Frontmatter key '{key}' must contain only non-empty string values.", $"{key}[{index}]");
                continue;
            }

            results.Add(stringValue);
        }

        return results;
    }

    private static ArtifactLinks? ReadLinks(IReadOnlyDictionary<string, object?> values, ValidationCollector collector)
    {
        if (!values.TryGetValue("links", out var rawValue))
        {
            collector.Add("artifact.frontmatter.missing", "Frontmatter key 'links' is required.", "links");
            return null;
        }

        if (rawValue is not IReadOnlyDictionary<string, object?> linksMap)
        {
            collector.Add("artifact.frontmatter.invalid", "Frontmatter key 'links' must be an object.", "links");
            return null;
        }

        foreach (var key in linksMap.Keys)
        {
            if (!ArtifactLinks.TryParseKind(key, out _))
            {
                collector.Add("artifact.links.key.invalid", $"Relationship key '{key}' is not allowed in links.", $"links.{key}");
            }
        }

        return new ArtifactLinks(ReadRelationships(linksMap, collector));
    }

    private static IReadOnlyList<ArtifactRelationship> ReadRelationships(
        IReadOnlyDictionary<string, object?> linksMap,
        ValidationCollector collector)
    {
        var relationships = new List<ArtifactRelationship>();

        foreach (var key in ArtifactLinks.FrontmatterKeys)
        {
            if (!ArtifactLinks.TryParseKind(key, out var kind))
            {
                continue;
            }

            relationships.AddRange(ReadRelationshipTargets(linksMap, key, kind, collector));
        }

        return relationships;
    }

    private static IReadOnlyList<ArtifactRelationship> ReadRelationshipTargets(
        IReadOnlyDictionary<string, object?> linksMap,
        string key,
        ArtifactRelationshipKind kind,
        ValidationCollector collector)
    {
        if (!linksMap.TryGetValue(key, out var rawValue))
        {
            return [];
        }

        if (rawValue is not IReadOnlyList<object?> listValue)
        {
            collector.Add("artifact.links.invalid", $"Relationship '{key}' must be a list of artifact ids.", $"links.{key}");
            return [];
        }

        var relationships = new List<ArtifactRelationship>(listValue.Count);

        for (var index = 0; index < listValue.Count; index++)
        {
            if (listValue[index] is not string stringValue || !ArtifactIdValidator.IsValid(stringValue))
            {
                collector.Add("artifact.links.value.invalid", $"Relationship '{key}' must contain artifact ids, not titles or arbitrary text.", $"links.{key}[{index}]");
                continue;
            }

            relationships.Add(new ArtifactRelationship(kind, stringValue));
        }

        return relationships;
    }

    private static bool HasAcceptanceCriteria(string content)
    {
        var lines = content.Split(["\r\n", "\n"], StringSplitOptions.None);

        return lines
            .Select(line => line.Trim())
            .Any(line => line.StartsWith("- ", StringComparison.Ordinal) ||
                         line.StartsWith("* ", StringComparison.Ordinal) ||
                         System.Text.RegularExpressions.Regex.IsMatch(line, "^\\d+\\.\\s"));
    }
}
