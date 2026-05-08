using System.Globalization;
using System.Net;
using System.Text;
using Memora.Core.Artifacts;
using Memora.Core.Import;
using Memora.Import.Readiness;
using Memora.Ui.FirstRunImport;
using Memora.Ui.Operator;

namespace Memora.Ui.Rendering;

internal static class OperatorShellPageRenderer
{
    public static string RenderHome(
        OperatorShellOptions options,
        IReadOnlyList<OperatorProjectSummary> projects)
    {
        var body = new StringBuilder();
        body.AppendLine("<section class=\"hero\">");
        body.AppendLine("<p class=\"eyebrow\">Memora Human Loop</p>");
        body.AppendLine("<h1>Minimal local operator shell</h1>");
        body.AppendLine("<p class=\"lede\">Browse local workspace artifacts, inspect draft revisions, review the current approval queue, and jump into governed context views without changing canonical project truth.</p>");
        body.AppendLine("</section>");

        body.AppendLine("<section class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\">");
        body.AppendLine("<h2>Select a project</h2>");
        body.AppendLine($"<p class=\"muted\">Workspace root: {Encode(options.NormalizedWorkspacesRootPath)}</p>");
        body.AppendLine("</div>");
        body.AppendLine("<div class=\"project-grid\">");

        foreach (var project in projects)
        {
            body.AppendLine("<article class=\"project-card\">");
            body.AppendLine($"<h3><a href=\"/projects/{Encode(project.ProjectId)}\">{Encode(project.Name)}</a></h3>");
            body.AppendLine($"<p class=\"muted\">{Encode(project.ProjectId)}</p>");
            body.AppendLine($"<p>{Encode(project.ArtifactCount.ToString(CultureInfo.InvariantCulture))} artifacts, {Encode(project.PendingCount.ToString(CultureInfo.InvariantCulture))} pending review.</p>");
            body.AppendLine($"<p class=\"muted\">Status: {Encode(project.Status ?? "unspecified")}</p>");
            body.AppendLine("</article>");
        }

        body.AppendLine("</div>");
        body.AppendLine("</section>");
        body.AppendLine(RenderScopeNote(options));

        return RenderLayout("Memora.Ui", options, projects, null, body.ToString());
    }

    public static string RenderProject(
        OperatorShellOptions options,
        IReadOnlyList<OperatorProjectSummary> projects,
        OperatorProjectSnapshot snapshot)
    {
        var body = new StringBuilder();
        body.AppendLine("<section class=\"hero compact\">");
        body.AppendLine($"<p class=\"eyebrow\">Project</p><h1>{Encode(snapshot.Workspace.Metadata.Name)}</h1>");
        body.AppendLine($"<p class=\"lede\">Workspace dashboard for <code>{Encode(snapshot.Workspace.ProjectId)}</code>. Use the navigation above to move between artifacts, first-run import, review queue, context, and understanding views.</p>");
        body.AppendLine("</section>");

        body.AppendLine("<section class=\"two-up\">");
        body.AppendLine("<article class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\">");
        body.AppendLine("<h2>Artifacts</h2>");
        body.AppendLine("<p class=\"muted\">Open file-backed records discovered from canonical, draft, and summary storage. Draft and proposed artifacts appear first.</p>");
        body.AppendLine("</div>");
        body.AppendLine("<div class=\"table-scroll\">");
        body.AppendLine("<table><thead><tr><th>Title</th><th>Type</th><th>Status</th><th>Revision</th><th>File</th></tr></thead><tbody>");

        foreach (var record in snapshot.Artifacts)
        {
            var artifactLink = BuildArtifactLink(snapshot.Workspace.ProjectId, record.RelativePath);
            body.AppendLine("<tr>");
            body.AppendLine($"<td><a href=\"{artifactLink}\">{Encode(record.Artifact.Title)}</a></td>");
            body.AppendLine($"<td>{Encode(record.Artifact.Type.ToSchemaValue())}</td>");
            body.AppendLine($"<td>{RenderStatusBadge(record.Artifact.Status)}</td>");
            body.AppendLine($"<td>{Encode(record.Artifact.Revision.ToString(CultureInfo.InvariantCulture))}</td>");
            body.AppendLine($"<td><code>{Encode(record.RelativePath)}</code></td>");
            body.AppendLine("</tr>");
        }

        body.AppendLine("</tbody></table>");
        body.AppendLine("</div>");
        body.AppendLine("</article>");

        body.AppendLine("<article class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\">");
        body.AppendLine("<h2>Approval Queue</h2>");
        body.AppendLine("<p class=\"muted\">Preview the current pending artifacts in core queue order.</p>");
        body.AppendLine("</div>");

        if (snapshot.PendingItems.Count == 0)
        {
            body.AppendLine("<p>No draft or proposed artifacts are waiting for review.</p>");
        }
        else
        {
            body.AppendLine("<ul class=\"list\">");
            foreach (var item in snapshot.PendingItems)
            {
                var reviewLink = BuildReviewLink(snapshot.Workspace.ProjectId, item.Record.RelativePath);
                body.AppendLine("<li>");
                body.AppendLine($"<a href=\"{reviewLink}\">{Encode(item.QueueItem.Title)}</a> ");
                body.AppendLine($"<span class=\"muted\">{Encode(item.QueueItem.ArtifactType.ToSchemaValue())} &middot; rev {Encode(item.QueueItem.Revision.ToString(CultureInfo.InvariantCulture))}</span>");
                body.AppendLine("</li>");
            }

            body.AppendLine("</ul>");
            body.AppendLine($"<p><a class=\"button ghost\" href=\"/projects/{Encode(snapshot.Workspace.ProjectId)}/queue\">Open review queue</a></p>");
        }

        body.AppendLine("</article>");

        body.AppendLine("<article class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\">");
        body.AppendLine("<h2>Proposal Review</h2>");
        body.AppendLine("<p class=\"muted\">Inspect proposed artifacts as non-canonical review inputs before any approval workflow can promote them.</p>");
        body.AppendLine("</div>");
        body.AppendLine($"<p>{Encode(snapshot.ProposedItems.Count.ToString(CultureInfo.InvariantCulture))} proposed artifact(s) need proposal review.</p>");
        body.AppendLine($"<p><a class=\"button ghost\" href=\"/projects/{Encode(snapshot.Workspace.ProjectId)}/proposals\">Open proposals</a></p>");
        body.AppendLine("</article>");

        body.AppendLine("<article class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\">");
        body.AppendLine("<h2>Trust Dashboard</h2>");
        body.AppendLine("<p class=\"muted\">Inspect review pressure, rebuild health, missing memory, and import warnings from shared diagnostics.</p>");
        body.AppendLine("</div>");
        body.AppendLine($"<p><a class=\"button ghost\" href=\"/projects/{Encode(snapshot.Workspace.ProjectId)}/trust\">Open trust dashboard</a></p>");
        body.AppendLine("</article>");

        body.AppendLine("<article class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\">");
        body.AppendLine("<h2>First-Run Import</h2>");
        body.AppendLine("<p class=\"muted\">Inspect attached repositories, imported evidence, candidate memory, and readiness state.</p>");
        body.AppendLine("</div>");
        body.AppendLine($"<p><a class=\"button ghost\" href=\"/projects/{Encode(snapshot.Workspace.ProjectId)}/first-run-import\">Open first-run import</a></p>");
        body.AppendLine("</article>");
        body.AppendLine("</section>");
        body.AppendLine(RenderScopeNote(options));

        return RenderLayout(snapshot.Workspace.Metadata.Name, options, projects, snapshot.Workspace.ProjectId, body.ToString());
    }

