using System.Security.Cryptography;
using System.Text;
using Memora.Core.Import;
using Memora.Import.Scan;

namespace Memora.Import.Readiness;

public sealed class RepoUnderstandingCandidateGenerator
{
    public IReadOnlyList<CandidateMemoryRecord> Generate(
        string projectId,
        ImportMode importMode,
        RepoScanResult scan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentNullException.ThrowIfNull(scan);

        var candidates = new List<CandidateMemoryRecord>();
        candidates.AddRange(BuildDecisionCandidates(projectId, importMode, scan));
        candidates.AddRange(BuildContractCandidates(projectId, importMode, scan));
        return candidates
            .OrderBy(c => c.Kind)
            .ThenBy(c => c.Title, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<CandidateMemoryRecord> BuildDecisionCandidates(
        string projectId,
        ImportMode importMode,
        RepoScanResult scan)
    {
        var topLevelPaths = scan.Entries
            .Select(e => e.TopLevelPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        if (HasLayeredArchitecture(topLevelPaths))
        {
            yield return Create(
                projectId,
                CandidateMemoryKind.Decision,
                CandidateMemorySource.Inferred,
                "Layered architecture with separate domain, application, and infrastructure concerns",
                "Repository structure suggests a deliberate layered or clean-architecture split across top-level directories.",
                0.65,
                "Directory naming conventions differ across projects; confirm intent before making this canonical.",
                "Detected top-level directories matching common layered or clean-architecture naming patterns.",
                InferredDisposition(importMode),
                []);
        }

        if (HasAgentInstructions(scan))
        {
            yield return Create(
                projectId,
                CandidateMemoryKind.Decision,
                CandidateMemorySource.EvidenceDerived,
                "Agent instructions file governs AI-assisted development",
                "Repository contains an agent instructions file (AGENTS.md or CLAUDE.md) that defines operating rules for AI agents.",
                0.88,
                "Contents may have evolved; review current file before treating this as an active constraint.",
                "Detected AGENTS.md or CLAUDE.md in the repo scan.",
                DirectObservationDisposition(importMode),
                []);
        }

        if (HasCiConfiguration(scan))
        {
            yield return Create(
                projectId,
                CandidateMemoryKind.Decision,
                CandidateMemorySource.EvidenceDerived,
                "CI/CD pipeline defined in repository",
                "Repository contains continuous integration or deployment pipeline configuration.",
                0.85,
                "Pipeline names and triggers may have changed; verify against current CI config before treating this as active.",
                "Detected .github/workflows, Jenkinsfile, or similar CI config files.",
                DirectObservationDisposition(importMode),
                []);
        }

        if (HasContributingGuide(scan))
        {
            yield return Create(
                projectId,
                CandidateMemoryKind.Decision,
                CandidateMemorySource.EvidenceDerived,
                "Contribution process documented in CONTRIBUTING file",
                "Repository contains a CONTRIBUTING file capturing expected contribution workflow.",
                0.82,
                "Content may be stale; verify against current file before adopting as a durable rule.",
                "Detected CONTRIBUTING.md or CONTRIBUTING file.",
                DirectObservationDisposition(importMode),
                []);
        }
    }

    private static IEnumerable<CandidateMemoryRecord> BuildContractCandidates(
        string projectId,
        ImportMode importMode,
        RepoScanResult scan)
    {
        var topLevelPaths = scan.Entries
            .Select(e => e.TopLevelPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        if (HasApiSurface(topLevelPaths, scan))
        {
            yield return Create(
                projectId,
                CandidateMemoryKind.Contract,
                CandidateMemorySource.Inferred,
                "HTTP API surface exposed from repository",
                "Repository structure suggests an HTTP API project or endpoint collection.",
                0.72,
                "API presence is inferred from directory names and file patterns; review actual project files to confirm scope.",
                "Detected directory or file patterns associated with HTTP API projects.",
                InferredDisposition(importMode),
                []);
        }

        if (HasCodeowners(scan))
        {
            yield return Create(
                projectId,
                CandidateMemoryKind.Contract,
                CandidateMemorySource.EvidenceDerived,
                "Code ownership areas defined in CODEOWNERS",
                "Repository contains a CODEOWNERS file mapping paths to responsible owners.",
                0.87,
                "Ownership may have shifted since CODEOWNERS was last updated.",
                "Detected CODEOWNERS file.",
                DirectObservationDisposition(importMode),
                []);
        }

        var moduleAreas = topLevelPaths
            .Where(p => !string.IsNullOrEmpty(p) &&
                        !p.StartsWith('.') &&
                        !string.Equals(p, "docs", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (moduleAreas.Length >= 2)
        {
            yield return Create(
                projectId,
                CandidateMemoryKind.Contract,
                CandidateMemorySource.Inferred,
                $"Repository has {moduleAreas.Length} top-level module areas",
                $"Top-level areas observed: {string.Join(", ", moduleAreas.Take(8))}.",
                0.60,
                "Module boundary intent is inferred from directory names; inter-module contracts may not be explicit.",
                "Grouped distinct top-level directories as potential module areas.",
                InferredDisposition(importMode),
                []);
        }

        if (HasMcpOrOpenApiSurface(scan))
        {
            yield return Create(
                projectId,
                CandidateMemoryKind.Contract,
                CandidateMemorySource.Inferred,
                "MCP or OpenAPI integration surface present",
                "Repository contains files suggesting an MCP or OpenAPI-based integration surface.",
                0.75,
                "Presence is inferred from file naming; review actual interface definitions to confirm.",
                "Detected MCP, OpenAPI, or swagger-related file names.",
                InferredDisposition(importMode),
                []);
        }
    }

    private static bool HasLayeredArchitecture(IReadOnlyList<string> topLevelPaths)
    {
        var layerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "core", "domain", "application", "infrastructure", "api", "ui",
            "web", "storage", "persistence", "adapters", "ports", "services"
        };

        return topLevelPaths.Count(p => layerNames.Contains(p)) >= 2;
    }

    private static bool HasAgentInstructions(RepoScanResult scan) =>
        scan.Entries.Any(e =>
            string.Equals(Path.GetFileName(e.RelativePath), "AGENTS.md", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(e.RelativePath), "CLAUDE.md", StringComparison.OrdinalIgnoreCase));

    private static bool HasCiConfiguration(RepoScanResult scan) =>
        scan.Entries.Any(e =>
            e.RelativePath.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(e.RelativePath), "Jenkinsfile", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(e.RelativePath), ".gitlab-ci.yml", StringComparison.OrdinalIgnoreCase));

    private static bool HasContributingGuide(RepoScanResult scan) =>
        scan.Entries.Any(e =>
            Path.GetFileNameWithoutExtension(e.RelativePath).Equals("CONTRIBUTING", StringComparison.OrdinalIgnoreCase));

    private static bool HasApiSurface(IReadOnlyList<string> topLevelPaths, RepoScanResult scan) =>
        topLevelPaths.Any(p =>
            p.EndsWith(".api", StringComparison.OrdinalIgnoreCase) ||
            p.EndsWith(".web", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p, "api", StringComparison.OrdinalIgnoreCase)) ||
        scan.Entries.Any(e =>
            e.RelativePath.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase) &&
            e.RelativePath.Contains("api", StringComparison.OrdinalIgnoreCase));

    private static bool HasCodeowners(RepoScanResult scan) =>
        scan.Entries.Any(e =>
            string.Equals(Path.GetFileName(e.RelativePath), "CODEOWNERS", StringComparison.OrdinalIgnoreCase));

    private static bool HasMcpOrOpenApiSurface(RepoScanResult scan) =>
        scan.Entries.Any(e =>
            e.RelativePath.Contains("mcp", StringComparison.OrdinalIgnoreCase) ||
            e.RelativePath.Contains("openapi", StringComparison.OrdinalIgnoreCase) ||
            e.RelativePath.Contains("swagger", StringComparison.OrdinalIgnoreCase));

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

    private static CandidateMemoryRecord Create(
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
