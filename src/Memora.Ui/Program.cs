using Memora.Hosting;
using Memora.Ui.ContextViewer;
using Memora.Ui.FirstRunImport;
using Memora.Ui.Operator;
using Memora.Ui.Rendering;
using Memora.Ui.Understanding;
using Memora.Index.Traceability;
using Microsoft.AspNetCore.Antiforgery;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(
    LoopbackBindingPolicy.ResolveRequiredUrls(
        builder.Configuration,
        "MemoraUi:Urls",
        "http://127.0.0.1:5080"));

builder.Services.AddSingleton(sp =>
    BuildShellOptions(
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<IHostEnvironment>()));
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<OperatorShellOptions>();
    return new LocalAccessTokenStore(options.NormalizedWorkspacesRootPath);
});
builder.Services.AddAntiforgery();
builder.Services.AddSingleton<LocalOperatorWorkspaceService>();
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<OperatorShellOptions>();
    return new FileSystemFirstRunImportStatusService(options.NormalizedWorkspacesRootPath);
});
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<OperatorShellOptions>();
    return new FileSystemContextViewerService(options.NormalizedWorkspacesRootPath);
});
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<OperatorShellOptions>();
    return new FileSystemUnderstandingOutputService(options.NormalizedWorkspacesRootPath);
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    var tokenStore = context.RequestServices.GetRequiredService<LocalAccessTokenStore>();

    if (AuthorizeLocalRequest(context, tokenStore, out var redirectUrl))
    {
        if (redirectUrl is not null)
        {
            context.Response.Redirect(redirectUrl);
            return;
        }

        await next();
        return;
    }

    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    context.Response.ContentType = "text/plain; charset=utf-8";
    await context.Response.WriteAsync($"Missing or invalid {LocalAccessDefaults.HeaderName} header.");
});

app.MapGet(
    "/",
    (LocalOperatorWorkspaceService service, OperatorShellOptions options) =>
    {
        var projects = service.GetProjects();
        var html = OperatorShellPageRenderer.RenderHome(options, projects);
        return Results.Content(html, "text/html");
    });

app.MapGet(
    "/projects/{projectId}",
    (string projectId, LocalOperatorWorkspaceService service, OperatorShellOptions options) =>
    {
        var snapshot = service.TryGetProject(projectId);
        if (snapshot is null)
        {
            return Results.NotFound();
        }

        var html = OperatorShellPageRenderer.RenderProject(options, service.GetProjects(), snapshot);
        return Results.Content(html, "text/html");
    });

app.MapGet(
    "/projects/{projectId}/artifacts",
    (string projectId, string? path, LocalOperatorWorkspaceService service, OperatorShellOptions options, IAntiforgery antiforgery, HttpContext httpContext) =>
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Results.Redirect($"/projects/{Uri.EscapeDataString(projectId)}");
        }

        var artifactView = service.TryGetArtifactView(projectId, path);
        if (artifactView is null)
        {
            return Results.NotFound();
        }

        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        var html = OperatorShellPageRenderer.RenderArtifact(
            options,
            service.GetProjects(),
            artifactView,
            [],
            tokens.FormFieldName,
            tokens.RequestToken);
        return Results.Content(html, "text/html");
    });

