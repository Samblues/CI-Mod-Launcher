using System.Net.Http.Headers;
using System.Text;

namespace CILauncher.Core;

public sealed class ManifestClient
{
    public const int MaxManifestBytes = 8 * 1024 * 1024;

    private readonly HttpClient _http;

    public ManifestClient(HttpClient http) => _http = http;

    public async Task<ModpackManifest> GetAsync(Uri uri, CancellationToken cancellationToken)
    {
        FeedAddress.EnsureAllowed(uri);
        var json = uri.IsFile
            ? await IoCompat.ReadTextAsync(uri.LocalPath, cancellationToken).ConfigureAwait(false)
            : await DownloadStringAsync(uri, cancellationToken).ConfigureAwait(false);
        if (Encoding.UTF8.GetByteCount(json) > MaxManifestBytes)
            throw new ManifestException("Manifest is too large.");
        return ManifestJson.Parse(json);
    }

    private async Task<string> DownloadStringAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new ManifestException($"Feed returned {(int)response.StatusCode}.");
        var declared = response.Content.Headers.ContentLength;
        if (declared > MaxManifestBytes)
            throw new ManifestException("Manifest is too large.");

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        var total = 0;
        int read;
        while ((read = await stream.ReadAsync(chunk, 0, chunk.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > MaxManifestBytes)
                throw new ManifestException("Manifest is too large.");
            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
