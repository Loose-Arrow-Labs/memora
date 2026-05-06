using System.Security.Cryptography;
using System.Text;
using Memora.Core.Import;

namespace Memora.Import.Readiness;

public sealed class FirstRunMemoryGenerator
{
    public FirstRunMemoryGenerationResult Generate(
        string projectId,
        ImportMode importMode,
        IReadOnlyList<ImportedEvidenceRecord> evidenceRecords)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentNullException.ThrowIfNull(evidenceRecords);

        var orderedEvidence = evidenceRecords
            .OrderBy(record => record.StableId, StringComparer.Ordinal)
            .ToArray();
        var candidates = new List<CandidateMemoryRecord>();

        candidates.AddRange(BuildRepoStructureCandidates(projectId, importMode, orderedEvidence));
        candidates.AddRange(BuildCommandCandidates(projectId, importMode, orderedEvidence));
        candidates.AddRange(BuildConstraintCandidates(projectId, importMode, orderedEvidence));
        candidates.AddRange(BuildOutcomeCandidates(projectId, importMode, orderedEvidence));
        candidates.AddRange(BuildContributionStyleCandidates(projectId, importMode, orderedEvidence));
        candidates.AddRange(BuildRiskCandidates(projectId, importMode, orderedEvidence));
        candidates.AddRange(BuildOpenQuestionCandidates(projectId, importMode, orderedEvidence));

        var distinctCandidates = candidates
            .GroupBy(candidate => $"{candidate.Kind}\u001f{candidate.Title}", StringComparer.Ordinal)
            .Select(group => MergeCandidates(projectId, importMode, group.First().Kind, group.First().Title, group))
            .OrderBy(candidate => candidate.Kind)
            .ThenBy(candidate => candidate.Title, StringComparer.Ordinal)
            .ToArray();