    public static string RenderFirstRunImport(
        OperatorShellOptions options,
        IReadOnlyList<OperatorProjectSummary> projects,
        FirstRunImportStatusPage page)
    {
        var body = new StringBuilder();
        body.AppendLine("<section class=\"hero compact\">");
        body.AppendLine("<p class=\"eyebrow\">First-Run Import</p>");
        body.AppendLine($"<h1>{Encode(page.ProjectName)}</h1>");
        body.AppendLine($"<p class=\"lede\">Import status for <code>{Encode(page.ProjectId)}</code>: attached repositories, bounded evidence, candidate memory, advisory discovery gaps, and governed context readiness.</p>");
        body.AppendLine("<div class=\"hero-actions\">");
        body.AppendLine($"<a class=\"button ghost\" href=\"/projects/{Encode(page.ProjectId)}\">Project artifacts</a>");
        body.AppendLine($"<a class=\"button ghost\" href=\"/projects/{Encode(page.ProjectId)}/queue\">Review queue</a>");
        body.AppendLine($"<a class=\"button ghost\" href=\"/context-viewer?projectId={Encode(page.ProjectId)}&taskDescription=Prepare%20agent%20readiness\">Agent setup context</a>");
        body.AppendLine("</div>");
        body.AppendLine("</section>");

        body.AppendLine("<section class=\"two-up\">");
        body.AppendLine("<article class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\"><h2>Import Mode</h2><p class=\"muted\">Promotion remains governed by import mode, lifecycle, provenance, safety filtering, and approval.</p></div>");
        body.AppendLine(RenderImportModeForm(page));
        body.AppendLine("</article>");
        body.AppendLine("<article class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\"><h2>Completion State</h2><p class=\"muted\">Current first-run progress from stored workspace files.</p></div>");
        body.AppendLine(RenderProgressSteps(page.ProgressSteps));
        body.AppendLine("</article>");
        body.AppendLine("</section>");

        body.AppendLine("<section class=\"two-up\">");
        body.AppendLine("<article class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\"><h2>Repository Identity</h2><p class=\"muted\">Source repositories are evidence sources; the Memora workspace stays app-managed.</p></div>");
        body.AppendLine(RenderRepositoryAttachments(page.RepositoryAttachments));
        body.AppendLine("</article>");
        body.AppendLine("<article class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\"><h2>Evidence Counts</h2><p class=\"muted\">Baseline evidence, canonical evidence, and reviewable evidence remain visible separately.</p></div>");
        body.AppendLine(RenderEvidenceCounters(page));
        body.AppendLine(RenderEvidenceSummaries(page.EvidenceSummaries));
        body.AppendLine("</article>");
        body.AppendLine("</section>");

        if (page.Warnings.Count > 0)
        {
            body.AppendLine("<section class=\"panel alert\">");
            body.AppendLine("<div class=\"panel-header\"><h2>Warnings</h2><p class=\"muted\">Diagnostics and readiness gaps are shown without exposing secret values.</p></div>");
            body.AppendLine("<ul class=\"list\">");
            foreach (var warning in page.Warnings)
            {
                body.AppendLine($"<li>{Encode(warning)}</li>");
            }

            body.AppendLine("</ul>");
            body.AppendLine("</section>");
        }

        body.AppendLine("<section class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\"><h2>Baseline Approval</h2><p class=\"muted\">Candidate disposition controls what is baseline memory versus review-needed meaning.</p></div>");
        body.AppendLine(RenderCandidateTrustSummary(page));
        body.AppendLine("</section>");

        body.AppendLine("<section class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\"><h2>Candidate Memory</h2><p class=\"muted\">Sources distinguish evidence-derived facts, inferred meaning, and advisory or future-advisory candidates.</p></div>");
        body.AppendLine(RenderCandidateTable(page.Candidates));
        body.AppendLine("</section>");

        body.AppendLine("<section class=\"two-up\">");
        body.AppendLine("<article class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\"><h2>Readiness Report</h2><p class=\"muted\">Agent readiness is visible before consumers attach.</p></div>");
        body.AppendLine(RenderReadinessReport(page));
        body.AppendLine("</article>");
        body.AppendLine("<article class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\"><h2>Next Actions</h2><p class=\"muted\">Review, agent setup, re-import, and advisory discovery stay explicit.</p></div>");
        body.AppendLine(RenderNextActions(page.NextActions));
        body.AppendLine("</article>");
        body.AppendLine("</section>");

        body.AppendLine(RenderScopeNote(options));
        return RenderLayout($"{page.ProjectName} first-run import", options, projects, page.ProjectId, body.ToString());
    }

    public static string RenderArtifact(
        OperatorShellOptions options,
        IReadOnlyList<OperatorProjectSummary> projects,
        OperatorArtifactView view,
        IReadOnlyList<string> validationErrors,
        string? antiforgeryFieldName = null,
        string? antiforgeryRequestToken = null)
    {
        var body = new StringBuilder();
        var artifact = view.SelectedArtifact.Artifact;

        body.AppendLine("<section class=\"hero compact\">");
        body.AppendLine($"<p class=\"eyebrow\">Artifact Record</p><h1>{Encode(artifact.Title)}</h1>");
        body.AppendLine($"<p class=\"lede\">{Encode(artifact.Type.ToSchemaValue())} &middot; {Encode(artifact.Status.ToSchemaValue())} &middot; revision {Encode(artifact.Revision.ToString(CultureInfo.InvariantCulture))}</p>");
        body.AppendLine($"<p class=\"lede context-note\">Filesystem-backed {Encode(artifact.Type.ToSchemaValue())} artifact from <code>{Encode(view.SelectedArtifact.RelativePath)}</code>. This page shows stored metadata, markdown sections, and draft editing controls when the artifact is pending review.</p>");
        body.AppendLine("<div class=\"hero-actions\">");
        body.AppendLine($"<a class=\"button ghost\" href=\"/projects/{Encode(view.Project.Workspace.ProjectId)}\">Project artifacts</a>");
        body.AppendLine($"<a class=\"button ghost\" href=\"/projects/{Encode(view.Project.Workspace.ProjectId)}/queue\">Review queue</a>");
        body.AppendLine("</div>");
        body.AppendLine("</section>");

        if (validationErrors.Count > 0)
        {
            body.AppendLine("<section class=\"panel alert\">");
            body.AppendLine("<h2>Draft edit validation</h2>");
            body.AppendLine("<ul class=\"list\">");
            foreach (var error in validationErrors)
            {
                body.AppendLine($"<li>{Encode(error)}</li>");
            }

            body.AppendLine("</ul>");
            body.AppendLine("</section>");
        }

        body.AppendLine("<section class=\"two-up\">");
        body.AppendLine("<article class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\"><h2>Artifact Detail</h2><p class=\"muted\">Filesystem-backed record view.</p></div>");
        body.AppendLine(RenderArtifactSummary(view));
        body.AppendLine("</article>");

        body.AppendLine("<article class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\"><h2>Sections</h2><p class=\"muted\">Structured section values from the markdown body.</p></div>");
        body.AppendLine(RenderSections(artifact.Sections));
        body.AppendLine("</article>");
        body.AppendLine("</section>");

        if (view.SelectedArtifact.IsPendingReview)
        {
            body.AppendLine("<section class=\"panel\">");
            body.AppendLine("<div class=\"panel-header\"><h2>Edit Draft</h2><p class=\"muted\">Edits create a new draft revision through the core editing flow.</p></div>");
            body.AppendLine(RenderEditForm(view, antiforgeryFieldName, antiforgeryRequestToken));
            body.AppendLine("</section>");
        }

        body.AppendLine(RenderScopeNote(options));

        return RenderLayout(artifact.Title, options, projects, view.Project.Workspace.ProjectId, body.ToString());
    }

    public static string RenderQueue(
        OperatorShellOptions options,
        IReadOnlyList<OperatorProjectSummary> projects,
        OperatorProjectSnapshot snapshot)
    {
        var body = new StringBuilder();
        body.AppendLine("<section class=\"hero compact\">");
        body.AppendLine("<p class=\"eyebrow\">Approval Queue</p>");
        body.AppendLine($"<h1>{Encode(snapshot.Workspace.Metadata.Name)}</h1>");
        body.AppendLine($"<p class=\"lede\">{Encode(snapshot.PendingItems.Count.ToString(CultureInfo.InvariantCulture))} pending review item(s) from the core queue model, ready for operator inspection.</p>");
        body.AppendLine("</section>");

        body.AppendLine(ReviewUiComponents.RenderPanel(
            "Pending Items",
            "Queue ordering comes from ApprovalQueueBuilder.",
            RenderPendingReviewItems(snapshot)));
        body.AppendLine(RenderScopeNote(options));

        return RenderLayout($"{snapshot.Workspace.Metadata.Name} queue", options, projects, snapshot.Workspace.ProjectId, body.ToString());
    }

    public static string RenderProposalReview(
        OperatorShellOptions options,
        IReadOnlyList<OperatorProjectSummary> projects,
        OperatorProjectSnapshot snapshot)
    {
        var body = new StringBuilder();
        body.AppendLine("<section class=\"hero compact\">");
        body.AppendLine("<p class=\"eyebrow\">Proposal Review</p>");
        body.AppendLine($"<h1>{Encode(snapshot.Workspace.Metadata.Name)}</h1>");
        body.AppendLine($"<p class=\"lede\">{Encode(snapshot.ProposedItems.Count.ToString(CultureInfo.InvariantCulture))} proposed artifact(s) are visible as non-canonical review inputs. Approved truth still comes only from governed lifecycle persistence.</p>");
        body.AppendLine("</section>");

        body.AppendLine(ReviewUiComponents.RenderPanel(
            "Pending Proposals",
            "Proposals are suggestions, not approved project memory.",
            RenderProposalTable(snapshot)));
        body.AppendLine(RenderScopeNote(options));

        return RenderLayout($"{snapshot.Workspace.Metadata.Name} proposals", options, projects, snapshot.Workspace.ProjectId, body.ToString());
    }

