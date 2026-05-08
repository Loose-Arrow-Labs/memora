using Memora.Core.Artifacts;
using Memora.Core.Projects;
using Memora.Storage.Parsing;
using Memora.Storage.Persistence;
using Memora.Storage.Workspaces;

namespace Memora.Storage.Tests.Persistence;

public sealed class ArtifactFileStoreTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-file-store-tests",
        Guid.NewGuid().ToString("N"));

    private readonly ArtifactFileStore _store = new();
    private readonly ArtifactMarkdownParser _parser = new();

    [Fact]
    public void Save_ApprovedArtifact_PersistsInCanonicalTypeDirectory()
    {
        var workspace = CreateWorkspace();
        var artifact = CreatePlanArtifact(status: ArtifactStatus.Approved, revision: 1);

        var path = _store.Save(workspace, artifact);

        Assert.Equal(Path.Combine(workspace.CanonicalPlansPath, "PLN-001.r0001.md"), path);
        Assert.True(File.Exists(path));
        Assert.False(File.Exists(Path.Combine(workspace.DraftsRootPath, "plan", "PLN-001.r0001.md")));

        var parsed = _parser.Parse(File.ReadAllText(path));
        Assert.True(parsed.Validation.IsValid);
        var roundTripped = Assert.IsType<PlanArtifact>(parsed.Artifact);
        Assert.Equal(ArtifactStatus.Approved, roundTripped.Status);
        Assert.Equal(1, roundTripped.Revision);
    }

    [Fact]
    public void Save_DraftArtifact_PersistsInDraftDirectory()
    {
        var workspace = CreateWorkspace();
        var artifact = CreateDecisionArtifact(status: ArtifactStatus.Draft, revision: 3);

        var path = _store.Save(workspace, artifact);

        Assert.Equal(Path.Combine(workspace.DraftsRootPath, "decision", "ADR-001.r0003.md"), path);
        Assert.True(File.Exists(path));
        Assert.False(File.Exists(Path.Combine(workspace.CanonicalDecisionsPath, "ADR-001.r0003.md")));

        var parsed = _parser.Parse(File.ReadAllText(path));
        Assert.True(parsed.Validation.IsValid);
        var roundTripped = Assert.IsType<ArchitectureDecisionArtifact>(parsed.Artifact);
        Assert.Equal(ArtifactStatus.Draft, roundTripped.Status);
    }

    [Fact]
    public void Save_NewApprovedRevision_PreservesPriorRevisionFile()
    {
        var workspace = CreateWorkspace();
        var revisionOne = CreatePlanArtifact(status: ArtifactStatus.Approved, revision: 1);
        var revisionTwo = CreatePlanArtifact(status: ArtifactStatus.Approved, revision: 2);

        var firstPath = _store.Save(workspace, revisionOne);
        var secondPath = _store.Save(workspace, revisionTwo);

        Assert.True(File.Exists(firstPath));
        Assert.True(File.Exists(secondPath));
        Assert.NotEqual(firstPath, secondPath);
    }

    [Fact]
    public void Save_SessionSummary_PersistsInSummariesDirectory()
    {
        var workspace = CreateWorkspace();
        var artifact = CreateSessionSummaryArtifact(revision: 2);

        var path = _store.Save(workspace, artifact);

        Assert.Equal(Path.Combine(workspace.SummariesRootPath, "SUM-001.r0002.md"), path);
        var parsed = _parser.Parse(File.ReadAllText(path));
        Assert.True(parsed.Validation.IsValid);
        Assert.IsType<SessionSummaryArtifact>(parsed.Artifact);
    }

    [Fact]
    public void Save_ProjectMismatch_IsRejected()
    {
        var workspace = CreateWorkspace();
        var artifact = CreatePlanArtifact(status: ArtifactStatus.Approved, revision: 1) with { ProjectId = "other" };

        var exception = Assert.Throws<InvalidOperationException>(() => _store.Save(workspace, artifact));

        Assert.Contains("does not match workspace project", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Save_DuplicateRevisionFile_IsRejected()
    {
        var workspace = CreateWorkspace();
        var artifact = CreatePlanArtifact(status: ArtifactStatus.Approved, revision: 1);

        _store.Save(workspace, artifact);

        var exception = Assert.Throws<ArtifactPersistenceException>(() => _store.Save(workspace, artifact));

        Assert.Equal("artifact.revision.exists", exception.Code);
        Assert.Contains("already exists", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AtomicFileWriter_DoesNotTreatGenericIoFailuresAsDuplicateTargets()
    {
        var exception = new IOException("Generic move failure.", unchecked((int)0x80131620));

        Assert.False(AtomicFileWriter.IsTargetAlreadyExistsMoveFailure(exception));
    }

    [Theory]
    [InlineData(80)]
    [InlineData(183)]
    [InlineData(17)]
    public void AtomicFileWriter_RecognizesTargetExistsMoveFailures(int nativeErrorCode)
    {
        var exception = new IOException("Target exists.", unchecked((int)0x80070000) | nativeErrorCode);

        Assert.True(AtomicFileWriter.IsTargetAlreadyExistsMoveFailure(exception));
    }

    [Fact]
    public async Task Save_ConcurrentDuplicateRevision_AllowsOnlyOneWriterAndLeavesNoTempFiles()
    {
        var workspace = CreateWorkspace();
        var artifact = CreatePlanArtifact(status: ArtifactStatus.Approved, revision: 1);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 16)
                .Select(_ => Task.Run(() =>
                {
                    try
                    {
                        _store.Save(workspace, artifact);
                        return "created";
                    }
                    catch (ArtifactPersistenceException exception)
                    {
                        return exception.Code;
                    }
                })));

        Assert.Equal(1, results.Count(result => result == "created"));
        Assert.Equal(15, results.Count(result => result == "artifact.revision.exists"));
        Assert.Single(Directory.EnumerateFiles(workspace.CanonicalPlansPath, "PLN-001.r0001.md"));
        Assert.Empty(Directory.EnumerateFiles(workspace.RootPath, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void ApprovalDecisionFilePersistence_FailureAfterSupersededWrite_RollsBackPreviousState()
    {
        var workspace = CreateWorkspace();
        var persistence = new ApprovalDecisionFilePersistence();
        var currentApproved = CreatePlanArtifact(status: ArtifactStatus.Approved, revision: 1);
        var draft = CreatePlanArtifact(status: ArtifactStatus.Draft, revision: 2);
        var approved = CreatePlanArtifact(status: ArtifactStatus.Approved, revision: 2);
        var superseded = CreatePlanArtifact(status: ArtifactStatus.Superseded, revision: 2);
        var currentApprovedPath = _store.Save(workspace, currentApproved);
        var draftPath = _store.Save(workspace, draft);
        var conflictingApprovedPath = _store.Save(workspace, approved);

        var result = persistence.SaveApproved(
            workspace,
            draftPath,
            approved,
            superseded,
            currentApprovedPath);

        Assert.False(result.IsSuccess);
        Assert.True(File.Exists(currentApprovedPath));
        Assert.True(File.Exists(draftPath));
        Assert.True(File.Exists(conflictingApprovedPath));
        Assert.Empty(Directory.EnumerateFiles(workspace.RootPath, "*.approving", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(workspace.RootPath, "*.superseding", SearchOption.AllDirectories));
        var parsedDraft = _parser.Parse(File.ReadAllText(draftPath));
        Assert.Equal(ArtifactStatus.Draft, parsedDraft.Artifact?.Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private ProjectWorkspace CreateWorkspace()
    {
        Directory.CreateDirectory(_rootPath);
        var metadata = new ProjectMetadata("memora", "Memora", "active");
        return new ProjectWorkspace(metadata, _rootPath);
    }

    private static PlanArtifact CreatePlanArtifact(ArtifactStatus status, int revision) =>
        new(
            "PLN-001",
            "memora",
            status,
            "Milestone 1 storage",
            new DateTimeOffset(2026, 4, 14, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 14, 12, 30, 0, TimeSpan.Zero),
            revision,
            ["storage", "core"],
            "user",
            "filesystem persistence",
            new ArtifactLinks(["CHR-001"], ["ADR-001"], [], []),
            """
            ## Goal
            Persist artifacts on disk.

            ## Scope
            Limit the change to filesystem storage.

            ## Acceptance Criteria
            - approved artifacts persist in canonical locations
            - draft artifacts persist in draft locations

            ## Notes
            Preserve revision traceability.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Goal"] = "Persist artifacts on disk.",
                ["Scope"] = "Limit the change to filesystem storage.",
                ["Acceptance Criteria"] = "- approved artifacts persist in canonical locations\n- draft artifacts persist in draft locations",
                ["Notes"] = "Preserve revision traceability."
            },
            ArtifactPriority.Normal,
            true);

    private static ArchitectureDecisionArtifact CreateDecisionArtifact(ArtifactStatus status, int revision) =>
        new(
            "ADR-001",
            "memora",
            status,
            "Store revisions by file",
            new DateTimeOffset(2026, 4, 14, 13, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 14, 13, 10, 0, TimeSpan.Zero),
            revision,
            ["storage"],
            "user",
            "traceability",
            ArtifactLinks.Empty,
            """
            ## Context
            Approved artifacts need durable storage.

            ## Decision
            Use revisioned markdown files.

            ## Alternatives Considered
            Overwriting canonical files in place.

            ## Consequences
            Prior revisions remain traceable.
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Context"] = "Approved artifacts need durable storage.",
                ["Decision"] = "Use revisioned markdown files.",
                ["Alternatives Considered"] = "Overwriting canonical files in place.",
                ["Consequences"] = "Prior revisions remain traceable."
            },
            "2026-04-14");

    private static SessionSummaryArtifact CreateSessionSummaryArtifact(int revision) =>
        new(
            "SUM-001",
            "memora",
            ArtifactStatus.Draft,
            "Session recap",
            new DateTimeOffset(2026, 4, 14, 14, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 14, 14, 30, 0, TimeSpan.Zero),
            revision,
            ["summary"],
            "agent",
            "session wrap-up",
            ArtifactLinks.Empty,
            """
            ## Summary
            Implemented storage scaffolding.

            ## Artifacts Created
            - PLN-001

            ## Artifacts Updated
            - ADR-001

            ## Open Threads
            - verify index schema next
            """,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Summary"] = "Implemented storage scaffolding.",
                ["Artifacts Created"] = "- PLN-001",
                ["Artifacts Updated"] = "- ADR-001",
                ["Open Threads"] = "- verify index schema next"
            },
            SessionType.Execution,
            false);
}
