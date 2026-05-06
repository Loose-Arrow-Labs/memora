using System.Net;
using System.Text;
using Memora.Context.Assembly;
using Memora.Context.Models;
using Memora.Core.AgentInteraction;
using Memora.Core.Artifacts;
using Memora.Storage.Parsing;
using Memora.Storage.Workspaces;

namespace Memora.Ui.ContextViewer;

internal sealed class FileSystemContextViewerService
{
    private readonly string _workspacesRootPath;
    private readonly WorkspaceDiscovery _workspaceDiscovery = new();
    private readonly ArtifactMarkdownParser _markdownParser = new();
    private readonly ContextBundleBuilder _contextBundleBuilder = new();

    public FileSystemContextViewerService(string workspacesRootPath)
    {
        _workspacesRootPath = Path.GetFullPath(workspacesRootPath ?? throw new ArgumentNullException(nameof(workspacesRootPath)));
    }

    public ContextViewerPageModel BuildPage(ContextViewerRequest request)
    {
        var workspace = FindWorkspace(request.ProjectId);
        if (workspace is null)
        {
            return new ContextViewerPageModel(
                request.ProjectId,
                request.TaskDescription,
                request.IncludeDraftArtifacts,
                request.IncludeLayer3History,
                $"Project '{request.ProjectId}' was not found in '{_workspacesRootPath}'.",
                null,
                [new AgentInteractionError("project.not_found", $"Project '{request.ProjectId}' was not found.", "projectId")]);
        }

        var artifacts = LoadArtifacts(workspace, request.IncludeDraftArtifacts, request.IncludeLayer3History, out var errors);
        if (errors.Count > 0)
        {
            return new ContextViewerPageModel(
                request.ProjectId,
                request.TaskDescription,
                request.IncludeDraftArtifacts,
                request.IncludeLayer3History,
                null,
                null,
                errors);
        }

        var contextRequest = new ContextBundleRequest(
            request.ProjectId,
            request.TaskDescription,
            request.IncludeDraftArtifacts,
            request.IncludeLayer3History);
        var bundle = _contextBundleBuilder.Build(
            contextRequest,
            artifacts);
        var stateView = ProjectStateViewSerializer.Normalize(
            new AgentContextBundle(
                new GetContextRequest(
                    request.ProjectId,
                    request.TaskDescription,
                    request.IncludeDraftArtifacts,
                    request.IncludeLayer3History),
                bundle.Layers.Select(MapLayer).ToArray()));

        return new ContextViewerPageModel(
            request.ProjectId,
            request.TaskDescription,
            request.IncludeDraftArtifacts,
            request.IncludeLayer3History,
            null,
            stateView,
            []);
    }