app.MapPost(
    "/projects/{projectId}/artifacts/edit",
    async (string projectId, HttpRequest request, LocalOperatorWorkspaceService service, OperatorShellOptions options, IAntiforgery antiforgery) =>
    {
        try
        {
            await antiforgery.ValidateRequestAsync(request.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Json(
                new
                {
                    error = new
                    {
                        code = "ui.csrf.invalid",
                        message = "The draft edit request is missing a valid antiforgery token."
                    }
                },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var form = await request.ReadFormAsync();
        var relativePath = form["path"].ToString();

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return Results.BadRequest();
        }

        var editInput = OperatorArtifactEditInput.FromForm(form);
        var result = service.EditDraft(projectId, relativePath, editInput);

        if (result.IsSuccess)
        {
            return Results.Redirect($"/projects/{Uri.EscapeDataString(projectId)}/artifacts?path={Uri.EscapeDataString(result.RelativePath!)}");
        }

        var artifactView = service.TryGetArtifactView(projectId, relativePath);
        if (artifactView is null)
        {
            return Results.NotFound();
        }

        var tokens = antiforgery.GetAndStoreTokens(request.HttpContext);
        var html = OperatorShellPageRenderer.RenderArtifact(
            options,
            service.GetProjects(),
            artifactView,
            result.ValidationErrors,
            tokens.FormFieldName,
            tokens.RequestToken);
        return Results.Content(html, "text/html", statusCode: StatusCodes.Status400BadRequest);
    });

app.MapGet(
    "/projects/{projectId}/queue",
    (string projectId, LocalOperatorWorkspaceService service, OperatorShellOptions options) =>
    {
        var snapshot = service.TryGetProject(projectId);
        if (snapshot is null)
        {
            return Results.NotFound();
        }

        var html = OperatorShellPageRenderer.RenderQueue(options, service.GetProjects(), snapshot);
        return Results.Content(html, "text/html");
    });

app.MapGet(
    "/projects/{projectId}/proposals",
    (string projectId, LocalOperatorWorkspaceService service, OperatorShellOptions options) =>
    {
        var snapshot = service.TryGetProject(projectId);
        if (snapshot is null)
        {
            return Results.NotFound();
        }

        var html = OperatorShellPageRenderer.RenderProposalReview(options, service.GetProjects(), snapshot);
        return Results.Content(html, "text/html");
    });

app.MapGet(
    "/projects/{projectId}/trust",
    (string projectId, LocalOperatorWorkspaceService service, OperatorShellOptions options) =>
    {
        var dashboard = service.TryBuildTrustDashboard(projectId);
        if (dashboard is null)
        {
            return Results.NotFound();
        }

        var html = OperatorShellPageRenderer.RenderTrustDashboard(options, service.GetProjects(), dashboard);
        return Results.Content(html, "text/html");
    });

app.MapGet(
    "/projects/{projectId}/first-run-import",
    (string projectId, string? importMode, FileSystemFirstRunImportStatusService importStatusService, LocalOperatorWorkspaceService service, OperatorShellOptions options) =>
    {
        var page = importStatusService.TryBuildPage(projectId, importMode);
        if (page is null)
        {
            return Results.NotFound();
        }

        var html = OperatorShellPageRenderer.RenderFirstRunImport(options, service.GetProjects(), page);
        return Results.Content(html, "text/html");
    });

app.MapGet(
    "/projects/{projectId}/review",
    (string projectId, string? path, LocalOperatorWorkspaceService service, OperatorShellOptions options) =>
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Results.Redirect($"/projects/{Uri.EscapeDataString(projectId)}/queue");
        }

        var artifactView = service.TryGetArtifactView(projectId, path);
        if (artifactView is null || !artifactView.SelectedArtifact.IsPendingReview)
        {
            return Results.NotFound();
        }

        var html = OperatorShellPageRenderer.RenderReview(options, service.GetProjects(), artifactView);
        return Results.Content(html, "text/html");
    });

app.MapPost(
    "/projects/{projectId}/review/decision",
    async (string projectId, HttpRequest request, LocalOperatorWorkspaceService service, OperatorShellOptions options) =>
    {
        var form = await request.ReadFormAsync();
        var relativePath = form["path"].ToString();
        var decisionValue = form["decision"].ToString();

        if (string.IsNullOrWhiteSpace(relativePath) ||
            !Enum.TryParse<OperatorReviewDecision>(decisionValue, ignoreCase: true, out var decision))
        {
            return Results.BadRequest();
        }

        var result = service.ApplyReviewDecision(projectId, relativePath, decision);
        if (result.IsSuccess)
        {
            return Results.Redirect($"/projects/{Uri.EscapeDataString(projectId)}/queue");
        }

        var artifactView = service.TryGetArtifactView(projectId, relativePath);
        if (result.IsNotFound || artifactView is null)
        {
            return Results.NotFound();
        }

        var html = OperatorShellPageRenderer.RenderReview(options, service.GetProjects(), artifactView, result.ValidationErrors);
        return Results.Content(html, "text/html", statusCode: StatusCodes.Status400BadRequest);
    });

