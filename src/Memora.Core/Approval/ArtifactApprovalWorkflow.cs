using System.Globalization;
using Memora.Core.Artifacts;
using Memora.Core.Validation;

namespace Memora.Core.Approval;

public sealed class ArtifactApprovalWorkflow
{
    private readonly ArtifactFactory _artifactFactory;

    public ArtifactApprovalWorkflow()
        : this(new ArtifactFactory())
    {
    }

    public ArtifactApprovalWorkflow(ArtifactFactory artifactFactory)
    {
        _artifactFactory = artifactFactory ?? throw new ArgumentNullException(nameof(artifactFactory));
    }

    public ArtifactApprovalDecisionResult Approve(
        ArtifactDocument pendingArtifact,
        DateTimeOffset decidedAtUtc,
        ArtifactDocument? currentApprovedArtifact = null)
    {
        ArgumentNullException.ThrowIfNull(pendingArtifact);

        var issues = ValidateDecisionTimestamp(decidedAtUtc).ToList();

        if (pendingArtifact.Status != ArtifactStatus.Draft)
        {
            issues.Add(new ArtifactValidationIssue(
                "approval.approve.status.invalid",
                "Only draft artifacts can be approved.",
                "status"));
        }

        if (currentApprovedArtifact is not null)
        {
            issues.AddRange(ValidateCurrentApprovedArtifact(pendingArtifact, currentApprovedArtifact));
        }

        if (issues.Count > 0)
        {
            return ArtifactApprovalDecisionResult.FromValidation(pendingArtifact, issues);
        }

        var approvedRebuild = RebuildArtifact(
            pendingArtifact,
            ArtifactStatus.Approved,
            pendingArtifact.Revision,
            decidedAtUtc);

        if (!approvedRebuild.Validation.IsValid || approvedRebuild.Artifact is null)
        {
            return ArtifactApprovalDecisionResult.FromValidation(
                pendingArtifact,
                MapRebuildIssues(pendingArtifact, approvedRebuild.Validation));
        }

        var approvedCandidate = approvedRebuild.Artifact;
        issues.AddRange(LifecycleTransitionValidator.Validate(pendingArtifact, approvedCandidate).Issues);

        ArtifactDocument? supersededArtifact = null;

        if (currentApprovedArtifact is not null)
        {
            var supersededRebuild = RebuildArtifact(
                currentApprovedArtifact,
                ArtifactStatus.Superseded,
                approvedCandidate.Revision,
                decidedAtUtc);

            if (!supersededRebuild.Validation.IsValid || supersededRebuild.Artifact is null)
            {
                return ArtifactApprovalDecisionResult.FromValidation(
                    pendingArtifact,
                    MapRebuildIssues(currentApprovedArtifact, supersededRebuild.Validation));
            }

            supersededArtifact = supersededRebuild.Artifact;
            issues.AddRange(LifecycleTransitionValidator.Validate(currentApprovedArtifact, supersededArtifact).Issues);
        }

        if (issues.Count > 0)
        {
            return ArtifactApprovalDecisionResult.FromValidation(pendingArtifact, issues);
        }

        return ArtifactApprovalDecisionResult.Approved(
            pendingArtifact,
            approvedCandidate,
            supersededArtifact);
    }

    public ArtifactApprovalDecisionResult Reject(
        ArtifactDocument pendingArtifact,
        DateTimeOffset decidedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(pendingArtifact);

        var issues = ValidateDecisionTimestamp(decidedAtUtc).ToList();

        if (pendingArtifact.Status is not ArtifactStatus.Draft and not ArtifactStatus.Proposed)
        {
            issues.Add(new ArtifactValidationIssue(
                "approval.reject.status.invalid",
                "Only draft or proposed artifacts can be rejected.",
                "status"));
        }

        if (issues.Count > 0)
        {
            return ArtifactApprovalDecisionResult.FromValidation(pendingArtifact, issues);
        }

        var rejectedRebuild = RebuildArtifact(
            pendingArtifact,
            ArtifactStatus.Deprecated,
            pendingArtifact.Revision,
            decidedAtUtc);

        if (!rejectedRebuild.Validation.IsValid || rejectedRebuild.Artifact is null)
        {
            return ArtifactApprovalDecisionResult.FromValidation(
                pendingArtifact,
                MapRebuildIssues(pendingArtifact, rejectedRebuild.Validation));
        }

        var rejectedArtifact = rejectedRebuild.Artifact;
        issues.AddRange(LifecycleTransitionValidator.Validate(pendingArtifact, rejectedArtifact).Issues);

        return issues.Count > 0
            ? ArtifactApprovalDecisionResult.FromValidation(pendingArtifact, issues)
            : ArtifactApprovalDecisionResult.Rejected(pendingArtifact, rejectedArtifact);
    }

