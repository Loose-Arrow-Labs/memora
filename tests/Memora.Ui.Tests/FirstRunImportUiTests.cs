using System.Net;
using Memora.Core.Import;
using Memora.Import.Evidence;
using Memora.Import.GitHub;
using Memora.Import.Readiness;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Memora.Ui.Tests;

public sealed class FirstRunImportUiTests : IDisposable
{
    private readonly string _workspacesRootPath = Path.Combine(
        Path.GetTempPath(),
        "memora-first-run-import-ui-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FirstRunImportRoute_RendersModeEvidenceCandidateAndReadinessState()
    {
        CreateImportedWorkspace("memora");

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Memora:WorkspacesRootPath"] = _workspacesRootPath
                    })));

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/projects/memora/first-run-import?importMode=bulk_approval");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("First-Run Import", html, StringComparison.Ordinal);
        Assert.Contains("Bulk Approval", html, StringComparison.Ordinal);
        Assert.Contains("operator selected", html, StringComparison.Ordinal);
        Assert.Contains("Repository Identity", html, StringComparison.Ordinal);
        Assert.Contains("alucero270/memora", html, StringComparison.Ordinal);
        Assert.Contains("Evidence Counts", html, StringComparison.Ordinal);
        Assert.Contains("Baseline Evidence", html, StringComparison.Ordinal);
        Assert.Contains("Reviewable Evidence", html, StringComparison.Ordinal);
        Assert.Contains("Baseline Memory", html, StringComparison.Ordinal);
        Assert.Contains("Review Needed Candidates", html, StringComparison.Ordinal);
        Assert.Contains("Evidence-Derived", html, StringComparison.Ordinal);
        Assert.Contains("Inferred", html, StringComparison.Ordinal);
        Assert.Contains("Advisory / Future Advisory", html, StringComparison.Ordinal);
        Assert.Contains("Confidence", html, StringComparison.Ordinal);
        Assert.Contains("Ambiguity", html, StringComparison.Ordinal);
        Assert.Contains("Extraction Reason", html, StringComparison.Ordinal);
        Assert.Contains("Provenance", html, StringComparison.Ordinal);
        Assert.Contains("Grounded Context Ready", html, StringComparison.Ordinal);
        Assert.Contains("Review candidate memory proposals.", html, StringComparison.Ordinal);
        Assert.Contains("Later advisory discovery", html, StringComparison.Ordinal);
        Assert.Contains("Run GitHub Import", html, StringComparison.Ordinal);
        Assert.Contains("https://github.com/alucero270/memora.git", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHubImportPost_AttachesImportsReportsAndDeduplicatesEvidence()
    {
        CreateImportedWorkspace("memora");

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Memora:WorkspacesRootPath"] = _workspacesRootPath
                    }));
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IGitHubEvidenceClient>();
                    services.AddSingleton<IGitHubEvidenceClient>(new FakeGitHubEvidenceClient(CreateGitHubSnapshot()));
                });
            });

        using var client = factory.CreateClient();
        var form = new Dictionary<string, string>
        {
            ["remoteUrl"] = "https://github.com/alucero270/memora.git",
            ["importMode"] = ImportMode.EvidenceCanonical.ToSchemaValue(),
            ["maxItems"] = "3"
        };

        var response = await client.PostAsync(
            "/projects/memora/first-run-import/github",
            new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("GitHub Import Result", html, StringComparison.Ordinal);
        Assert.Contains("Imported 3 GitHub evidence record(s): 3 new, 0 already present.", html, StringComparison.Ordinal);
        Assert.Contains("github_attachment.created", html, StringComparison.Ordinal);
        Assert.Contains("github.import.completed", html, StringComparison.Ordinal);
        Assert.Contains("Canonical Evidence", html, StringComparison.Ordinal);

        var evidence = new FileBackedImportedEvidenceStore()
            .ReadAll(Path.Combine(_workspacesRootPath, "memora"));
        Assert.Contains(evidence, record =>
            record.SourceType == ImportedEvidenceSourceType.GitHubIssue &&
            record.TrustState == ImportedEvidenceTrustState.CanonicalEvidence &&
            record.SourceReference == "248");

        var repeatResponse = await client.PostAsync(
            "/projects/memora/first-run-import/github",
            new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.OK, repeatResponse.StatusCode);
        var repeatHtml = await repeatResponse.Content.ReadAsStringAsync();
        Assert.Contains("Imported 3 GitHub evidence record(s): 0 new, 3 already present.", repeatHtml, StringComparison.Ordinal);
        Assert.Contains("github_attachment.already_attached", repeatHtml, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspacesRootPath))
        {
            Directory.Delete(_workspacesRootPath, recursive: true);
        }
    }

    private void CreateImportedWorkspace(string projectId)
    {
        Directory.CreateDirectory(_workspacesRootPath);
        var workspaceRootPath = Path.Combine(_workspacesRootPath, projectId);
        Directory.CreateDirectory(workspaceRootPath);

        File.WriteAllText(
            Path.Combine(workspaceRootPath, "project.json"),
            $$"""
              {
                "projectId": "{{projectId}}",
                "name": "Memora",
                "status": "active",
                "repositoryAttachments": [
                  {
                    "attachmentId": "repo-local",
                    "projectId": "{{projectId}}",
                    "kind": "local_git",
                    "repositoryIdentity": "alucero270/memora",
                    "localPath": "C:\\source\\memora",
                    "remoteUrl": null,
                    "defaultBranch": "main",
                    "originRemoteName": "origin",
                    "originUrl": "https://github.com/alucero270/memora.git",
                    "attachedAtUtc": "2026-05-06T08:00:00Z"
                  }
                ]
              }
              """);

        var importedAtUtc = new DateTimeOffset(2026, 5, 6, 8, 15, 0, TimeSpan.Zero);
        var evidenceRecords = new[]
        {
            new ImportedEvidenceRecord(
                "local-commit-001",
                projectId,
                ImportedEvidenceSourceType.LocalGitCommit,
                "repo-local",
                "alucero270/memora",
                "abc1234",
                "feat(ui): add first-run import",
                "Changed src/Memora.Ui/Program.cs and tests/Memora.Ui.Tests.",
                importedAtUtc.AddMinutes(-10),
                importedAtUtc,
                "git commit abc1234",
                ImportedEvidenceTrustState.BaselineEvidence,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["changed_files"] = "src/Memora.Ui/Program.cs;tests/Memora.Ui.Tests/FirstRunImportUiTests.cs"
                }),
            new ImportedEvidenceRecord(
                "github-issue-215",
                projectId,
                ImportedEvidenceSourceType.GitHubIssue,
                "repo-local",
                "alucero270/memora",
                "https://github.com/alucero270/memora/issues/215",
                "M10-07: Build first-run import progress and baseline approval UI",
                "Issue requests candidate provenance, confidence, ambiguity, and advisory source visibility.",
                importedAtUtc.AddMinutes(-5),
                importedAtUtc,
                "github issue #215",
                ImportedEvidenceTrustState.ReviewableEvidence)
        };

        new FileBackedImportedEvidenceStore()
            .Save(new ProjectEvidenceWriteRequest(workspaceRootPath, evidenceRecords));

        var candidates = new[]
        {
            new CandidateMemoryRecord(
                "candidate-repo-structure",
                CandidateMemoryKind.RepoStructure,
                CandidateMemorySource.EvidenceDerived,
                "UI project area changed",
                "Imported evidence references Memora.Ui files.",
                0.91,
                "Directory role is observed, but ownership still needs review.",
                "Grouped changed-file paths by top-level directory.",
                CandidateMemoryDisposition.BaselineMemory,
                ["local-commit-001"]),
            new CandidateMemoryRecord(
                "candidate-contribution-style",
                CandidateMemoryKind.ContributionStyle,
                CandidateMemorySource.Inferred,
                "Use scoped conventional prefixes",
                "Commit titles appear to use scoped prefixes.",
                0.72,
                "Style inference should be confirmed before durable memory.",
                "Matched conventional commit-style evidence title.",
                CandidateMemoryDisposition.ReviewRequired,
                ["local-commit-001"]),
            new CandidateMemoryRecord(
                "candidate-advisory-review",
                CandidateMemoryKind.OpenQuestion,
                CandidateMemorySource.Advisory,
                "Inspect onboarding gaps later",
                "Advisory discovery could suggest follow-up memory after governed review.",
                0.55,
                "Future advisory source is not approved meaning.",
                "Recorded advisory discovery gap from readiness report.",
                CandidateMemoryDisposition.ReviewRequired,
                ["github-issue-215"])
        };

        var report = new AgentReadinessReport(
            projectId,
            importedAtUtc.AddMinutes(5),
            evidenceRecords.Length,
            candidates.Length,
            ReadyForAgentUse: false,
            MissingContext: ["No approved import baseline has been reviewed yet."],
            MissingTests: ["No UI import approval persistence test exists yet."],
            RiskyModules: ["src/Memora.Ui"],
            AdvisoryDiscoveryGaps: ["Advisory discovery could inspect CI files for additional first-run gaps."],
            NextReviewSteps: ["Review candidate memory proposals."]);

        new FileBackedFirstRunReportStore()
            .Save(workspaceRootPath, new FirstRunMemoryGenerationResult(candidates, report));
    }

    private static GitHubEvidenceSnapshot CreateGitHubSnapshot()
    {
        var observedAtUtc = new DateTimeOffset(2026, 5, 6, 10, 0, 0, TimeSpan.Zero);
        return new GitHubEvidenceSnapshot(
            [
                new GitHubIssueEvidence(
                    248,
                    "https://github.com/alucero270/memora/issues/248",
                    "Add first-run GitHub import execution UI",
                    "open",
                    observedAtUtc.AddDays(-1),
                    observedAtUtc)
            ],
            [
                new GitHubPullRequestEvidence(
                    249,
                    "https://github.com/alucero270/memora/pull/249",
                    "Add first-run GitHub import execution UI",
                    "open",
                    null,
                    observedAtUtc.AddHours(-3),
                    observedAtUtc)
            ],
            [],
            [],
            [
                new GitHubCommitEvidence(
                    "abcdef1234567890",
                    "https://github.com/alucero270/memora/commit/abcdef1234567890",
                    "feat(ui): run github import from first-run page",
                    "Alex Lucero",
                    observedAtUtc)
            ],
            [],
            [],
            IsPartial: false,
            [
                GitHubImportDiagnostic.Info(
                    "github.discussions.not_available",
                    "GitHub discussion linkage was skipped in this bounded test.",
                    "discussions")
            ]);
    }

    private sealed class FakeGitHubEvidenceClient : IGitHubEvidenceClient
    {
        private readonly GitHubEvidenceSnapshot _snapshot;

        public FakeGitHubEvidenceClient(GitHubEvidenceSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public GitHubEvidenceClientResult Fetch(string remoteUrl, int maxItems) =>
            GitHubEvidenceClientResult.Succeeded(_snapshot);
    }
}
