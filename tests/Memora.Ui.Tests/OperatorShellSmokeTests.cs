using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Memora.Core.Artifacts;
using Microsoft.Extensions.DependencyInjection;
using Memora.Core.Import;
using Memora.Hosting;
using Memora.Import.Evidence;
using Memora.Import.Readiness;
using Memora.Ui.Operator;

namespace Memora.Ui.Tests;

public sealed class OperatorShellSmokeTests : IClassFixture<OperatorShellFactory>
{
    private readonly OperatorShellFactory _factory;

    public OperatorShellSmokeTests(OperatorShellFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Root_renders_project_selector_and_tree_sidebar()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);

        var html = await client.GetStringAsync("/");

        Assert.Contains("Select a project", html);
        Assert.Contains("Demo Project", html);
        Assert.Contains("class=\"tree-pane\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Primary navigation\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"tree-label\">Memora</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Root_NoConfiguredRoot_UsesSampleWorkspaces()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = LocalAuthTestClient.CreateAuthorizedClient(factory);

        var html = await client.GetStringAsync("/");

        Assert.Contains("Demo Project", html);
    }

    [Fact]
    public async Task Project_page_renders_artifact_browser_queue_and_hierarchical_tree()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);

        var html = await client.GetStringAsync("/projects/demo-project");

        Assert.Contains("Artifacts", html);
        Assert.Contains("Approval Queue", html);
        Assert.Contains("CHR-001.r0001.md", html);
        Assert.Contains("Primary navigation", html);
        Assert.Contains("Agent resources", html, StringComparison.Ordinal);
        Assert.Contains("Project root", html, StringComparison.Ordinal);
        Assert.Contains("class=\"breadcrumbs\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Artifact_page_renders_draft_editor()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);

        var html = await client.GetStringAsync("/projects/demo-project/artifacts?path=drafts%2Fplan%2FPLN-001.r0001.md");

        Assert.Contains("Edit Draft", html);
        Assert.Contains("Save new draft revision", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Contains("Expand Milestone 1 test coverage", html);
    }

    [Fact]
    public async Task EditPost_NoAntiforgery_ReturnsBadRequest()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);

        var response = await client.PostAsync(
            "/projects/demo-project/artifacts/edit",
            new FormUrlEncodedContent(CreateEditFormFields(includeAntiforgeryToken: null)));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("ui.csrf.invalid", payload.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Edit_post_with_antiforgery_token_saves_new_draft_revision()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);
        var formPage = await client.GetStringAsync("/projects/demo-project/artifacts?path=drafts%2Fplan%2FPLN-001.r0001.md");
        var requestToken = ExtractInputValue(formPage, "__RequestVerificationToken");

        var response = await client.PostAsync(
            "/projects/demo-project/artifacts/edit",
            new FormUrlEncodedContent(CreateEditFormFields(requestToken)));

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Expand Milestone 1 test coverage with CSRF", html);
    }

    [Fact]
    public async Task Review_page_renders_revision_preview()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);

        var html = await client.GetStringAsync("/projects/demo-project/review?path=drafts%2Fplan%2FPLN-001.r0001.md");

        Assert.Contains("Revision Review", html);
        Assert.Contains("Review item ", html);
        // The previous "Decision Readiness" / "Current UI boundary" / "Current
        // workflow scope" labels were architecture vocabulary aimed at the
        // engineer building Memora, not at the user. PBR-11 replaces or
        // removes them.
        Assert.Contains("What this needs before approval", html);
        Assert.DoesNotContain("Decision Readiness", html);
        Assert.DoesNotContain("Current UI boundary", html);
        Assert.DoesNotContain("Current workflow scope", html);
        Assert.Contains("Approve", html);
        Assert.Contains("Reject", html);
        Assert.Contains("Return to queue", html);
        Assert.Contains("Previous item", html);
        Assert.Contains("Next item", html);
    }

    [Fact]
    public async Task Proposal_page_renders_pending_proposals_as_pending_review()
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
        using var client = LocalAuthTestClient.CreateAuthorizedClient(factory);

        var html = await client.GetStringAsync("/projects/demo-project/proposals");

        Assert.Contains("Proposal Review", html, StringComparison.Ordinal);
        Assert.Contains("Proposed review workflow", html, StringComparison.Ordinal);
        // PBR-11: "Non-canonical" is internal vocabulary. The user-facing label
        // is "Pending review".
        Assert.Contains("Pending review", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Non-canonical", html, StringComparison.Ordinal);
        Assert.Contains("Inspect proposal details and diff", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProposalReview_RendersProvenanceNotes()
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
        using var client = LocalAuthTestClient.CreateAuthorizedClient(factory);

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
    public async Task ProposalReview_MissingProvenance_BlocksReadiness()
    {
        using var factory = new OperatorShellFactory();
        var workspaceRoot = Path.Combine(factory.WorkspacesRootPath, "demo-project");
        await OperatorShellFactory.WriteProposedPlanAsync(
            workspaceRoot,
            "PLN-997",
            "Proposal with missing provenance",
            "evidence:missing-evidence",
            "missing-evidence");
        using var client = LocalAuthTestClient.CreateAuthorizedClient(factory);

        var html = await client.GetStringAsync("/projects/demo-project/review?path=drafts%2Fplan%2FPLN-997.r0001.md");

        Assert.Contains("Approval readiness", html, StringComparison.Ordinal);
        Assert.Contains(">blocked<", html, StringComparison.Ordinal);
        Assert.Contains("Missing Or Invalid Provenance", html, StringComparison.Ordinal);
        Assert.Contains("missing-evidence", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProposalReview_RelationshipsNotEvidenceIds()
    {
        using var factory = new OperatorShellFactory();
        var workspaceRoot = Path.Combine(factory.WorkspacesRootPath, "demo-project");
        var evidenceId = "local-commit-998";
        OperatorShellFactory.SeedImportedEvidence(workspaceRoot, evidenceId);
        await OperatorShellFactory.WriteProposedPlanAsync(
            workspaceRoot,
            "PLN-996",
            "Proposal derived from an artifact",
            $"evidence:{evidenceId}",
            evidenceId,
            "ADR-001");
        using var client = LocalAuthTestClient.CreateAuthorizedClient(factory);

        var html = await client.GetStringAsync("/projects/demo-project/review?path=drafts%2Fplan%2FPLN-996.r0001.md");

        Assert.Contains("Approval readiness", html, StringComparison.Ordinal);
        Assert.Contains(">ready<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ADR-001", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Missing Or Invalid Provenance", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TrustDashboard_RendersReviewDiagnosticSummary()
    {
        using var client = LocalAuthTestClient.CreateAuthorizedClient(_factory);

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
    public void TrustDashboard_RebuildDiagnostics_ScopedToProject()
    {
        using var factory = new OperatorShellFactory();
        var service = new LocalOperatorWorkspaceService(new OperatorShellOptions(factory.WorkspacesRootPath, UsesSeededSampleRoot: false));
        var baselineDashboard = service.TryBuildTrustDashboard("demo-project");
        Assert.NotNull(baselineDashboard);
        var baselineBrokenRelationships = Assert.Single(
            baselineDashboard!.Metrics,
            metric => metric.Label == "Broken relationships").Count;
        OperatorShellFactory.WriteWorkspaceWithBrokenRelationship(factory.WorkspacesRootPath, "other-project");

        var dashboard = service.TryBuildTrustDashboard("demo-project");
        Assert.NotNull(dashboard);

        var brokenRelationships = Assert.Single(
            dashboard!.Metrics,
            metric => metric.Label == "Broken relationships");
        Assert.Equal(baselineBrokenRelationships, brokenRelationships.Count);
    }

    [Fact]
    public void TrustDashboard_CountsFirstRunReadinessWarnings()
    {
        using var factory = new OperatorShellFactory();
        var workspaceRoot = Path.Combine(factory.WorkspacesRootPath, "demo-project");
        OperatorShellFactory.SeedReadinessReport(
            workspaceRoot,
            "local-commit-997",
            missingContext: ["No approved import baseline has been reviewed yet."],
            missingTests: ["No deterministic test command candidate was found."],
            riskyModules: ["src/Memora.Ui"],
            advisoryDiscoveryGaps: ["Advisory discovery could inspect CI files."]);
        var service = new LocalOperatorWorkspaceService(new OperatorShellOptions(factory.WorkspacesRootPath, UsesSeededSampleRoot: false));

        var dashboard = service.TryBuildTrustDashboard("demo-project");
        Assert.NotNull(dashboard);

        var importWarnings = Assert.Single(
            dashboard!.Metrics,
            metric => metric.Label == "Recent import warnings");
        Assert.Equal(4, importWarnings.Count);
        Assert.Equal(OperatorTrustMetricState.NeedsReview, importWarnings.State);
        Assert.Contains("No approved import baseline has been reviewed yet.", importWarnings.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_ApproveDraft_PersistsApprovedRevision()
    {
        using var factory = new OperatorShellFactory();
        using var client = LocalAuthTestClient.CreateAuthorizedClient(factory, new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
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
    public async Task Review_ApproveUpdate_PersistsSupersededBaseline()
    {
        using var factory = new OperatorShellFactory();
        using var client = LocalAuthTestClient.CreateAuthorizedClient(factory, new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var workspaceRoot = Path.Combine(factory.WorkspacesRootPath, "demo-project");
        await OperatorShellFactory.WritePlanAsync(
            workspaceRoot,
            "PLN-995",
            ArtifactStatus.Approved,
            revision: 1,
            "Existing approved plan");
        await OperatorShellFactory.WritePlanAsync(
            workspaceRoot,
            "PLN-995",
            ArtifactStatus.Draft,
            revision: 2,
            "Updated draft plan");
        var currentApprovedPath = Path.Combine(workspaceRoot, "canonical", "plans", "PLN-995.r0001.md");
        var approvedPath = Path.Combine(workspaceRoot, "canonical", "plans", "PLN-995.r0002.md");
        var supersededPath = Path.Combine(workspaceRoot, "drafts", "plan", "PLN-995.r0002.md");

        var response = await client.PostAsync(
            "/projects/demo-project/review/decision",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["path"] = "drafts/plan/PLN-995.r0002.md",
                ["decision"] = "Approve"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(File.Exists(currentApprovedPath));
        Assert.True(File.Exists(approvedPath));
        Assert.True(File.Exists(supersededPath));
        Assert.Contains("status: approved", await File.ReadAllTextAsync(approvedPath), StringComparison.Ordinal);
        Assert.Contains("status: superseded", await File.ReadAllTextAsync(supersededPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_reject_proposal_persists_deprecated_pending_record()
    {
        using var factory = new OperatorShellFactory();
        using var client = LocalAuthTestClient.CreateAuthorizedClient(factory, new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
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

    [Fact]
    public async Task Review_PromoteProposal_TransitionsToDraft()
    {
        using var factory = new OperatorShellFactory();
        using var client = LocalAuthTestClient.CreateAuthorizedClient(factory, new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var workspaceRoot = Path.Combine(factory.WorkspacesRootPath, "demo-project");
        await OperatorShellFactory.WriteProposedPlanAsync(
            workspaceRoot,
            "PLN-995",
            "Promotable proposal",
            "evidence:missing-evidence",
            "missing-evidence");
        var draftPath = Path.Combine(workspaceRoot, "drafts", "plan", "PLN-995.r0001.md");

        // Before promotion, the review page should show a Promote-to-draft form and no Approve form.
        using var beforeClient = LocalAuthTestClient.CreateAuthorizedClient(factory);
        var beforeHtml = await beforeClient.GetStringAsync("/projects/demo-project/review?path=drafts/plan/PLN-995.r0001.md");
        Assert.Contains("Promote to draft", beforeHtml, StringComparison.Ordinal);
        Assert.Contains("\"decision\" value=\"Promote\"", beforeHtml, StringComparison.Ordinal);

        var response = await client.PostAsync(
            "/projects/demo-project/review/decision",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["path"] = "drafts/plan/PLN-995.r0001.md",
                ["decision"] = "Promote"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(File.Exists(draftPath));
        var promotedContents = await File.ReadAllTextAsync(draftPath);
        Assert.Contains("status: draft", promotedContents, StringComparison.Ordinal);
        Assert.DoesNotContain("status: proposed", promotedContents, StringComparison.Ordinal);

        // After promotion, the review page should show an Approve form and no Promote form.
        var afterHtml = await beforeClient.GetStringAsync("/projects/demo-project/review?path=drafts/plan/PLN-995.r0001.md");
        Assert.Contains("\"decision\" value=\"Approve\"", afterHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("\"decision\" value=\"Promote\"", afterHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_promote_rejects_draft_artifact_with_validation_error()
    {
        using var factory = new OperatorShellFactory();
        using var client = LocalAuthTestClient.CreateAuthorizedClient(factory, new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var workspaceRoot = Path.Combine(factory.WorkspacesRootPath, "demo-project");
        var draftPath = Path.Combine(workspaceRoot, "drafts", "plan", "PLN-001.r0001.md");
        Assert.True(File.Exists(draftPath), "Expected the seeded PLN-001 draft to be present.");
        var contentsBefore = await File.ReadAllTextAsync(draftPath);
        Assert.Contains("status: draft", contentsBefore, StringComparison.Ordinal);

        var response = await client.PostAsync(
            "/projects/demo-project/review/decision",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["path"] = "drafts/plan/PLN-001.r0001.md",
                ["decision"] = "Promote"
            }));

        // The decision route returns 400 + a re-rendered review page on validation
        // failure (per Program.cs). Status should remain "draft" because Promote
        // refuses non-proposed artifacts.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("approval.promote.status.invalid", body, StringComparison.Ordinal);
        var contentsAfter = await File.ReadAllTextAsync(draftPath);
        Assert.Equal(contentsBefore, contentsAfter);
    }

    [Fact]
    public async Task Root_without_local_token_returns_unauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Startup_RejectsNonLoopbackUrls()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting(WebHostDefaults.ServerUrlsKey, "http://0.0.0.0:5080"));

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("loopback", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_ValidatesEffectiveLoopbackUrlSource()
    {
        var previousAspNetCoreUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://0.0.0.0:5080");
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseSetting("MemoraUi:Urls", "http://127.0.0.1:5080"));

            using var client = LocalAuthTestClient.CreateAuthorizedClient(factory);

            Assert.NotNull(client);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", previousAspNetCoreUrls);
        }
    }

    [Fact]
    public async Task QueryToken_SetsLocalCookie_RedirectsSanitizedUrl()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = _factory.Services.GetRequiredService<LocalAccessTokenStore>().GetOrCreateToken();

        var response = await client.GetAsync($"/projects/demo-project?localToken={Uri.EscapeDataString(token)}&view=trust");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/projects/demo-project?view=trust", response.Headers.Location?.OriginalString);
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith($"{LocalAccessDefaults.CookieName}=", StringComparison.Ordinal));
    }

    private static IReadOnlyDictionary<string, string> CreateEditFormFields(string? includeAntiforgeryToken)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["path"] = "drafts/plan/PLN-001.r0001.md",
            ["title"] = "Expand Milestone 1 test coverage with CSRF",
            ["reason"] = "Protect draft edit POST.",
            ["tags"] = "milestone-1, tests",
            ["section:Goal"] = "Expand Milestone 1 test coverage with CSRF.",
            ["section:Scope"] = "Keep the operator edit flow protected.",
            ["section:Acceptance Criteria"] = "- draft edits require antiforgery tokens",
            ["section:Notes"] = "Token came from the freshly rendered form."
        };

        if (!string.IsNullOrWhiteSpace(includeAntiforgeryToken))
        {
            fields["__RequestVerificationToken"] = includeAntiforgeryToken;
        }

        return fields;
    }

    private static string ExtractInputValue(string html, string inputName)
    {
        var marker = $"name=\"{inputName}\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Input '{inputName}' was not found.");
        start += marker.Length;
        var end = html.IndexOf('"', start);
        Assert.True(end > start, $"Input '{inputName}' value was not found.");
        return System.Net.WebUtility.HtmlDecode(html[start..end]);
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

    private static string FormatYamlList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "[]"
            : $"""

                  - {value.Trim()}
              """;

    public static async Task WriteProposedPlanAsync(
        string workspaceRoot,
        string artifactId,
        string title,
        string provenance,
        string evidenceId,
        string? derivedFromArtifactId = null)
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
              derived_from: {{FormatYamlList(derivedFromArtifactId)}}
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

    public static async Task WritePlanAsync(
        string workspaceRoot,
        string artifactId,
        ArtifactStatus status,
        int revision,
        string title)
    {
        var relativeDirectory = status == ArtifactStatus.Approved
            ? Path.Combine("canonical", "plans")
            : Path.Combine("drafts", "plan");
        var planDirectory = Path.Combine(workspaceRoot, relativeDirectory);
        Directory.CreateDirectory(planDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(planDirectory, $"{artifactId}.r{revision:D4}.md"),
            $$"""
            ---
            id: {{artifactId}}
            project_id: demo-project
            type: plan
            status: {{status.ToSchemaValue()}}
            title: {{title}}
            created_at: 2026-05-06T10:00:00Z
            updated_at: 2026-05-06T10:{{revision:D2}}:00Z
            revision: {{revision}}
            tags:
              - review
            provenance: user
            reason: review approval workflow test
            links:
              depends_on: []
              affects: []
              derived_from: []
              supersedes: []
            priority: normal
            active: false
            ---
            ## Goal
            Verify approval persistence.

            ## Scope
            Keep lifecycle files consistent.

            ## Acceptance Criteria
            - approval persistence is complete

            ## Notes
            Test fixture.
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
        SeedReadinessReport(
            workspaceRoot,
            evidenceId,
            missingContext: [],
            missingTests: [],
            riskyModules: [],
            advisoryDiscoveryGaps: []);
    }

    public static void SeedReadinessReport(
        string workspaceRoot,
        string evidenceId,
        IReadOnlyList<string> missingContext,
        IReadOnlyList<string> missingTests,
        IReadOnlyList<string> riskyModules,
        IReadOnlyList<string> advisoryDiscoveryGaps)
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
            MissingContext: missingContext,
            MissingTests: missingTests,
            RiskyModules: riskyModules,
            AdvisoryDiscoveryGaps: advisoryDiscoveryGaps,
            NextReviewSteps: ["Review inferred candidate before promotion."]);

        new FileBackedFirstRunReportStore()
            .Save(workspaceRoot, new FirstRunMemoryGenerationResult([candidate], report));
    }

    public static void WriteWorkspaceWithBrokenRelationship(string workspacesRoot, string projectId)
    {
        var workspaceRoot = Path.Combine(workspacesRoot, projectId);
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "canonical", "plans"));
        File.WriteAllText(
            Path.Combine(workspaceRoot, "project.json"),
            $$"""
            {
              "projectId": "{{projectId}}",
              "name": "Other Project",
              "status": "active"
            }
            """);
        File.WriteAllText(
            Path.Combine(workspaceRoot, "canonical", "plans", "PLN-BAD.r0001.md"),
            $$"""
            ---
            id: PLN-BAD
            project_id: {{projectId}}
            type: plan
            status: approved
            title: Bad relationship fixture
            created_at: 2026-05-06T10:00:00Z
            updated_at: 2026-05-06T10:00:00Z
            revision: 1
            tags:
              - review
            provenance: user
            reason: cross-project diagnostic fixture
            links:
              depends_on:
                - MISSING-ARTIFACT
              affects: []
              derived_from: []
              supersedes: []
            priority: normal
            active: false
            ---
            ## Goal
            Trigger an index diagnostic in a different project.

            ## Scope
            Keep this diagnostic outside the selected dashboard project.

            ## Acceptance Criteria
            - selected project trust metrics stay scoped

            ## Notes
            Test fixture.
            """);
    }
}