        var report = BuildReadinessReport(projectId, orderedEvidence, distinctCandidates);
        return new FirstRunMemoryGenerationResult(distinctCandidates, report);
    }

    private static IEnumerable<CandidateMemoryRecord> BuildRepoStructureCandidates(
        string projectId,
        ImportMode importMode,
        IReadOnlyList<ImportedEvidenceRecord> evidenceRecords)
    {
        var changedFilesByTopLevel = evidenceRecords
            .SelectMany(record => ReadChangedFiles(record).Select(file => (record, file)))
            .Select(pair => (pair.record, topLevel: GetTopLevelPath(pair.file)))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.topLevel))
            .GroupBy(pair => pair.topLevel, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        foreach (var group in changedFilesByTopLevel)
        {
            yield return CreateCandidate(
                projectId,
                CandidateMemoryKind.RepoStructure,
                CandidateMemorySource.EvidenceDerived,
                $"Top-level area: {group.Key}",
                $"Imported Git evidence references files under `{group.Key}`.",
                confidence: 0.9,
                ambiguity: "Directory role is observed from changed-file paths, but its ownership or intent still needs review.",
                extractionReason: "Grouped changed-file paths by top-level directory.",
                disposition: DirectObservationDisposition(importMode),
                group.Select(pair => pair.record.StableId));
        }
    }

    private static IEnumerable<CandidateMemoryRecord> BuildCommandCandidates(
        string projectId,
        ImportMode importMode,
        IReadOnlyList<ImportedEvidenceRecord> evidenceRecords)
    {
        var files = evidenceRecords
            .SelectMany(record => ReadChangedFiles(record).Select(file => (record, file)))
            .ToArray();

        if (files.Any(pair => pair.file.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                              pair.file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
        {
            yield return CreateCandidate(
                projectId,
                CandidateMemoryKind.BuildCommand,
                CandidateMemorySource.EvidenceDerived,
                "Build with dotnet build",
                "Imported file evidence references .NET solution or project files.",
                0.86,
                "The exact solution path may still need operator confirmation.",
                "Detected .sln or .csproj paths in changed-file evidence.",
                DirectObservationDisposition(importMode),
                files.Where(pair => pair.file.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                                    pair.file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                    .Select(pair => pair.record.StableId));

            if (files.Any(pair => pair.file.Contains("test", StringComparison.OrdinalIgnoreCase)))
            {
                yield return CreateCandidate(
                    projectId,
                    CandidateMemoryKind.TestCommand,
                    CandidateMemorySource.EvidenceDerived,
                    "Test with dotnet test",
                    "Imported file evidence references .NET test projects or test paths.",
                    0.86,
                    "The exact test project set may still need operator confirmation.",
                    "Detected .NET project paths and test-related changed files.",
                    DirectObservationDisposition(importMode),
                    files.Where(pair => pair.file.Contains("test", StringComparison.OrdinalIgnoreCase))
                        .Select(pair => pair.record.StableId));
            }
        }

        if (files.Any(pair => string.Equals(Path.GetFileName(pair.file), "package.json", StringComparison.OrdinalIgnoreCase)))
        {
            yield return CreateCandidate(
                projectId,
                CandidateMemoryKind.TestCommand,
                CandidateMemorySource.EvidenceDerived,
                "Check package.json scripts",
                "Imported file evidence references package.json.",
                0.72,
                "The actual npm/yarn/pnpm command must be confirmed from package scripts.",
                "Detected package.json in changed-file evidence.",
                DirectObservationDisposition(importMode),
                files.Where(pair => string.Equals(Path.GetFileName(pair.file), "package.json", StringComparison.OrdinalIgnoreCase))
                    .Select(pair => pair.record.StableId));
        }
    }

    private static IEnumerable<CandidateMemoryRecord> BuildConstraintCandidates(
        string projectId,
        ImportMode importMode,
        IReadOnlyList<ImportedEvidenceRecord> evidenceRecords)
    {
        var constraintEvidence = evidenceRecords
            .Where(record => ContainsAny(record, "must", "do not", "approval", "deterministic", "filesystem", "canonical"))
            .ToArray();
        if (constraintEvidence.Length == 0)
        {
            yield break;
        }

        yield return CreateCandidate(
            projectId,
            CandidateMemoryKind.Constraint,
            CandidateMemorySource.Inferred,
            "Review imported governance constraints",
            "Imported evidence contains governance or constraint language.",
            0.62,
            "The exact constraint wording is inferred from evidence text and should be reviewed.",
            "Matched constraint-oriented terms in evidence title or summary.",
            InferredDisposition(importMode),
            constraintEvidence.Select(record => record.StableId));
    }

    private static IEnumerable<CandidateMemoryRecord> BuildOutcomeCandidates(
        string projectId,
        ImportMode importMode,
        IReadOnlyList<ImportedEvidenceRecord> evidenceRecords)
    {
        var outcomeEvidence = evidenceRecords
            .Where(record => record.SourceType is ImportedEvidenceSourceType.GitHubPullRequest or ImportedEvidenceSourceType.GitHubRelease)
            .Where(record => ContainsAny(record, "closed", "merged", "release", "tag"))
            .ToArray();
        if (outcomeEvidence.Length == 0)
        {
            yield break;
        }

        yield return CreateCandidate(
            projectId,
            CandidateMemoryKind.Outcome,
            CandidateMemorySource.Inferred,
            "Review imported delivery outcomes",
            "Imported GitHub evidence includes completed PR or release signals.",
            0.68,
            "Completion state is observed, but project impact should be reviewed before becoming memory.",
            "Matched PR/release source evidence with completion-oriented state.",
            InferredDisposition(importMode),
            outcomeEvidence.Select(record => record.StableId));
    }

    private static IEnumerable<CandidateMemoryRecord> BuildContributionStyleCandidates(
        string projectId,
        ImportMode importMode,
        IReadOnlyList<ImportedEvidenceRecord> evidenceRecords)
    {
        var conventionalCommitEvidence = evidenceRecords
            .Where(record => record.Title.Contains(':', StringComparison.Ordinal) &&
                             (record.Title.StartsWith("feat(", StringComparison.OrdinalIgnoreCase) ||
                              record.Title.StartsWith("fix(", StringComparison.OrdinalIgnoreCase) ||
                              record.Title.StartsWith("docs(", StringComparison.OrdinalIgnoreCase) ||
                              record.Title.StartsWith("test(", StringComparison.OrdinalIgnoreCase) ||
                              record.Title.StartsWith("refactor(", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (conventionalCommitEvidence.Length == 0)
        {
            yield break;
        }

        yield return CreateCandidate(
            projectId,
            CandidateMemoryKind.ContributionStyle,
            CandidateMemorySource.Inferred,
            "Use conventional commit-style prefixes",
            "Imported commit evidence uses scoped conventional commit-style titles.",
            0.72,
            "Style inference is based on observed titles and should be confirmed before becoming a durable rule.",
            "Matched conventional commit-style prefixes in imported evidence titles.",
            InferredDisposition(importMode),
            conventionalCommitEvidence.Select(record => record.StableId));
    }

    private static IEnumerable<CandidateMemoryRecord> BuildRiskCandidates(
        string projectId,
        ImportMode importMode,
        IReadOnlyList<ImportedEvidenceRecord> evidenceRecords)
    {
        var riskyEvidence = evidenceRecords
            .Where(record => ContainsAny(record, "risk", "bug", "fix", "regression", "secret", "security"))
            .ToArray();
        if (riskyEvidence.Length == 0)
        {
            yield break;
        }

        yield return CreateCandidate(
            projectId,
            CandidateMemoryKind.Risk,
            CandidateMemorySource.Inferred,
            "Review recurring risk signals",
            "Imported evidence contains risk, bug, fix, security, or regression language.",
            0.58,
            "Risk grouping is inferred from keywords and needs human review.",
            "Matched risk-oriented terms in evidence title or summary.",
            InferredDisposition(importMode),
            riskyEvidence.Select(record => record.StableId));
    }

    private static IEnumerable<CandidateMemoryRecord> BuildOpenQuestionCandidates(
        string projectId,
        ImportMode importMode,
        IReadOnlyList<ImportedEvidenceRecord> evidenceRecords)
    {
        var questionEvidence = evidenceRecords
            .Where(record => record.SourceType == ImportedEvidenceSourceType.GitHubIssue)
            .Where(record => record.Title.Contains('?', StringComparison.Ordinal) ||
                             ContainsAny(record, "question", "unclear", "todo"))
            .ToArray();
        if (questionEvidence.Length == 0)
        {
            yield break;
        }

        yield return CreateCandidate(
            projectId,
            CandidateMemoryKind.OpenQuestion,
            CandidateMemorySource.Inferred,
            "Review unresolved imported questions",
            "Imported issue evidence appears to contain questions or unclear follow-up.",
            0.64,
            "Question status should be checked before creating durable project memory.",
            "Matched question-oriented terms or punctuation in issue evidence.",
            InferredDisposition(importMode),
            questionEvidence.Select(record => record.StableId));
    }

    private static AgentReadinessReport BuildReadinessReport(
        string projectId,
        IReadOnlyList<ImportedEvidenceRecord> evidenceRecords,
        IReadOnlyList<CandidateMemoryRecord> candidates)
    {
        var changedFiles = evidenceRecords.SelectMany(ReadChangedFiles).ToArray();
        var hasReadme = changedFiles.Any(file => string.Equals(Path.GetFileName(file), "README.md", StringComparison.OrdinalIgnoreCase));
        var hasAgents = changedFiles.Any(file => string.Equals(Path.GetFileName(file), "AGENTS.md", StringComparison.OrdinalIgnoreCase));
        var hasTests = candidates.Any(candidate => candidate.Kind == CandidateMemoryKind.TestCommand);
        var riskCandidate = candidates.FirstOrDefault(candidate => candidate.Kind == CandidateMemoryKind.Risk);
        var riskyModules = riskCandidate is null
            ? []
            : changedFiles
                .Select(GetTopLevelPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Take(5)
                .ToArray();

        var missingContext = new List<string>();
        if (!hasReadme)
        {
            missingContext.Add("README evidence has not been imported yet.");
        }

        if (!hasAgents)
        {
            missingContext.Add("Agent operating instructions such as AGENTS.md have not been imported yet.");
        }

        var missingTests = hasTests ? Array.Empty<string>() : ["No deterministic test command candidate was found."];
        var advisoryDiscoveryGaps = new List<string>();
        if (missingContext.Count > 0)
        {
            advisoryDiscoveryGaps.Add("Advisory discovery could search additional project docs, agent instructions, or onboarding material.");
        }

        if (!hasTests)
        {
            advisoryDiscoveryGaps.Add("Advisory discovery could inspect build configuration or CI files for missing test commands.");
        }

        if (riskCandidate is not null)
        {
            advisoryDiscoveryGaps.Add("Advisory discovery could cluster recurring bug, security, or regression signals before proposing follow-up memory.");
        }

        var nextSteps = new List<string>
        {
            "Review candidate memory before treating inferred meaning as canonical truth.",
            "Confirm build and test commands against the source checkout.",
            "Attach MCP/OpenAPI consumers only after project resolution and readiness state are visible.",
            "Keep advisory discovery outputs reviewable until they are approved or explicitly allowed."
        };

        if (missingContext.Count > 0)
        {
            nextSteps.Add("Import or author missing project orientation context before broad agent work.");
        }

        return new AgentReadinessReport(
            projectId,
            DateTimeOffset.UtcNow,
            evidenceRecords.Count,
            candidates.Count,
            ReadyForAgentUse: missingContext.Count == 0 && hasTests && riskCandidate is null,
            missingContext,
            missingTests,
            riskyModules,
            advisoryDiscoveryGaps,
            nextSteps);
    }

    private static CandidateMemoryRecord MergeCandidates(
        string projectId,
        ImportMode importMode,
        CandidateMemoryKind kind,
        string title,
        IEnumerable<CandidateMemoryRecord> candidates)
    {
        var candidateArray = candidates.ToArray();
        return CreateCandidate(
            projectId,
            kind,
            SelectMergedSource(candidateArray),
            title,
            candidateArray[0].Summary,
            candidateArray.Max(candidate => candidate.Confidence),
            candidateArray[0].Ambiguity,
            candidateArray[0].ExtractionReason,
            kind is CandidateMemoryKind.RepoStructure or CandidateMemoryKind.BuildCommand or CandidateMemoryKind.TestCommand
                ? DirectObservationDisposition(importMode)
                : InferredDisposition(importMode),
            candidateArray.SelectMany(candidate => candidate.EvidenceStableIds));
    }

    private static CandidateMemoryRecord CreateCandidate(
        string projectId,
        CandidateMemoryKind kind,
        CandidateMemorySource source,
        string title,
        string summary,
        double confidence,
        string ambiguity,
        string extractionReason,
        CandidateMemoryDisposition disposition,
        IEnumerable<string> evidenceStableIds)
    {
        var evidenceIds = evidenceStableIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var id = CreateCandidateId(projectId, kind, source, title, evidenceIds);
        return new CandidateMemoryRecord(id, kind, source, title, summary, confidence, ambiguity, extractionReason, disposition, evidenceIds);
    }

    private static CandidateMemorySource SelectMergedSource(IReadOnlyList<CandidateMemoryRecord> candidates)
    {
        if (candidates.Any(candidate => candidate.Source == CandidateMemorySource.Advisory))
        {
            return CandidateMemorySource.Advisory;
        }

        return candidates.Any(candidate => candidate.Source == CandidateMemorySource.Inferred)
            ? CandidateMemorySource.Inferred
            : CandidateMemorySource.EvidenceDerived;
    }

    private static CandidateMemoryDisposition DirectObservationDisposition(ImportMode importMode) =>
        importMode switch
        {
            ImportMode.StrictGovernance => CandidateMemoryDisposition.ReviewRequired,
            ImportMode.BulkApproval => CandidateMemoryDisposition.GroupedBaselineReview,
            _ => CandidateMemoryDisposition.BaselineMemory
        };

    private static CandidateMemoryDisposition InferredDisposition(ImportMode importMode) =>
        importMode == ImportMode.BulkApproval
            ? CandidateMemoryDisposition.GroupedBaselineReview
            : CandidateMemoryDisposition.ReviewRequired;

    private static bool ContainsAny(ImportedEvidenceRecord record, params string[] terms)
    {
        var text = $"{record.Title}\n{record.Summary}".ToLowerInvariant();
        return terms.Any(term => text.Contains(term.ToLowerInvariant(), StringComparison.Ordinal));
    }

    private static IEnumerable<string> ReadChangedFiles(ImportedEvidenceRecord record)
    {
        if (!record.Metadata.TryGetValue("changedFiles", out var value))
        {
            return [];
        }

        return value
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(file => file, StringComparer.Ordinal);
    }

    private static string GetTopLevelPath(string path)
    {
        var normalized = path.Replace('\\', '/').Trim('/');
        var slashIndex = normalized.IndexOf('/', StringComparison.Ordinal);
        return slashIndex < 0 ? normalized : normalized[..slashIndex];
    }

    private static string CreateCandidateId(
        string projectId,
        CandidateMemoryKind kind,
        CandidateMemorySource source,
        string title,
        IReadOnlyList<string> evidenceIds)
    {
        var input = $"{projectId}\n{kind}\n{source}\n{title}\n{string.Join("\n", evidenceIds)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"CAN-{Convert.ToHexString(hash)[..16]}";
    }
}
