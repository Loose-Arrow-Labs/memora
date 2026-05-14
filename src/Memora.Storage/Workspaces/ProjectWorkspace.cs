using Memora.Core.Projects;

namespace Memora.Storage.Workspaces;

public sealed record ProjectWorkspace
{
    public ProjectWorkspace(ProjectMetadata metadata, string rootPath)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        Metadata = metadata;
        RootPath = Path.GetFullPath(rootPath);
        ProjectMetadataPath = Path.Combine(RootPath, "project.json");
        CanonicalRootPath = Path.Combine(RootPath, "canonical");
        DraftsRootPath = Path.Combine(RootPath, "drafts");
        SummariesRootPath = Path.Combine(RootPath, "summaries");
        EvidenceRootPath = Path.Combine(RootPath, "evidence");
        CanonicalChartersPath = Path.Combine(CanonicalRootPath, "charters");
        CanonicalDecisionsPath = Path.Combine(CanonicalRootPath, "decisions");
        CanonicalPlansPath = Path.Combine(CanonicalRootPath, "plans");
        CanonicalConstraintsPath = Path.Combine(CanonicalRootPath, "constraints");
        CanonicalQuestionsPath = Path.Combine(CanonicalRootPath, "questions");
        CanonicalOutcomesPath = Path.Combine(CanonicalRootPath, "outcomes");
        CanonicalRepoPath = Path.Combine(CanonicalRootPath, "repo");
        CanonicalNotesPath = Path.Combine(CanonicalRootPath, "notes");
    }

    public ProjectMetadata Metadata { get; }

    public string ProjectId => Metadata.ProjectId;

    public string RootPath { get; }

    public string ProjectMetadataPath { get; }

    public string CanonicalRootPath { get; }

    public string DraftsRootPath { get; }

    public string SummariesRootPath { get; }

    public string EvidenceRootPath { get; }

    public string CanonicalChartersPath { get; }

    public string CanonicalDecisionsPath { get; }

    public string CanonicalPlansPath { get; }

    public string CanonicalConstraintsPath { get; }

    public string CanonicalQuestionsPath { get; }

    public string CanonicalOutcomesPath { get; }

    public string CanonicalRepoPath { get; }

    public string CanonicalNotesPath { get; }
}
