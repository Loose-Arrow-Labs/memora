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

        var effectiveUrls = FirstConfigured(
            (configuration[applicationUrlsConfigurationKey], applicationUrlsConfigurationKey),
            (configuration["urls"], "urls"),
            (Environment.GetEnvironmentVariable("ASPNETCORE_URLS"), "ASPNETCORE_URLS"),
            (defaultLoopbackUrl, "defaultLoopbackUrl"));

        ValidateConfigured(effectiveUrls.Urls, effectiveUrls.SourceName);
        return effectiveUrls.Urls;
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

    private static void ValidateConfigured(string configuredUrls, string sourceName)
    {
        foreach (var url in SplitUrls(configuredUrls))
        {
            if (!IsLoopbackUrl(url))
            {
                throw new InvalidOperationException(
                    $"Memora local hosts must bind only to loopback addresses. Refusing '{url}' from {sourceName}.");
            }
        }
    }

    private static (string Urls, string SourceName) FirstConfigured(params (string? Urls, string SourceName)[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate.Urls))
            {
                return (candidate.Urls.Trim(), candidate.SourceName);
            }
        }

        throw new InvalidOperationException("No loopback URL source was configured.");
    }

    private static IEnumerable<string> SplitUrls(string urls) =>
        urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
