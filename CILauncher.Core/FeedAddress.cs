namespace CILauncher.Core;

public static class FeedAddress
{
    public static string? Resolve(string? configured)
        => string.IsNullOrWhiteSpace(configured) ? null : configured.Trim();

    public static Uri Parse(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            throw new ManifestException("Feed URL is not a valid absolute URL.");
        EnsureAllowed(uri);
        return uri;
    }

    public static void EnsureAllowed(Uri uri)
    {
        if (uri.IsFile)
            return;
        if (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return;
        if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && IsLoopback(uri))
            return;
        throw new ManifestException("The modpack feed must use HTTPS.");
    }

    public static bool IsLoopback(Uri uri)
        => uri.IsLoopback
           || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
           || uri.Host == "127.0.0.1"
           || uri.Host == "::1";

    public static Uri ResolveBlob(Uri manifestUri, string url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Contains("..", StringComparison.Ordinal))
            throw new ManifestException("A modpack file URL is not allowed.");

        Uri blob = Uri.TryCreate(url, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(manifestUri, url);
        EnsureAllowed(blob);
        return blob;
    }
}