app.MapGet(
    "/context-viewer",
    (HttpRequest request, FileSystemContextViewerService service) =>
    {
        var projectId = request.Query["projectId"].ToString();
        var taskDescription = request.Query["taskDescription"].ToString();
        var includeDraftArtifacts = string.Equals(request.Query["includeDraftArtifacts"], "true", StringComparison.OrdinalIgnoreCase);
        var includeLayer3History = string.Equals(request.Query["includeLayer3History"], "true", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(taskDescription))
        {
            var emptyPage = new ContextViewerPageModel(projectId, taskDescription, includeDraftArtifacts, includeLayer3History, null, null, []);
            return Results.Content(service.RenderPage(emptyPage), "text/html");
        }

        var page = service.BuildPage(new ContextViewerRequest(projectId, taskDescription, includeDraftArtifacts, includeLayer3History));
        return Results.Content(service.RenderPage(page), "text/html");
    });

app.MapGet(
    "/understanding",
    (HttpRequest request, FileSystemUnderstandingOutputService service) =>
    {
        var projectId = request.Query["projectId"].ToString();
        var taskDescription = request.Query["taskDescription"].ToString();
        var artifactId = request.Query["artifactId"].ToString();
        var includeDraftArtifacts = string.Equals(request.Query["includeDraftArtifacts"], "true", StringComparison.OrdinalIgnoreCase);
        var includeLayer3History = string.Equals(request.Query["includeLayer3History"], "true", StringComparison.OrdinalIgnoreCase);
        var traceabilityKind = Enum.TryParse<TraceabilityQueryKind>(request.Query["traceabilityKind"], ignoreCase: true, out var parsedKind)
            ? parsedKind
            : TraceabilityQueryKind.Direct;

        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(taskDescription))
        {
            var emptyPage = new UnderstandingPageModel(
                projectId,
                taskDescription,
                artifactId,
                traceabilityKind,
                includeDraftArtifacts,
                includeLayer3History,
                null,
                null,
                null,
                null);

            return Results.Content(service.RenderPage(emptyPage), "text/html");
        }

        var page = service.BuildPage(new UnderstandingRequest(
            projectId,
            taskDescription,
            artifactId,
            traceabilityKind,
            includeDraftArtifacts,
            includeLayer3History));

        return Results.Content(service.RenderPage(page), "text/html");
    });

app.Run();

static OperatorShellOptions BuildShellOptions(IConfiguration configuration, IHostEnvironment environment)
{
    var configuredWorkspacesRoot = configuration["MemoraUi:WorkspacesRoot"] ??
                                   configuration["Memora:WorkspacesRootPath"] ??
                                   Environment.GetEnvironmentVariable("MEMORA_WORKSPACES_ROOT");

    return string.IsNullOrWhiteSpace(configuredWorkspacesRoot)
        ? new OperatorShellOptions(
            SampleWorkspacesBootstrapper.PrepareDefaultRoot(environment.ContentRootPath),
            UsesSeededSampleRoot: true)
        : new OperatorShellOptions(
            Path.GetFullPath(configuredWorkspacesRoot),
            UsesSeededSampleRoot: false);
}

static bool AuthorizeLocalRequest(HttpContext context, LocalAccessTokenStore tokenStore, out string? redirectUrl)
{
    redirectUrl = null;

    var suppliedHeader = context.Request.Headers[LocalAccessDefaults.HeaderName].FirstOrDefault();
    if (tokenStore.IsValidToken(suppliedHeader))
    {
        return true;
    }

    if (context.Request.Cookies.TryGetValue(LocalAccessDefaults.CookieName, out var cookieToken) &&
        tokenStore.IsValidToken(cookieToken))
    {
        return true;
    }

    var queryToken = context.Request.Query["localToken"].FirstOrDefault();
    if (!tokenStore.IsValidToken(queryToken))
    {
        return false;
    }

    context.Response.Cookies.Append(
        LocalAccessDefaults.CookieName,
        queryToken!,
        new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            Path = "/"
        });
    redirectUrl = BuildRedirectWithoutLocalToken(context.Request);
    return true;
}

static string BuildRedirectWithoutLocalToken(HttpRequest request)
{
    var query = request.Query
        .Where(parameter => !string.Equals(parameter.Key, "localToken", StringComparison.Ordinal))
        .SelectMany(parameter => parameter.Value.Select(value => new KeyValuePair<string, string?>(parameter.Key, value)));

    return request.PathBase + request.Path + QueryString.Create(query);
}

public partial class Program;
