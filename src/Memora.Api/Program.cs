using System.Text.Json;
using Memora.Api;
using Memora.Api.Services;
using Memora.Core.AgentInteraction;
using Memora.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(
    LoopbackBindingPolicy.ResolveRequiredUrls(
        builder.Configuration,
        "Memora:Api:Urls",
        "http://127.0.0.1:5081"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Pin web JSON defaults so the OpenAPI body and `ProjectStateViewSerializer.Serialize`
// (used on the MCP path) stay byte-identical. Drift here silently desyncs runtime surfaces.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
    options.SerializerOptions.WriteIndented = false;
});

var workspacesRootPath = builder.Configuration["Memora:WorkspacesRootPath"] ??
                         Environment.GetEnvironmentVariable("MEMORA_WORKSPACES_ROOT");
var localAccessRootPath = builder.Configuration["Memora:LocalAccessRootPath"] ??
                          workspacesRootPath ??
                          Path.Combine(Path.GetTempPath(), "memora-local-access", "api");

builder.Services.AddSingleton(new LocalAccessTokenStore(localAccessRootPath));

if (string.IsNullOrWhiteSpace(workspacesRootPath))
{
    builder.Services.AddSingleton<UnavailableAgentInteractionService>();
    builder.Services.AddSingleton<IAgentInteractionService>(serviceProvider =>
        serviceProvider.GetRequiredService<UnavailableAgentInteractionService>());
    builder.Services.AddSingleton<IReviewInboxService>(serviceProvider =>
        serviceProvider.GetRequiredService<UnavailableAgentInteractionService>());
}
else
{
    builder.Services.AddSingleton(_ =>
        new FileSystemAgentInteractionService(workspacesRootPath));
    builder.Services.AddSingleton<IAgentInteractionService>(serviceProvider =>
        serviceProvider.GetRequiredService<FileSystemAgentInteractionService>());
    builder.Services.AddSingleton<IReviewInboxService>(serviceProvider =>
        serviceProvider.GetRequiredService<FileSystemAgentInteractionService>());
}

var app = builder.Build();

app.Use(async (context, next) =>
{
    var tokenStore = context.RequestServices.GetRequiredService<LocalAccessTokenStore>();
    var suppliedToken = context.Request.Headers[LocalAccessDefaults.HeaderName].FirstOrDefault();

    if (!tokenStore.IsValidToken(suppliedToken))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new
        {
            error = new
            {
                code = "local_auth.required",
                message = $"Missing or invalid {LocalAccessDefaults.HeaderName} header."
            }
        });
        return;
    }

    await next();
});

app.MapOpenApi("/openapi.json");

app.MapGet(
    "/api/projects/{projectId}",
    (string projectId, IAgentInteractionService service) =>
        AgentInteractionHttpResults.FromProjectResponse(service.GetProject(projectId)));

app.MapPost(
    "/api/context",
    (GetContextRequest request, IAgentInteractionService service) =>
        AgentInteractionHttpResults.FromContextResponse(service.GetContext(request)));

app.MapPost(
    "/api/artifacts/proposals",
    (ProposeArtifactRequest request, IAgentInteractionService service) =>
        AgentInteractionHttpResults.FromProposalResponse(service.ProposeArtifact(request)));

app.MapPost(
    "/api/artifacts/updates",
    (ProposeUpdateRequest request, IAgentInteractionService service) =>
        AgentInteractionHttpResults.FromProposalResponse(service.ProposeUpdate(request)));

app.MapPost(
    "/api/outcomes",
    (RecordOutcomeRequest request, IAgentInteractionService service) =>
        AgentInteractionHttpResults.FromOutcomeResponse(service.RecordOutcome(request)));

app.MapGet(
    "/api/projects/{projectId}/review/inbox",
    (string projectId, IReviewInboxService service) =>
        AgentInteractionHttpResults.FromReviewInboxResponse(service.GetReviewInbox(projectId)));

app.MapGet(
    "/api/projects/{projectId}/review/preview",
    (string projectId, string path, IReviewInboxService service) =>
        AgentInteractionHttpResults.FromReviewArtifactPreviewResponse(service.GetReviewArtifactPreview(projectId, path)));

app.MapPost(
    "/api/projects/{projectId}/review/decisions",
    (string projectId, ReviewDecisionRequest request, IReviewInboxService service) =>
        AgentInteractionHttpResults.FromReviewDecisionResponse(service.ApplyReviewDecision(projectId, request)));

app.Run();

public partial class Program;
