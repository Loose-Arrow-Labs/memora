using Memora.Core.Artifacts;
using Memora.Core.Automation;
using Memora.Core.Tests.Artifacts;

namespace Memora.Core.Tests.Automation;

public sealed class SafeAutomationTriggerEvaluatorTests
{
    private readonly SafeAutomationTriggerEvaluator _evaluator = new();

    [Fact]
    public void LifecycleTransition_StatusChange_NoMutation()
    {
        var before = CreateSessionSummary(ArtifactStatus.Proposed, revision: 1);
        var after = CreateSessionSummary(ArtifactStatus.Draft, revision: 1);

        var triggerEvent = ControlledAutomationTriggerEvent.FromLifecycleTransition(
            "event-001",
            before,
            after,
            new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(ControlledAutomationEventKind.ArtifactLifecycleChanged, triggerEvent.Kind);
        Assert.Equal("SUM-001", triggerEvent.ArtifactId);
        Assert.Equal(ArtifactStatus.Proposed, triggerEvent.PreviousStatus);
        Assert.Equal(ArtifactStatus.Draft, triggerEvent.CurrentStatus);
        Assert.Equal(ArtifactStatus.Proposed, before.Status);
        Assert.Equal(ArtifactStatus.Draft, after.Status);
    }

    [Fact]
    public void Evaluate_Lifecycle_IgnoredWithoutExplicitTrigger()
    {
        var policy = CreateSessionSummaryPolicy(enabled: true);
        var before = CreateSessionSummary(ArtifactStatus.Proposed, revision: 1);
        var after = CreateSessionSummary(ArtifactStatus.Draft, revision: 1);
        var triggerEvent = ControlledAutomationTriggerEvent.FromLifecycleTransition(
            "event-001",
            before,
            after,
            new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero));

        var decision = _evaluator.Evaluate(policy, triggerEvent);

        Assert.False(decision.IsEligible);
        Assert.Contains("automation.trigger.explicit_required", decision.ReasonCodes);
    }

    [Fact]
    public void Evaluate_OperatorRequest_AllowedForPermission()
    {
        var policy = CreateSessionSummaryPolicy(enabled: true);
        var triggerEvent = ControlledAutomationTriggerEvent.ExplicitOperatorRequest(
            "event-002",
            "memora",
            ArtifactType.SessionSummary,
            "SUM-001",
            new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero));

        var decision = _evaluator.Evaluate(policy, triggerEvent);

        Assert.True(decision.IsEligible);
        Assert.Contains("automation.trigger.policy_permission_matched", decision.ReasonCodes);
    }

    [Fact]
    public void Evaluate_BlocksDisabledPolicies()
    {
        var policy = CreateSessionSummaryPolicy(enabled: false);
        var triggerEvent = ControlledAutomationTriggerEvent.ExplicitOperatorRequest(
            "event-002",
            "memora",
            ArtifactType.SessionSummary,
            "SUM-001",
            new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero));

        var decision = _evaluator.Evaluate(policy, triggerEvent);

        Assert.False(decision.IsEligible);
        Assert.Contains("automation.trigger.policy_disabled", decision.ReasonCodes);
    }

    [Fact]
    public void TriggerEvent_NonUtcTimestamp_IsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ControlledAutomationTriggerEvent.ExplicitOperatorRequest(
                "event-003",
                "memora",
                ArtifactType.SessionSummary,
                "SUM-001",
                new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.FromHours(2))));

        Assert.Contains("UTC", exception.Message, StringComparison.Ordinal);
    }

    private static ControlledAutomationPolicy CreateSessionSummaryPolicy(bool enabled)
    {
        Assert.True(LowRiskArtifactClassCatalog.TryGetDefinition(ArtifactType.SessionSummary, out var definition));

        return new ControlledAutomationPolicy(
            "summary-direct-write",
            "Summary direct-write prototype",
            enabled,
            requiresExplicitTrigger: true,
            [
                new ControlledAutomationPermission(
                    ControlledAutomationAction.DirectWrite,
                    ArtifactType.SessionSummary,
                    definition.StorageScope,
                    definition.RequiredGuardrails)
            ]);
    }

    private static SessionSummaryArtifact CreateSessionSummary(ArtifactStatus status, int revision)
    {
        var frontmatter = ArtifactTestBuilder.CreateFrontmatter(ArtifactType.SessionSummary);
        var sections = ArtifactTestBuilder.CreateSections(ArtifactType.SessionSummary);
        frontmatter["id"] = "SUM-001";
        frontmatter["status"] = status.ToSchemaValue();
        frontmatter["revision"] = revision;

        var result = new Memora.Core.Validation.ArtifactFactory()
            .Create(frontmatter, ArtifactTestBuilder.CreateBody(sections), sections);

        Assert.True(result.Validation.IsValid);
        return Assert.IsType<SessionSummaryArtifact>(result.Artifact);
    }
}
