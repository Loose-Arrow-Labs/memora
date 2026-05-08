using Memora.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Memora.Ui.Tests;

internal static class LocalAuthTestClient
{
    public static HttpClient CreateAuthorizedClient(
        WebApplicationFactory<Program> factory,
        WebApplicationFactoryClientOptions? options = null)
    {
        var client = options is null ? factory.CreateClient() : factory.CreateClient(options);
        var token = factory.Services.GetRequiredService<LocalAccessTokenStore>().GetOrCreateToken();
        client.DefaultRequestHeaders.Add(LocalAccessDefaults.HeaderName, token);
        return client;
    }
}