    public static string RenderTrustDashboard(
        OperatorShellOptions options,
        IReadOnlyList<OperatorProjectSummary> projects,
        OperatorTrustDashboard dashboard)
    {
        var body = new StringBuilder();
        body.AppendLine("<section class=\"hero compact\">");
        body.AppendLine("<p class=\"eyebrow\">Trust Dashboard</p>");
        body.AppendLine($"<h1>{Encode(dashboard.ProjectName)}</h1>");
        body.AppendLine($"<p class=\"lede\">Review, observe, and import-health signals for <code>{Encode(dashboard.ProjectId)}</code>. Values are derived from queue, filesystem, import, and rebuild diagnostics.</p>");
        body.AppendLine("</section>");

        body.AppendLine("<section class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\"><h2>Trust Summary</h2><p class=\"muted\">Each card links to the relevant review or diagnostic surface.</p></div>");
        body.AppendLine("<div class=\"trust-grid\">");
        foreach (var metric in dashboard.Metrics)
        {
            body.AppendLine($"<article class=\"trust-card trust-{Encode(metric.State.ToString().ToLowerInvariant())}\">");
            body.AppendLine($"<a href=\"{Encode(metric.Url)}\"><h3>{Encode(metric.Label)}</h3></a>");
            body.AppendLine($"<p class=\"trust-count\">{Encode(metric.Count.ToString(CultureInfo.InvariantCulture))}</p>");
            body.AppendLine($"<p><span class=\"badge badge-trust-{Encode(metric.State.ToString().ToLowerInvariant())}\">{Encode(FormatTrustMetricState(metric.State))}</span></p>");
            body.AppendLine($"<p class=\"muted\">{Encode(metric.Detail)}</p>");
            body.AppendLine("</article>");
        }

        body.AppendLine("</div>");
        body.AppendLine("</section>");
        body.AppendLine(RenderScopeNote(options));

        return RenderLayout($"{dashboard.ProjectName} trust", options, projects, dashboard.ProjectId, body.ToString());
    }

