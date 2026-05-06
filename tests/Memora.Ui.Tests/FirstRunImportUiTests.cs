using System.Net;
using Memora.Core.Import;
using Memora.Import.Evidence;
using Memora.Import.Readiness;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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
}
