using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Memora.Core.Import;
using Memora.Import.Evidence;
using Memora.Import.Readiness;

namespace Memora.Ui.Tests;

public sealed class OperatorShellSmokeTests : IClassFixture<OperatorShellFactory>
{
    private readonly OperatorShellFactory _factory;

    public OperatorShellSmokeTests(OperatorShellFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Root_renders_project_selector()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/");

        Assert.Contains("Select a project", html);
        Assert.Contains("Demo Project", html);
        Assert.Contains("Project Selector", html);
    }

    [Fact]
    public async Task Root_uses_repo_sample_workspaces_when_no_workspace_root_is_configured()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");

        Assert.Contains("Demo Project", html);
    }

    [Fact]
    public async Task Project_page_renders_artifact_browser_and_queue()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/projects/demo-project");

        Assert.Contains("Artifacts", html);
        Assert.Contains("Approval Queue", html);
        Assert.Contains("CHR-001.r0001.md", html);
        Assert.Contains("Primary navigation", html);
        Assert.Contains(">Understanding</a>", html);
    }

    [Fact]
    public async Task Artifact_page_renders_draft_editor()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/projects/demo-project/artifacts?path=drafts%2Fplan%2FPLN-001.r0001.md");

        Assert.Contains("Edit Draft", html);
        Assert.Contains("Save new draft revision", html);
        Assert.Contains("Expand Milestone 1 test coverage", html);
    }

    [Fact]
    public async Task Review_page_renders_revision_preview()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/projects/demo-project/review?path=drafts%2Fplan%2FPLN-001.r0001.md");

        Assert.Contains("Revision Review", html);
        Assert.Contains("Review item ", html);
        Assert.Contains("Decision Readiness", html);
        Assert.Contains("Approve", html);
        Assert.Contains("Reject", html);
        Assert.Contains("Return to queue", html);
        Assert.Contains("Previous item", html);
        Assert.Contains("Next item", html);
        Assert.Contains("Current UI boundary", html);
        Assert.Contains("persist through the governed core workflow", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Proposal_page_renders_pending_proposals_as_non_canonical()
    {
        using var factory = new OperatorShellFactory();
        var proposalDirectory = Path.Combine(factory.WorkspacesRootPath, "demo-project", "drafts", "plan");
        Directory.CreateDirectory(proposalDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(proposalDirectory, "PLN-999.r0001.md"),
            """
            ---
            id: PLN-999
            project_id: demo-project
            type: plan
            status: proposed
            title: Proposed review workflow
            created_at: 2026-05-06T10:00:00Z
            updated_at: 2026-05-06T10:00:00Z
            revision: 1
            tags:
              - review
            provenance: agent
            reason: show pending proposals in the operator review interface
            links:
              depends_on: []
              affects: []
              derived_from: []
              supersedes: []
            priority: normal
            active: false
            ---
            ## Goal
            Show pending proposals.

            ## Scope
            Keep proposal review separate from approved truth.

            ## Acceptance Criteria
            - proposed artifacts are visible

            ## Notes
            This is review-only input.
            """);
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/projects/demo-project/proposals");

        Assert.Contains("Proposal Review", html, StringComparison.Ordinal);
        Assert.Contains("Proposed review workflow", html, StringComparison.Ordinal);
        Assert.Contains("Non-canonical", html, StringComparison.Ordinal);
        Assert.Contains("Inspect proposal details and diff", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Proposal_review_renders_evidence_provenance_and_candidate_notes()
    {
        using var factory = new OperatorShellFactory();
        var workspaceRoot = Path.Combine(factory.WorkspacesRootPath, "demo-project");
        var evidenceId = "local-commit-999";
        OperatorShellFactory.SeedImportedEvidence(workspaceRoot, evidenceId);
        OperatorShellFactory.SeedReadinessReport(workspaceRoot, evidenceId);
        await OperatorShellFactory.WriteProposedPlanAsync(
            workspaceRoot,
            "PLN-998",
            "Proposal with resolved provenance",
            $"candidate-provenance evidence:{evidenceId}",
            evidenceId);
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/projects/demo-project/review?path=drafts%2Fplan%2FPLN-998.r0001.md");

        Assert.Contains("Evidence Provenance", html, StringComparison.Ordinal);
        Assert.Contains("Directly Observed Evidence", html, StringComparison.Ordinal);
        Assert.Contains("local_git_commit", html, StringComparison.Ordinal);
        Assert.Contains("abc1234", html, StringComparison.Ordinal);
        Assert.Contains("Inferred Meaning And Candidate Notes", html, StringComparison.Ordinal);
        Assert.Contains("Matched commit title pattern.", html, StringComparison.Ordinal);
        Assert.Contains("Approval readiness", html, StringComparison.Ordinal);
        Assert.Contains(">ready<", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Proposal_review_blocks_readiness_when_required_provenance_is_missing()
    {
        using var factory = new OperatorShellFactory();
        var workspaceRoot = Path.Combine(factory.WorkspacesRootPath, "demo-project");
        await OperatorShellFactory.WriteProposedPlanAsync(
            workspaceRoot,
            "PLN-997",
            "Proposal with missing provenance",
            "evidence:missing-evidence",
            "missing-evidence");
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/projects/demo-project/review?path=drafts%2Fplan%2FPLN-997.r0001.md");

        Assert.Contains("Approval readiness", html, StringComparison.Ordinal);
        Assert.Contains(">blocked<", html, StringComparison.Ordinal);
        Assert.Contains("Missing Or Invalid Provenance", html, StringComparison.Ordinal);
        Assert.Contains("missing-evidence", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Trust_dashboard_renders_review_and_diagnostic_summary()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/projects/demo-project/trust");

        Assert.Contains("Trust Dashboard", html, StringComparison.Ordinal);
        Assert.Contains("Pending proposals", html, StringComparison.Ordinal);
        Assert.Contains("Stale drafts", html, StringComparison.Ordinal);
        Assert.Contains("Broken relationships", html, StringComparison.Ordinal);
        Assert.Contains("Rebuild diagnostics", html, StringComparison.Ordinal);
        Assert.Contains("Missing project memory", html, StringComparison.Ordinal);
        Assert.Contains("Recent import warnings", html, StringComparison.Ordinal);
        Assert.Contains("/projects/demo-project/proposals", html, StringComparison.Ordinal);
        Assert.Contains("/understanding?projectId=demo-project", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_approve_draft_persists_approved_revision_through_workflow()
    {
        using var factory = new OperatorShellFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var workspaceRoot = Path.Combine(factory.WorkspacesRootPath, "demo-project");
        var draftPath = Path.Combine(workspaceRoot, "drafts", "plan", "PLN-001.r0001.md");
        var approvedPath = Path.Combine(workspaceRoot, "canonical", "plans", "PLN-001.r0001.md");

        var response = await client.PostAsync(
            "/projects/demo-project/review/decision",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["path"] = "drafts/plan/PLN-001.r0001.md",
                ["decision"] = "Approve"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(File.Exists(approvedPath));
        Assert.False(File.Exists(draftPath));
        Assert.Contains("status: approved", await File.ReadAllTextAsync(approvedPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_reject_proposal_persists_deprecated_pending_record()
    {
        using var factory = new OperatorShellFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var workspaceRoot = Path.Combine(factory.WorkspacesRootPath, "demo-project");
        await OperatorShellFactory.WriteProposedPlanAsync(
            workspaceRoot,
            "PLN-996",
            "Rejectable proposal",
            "evidence:missing-evidence",
            "missing-evidence");
        var draftPath = Path.Combine(workspaceRoot, "drafts", "plan", "PLN-996.r0001.md");

        var response = await client.PostAsync(
            "/projects/demo-project/review/decision",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["path"] = "drafts/plan/PLN-996.r0001.md",
                ["decision"] = "Reject"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(File.Exists(draftPath));
        Assert.Contains("status: deprecated", await File.ReadAllTextAsync(draftPath), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(workspaceRoot, "canonical", "plans", "PLN-996.r0001.md")));
    }
}

public sealed class OperatorShellFactory : WebApplicationFactory<Program>
{
    private readonly string _tempRootPath;

    public OperatorShellFactory()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "samples", "workspaces");
        _tempRootPath = Path.Combine(Path.GetTempPath(), "Memora.Ui.Tests", Guid.NewGuid().ToString("N"));
        CopyDirectory(sourceRoot, _tempRootPath);
    }

    public string WorkspacesRootPath => _tempRootPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MemoraUi:WorkspacesRoot"] = _tempRootPath
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_tempRootPath))
        {
            Directory.Delete(_tempRootPath, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Memora.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root for Memora.Ui tests.");
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory))
        {
            File.Copy(filePath, Path.Combine(targetDirectory, Path.GetFileName(filePath)), overwrite: true);
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDirectory))
        {
            CopyDirectory(
                directoryPath,
                Path.Combine(targetDirectory, Path.GetFileName(directoryPath)));
        }
    }

    public static async Task WriteProposedPlanAsync(
        string workspaceRoot,
        string artifactId,
        string title,
        string provenance,
        string evidenceId)
    {
        var proposalDirectory = Path.Combine(workspaceRoot, "drafts", "plan");
        Directory.CreateDirectory(proposalDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(proposalDirectory, $"{artifactId}.r0001.md"),
            $$"""
            ---
            id: {{artifactId}}
            project_id: demo-project
            type: plan
            status: proposed
            title: {{title}}
            created_at: 2026-05-06T10:00:00Z
            updated_at: 2026-05-06T10:00:00Z
            revision: 1
            tags:
              - review
            provenance: {{provenance}}
            reason: show evidence provenance in proposal review
            links:
              depends_on: []
              affects: []
              derived_from: []
              supersedes: []
            priority: normal
            active: false
            ---
            ## Goal
            Show proposal provenance.

            ## Scope
            Keep proposal review separate from approved truth.

            ## Acceptance Criteria
            - provenance is visible

            ## Notes
            This is review-only input.
            """);
    }

    public static void SeedImportedEvidence(string workspaceRoot, string evidenceId)
    {
        var importedAtUtc = new DateTimeOffset(2026, 5, 6, 10, 0, 0, TimeSpan.Zero);
        var record = new ImportedEvidenceRecord(
            evidenceId,
            "demo-project",
            ImportedEvidenceSourceType.LocalGitCommit,
            "ATT-999",
            "local:demo",
            "abc1234",
            "feat(ui): add review",
            "Changed review UI files.",
            importedAtUtc.AddMinutes(-10),
            importedAtUtc,
            "git commit abc1234",
            ImportedEvidenceTrustState.ReviewableEvidence);

        new FileBackedImportedEvidenceStore()
            .Save(new ProjectEvidenceWriteRequest(workspaceRoot, [record]));
    }

    public static void SeedReadinessReport(string workspaceRoot, string evidenceId)
    {
        var generatedAtUtc = new DateTimeOffset(2026, 5, 6, 10, 5, 0, TimeSpan.Zero);
        var candidate = new CandidateMemoryRecord(
            "candidate-provenance",
            CandidateMemoryKind.ContributionStyle,
            CandidateMemorySource.Inferred,
            "Use review prefixes",
            "Commit titles suggest review UI conventions.",
            0.72,
            "Style inference needs review.",
            "Matched commit title pattern.",
            CandidateMemoryDisposition.ReviewRequired,
            [evidenceId]);
        var report = new AgentReadinessReport(
            "demo-project",
            generatedAtUtc,
            1,
            1,
            ReadyForAgentUse: false,
            MissingContext: [],
            MissingTests: [],
            RiskyModules: [],
            AdvisoryDiscoveryGaps: [],
            NextReviewSteps: ["Review inferred candidate before promotion."]);

        new FileBackedFirstRunReportStore()
            .Save(workspaceRoot, new FirstRunMemoryGenerationResult([candidate], report));
    }
}
