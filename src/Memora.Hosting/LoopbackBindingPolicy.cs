using System.Net;
using Microsoft.Extensions.Configuration;

namespace Memora.Hosting;

public static class LoopbackBindingPolicy
{
    public static string ResolveRequiredUrls(
        IConfiguration configuration,
        string applicationUrlsConfigurationKey,
        string defaultLoopbackUrl)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationUrlsConfigurationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultLoopbackUrl);

        var configuredApplicationUrls = configuration[applicationUrlsConfigurationKey];
        var configuredHostUrls = configuration["urls"];
        var aspNetCoreUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");

        ValidateIfConfigured(configuredApplicationUrls, applicationUrlsConfigurationKey);
        ValidateIfConfigured(configuredHostUrls, "urls");
        ValidateIfConfigured(aspNetCoreUrls, "ASPNETCORE_URLS");

        return FirstConfigured(configuredApplicationUrls, configuredHostUrls, aspNetCoreUrls) ?? defaultLoopbackUrl;
    }

    public static bool IsLoopbackUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address);
    }

    private static void ValidateIfConfigured(string? configuredUrls, string sourceName)
    {
        if (string.IsNullOrWhiteSpace(configuredUrls))
        {
            return;
        }

        foreach (var url in SplitUrls(configuredUrls))
        {
            if (!IsLoopbackUrl(url))
            {
                throw new InvalidOperationException(
                    $"Memora local hosts must bind only to loopback addresses. Refusing '{url}' from {sourceName}.");
            }
        }
    }

    private static string? FirstConfigured(params string?[] candidates) =>
        candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate))?.Trim();

    private static IEnumerable<string> SplitUrls(string urls) =>
        urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