    private IEnumerable<ArtifactValidationIssue> ValidateDecisionTimestamp(DateTimeOffset decidedAtUtc)
    {
        if (decidedAtUtc == default || decidedAtUtc.Offset != TimeSpan.Zero)
        {
            yield return new ArtifactValidationIssue(
                "approval.timestamp.invalid",
                "Approval decision timestamps must be non-default UTC values.",
                "updated_at");
        }
    }

    private IEnumerable<ArtifactValidationIssue> ValidateCurrentApprovedArtifact(
        ArtifactDocument pendingArtifact,
        ArtifactDocument currentApprovedArtifact)
    {
        if (currentApprovedArtifact.Status != ArtifactStatus.Approved)
        {
            yield return new ArtifactValidationIssue(
                "approval.current.status.invalid",
                "Current canonical artifact must be approved.",
                "current.status");
        }

        if (!string.Equals(pendingArtifact.Id, currentApprovedArtifact.Id, StringComparison.Ordinal))
        {
            yield return new ArtifactValidationIssue(
                "approval.current.id.mismatch",
                "Updated drafts must target the same artifact id as the current approved artifact.",
                "current.id");
        }

        if (!string.Equals(pendingArtifact.ProjectId, currentApprovedArtifact.ProjectId, StringComparison.Ordinal))
        {
            yield return new ArtifactValidationIssue(
                "approval.current.project_id.mismatch",
                "Updated drafts must target the same project as the current approved artifact.",
                "current.project_id");
        }

        if (pendingArtifact.Type != currentApprovedArtifact.Type)
        {
            yield return new ArtifactValidationIssue(
                "approval.current.type.mismatch",
                "Updated drafts must target the same artifact type as the current approved artifact.",
                "current.type");
        }

        if (pendingArtifact.Revision <= currentApprovedArtifact.Revision)
        {
            yield return new ArtifactValidationIssue(
                "approval.current.revision.invalid",
                "Updated drafts must have a higher revision than the current approved artifact.",
                "revision");
        }
    }

    private ArtifactCreationResult RebuildArtifact(
        ArtifactDocument artifact,
        ArtifactStatus status,
        int revision,
        DateTimeOffset updatedAtUtc)
    {
        var frontmatter = ArtifactDocumentRehydrator.BuildFrontmatter(artifact);
        frontmatter["status"] = status.ToSchemaValue();
        frontmatter["revision"] = revision;
        frontmatter["updated_at"] = updatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);

        var sections = new Dictionary<string, string>(artifact.Sections, StringComparer.Ordinal);
        return _artifactFactory.Create(
            frontmatter,
            ArtifactDocumentRehydrator.BuildBody(sections),
            sections);
    }

    private static IEnumerable<ArtifactValidationIssue> MapRebuildIssues(
        ArtifactDocument artifact,
        ArtifactValidationResult validation)
    {
        if (validation.Issues.Count == 0)
        {
            yield return new ArtifactValidationIssue(
                "approval.rebuild.invalid",
                $"Approval workflow could not rebuild artifact '{artifact.Id}' for the requested lifecycle transition.",
                "artifact");
            yield break;
        }

        foreach (var issue in validation.Issues)
        {
            yield return new ArtifactValidationIssue(
                "approval.rebuild.invalid",
                $"Approval workflow could not rebuild artifact '{artifact.Id}': {issue.Message}",
                issue.Path);
        }
    }
}

public sealed class ArtifactApprovalDecisionResult
{
    private ArtifactApprovalDecisionResult(
        ArtifactDocument pendingArtifact,
        ArtifactDocument? approvedArtifact,
        ArtifactDocument? rejectedArtifact,
        ArtifactDocument? supersededArtifact,
        IEnumerable<ArtifactValidationIssue> validationIssues)
    {
        PendingArtifact = pendingArtifact ?? throw new ArgumentNullException(nameof(pendingArtifact));
        ApprovedArtifact = approvedArtifact;
        RejectedArtifact = rejectedArtifact;
        SupersededArtifact = supersededArtifact;
        Validation = new ArtifactValidationResult(validationIssues);
    }

    public ArtifactDocument PendingArtifact { get; }

    public ArtifactDocument? ApprovedArtifact { get; }

    public ArtifactDocument? RejectedArtifact { get; }

    public ArtifactDocument? SupersededArtifact { get; }

    public ArtifactValidationResult Validation { get; }

    public bool IsSuccess => Validation.IsValid;

    public static ArtifactApprovalDecisionResult Approved(
        ArtifactDocument pendingArtifact,
        ArtifactDocument approvedArtifact,
        ArtifactDocument? supersededArtifact) =>
        new(pendingArtifact, approvedArtifact, null, supersededArtifact, []);

    public static ArtifactApprovalDecisionResult Rejected(
        ArtifactDocument pendingArtifact,
        ArtifactDocument rejectedArtifact) =>
        new(pendingArtifact, null, rejectedArtifact, null, []);

    public static ArtifactApprovalDecisionResult FromValidation(
        ArtifactDocument pendingArtifact,
        IEnumerable<ArtifactValidationIssue> issues) =>
        new(pendingArtifact, null, null, null, issues);
}