    private static string RenderPendingReviewItems(OperatorProjectSnapshot snapshot)
    {
        if (snapshot.PendingItems.Count == 0)
        {
            return "<p>No draft or proposed artifacts are queued.</p>";
        }

        var html = new StringBuilder();
        html.AppendLine("<div class=\"table-scroll\">");
        html.AppendLine("<table><thead><tr><th>Position</th><th>Title</th><th>Status</th><th>Type</th><th>Pending Since</th><th>Review</th></tr></thead><tbody>");
        for (var index = 0; index < snapshot.PendingItems.Count; index++)
        {
            var item = snapshot.PendingItems[index];
            var reviewLink = BuildReviewLink(snapshot.Workspace.ProjectId, item.Record.RelativePath);
            html.AppendLine("<tr>");
            html.AppendLine($"<td>{Encode((index + 1).ToString(CultureInfo.InvariantCulture))} of {Encode(snapshot.PendingItems.Count.ToString(CultureInfo.InvariantCulture))}</td>");
            html.AppendLine($"<td>{Encode(item.QueueItem.Title)}</td>");
            html.AppendLine($"<td>{RenderStatusBadge(item.QueueItem.PendingStatus)}</td>");
            html.AppendLine($"<td>{Encode(item.QueueItem.ArtifactType.ToSchemaValue())}</td>");
            html.AppendLine($"<td>{Encode(item.QueueItem.PendingSinceUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture))}</td>");
            html.AppendLine($"<td><a href=\"{reviewLink}\">Review revision</a></td>");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</tbody></table>");
        html.AppendLine("</div>");
        return html.ToString();
    }

    private static string RenderProposalTable(OperatorProjectSnapshot snapshot)
    {
        if (snapshot.ProposedItems.Count == 0)
        {
            return "<p>No proposed artifacts are waiting for review.</p>";
        }

        var html = new StringBuilder();
        html.AppendLine("<div class=\"table-scroll\">");
        html.AppendLine("<table><thead><tr><th>Title</th><th>Status</th><th>Type</th><th>Revision</th><th>Provenance</th><th>Diff Context</th></tr></thead><tbody>");
        foreach (var item in snapshot.ProposedItems)
        {
            var artifact = item.Record.Artifact;
            var reviewLink = BuildReviewLink(snapshot.Workspace.ProjectId, item.Record.RelativePath);
            html.AppendLine("<tr>");
            html.AppendLine($"<td><strong>{Encode(artifact.Title)}</strong><br><code>{Encode(artifact.Id)}</code></td>");
            html.AppendLine($"<td>{RenderStatusBadge(artifact.Status)}<br><span class=\"muted\">Non-canonical</span></td>");
            html.AppendLine($"<td>{Encode(artifact.Type.ToSchemaValue())}</td>");
            html.AppendLine($"<td>{Encode(artifact.Revision.ToString(CultureInfo.InvariantCulture))}</td>");
            html.AppendLine($"<td>{Encode(artifact.Provenance)}<br><span class=\"muted\">{Encode(artifact.Reason)}</span></td>");
            html.AppendLine($"<td><a href=\"{reviewLink}\">Inspect proposal details and diff</a></td>");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</tbody></table>");
        html.AppendLine("</div>");
        return html.ToString();
    }

    public static string RenderReview(
        OperatorShellOptions options,
        IReadOnlyList<OperatorProjectSummary> projects,
        OperatorArtifactView view,
        IReadOnlyList<string>? decisionErrors = null)
    {
        var artifact = view.SelectedArtifact.Artifact;
        var body = new StringBuilder();
        var errors = decisionErrors ?? [];

        body.AppendLine("<section class=\"hero compact\">");
        body.AppendLine("<p class=\"eyebrow\">Revision Review</p>");
        body.AppendLine($"<h1>{Encode(artifact.Title)}</h1>");
        body.AppendLine($"<p class=\"lede\">Queue review preview for <code>{Encode(view.SelectedArtifact.RelativePath)}</code>.</p>");
        if (view.ReviewQueueContext is not null)
        {
            body.AppendLine($"<p class=\"queue-position\">Review item {Encode(view.ReviewQueueContext.Position.ToString(CultureInfo.InvariantCulture))} of {Encode(view.ReviewQueueContext.TotalItems.ToString(CultureInfo.InvariantCulture))}</p>");
        }
        body.AppendLine("</section>");

        if (errors.Count > 0)
        {
            body.AppendLine("<section class=\"panel alert\">");
            body.AppendLine("<h2>Review Decision Failed</h2>");
            body.AppendLine("<ul class=\"list\">");
            foreach (var error in errors)
            {
                body.AppendLine($"<li>{Encode(error)}</li>");
            }

            body.AppendLine("</ul>");
            body.AppendLine("</section>");
        }

        if (artifact.Status == ArtifactStatus.Proposed)
        {
            body.AppendLine(ReviewUiComponents.RenderPanel(
                "Non-Canonical Proposal",
                "This proposal is review input only; it is not approved project truth.",
                "<p>Use this view to inspect metadata, provenance, sections, and diff context before a governed lifecycle action changes filesystem-backed state.</p>",
                "note"));
        }

        body.AppendLine(RenderReviewNavigation(view));

        body.AppendLine("<section class=\"two-up\">");
        body.AppendLine(ReviewUiComponents.RenderArticlePanel(
            "Pending Revision",
            "Current draft or proposed artifact under review.",
            RenderArtifactSummary(view) +
            $"<p><a class=\"button ghost\" href=\"{BuildArtifactLink(view.Project.Workspace.ProjectId, view.SelectedArtifact.RelativePath)}\">Open artifact detail</a></p>"));

        body.AppendLine(ReviewUiComponents.RenderArticlePanel(
            "Current Approved Revision",
            "Used for diff previews when one exists.",
            view.CurrentApprovedArtifact is null
                ? "<p>No approved artifact exists for this id yet, so this review is for a net-new artifact.</p>"
                : RenderApprovedSummary(view.CurrentApprovedArtifact)));
        body.AppendLine("</section>");

        body.AppendLine(RenderProvenanceReview(view.ProvenanceReview));

        body.AppendLine("<section class=\"panel\">");
        body.AppendLine("<div class=\"panel-header\"><h2>Revision Diff</h2><p class=\"muted\">Field-level changes from the core diff model.</p></div>");

        if (view.DiffIssues.Count > 0)
        {
            body.AppendLine("<ul class=\"list\">");
            foreach (var issue in view.DiffIssues)
            {
                body.AppendLine($"<li>{Encode(issue)}</li>");
            }

            body.AppendLine("</ul>");
        }
        else if (view.RevisionDiff is null || !view.RevisionDiff.HasChanges)
        {
            body.AppendLine("<p>No field-level diff is available for this review.</p>");
        }
        else
        {
            body.AppendLine(RenderRevisionDiffSummary(view.RevisionDiff));
            body.AppendLine("<div class=\"table-scroll\">");
            body.AppendLine("<table><thead><tr><th>Area</th><th>Field</th><th>Change</th><th>Before</th><th>After</th></tr></thead><tbody>");
            foreach (var change in view.RevisionDiff.Changes)
            {
                body.AppendLine("<tr>");
                body.AppendLine($"<td>{Encode(FormatChangeArea(change.Area))}</td>");
                body.AppendLine($"<td>{Encode(change.DisplayPath)}<br><code>{Encode(change.Path)}</code></td>");
                body.AppendLine($"<td>{Encode(change.Kind.ToString().ToLowerInvariant())}</td>");
                body.AppendLine($"<td>{Encode(change.BeforeValue ?? "n/a")}</td>");
                body.AppendLine($"<td>{Encode(change.AfterValue ?? "n/a")}</td>");
                body.AppendLine("</tr>");
            }

            body.AppendLine("</tbody></table>");
            body.AppendLine("</div>");
        }

        body.AppendLine("</section>");
        body.AppendLine(RenderDecisionPanel(view));
        body.AppendLine("<section class=\"panel note\">");
        body.AppendLine("<h2>Current UI boundary</h2>");
        body.AppendLine("<p>Approval and rejection actions now persist through the governed core workflow. The UI still cannot directly edit canonical truth or bypass lifecycle validation.</p>");
        body.AppendLine("</section>");
        body.AppendLine(RenderScopeNote(options));

        return RenderLayout($"{artifact.Title} review", options, projects, view.Project.Workspace.ProjectId, body.ToString());
    }

    private static string RenderProvenanceReview(OperatorProvenanceReview review)
    {
        var html = new StringBuilder();
        html.AppendLine("<section class=\"panel\">");
        html.AppendLine("<div class=\"panel-header\"><h2>Evidence Provenance</h2><p class=\"muted\">Directly observed evidence is separated from inferred candidate meaning before approval readiness.</p></div>");
        html.AppendLine(ReviewUiComponents.RenderMetadataGrid(
        [
            new("Declared provenance", review.DeclaredProvenance),
            new("Evidence requirement", review.RequiresImportedEvidence ? "required for proposal approval readiness" : "optional for this review item"),
            new("Approval readiness", review.IsApprovalReady ? "ready" : "blocked"),
            new("Readiness reason", review.ReadinessMessage)
        ]));

        if (review.MissingEvidenceIds.Count > 0)
        {
            html.AppendLine("<div class=\"body-card alert\"><h3>Missing Or Invalid Provenance</h3><ul class=\"list\">");
            foreach (var evidenceId in review.MissingEvidenceIds)
            {
                html.AppendLine($"<li><code>{Encode(evidenceId)}</code> does not resolve to imported evidence in this workspace.</li>");
            }

            html.AppendLine("</ul></div>");
        }

        if (review.Warnings.Count > 0)
        {
            html.AppendLine("<div class=\"body-card alert\"><h3>Provenance Diagnostics</h3><ul class=\"list\">");
            foreach (var warning in review.Warnings)
            {
                html.AppendLine($"<li>{Encode(warning)}</li>");
            }

            html.AppendLine("</ul></div>");
        }

        html.AppendLine("<h3>Directly Observed Evidence</h3>");
        html.AppendLine(RenderDirectEvidenceTable(review.DirectEvidence));
        html.AppendLine("<h3>Inferred Meaning And Candidate Notes</h3>");
        html.AppendLine(RenderCandidateProvenanceTable(review.CandidateNotes));
        html.AppendLine("</section>");
        return html.ToString();
    }

    private static string RenderDirectEvidenceTable(IReadOnlyList<OperatorEvidenceProvenanceItem> evidence)
    {
        if (evidence.Count == 0)
        {
            return "<p class=\"muted\">No imported evidence records resolved for this artifact.</p>";
        }

        var html = new StringBuilder();
        html.AppendLine("<div class=\"table-scroll\">");
        html.AppendLine("<table><thead><tr><th>Evidence</th><th>Source Type</th><th>URL / Path / SHA / Issue / PR</th><th>Trust</th><th>Observed</th><th>Summary</th></tr></thead><tbody>");
        foreach (var item in evidence)
        {
            html.AppendLine("<tr>");
            html.AppendLine($"<td><code>{Encode(item.StableId)}</code><br><span class=\"muted\">{Encode(item.Provenance)}</span></td>");
            html.AppendLine($"<td>{Encode(item.SourceType.ToSchemaValue())}</td>");
            html.AppendLine($"<td><code>{Encode(item.SourceReference)}</code></td>");
            html.AppendLine($"<td>{Encode(item.TrustState.ToSchemaValue())}</td>");
            html.AppendLine($"<td>{Encode(item.ObservedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture))}</td>");
            html.AppendLine($"<td><strong>{Encode(item.Title)}</strong><br><span class=\"muted\">{Encode(item.Summary)}</span></td>");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</tbody></table>");
        html.AppendLine("</div>");
        return html.ToString();
    }

    private static string RenderCandidateProvenanceTable(IReadOnlyList<OperatorCandidateProvenanceItem> candidates)
    {
        if (candidates.Count == 0)
        {
            return "<p class=\"muted\">No first-run candidate metadata is linked to this artifact.</p>";
        }

        var html = new StringBuilder();
        html.AppendLine("<div class=\"table-scroll\">");
        html.AppendLine("<table><thead><tr><th>Candidate</th><th>Kind</th><th>Source</th><th>Disposition</th><th>Confidence Notes</th><th>Extraction Reason</th><th>Evidence Ids</th></tr></thead><tbody>");
        foreach (var candidate in candidates)
        {
            html.AppendLine("<tr>");
            html.AppendLine($"<td><strong>{Encode(candidate.Title)}</strong><br><code>{Encode(candidate.CandidateId)}</code><br><span class=\"muted\">{Encode(candidate.Summary)}</span></td>");
            html.AppendLine($"<td>{Encode(FormatCandidateKind(candidate.Kind))}</td>");
            html.AppendLine($"<td>{Encode(FormatCandidateSource(candidate.Source))}</td>");
            html.AppendLine($"<td>{Encode(FormatCandidateDisposition(candidate.Disposition))}</td>");
            html.AppendLine($"<td>{Encode(candidate.Confidence.ToString("0.00", CultureInfo.InvariantCulture))}<br><span class=\"muted\">{Encode(candidate.Ambiguity)}</span></td>");
            html.AppendLine($"<td>{Encode(candidate.ExtractionReason)}</td>");
            html.AppendLine($"<td>{RenderProvenanceList(candidate.EvidenceStableIds)}</td>");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</tbody></table>");
        html.AppendLine("</div>");
        return html.ToString();
    }

    private static string RenderLayout(
        string title,
        OperatorShellOptions options,
        IReadOnlyList<OperatorProjectSummary> projects,
        string? selectedProjectId,
        string body)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"utf-8\" />");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        html.AppendLine($"<title>{Encode(title)}</title>");
        html.AppendLine("<style>");
        html.AppendLine(Styles);
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<div class=\"shell\">");
        html.AppendLine("<header class=\"topbar\">");
        html.AppendLine("<div class=\"topbar-main\">");
        html.AppendLine("<div class=\"brand-block\">");
        html.AppendLine("<a class=\"brand\" href=\"/\">Memora.Ui</a>");
        html.AppendLine("<p class=\"topbar-copy\">Human-loop operator shell for local workspace files.</p>");
        html.AppendLine("</div>");
        html.AppendLine(RenderNavigation(selectedProjectId));
        html.AppendLine("</div>");
        html.AppendLine(RenderProjectSelector(projects, selectedProjectId));
        html.AppendLine("</header>");
        html.AppendLine("<main>");
        html.AppendLine(body);
        html.AppendLine("</main>");
        html.AppendLine("<footer class=\"footer\">");
        html.AppendLine($"<span>Workspace root: <code>{Encode(options.NormalizedWorkspacesRootPath)}</code></span>");
        html.AppendLine("</footer>");
        html.AppendLine("</div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private static string RenderNavigation(string? selectedProjectId)
    {
        var html = new StringBuilder();
        html.AppendLine("<nav class=\"topnav\" aria-label=\"Primary navigation\">");
        html.AppendLine("<div class=\"nav-group\"><span>Configure</span><a href=\"/\">Home</a>");

        if (!string.IsNullOrWhiteSpace(selectedProjectId))
        {
            var projectId = Encode(selectedProjectId);
            html.AppendLine($"<a href=\"/projects/{projectId}\">Artifacts</a>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"nav-group\"><span>Review</span>");
            html.AppendLine($"<a href=\"/projects/{projectId}/queue\">Queue</a>");
            html.AppendLine($"<a href=\"/projects/{projectId}/proposals\">Proposals</a>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"nav-group\"><span>Trust</span>");
            html.AppendLine($"<a href=\"/projects/{projectId}/trust\">Trust</a>");
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"nav-group\"><span>Observe</span>");
            html.AppendLine($"<a href=\"/projects/{projectId}/first-run-import\">First Run</a>");
            html.AppendLine($"<a href=\"/context-viewer?projectId={projectId}\">Context</a>");
            html.AppendLine($"<a href=\"/understanding?projectId={projectId}\">Understanding</a>");
            html.AppendLine("</div>");
        }
        else
        {
            html.AppendLine("</div>");
            html.AppendLine("<div class=\"nav-group\"><span>Observe</span>");
            html.AppendLine("<a href=\"/context-viewer\">Context</a>");
            html.AppendLine("<a href=\"/understanding\">Understanding</a>");
            html.AppendLine("</div>");
        }

        html.AppendLine("</nav>");
        return html.ToString();
    }

    private static string RenderProjectSelector(
        IReadOnlyList<OperatorProjectSummary> projects,
        string? selectedProjectId)
    {
        var html = new StringBuilder();
        html.AppendLine("<label class=\"selector\">");
        html.AppendLine("<span class=\"selector-title\">Project Selector</span>");
        html.AppendLine($"<span class=\"selector-hint\">{Encode(string.IsNullOrWhiteSpace(selectedProjectId) ? "Choose a workspace" : "Switch workspace context")}</span>");
        html.AppendLine("<select onchange=\"if (this.value) window.location.href = this.value;\">");
        html.AppendLine("<option value=\"/\">Choose a project</option>");

        foreach (var project in projects)
        {
            var isSelected = string.Equals(project.ProjectId, selectedProjectId, StringComparison.Ordinal)
                ? " selected"
                : string.Empty;

            html.AppendLine($"<option value=\"/projects/{Encode(project.ProjectId)}\"{isSelected}>{Encode(project.Name)} ({Encode(project.ProjectId)})</option>");
        }

        html.AppendLine("</select>");
        html.AppendLine("</label>");
        return html.ToString();
    }

    private static string RenderImportModeForm(FirstRunImportStatusPage page)
    {
        var html = new StringBuilder();
        html.AppendLine($"<form method=\"get\" action=\"/projects/{Encode(page.ProjectId)}/first-run-import\" class=\"edit-form compact-form\">");
        html.AppendLine("<label><span>Selected import mode</span>");
        html.AppendLine("<select name=\"importMode\">");
        foreach (var mode in Enum.GetValues<ImportMode>())
        {
            var selected = mode == page.SelectedImportMode ? " selected" : string.Empty;
            html.AppendLine($"<option value=\"{Encode(mode.ToSchemaValue())}\"{selected}>{Encode(FormatImportMode(mode))}</option>");
        }

        html.AppendLine("</select></label>");
        html.AppendLine("<button class=\"button\" type=\"submit\">Apply mode</button>");
        html.AppendLine("</form>");
        html.AppendLine("<dl class=\"meta-grid\">");
        html.AppendLine($"<div><dt>Selected mode</dt><dd>{Encode(FormatImportMode(page.SelectedImportMode))}</dd></div>");
        html.AppendLine($"<div><dt>Selection source</dt><dd>{Encode(FormatImportModeSelection(page.ImportModeSelectionSource))}</dd></div>");
        html.AppendLine("</dl>");
        return html.ToString();
    }

    private static string RenderProgressSteps(IReadOnlyList<FirstRunProgressStep> steps)
    {
        var html = new StringBuilder();
        html.AppendLine("<ol class=\"progress-list\">");
        foreach (var step in steps)
        {
            html.AppendLine("<li>");
            html.AppendLine($"<span class=\"badge badge-progress-{Encode(step.State.ToString().ToLowerInvariant())}\">{Encode(FormatProgressState(step.State))}</span>");
            html.AppendLine($"<strong>{Encode(step.Label)}</strong>");
            html.AppendLine($"<span class=\"muted\">{Encode(step.Detail)}</span>");
            html.AppendLine("</li>");
        }

        html.AppendLine("</ol>");
        return html.ToString();
    }

    private static string RenderRepositoryAttachments(IReadOnlyList<ProjectRepositoryAttachment> attachments)
    {
        if (attachments.Count == 0)
        {
            return "<p>No repository attachment is recorded yet.</p>";
        }

        var html = new StringBuilder();
        html.AppendLine("<div class=\"table-scroll\">");
        html.AppendLine("<table><thead><tr><th>Kind</th><th>Repository Identity</th><th>Default Branch</th><th>Source</th><th>Attached</th></tr></thead><tbody>");
        foreach (var attachment in attachments.OrderBy(attachment => attachment.AttachmentId, StringComparer.Ordinal))
        {
            var source = attachment.LocalPath ?? attachment.RemoteUrl ?? attachment.OriginUrl ?? "unspecified";
            html.AppendLine("<tr>");
            html.AppendLine($"<td>{Encode(FormatAttachmentKind(attachment.Kind))}</td>");
            html.AppendLine($"<td><code>{Encode(attachment.RepositoryIdentity)}</code></td>");
            html.AppendLine($"<td>{Encode(attachment.DefaultBranch)}</td>");
            html.AppendLine($"<td><code>{Encode(source)}</code></td>");
            html.AppendLine($"<td>{Encode(attachment.AttachedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture))}</td>");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</tbody></table>");
        html.AppendLine("</div>");
        return html.ToString();
    }

    private static string RenderEvidenceCounters(FirstRunImportStatusPage page)
    {
        var html = new StringBuilder();
        html.AppendLine("<dl class=\"stat-grid\">");
        html.AppendLine($"<div><dt>Total Evidence</dt><dd>{Encode(page.EvidenceRecordCount.ToString(CultureInfo.InvariantCulture))}</dd></div>");
        html.AppendLine($"<div><dt>Baseline Evidence</dt><dd>{Encode(page.BaselineEvidenceCount.ToString(CultureInfo.InvariantCulture))}</dd></div>");
        html.AppendLine($"<div><dt>Canonical Evidence</dt><dd>{Encode(page.CanonicalEvidenceCount.ToString(CultureInfo.InvariantCulture))}</dd></div>");
        html.AppendLine($"<div><dt>Reviewable Evidence</dt><dd>{Encode(page.ReviewableEvidenceCount.ToString(CultureInfo.InvariantCulture))}</dd></div>");
        html.AppendLine("</dl>");
        return html.ToString();
    }

    private static string RenderEvidenceSummaries(IReadOnlyList<FirstRunEvidenceSummary> summaries)
    {
        if (summaries.Count == 0)
        {
            return "<p>No evidence records are stored yet.</p>";
        }

        var html = new StringBuilder();
        html.AppendLine("<div class=\"table-scroll\">");
        html.AppendLine("<table><thead><tr><th>Source Type</th><th>Trust State</th><th>Count</th></tr></thead><tbody>");
        foreach (var summary in summaries)
        {
            html.AppendLine("<tr>");
            html.AppendLine($"<td>{Encode(summary.SourceType.ToSchemaValue())}</td>");
            html.AppendLine($"<td>{Encode(summary.TrustState.ToSchemaValue())}</td>");
            html.AppendLine($"<td>{Encode(summary.Count.ToString(CultureInfo.InvariantCulture))}</td>");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</tbody></table>");
        html.AppendLine("</div>");
        return html.ToString();
    }

    private static string RenderCandidateTrustSummary(FirstRunImportStatusPage page)
    {
        var html = new StringBuilder();
        html.AppendLine("<dl class=\"stat-grid\">");
        html.AppendLine($"<div><dt>Baseline Memory</dt><dd>{Encode(page.BaselineMemoryCandidateCount.ToString(CultureInfo.InvariantCulture))}</dd></div>");
        html.AppendLine($"<div><dt>Review Needed Candidates</dt><dd>{Encode((page.ReviewRequiredCandidateCount + page.GroupedBaselineReviewCandidateCount).ToString(CultureInfo.InvariantCulture))}</dd></div>");
        html.AppendLine($"<div><dt>Evidence-Derived</dt><dd>{Encode(page.EvidenceDerivedCandidateCount.ToString(CultureInfo.InvariantCulture))}</dd></div>");
        html.AppendLine($"<div><dt>Inferred</dt><dd>{Encode(page.InferredCandidateCount.ToString(CultureInfo.InvariantCulture))}</dd></div>");
        html.AppendLine($"<div><dt>Advisory / Future Advisory</dt><dd>{Encode((page.AdvisoryCandidateCount + page.FutureAdvisoryGapCount).ToString(CultureInfo.InvariantCulture))}</dd></div>");
        html.AppendLine("</dl>");
        return html.ToString();
    }

    private static string RenderCandidateTable(IReadOnlyList<FirstRunCandidateView> candidates)
    {
        if (candidates.Count == 0)
        {
            return "<p>No candidate memory has been generated yet.</p>";
        }

        var html = new StringBuilder();
        html.AppendLine("<div class=\"table-scroll\">");
        html.AppendLine("<table><thead><tr><th>Candidate</th><th>Kind</th><th>Source</th><th>Disposition</th><th>Confidence</th><th>Ambiguity</th><th>Extraction Reason</th><th>Provenance</th></tr></thead><tbody>");
        foreach (var candidate in candidates)
        {
            html.AppendLine("<tr>");
            html.AppendLine($"<td><strong>{Encode(candidate.Title)}</strong><br><span class=\"muted\">{Encode(candidate.Summary)}</span><br><code>{Encode(candidate.CandidateId)}</code></td>");
            html.AppendLine($"<td>{Encode(FormatCandidateKind(candidate.Kind))}</td>");
            html.AppendLine($"<td>{Encode(FormatCandidateSource(candidate.Source))}</td>");
            html.AppendLine($"<td>{Encode(FormatCandidateDisposition(candidate.Disposition))}</td>");
            html.AppendLine($"<td>{Encode(candidate.Confidence.ToString("0.00", CultureInfo.InvariantCulture))}</td>");
            html.AppendLine($"<td>{Encode(candidate.Ambiguity)}</td>");
            html.AppendLine($"<td>{Encode(candidate.ExtractionReason)}</td>");
            html.AppendLine($"<td>{RenderProvenanceList(candidate.EvidenceProvenance)}</td>");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</tbody></table>");
        html.AppendLine("</div>");
        return html.ToString();
    }

    private static string RenderReadinessReport(FirstRunImportStatusPage page)
    {
        var report = page.ReadinessReport;
        if (report is null)
        {
            return "<p>No readiness report is stored yet.</p>";
        }

        var html = new StringBuilder();
        html.AppendLine("<dl class=\"meta-grid\">");
        html.AppendLine($"<div><dt>Grounded Context Ready</dt><dd>{Encode(report.ReadyForAgentUse ? "yes" : "needs review")}</dd></div>");
        html.AppendLine($"<div><dt>Generated</dt><dd>{Encode(report.GeneratedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture))}</dd></div>");
        html.AppendLine($"<div><dt>Evidence Records</dt><dd>{Encode(report.EvidenceRecordCount.ToString(CultureInfo.InvariantCulture))}</dd></div>");
        html.AppendLine($"<div><dt>Candidates</dt><dd>{Encode(report.CandidateCount.ToString(CultureInfo.InvariantCulture))}</dd></div>");
        html.AppendLine("</dl>");
        html.AppendLine(RenderReportList("Missing Context", report.MissingContext));
        html.AppendLine(RenderReportList("Missing Tests", report.MissingTests));
        html.AppendLine(RenderReportList("Risky Modules", report.RiskyModules));
        html.AppendLine(RenderReportList("Advisory / Future Advisory Gaps", report.AdvisoryDiscoveryGaps));
        return html.ToString();
    }

    private static string RenderNextActions(IReadOnlyList<FirstRunNextAction> actions)
    {
        if (actions.Count == 0)
        {
            return "<p>No next actions are recorded yet.</p>";
        }

        var html = new StringBuilder();
        html.AppendLine("<ul class=\"list next-actions\">");
        foreach (var action in actions)
        {
            html.AppendLine("<li>");
            if (string.IsNullOrWhiteSpace(action.Url))
            {
                html.AppendLine($"<strong>{Encode(action.Label)}</strong>");
            }
            else
            {
                html.AppendLine($"<a href=\"{Encode(action.Url)}\"><strong>{Encode(action.Label)}</strong></a>");
            }

            html.AppendLine($"<span class=\"muted\">{Encode(action.Detail)}</span>");
            html.AppendLine("</li>");
        }

        html.AppendLine("</ul>");
        return html.ToString();
    }

    private static string RenderReportList(string title, IReadOnlyList<string> values)
    {
        var html = new StringBuilder();
        html.AppendLine($"<h3>{Encode(title)}</h3>");
        if (values.Count == 0)
        {
            html.AppendLine("<p class=\"muted\">None recorded.</p>");
            return html.ToString();
        }

        html.AppendLine("<ul class=\"list\">");
        foreach (var value in values)
        {
            html.AppendLine($"<li>{Encode(value)}</li>");
        }

        html.AppendLine("</ul>");
        return html.ToString();
    }

    private static string RenderProvenanceList(IReadOnlyList<string> provenance)
    {
        if (provenance.Count == 0)
        {
            return "<span class=\"muted\">No evidence ids recorded.</span>";
        }

        var html = new StringBuilder();
        html.AppendLine("<ul class=\"provenance-list\">");
        foreach (var item in provenance)
        {
            html.AppendLine($"<li><code>{Encode(item)}</code></li>");
        }

        html.AppendLine("</ul>");
        return html.ToString();
    }

    private static string RenderArtifactSummary(OperatorArtifactView view)
    {
        var artifact = view.SelectedArtifact.Artifact;
        var html = new StringBuilder();
        html.AppendLine(ReviewUiComponents.RenderMetadataGrid(
        [
            new("Id", artifact.Id, TreatAsCode: true),
            new("Status", RenderStatusBadge(artifact.Status), IsHtml: true),
            new("Revision", artifact.Revision.ToString(CultureInfo.InvariantCulture)),
            new("Updated", artifact.UpdatedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)),
            new("Provenance", artifact.Provenance),
            new("Reason", artifact.Reason),
            new("Tags", string.Join(", ", artifact.Tags)),
            new("File", view.SelectedArtifact.RelativePath, TreatAsCode: true)
        ]));
        html.AppendLine($"<div class=\"body-card\"><h3>Body</h3><pre>{Encode(artifact.Body)}</pre></div>");
        return html.ToString();
    }

    private static string RenderApprovedSummary(ArtifactDocument artifact)
    {
        return ReviewUiComponents.RenderMetadataGrid(
        [
            new("Id", artifact.Id, TreatAsCode: true),
            new("Status", RenderStatusBadge(artifact.Status), IsHtml: true),
            new("Revision", artifact.Revision.ToString(CultureInfo.InvariantCulture)),
            new("Updated", artifact.UpdatedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture))
        ]);
    }

    private static string RenderSections(IReadOnlyDictionary<string, string> sections)
    {
        if (sections.Count == 0)
        {
            return "<p>No structured sections were found.</p>";
        }

        var html = new StringBuilder();
        html.AppendLine("<div class=\"section-stack\">");
        foreach (var pair in sections)
        {
            html.AppendLine("<article class=\"section-card\">");
            html.AppendLine($"<h3>{Encode(pair.Key)}</h3>");
            html.AppendLine($"<pre>{Encode(pair.Value)}</pre>");
            html.AppendLine("</article>");
        }

        html.AppendLine("</div>");
        return html.ToString();
    }

    private static string RenderEditForm(
        OperatorArtifactView view,
        string? antiforgeryFieldName,
        string? antiforgeryRequestToken)
    {
        var artifact = view.SelectedArtifact.Artifact;
        var html = new StringBuilder();
        html.AppendLine($"<form method=\"post\" action=\"/projects/{Encode(view.Project.Workspace.ProjectId)}/artifacts/edit\" class=\"edit-form\">");
        if (!string.IsNullOrWhiteSpace(antiforgeryFieldName) && !string.IsNullOrWhiteSpace(antiforgeryRequestToken))
        {
            html.AppendLine($"<input type=\"hidden\" name=\"{Encode(antiforgeryFieldName)}\" value=\"{Encode(antiforgeryRequestToken)}\" />");
        }

        html.AppendLine($"<input type=\"hidden\" name=\"path\" value=\"{Encode(view.SelectedArtifact.RelativePath)}\" />");
        html.AppendLine("<label><span>Title</span>");
        html.AppendLine($"<input type=\"text\" name=\"title\" value=\"{Encode(artifact.Title)}\" /></label>");
        html.AppendLine("<label><span>Reason</span>");
        html.AppendLine($"<input type=\"text\" name=\"reason\" value=\"{Encode(artifact.Reason)}\" /></label>");
        html.AppendLine("<label><span>Tags (comma separated)</span>");
        html.AppendLine($"<input type=\"text\" name=\"tags\" value=\"{Encode(string.Join(", ", artifact.Tags))}\" /></label>");

        foreach (var pair in artifact.Sections)
        {
            html.AppendLine($"<label><span>{Encode(pair.Key)}</span>");
            html.AppendLine($"<textarea name=\"section:{Encode(pair.Key)}\" rows=\"6\">{Encode(pair.Value)}</textarea></label>");
        }

        html.AppendLine("<button class=\"button\" type=\"submit\">Save new draft revision</button>");
        html.AppendLine("</form>");
        return html.ToString();
    }

    private static string RenderRevisionDiffSummary(Memora.Core.Revisions.ArtifactRevisionDiff diff)
    {
        var changedAreas = string.Join(
            ", ",
            diff.ChangedAreas.Select(FormatChangeArea));

        return $"<p class=\"diff-summary\">{Encode(diff.ChangeCount.ToString(CultureInfo.InvariantCulture))} field change(s) across {Encode(changedAreas)}.</p>";
    }

    private static string FormatChangeArea(Memora.Core.Revisions.ArtifactFieldChangeArea area) =>
        area switch
        {
            Memora.Core.Revisions.ArtifactFieldChangeArea.Metadata => "Metadata",
            Memora.Core.Revisions.ArtifactFieldChangeArea.Sections => "Sections",
            Memora.Core.Revisions.ArtifactFieldChangeArea.Links => "Links",
            Memora.Core.Revisions.ArtifactFieldChangeArea.TypeSpecific => "Type-specific",
            _ => area.ToString()
        };

    private static string RenderReviewNavigation(OperatorArtifactView view)
    {
        var context = view.ReviewQueueContext;
        if (context is null)
        {
            return string.Empty;
        }

        var html = new StringBuilder();
        html.AppendLine("<section class=\"review-nav panel\">");
        html.AppendLine($"<a class=\"button ghost\" href=\"/projects/{Encode(view.Project.Workspace.ProjectId)}/queue\">Back to queue</a>");

        if (context.PreviousItem is null)
        {
            html.AppendLine("<span class=\"button disabled\">Previous item</span>");
        }
        else
        {
            html.AppendLine($"<a class=\"button ghost\" href=\"{BuildReviewLink(view.Project.Workspace.ProjectId, context.PreviousItem.Record.RelativePath)}\">Previous item</a>");
        }

        if (context.NextItem is null)
        {
            html.AppendLine("<span class=\"button disabled\">Next item</span>");
        }
        else
        {
            html.AppendLine($"<a class=\"button ghost\" href=\"{BuildReviewLink(view.Project.Workspace.ProjectId, context.NextItem.Record.RelativePath)}\">Next item</a>");
        }

        html.AppendLine("</section>");
        return html.ToString();
    }

    private static string RenderDecisionPanel(OperatorArtifactView view)
    {
        var artifact = view.SelectedArtifact.Artifact;
        var html = new StringBuilder();
        html.AppendLine("<section class=\"panel decision-panel\">");
        html.AppendLine("<div class=\"panel-header\"><h2>Decision Readiness</h2><p class=\"muted\">Core workflow alignment for this pending artifact.</p></div>");
        html.AppendLine(ReviewUiComponents.RenderMetadataGrid(
        [
            new("Pending status", RenderStatusBadge(artifact.Status), IsHtml: true),
            new("Candidate revision", artifact.Revision.ToString(CultureInfo.InvariantCulture)),
            new("Approved baseline", view.CurrentApprovedArtifact is null ? "none" : "revision " + view.CurrentApprovedArtifact.Revision.ToString(CultureInfo.InvariantCulture)),
            new("Diff status", view.DiffIssues.Count > 0 ? "needs attention" : view.RevisionDiff is null ? "net new or unavailable" : "ready to inspect")
        ]));
        html.AppendLine(ReviewUiComponents.RenderActionGroup(
            BuildDecisionActions(view)));
        html.AppendLine("<p class=\"muted\">Approval and rejection submit through the existing core approval workflow before filesystem-backed state changes. Proposed artifacts remain non-canonical unless a governed transition succeeds.</p>");
        html.AppendLine("</section>");
        return html.ToString();
    }

    private static IReadOnlyList<string> BuildDecisionActions(OperatorArtifactView view)
    {
        var actions = new List<string>();
        var artifact = view.SelectedArtifact.Artifact;
        var postPath = $"/projects/{Encode(view.Project.Workspace.ProjectId)}/review/decision";
        var pathInput = $"<input type=\"hidden\" name=\"path\" value=\"{Encode(view.SelectedArtifact.RelativePath)}\" />";

        if (artifact.Status == ArtifactStatus.Draft && view.ProvenanceReview.IsApprovalReady)
        {
            actions.Add($"""
                <form method="post" action="{postPath}" class="inline-decision-form">
                {pathInput}
                <input type="hidden" name="decision" value="Approve" />
                <button class="button" type="submit">Approve</button>
                </form>
                """);
        }
        else
        {
            actions.Add("<span class=\"button disabled\">Approve</span>");
        }

        actions.Add($"""
            <form method="post" action="{postPath}" class="inline-decision-form">
            {pathInput}
            <input type="hidden" name="decision" value="Reject" />
            <button class="button danger" type="submit">Reject</button>
            </form>
            """);
        actions.Add("<a class=\"button ghost\" href=\"/projects/" + Encode(view.Project.Workspace.ProjectId) + "/queue\">Return to queue</a>");
        return actions;
    }

    private static string RenderScopeNote(OperatorShellOptions options)
    {
        var rootMode = options.UsesSeededSampleRoot
            ? "The shell is using a writable local copy of the sample workspaces so you can explore without touching the repo fixtures."
            : "The shell is using the configured workspace root directly.";

        return $"<section class=\"panel note\"><h2>Current workflow scope</h2><p>{Encode(rootMode)}</p><p>Draft inspection, editing, approval, and rejection are wired through current core and storage behavior. Canonical truth still changes only through governed approval persistence.</p></section>";
    }

    private static string RenderStatusBadge(ArtifactStatus status) =>
        ReviewUiComponents.RenderStatusBadge(status);

    private static string FormatImportMode(ImportMode mode) =>
        mode switch
        {
            ImportMode.FastBaseline => "Fast Baseline",
            ImportMode.StrictGovernance => "Strict Governance",
            ImportMode.EvidenceCanonical => "Evidence Canonical",
            ImportMode.BulkApproval => "Bulk Approval",
            _ => mode.ToString()
        };

    private static string FormatImportModeSelection(FirstRunImportModeSelectionSource source) =>
        source switch
        {
            FirstRunImportModeSelectionSource.OperatorSelected => "operator selected",
            FirstRunImportModeSelectionSource.InferredFromEvidence => "inferred from evidence",
            FirstRunImportModeSelectionSource.InferredFromReadiness => "inferred from readiness",
            FirstRunImportModeSelectionSource.Defaulted => "defaulted",
            _ => source.ToString()
        };

    private static string FormatProgressState(FirstRunProgressState state) =>
        state switch
        {
            FirstRunProgressState.Waiting => "waiting",
            FirstRunProgressState.Complete => "complete",
            FirstRunProgressState.NeedsReview => "needs review",
            FirstRunProgressState.Ready => "ready",
            _ => state.ToString()
        };

    private static string FormatAttachmentKind(RepositoryAttachmentKind kind) =>
        kind switch
        {
            RepositoryAttachmentKind.LocalGit => "Local Git",
            RepositoryAttachmentKind.GitHub => "GitHub",
            _ => kind.ToString()
        };

    private static string FormatCandidateKind(CandidateMemoryKind kind) =>
        kind switch
        {
            CandidateMemoryKind.RepoStructure => "Repo Structure",
            CandidateMemoryKind.BuildCommand => "Build Command",
            CandidateMemoryKind.TestCommand => "Test Command",
            CandidateMemoryKind.Constraint => "Constraint",
            CandidateMemoryKind.Outcome => "Outcome",
            CandidateMemoryKind.ContributionStyle => "Contribution Style",
            CandidateMemoryKind.Risk => "Risk",
            CandidateMemoryKind.OpenQuestion => "Open Question",
            _ => kind.ToString()
        };

    private static string FormatCandidateSource(CandidateMemorySource source) =>
        source switch
        {
            CandidateMemorySource.EvidenceDerived => "Evidence-Derived",
            CandidateMemorySource.Inferred => "Inferred",
            CandidateMemorySource.Advisory => "Advisory / Future Advisory",
            _ => source.ToString()
        };

    private static string FormatCandidateDisposition(CandidateMemoryDisposition disposition) =>
        disposition switch
        {
            CandidateMemoryDisposition.BaselineMemory => "Baseline Memory",
            CandidateMemoryDisposition.ReviewRequired => "Review Required",
            CandidateMemoryDisposition.GroupedBaselineReview => "Grouped Baseline Review",
            _ => disposition.ToString()
        };

    private static string FormatTrustMetricState(OperatorTrustMetricState state) =>
        state switch
        {
            OperatorTrustMetricState.Ready => "ready",
            OperatorTrustMetricState.NeedsReview => "needs review",
            OperatorTrustMetricState.Blocked => "blocked",
            _ => state.ToString()
        };

    private static string BuildArtifactLink(string projectId, string relativePath) =>
        $"/projects/{Uri.EscapeDataString(projectId)}/artifacts?path={Uri.EscapeDataString(relativePath)}";

    private static string BuildReviewLink(string projectId, string relativePath) =>
        $"/projects/{Uri.EscapeDataString(projectId)}/review?path={Uri.EscapeDataString(relativePath)}";

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private const string Styles = """
* { box-sizing: border-box; }
html, body { overflow-x: hidden; }
body { margin: 0; font-family: "Iowan Old Style", "Palatino Linotype", "Book Antiqua", Georgia, serif; background: radial-gradient(circle at top left, rgba(232, 188, 124, 0.22), transparent 30%), linear-gradient(180deg, #f5efe5 0%, #ebe0d0 100%); color: #1f1a16; }
a { color: #7d341f; }
code, pre, select, input, textarea, button { font-family: "Cascadia Code", "Consolas", monospace; }
code, pre { overflow-wrap: anywhere; word-break: break-word; }
.shell { max-width: 1180px; margin: 0 auto; padding: 24px; }
.topbar, .footer, .hero, .panel, .project-card, .section-card { backdrop-filter: blur(8px); background: rgba(255, 249, 241, 0.82); border: 1px solid rgba(91, 56, 35, 0.16); box-shadow: 0 18px 40px rgba(57, 35, 21, 0.08); }
.topbar, .footer { border-radius: 24px; padding: 18px 22px; }
.topbar { display: grid; grid-template-columns: minmax(0, 1fr) minmax(220px, 320px); gap: 20px; align-items: end; margin-bottom: 20px; }
.topbar-main { min-width: 0; display: grid; gap: 14px; }
.brand-block { min-width: 0; }
.brand { font-size: 1.4rem; font-weight: 700; text-decoration: none; }
.topbar-copy, .muted { color: #695748; overflow-wrap: anywhere; }
.topnav { display: flex; flex-wrap: wrap; gap: 10px; align-items: stretch; }
.nav-group { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; padding: 6px; border: 1px solid rgba(91, 56, 35, 0.12); border-radius: 16px; background: rgba(255, 255, 255, 0.34); }
.nav-group span { color: #8a6041; font-size: 0.72rem; text-transform: uppercase; letter-spacing: 0.08em; padding: 0 4px; }
.topnav a { display: inline-flex; align-items: center; min-height: 34px; padding: 7px 10px; border: 1px solid rgba(125, 52, 31, 0.18); border-radius: 999px; background: rgba(255, 255, 255, 0.52); color: #7d341f; text-decoration: none; }
.selector { display: grid; grid-template-columns: 1fr; gap: 4px; min-width: 0; max-width: 100%; }
.selector-title { font-size: 0.92rem; }
.selector-hint { color: #695748; font-size: 0.78rem; }
.selector select, input, textarea { width: 100%; border-radius: 14px; border: 1px solid rgba(91, 56, 35, 0.2); padding: 12px 14px; background: rgba(255, 255, 255, 0.86); }
.hero, .panel, .project-card, .section-card { border-radius: 28px; padding: 24px; }
.hero { margin-bottom: 20px; position: relative; overflow: hidden; }
.hero::after { content: ""; position: absolute; inset: auto -60px -60px auto; width: 180px; height: 180px; border-radius: 999px; background: rgba(125, 52, 31, 0.08); }
.hero.compact h1 { font-size: 2.2rem; }
.eyebrow { text-transform: uppercase; letter-spacing: 0.12em; font-size: 0.8rem; color: #8a6041; }
h1, h2, h3 { margin-top: 0; }
.lede { max-width: 70ch; font-size: 1.05rem; }
.context-note { color: #4d4037; }
.hero-actions { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 16px; }
.panel { margin-bottom: 20px; }
.panel-header { display: grid; gap: 8px; margin-bottom: 16px; }
.panel-header h2, .panel-header p { margin-bottom: 0; min-width: 0; }
.project-grid, .two-up { display: grid; gap: 20px; }
.project-grid { grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); }
.two-up { grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); }
.trust-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(230px, 1fr)); gap: 14px; }
.trust-card { border: 1px solid rgba(91, 56, 35, 0.12); border-radius: 16px; padding: 16px; background: rgba(255, 255, 255, 0.58); }
.trust-card h3 { margin-bottom: 8px; }
.trust-count { font-size: 2rem; font-weight: 700; margin: 0 0 8px; }
.list { display: grid; gap: 10px; padding-left: 18px; }
.badge { display: inline-flex; padding: 4px 10px; border-radius: 999px; font-size: 0.84rem; text-transform: uppercase; letter-spacing: 0.08em; background: #ead7b6; }
.badge-draft, .badge-proposed { background: #f0c98b; }
.badge-approved { background: #b8d1b0; }
.badge-superseded, .badge-deprecated { background: #d9c6bb; }
.badge-progress-complete, .badge-progress-ready { background: #b8d1b0; }
.badge-progress-waiting { background: #d9c6bb; }
.badge-progress-needsreview { background: #f0c98b; }
.badge-trust-ready { background: #b8d1b0; }
.badge-trust-needsreview { background: #f0c98b; }
.badge-trust-blocked { background: #e7aaa1; }
.table-scroll { max-width: 100%; overflow-x: auto; overscroll-behavior-x: contain; border-radius: 18px; }
table { width: 100%; min-width: 680px; border-collapse: collapse; }
th, td { text-align: left; vertical-align: top; padding: 12px 10px; border-bottom: 1px solid rgba(91, 56, 35, 0.12); overflow-wrap: anywhere; }
.meta-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 14px; }
.meta-grid dt { color: #8a6041; font-size: 0.85rem; text-transform: uppercase; letter-spacing: 0.08em; }
.meta-grid dd { margin: 6px 0 0; }
.stat-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 12px; margin: 0 0 16px; }
.stat-grid div { border: 1px solid rgba(91, 56, 35, 0.12); border-radius: 16px; padding: 14px; background: rgba(255, 255, 255, 0.58); }
.stat-grid dt { color: #8a6041; font-size: 0.76rem; text-transform: uppercase; letter-spacing: 0.08em; }
.stat-grid dd { margin: 6px 0 0; font-size: 1.35rem; font-weight: 700; }
.section-stack { display: grid; gap: 14px; }
.body-card, .section-card { background: rgba(255, 255, 255, 0.72); border-radius: 18px; padding: 16px; border: 1px solid rgba(91, 56, 35, 0.1); }
pre { white-space: pre-wrap; margin: 0; }
.edit-form { display: grid; gap: 16px; }
.inline-decision-form { margin: 0; }
.compact-form { margin-bottom: 16px; }
.edit-form label { display: grid; gap: 8px; }
.button { display: inline-flex; align-items: center; justify-content: center; width: fit-content; border: none; border-radius: 999px; padding: 12px 18px; background: #7d341f; color: #fff8f3; text-decoration: none; cursor: pointer; }
.button.ghost { background: transparent; color: #7d341f; border: 1px solid rgba(125, 52, 31, 0.24); }
.button.disabled { background: #d9c6bb; color: #695748; cursor: not-allowed; }
.button.danger { background: #9d3d30; color: #fff8f3; }
.review-nav, .decision-actions { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; }
.progress-list { display: grid; gap: 12px; padding-left: 20px; }
.progress-list li { padding-left: 4px; }
.progress-list strong, .next-actions strong { display: block; margin: 6px 0 2px; }
.provenance-list { display: grid; gap: 6px; margin: 0; padding-left: 16px; }
.queue-position { display: inline-flex; border: 1px solid rgba(125, 52, 31, 0.18); border-radius: 999px; padding: 8px 12px; background: rgba(255, 255, 255, 0.58); }
.decision-panel { border-color: rgba(91, 56, 35, 0.24); }
.diff-summary { font-weight: 700; }
.alert { border-color: rgba(146, 50, 40, 0.34); }
.note { background: rgba(245, 232, 210, 0.85); }
.footer { margin-top: 20px; }
@media (max-width: 720px) {
  .shell { padding: 16px; }
  .topbar { grid-template-columns: 1fr; align-items: stretch; }
  .topnav { gap: 6px; }
  .nav-group { flex: 1 1 100%; }
  .topnav a { flex: 1 1 auto; justify-content: center; }
  .hero, .panel, .project-card, .section-card { border-radius: 22px; padding: 24px; }
  .hero.compact h1 { font-size: 1.9rem; }
  .two-up { grid-template-columns: 1fr; }
}
@media (max-width: 460px) {
  .shell { padding: 12px; }
  .topbar, .footer { padding: 16px; border-radius: 20px; }
  .hero, .panel, .project-card, .section-card { padding: 22px; }
  .project-grid { grid-template-columns: 1fr; }
}
""";
}
