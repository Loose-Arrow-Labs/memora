using System.Text.RegularExpressions;
using Memora.Core.Artifacts;

namespace Memora.Core.Validation;

public static partial class ArtifactIdValidator
{
    private static readonly IReadOnlyDictionary<ArtifactType, string> Prefixes = new Dictionary<ArtifactType, string>
    {
        [ArtifactType.Charter] = "CHR",
        [ArtifactType.Plan] = "PLN",
        [ArtifactType.Decision] = "ADR",
        [ArtifactType.Constraint] = "CNS",
        [ArtifactType.Question] = "QST",
        [ArtifactType.Outcome] = "OUT",
        [ArtifactType.RepoStructure] = "REP",
        [ArtifactType.SessionSummary] = "SUM",
        [ArtifactType.Note] = "NTE"
    };

    [GeneratedRegex("^(CHR|PLN|ADR|CNS|QST|OUT|REP|SUM|NTE)-\\d{3,}$", RegexOptions.CultureInvariant)]
    private static partial Regex ArtifactIdPattern();

    public static bool IsValid(string? artifactId) =>
        !string.IsNullOrWhiteSpace(artifactId) && ArtifactIdPattern().IsMatch(artifactId);

    public static bool IsValidForType(string? artifactId, ArtifactType type) =>
        IsValid(artifactId) && artifactId!.StartsWith($"{Prefixes[type]}-", StringComparison.Ordinal);

    public static string GetPrefix(ArtifactType type) => Prefixes[type];
}