    public string RenderPage(ContextViewerPageModel model)
    {
        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("<html><head><meta charset=\"utf-8\"><title>Memora Context Viewer</title>");
        html.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:2rem;line-height:1.5;}form{display:grid;gap:.75rem;max-width:38rem;margin-bottom:2rem;}label{display:grid;gap:.25rem;font-weight:600;}input[type=text]{padding:.65rem;border:1px solid #bbb;border-radius:.4rem;}fieldset{border:1px solid #ddd;padding:1rem;border-radius:.5rem;}section{margin-top:1.5rem;}article{border:1px solid #ddd;border-radius:.5rem;padding:1rem;margin:.75rem 0;background:#fafafa;}small{color:#555;}ul{margin:.5rem 0 0 1.25rem;}button{padding:.7rem 1rem;border:none;border-radius:.4rem;background:#0f5cc0;color:#fff;font-weight:600;cursor:pointer;}</style>");
        html.AppendLine("</head><body>");
        html.AppendLine("<h1>Context Viewer</h1>");
        html.AppendLine("<p>Inspect the deterministic state view carried by <code>GetContextResponse.bundle</code>. This page renders the shared runtime contract instead of a UI-owned project-state model.</p>");
        html.AppendLine("<form method=\"get\" action=\"/context-viewer\">");
        html.AppendLine($"<label>Project Id<input type=\"text\" name=\"projectId\" value=\"{WebUtility.HtmlEncode(model.ProjectId ?? string.Empty)}\" /></label>");
        html.AppendLine($"<label>Task Description<input type=\"text\" name=\"taskDescription\" value=\"{WebUtility.HtmlEncode(model.TaskDescription ?? string.Empty)}\" /></label>");
        html.AppendLine($"<label><input type=\"checkbox\" name=\"includeDraftArtifacts\" value=\"true\" {(model.IncludeDraftArtifacts ? "checked" : string.Empty)} /> Include draft artifacts</label>");
        html.AppendLine($"<label><input type=\"checkbox\" name=\"includeLayer3History\" value=\"true\" {(model.IncludeLayer3History ? "checked" : string.Empty)} /> Include Layer 3 history</label>");
        html.AppendLine("<button type=\"submit\">Build Context</button>");
        html.AppendLine("</form>");

        if (!string.IsNullOrWhiteSpace(model.ErrorMessage))
        {
            html.AppendLine($"<p><strong>Error:</strong> {WebUtility.HtmlEncode(model.ErrorMessage)}</p>");
        }

        if (model.Errors.Count > 0)
        {
            html.AppendLine("<section><h2>State View Errors</h2><ul>");
            foreach (var error in model.Errors)
            {
                html.AppendLine($"<li><code>{WebUtility.HtmlEncode(error.Code)}</code>: {WebUtility.HtmlEncode(error.Message)} <small>{WebUtility.HtmlEncode(error.Path ?? string.Empty)}</small></li>");
            }

            html.AppendLine("</ul></section>");
        }

        if (model.Bundle is null)
        {
            html.AppendLine("</body></html>");
            return html.ToString();
        }

        html.AppendLine("<section><h2>Request</h2>");
        html.AppendLine("<dl>");
        html.AppendLine($"<dt>Project</dt><dd><code>{WebUtility.HtmlEncode(model.Bundle.Request.ProjectId)}</code></dd>");
        html.AppendLine($"<dt>Task</dt><dd>{WebUtility.HtmlEncode(model.Bundle.Request.TaskDescription)}</dd>");
        html.AppendLine($"<dt>Drafts included</dt><dd>{WebUtility.HtmlEncode(model.Bundle.Request.IncludeDraftArtifacts ? "yes" : "no")}</dd>");
        html.AppendLine($"<dt>Layer 3 included</dt><dd>{WebUtility.HtmlEncode(model.Bundle.Request.IncludeLayer3History ? "yes" : "no")}</dd>");
        html.AppendLine("</dl></section>");

        foreach (var layer in model.Bundle.Layers)
        {
            html.AppendLine($"<section><h2>{WebUtility.HtmlEncode(layer.Kind.ToString())}</h2>");
            if (layer.Artifacts.Count == 0)
            {
                html.AppendLine("<p><small>No artifacts selected for this layer.</small></p></section>");
                continue;
            }

            foreach (var artifact in layer.Artifacts)
            {
                var document = artifact.Artifact;
                html.AppendLine("<article>");
                html.AppendLine($"<h3>{WebUtility.HtmlEncode(document.Id)} - {WebUtility.HtmlEncode(document.Title)}</h3>");
                html.AppendLine($"<p><small>artifact.status: {WebUtility.HtmlEncode(document.Status.ToSchemaValue())} | artifact.type: {WebUtility.HtmlEncode(document.Type.ToSchemaValue())} | revision {WebUtility.HtmlEncode(document.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture))}</small></p>");
                html.AppendLine($"<p><small>provenance: {WebUtility.HtmlEncode(document.Provenance)} | reason: {WebUtility.HtmlEncode(document.Reason)}</small></p>");
                html.AppendLine("<ul>");
                foreach (var reason in artifact.InclusionReasons)
                {
                    var related = reason.RelatedArtifactIds.Count == 0
                        ? "no related artifacts"
                        : string.Join(", ", reason.RelatedArtifactIds);
                    html.AppendLine($"<li><code>{WebUtility.HtmlEncode(reason.Code)}</code>: {WebUtility.HtmlEncode(reason.Description)} <small>{WebUtility.HtmlEncode(related)}</small></li>");
                }

                html.AppendLine("</ul></article>");
            }

            html.AppendLine("</section>");
        }

        html.AppendLine("</body></html>");
        return html.ToString();
    }

    private ProjectWorkspace? FindWorkspace(string projectId)
    {
        if (!Directory.Exists(_workspacesRootPath))
        {
            return null;
        }

        return _workspaceDiscovery
            .Discover(_workspacesRootPath)
            .SingleOrDefault(workspace => string.Equals(workspace.ProjectId, projectId, StringComparison.Ordinal));
    }

    private IReadOnlyList<ArtifactDocument> LoadArtifacts(
        ProjectWorkspace workspace,
        bool includeDrafts,
        bool includeSummaries,
        out IReadOnlyList<AgentInteractionError> errors)
    {
        var files = new List<string>();
        var collectedErrors = new List<AgentInteractionError>();
        AddMarkdownFiles(files, workspace.CanonicalRootPath);

        if (includeDrafts)
        {
            AddMarkdownFiles(files, workspace.DraftsRootPath);
        }

        if (includeSummaries)
        {
            AddMarkdownFiles(files, workspace.SummariesRootPath);
        }

        files.Sort(StringComparer.Ordinal);

        var artifacts = new List<ArtifactDocument>();

        foreach (var filePath in files)
        {
            var parsed = _markdownParser.Parse(File.ReadAllText(filePath));
            if (!parsed.Validation.IsValid || parsed.Artifact is null)
            {
                collectedErrors.AddRange(parsed.Validation.Issues.Select(issue =>
                    new AgentInteractionError(issue.Code, issue.DiagnosticMessage, issue.Path ?? filePath)));
                continue;
            }

            artifacts.Add(parsed.Artifact);
        }

        errors = collectedErrors;
        return artifacts;
    }

    private static void AddMarkdownFiles(ICollection<string> files, string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(rootPath, "*.md", SearchOption.AllDirectories))
        {
            files.Add(filePath);
        }
    }

    private static AgentContextLayer MapLayer(ContextBundleLayer layer) =>
        new(
            layer.Kind switch
            {
                ContextLayerKind.Layer1 => AgentContextLayerKind.Layer1,
                ContextLayerKind.Layer2 => AgentContextLayerKind.Layer2,
                _ => AgentContextLayerKind.Layer3
            },
            layer.Artifacts.Select(artifact =>
                new AgentContextArtifact(
                    artifact.Artifact,
                    artifact.InclusionReasons.Select(reason =>
                        new AgentContextInclusionReason(
                            reason.Code,
                            reason.Description,
                            reason.RelatedArtifactIds))
                        .ToArray()))
                .ToArray());
}
