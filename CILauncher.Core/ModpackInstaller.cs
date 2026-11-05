using System.Buffers;
using System.Net;
using System.Net.Http.Headers;

namespace CILauncher.Core;

public sealed class ModpackInstaller
{
    private readonly HttpClient _http;

    public ModpackInstaller(HttpClient http) => _http = http;

    public async Task<InstallResult> InstallAsync(
        ModpackManifest manifest,
        Uri manifestUri,
        InstallTarget target,
        int concurrency,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var record = InstallRecordStore.TryLoad(target.Root);
        var plan = await InstallPlanner.BuildAsync(manifest, target, record, progress, cancellationToken).ConfigureAwait(false);
        var totalBytes = plan.Download.Sum(file => file.Size);
        EnsureDiskSpace(target.Root, plan);

        long completedBytes = 0;
        long networkBytes = 0;
        var completedFiles = 0;
        var started = System.Diagnostics.Stopwatch.StartNew();
        var gate = new SemaphoreSlim(IoCompat.Clamp(concurrency, 1, 16));
        var sync = new object();
        long lastReport = 0;
        Exception? firstError = null;
        using var failure = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        void Report(string detail, bool force)
        {
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!force && now - lastReport < System.Diagnostics.Stopwatch.Frequency / 10)
                return;
            lastReport = now;
            var elapsed = started.Elapsed.TotalSeconds;
            var rate = elapsed > 0.25 ? networkBytes / elapsed : 0;
            progress?.Report(new InstallProgress(
                InstallPhase.Downloading,
                detail,
                completedBytes,
                totalBytes,
                completedFiles,
                plan.Download.Count,
                rate));
        }

        async Task DownloadOne(ModpackFile file)
        {
            await gate.WaitAsync(failure.Token).ConfigureAwait(false);
            try
            {
                failure.Token.ThrowIfCancellationRequested();
                var destination = target.Absolute(file.Path);
                var partial = destination + ".ci-partial";
                await DownloadPartialAsync(
                    file,
                    manifestUri,
                    partial,
                    (bytes, network) =>
                    {
                        lock (sync)
                        {
                            completedBytes += bytes;
                            if (network)
                                networkBytes += bytes;
                            Report(file.Path, force: false);
                        }
                    },
                    failure.Token).ConfigureAwait(false);

                lock (sync)
                {
                    completedFiles++;
                    Report(file.Path, force: true);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lock (sync)
                    firstError ??= ex;
                await failure.CancelAsync().ConfigureAwait(false);
                throw;
            }
            finally
            {
                gate.Release();
            }
        }

        try
        {
            await Task.WhenAll(plan.Download.Select(DownloadOne)).ConfigureAwait(false);
        }
        catch (Exception) when (firstError != null)
        {
            throw firstError;
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new InstallProgress(InstallPhase.Installing, "Copying into the game folder", totalBytes, totalBytes, 0, plan.Download.Count, 0));

        var placed = 0;
        foreach (var file in plan.Download)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destination = target.Absolute(file.Path);
            var partial = destination + ".ci-partial";
            if (!File.Exists(partial))
                throw new IOException($"Download for {file.Path} did not finish.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await InstalledFile.ReplaceAsync(partial, destination, cancellationToken).ConfigureAwait(false);
            placed++;
            progress?.Report(new InstallProgress(InstallPhase.Installing, file.Path, totalBytes, totalBytes, placed, plan.Download.Count, 0));
        }

        progress?.Report(new InstallProgress(InstallPhase.Cleaning, "Removing old files", 0, 0, 0, plan.Remove.Count, 0));
        var removed = 0;
        foreach (var relative in plan.Remove)
        {
            var absolute = SafePaths.CombineUnderRoot(target.Root, relative);
            if (File.Exists(absolute))
            {
                InstalledFile.Delete(absolute);
                removed++;
            }
        }

        foreach (var directory in target.ManagedDirectories(manifest))
        {
            if (!Directory.Exists(directory))
                continue;
            foreach (var partial in Directory.EnumerateFiles(directory, "*.ci-partial", SearchOption.AllDirectories))
                InstalledFile.Delete(partial);
            DeleteEmptyDirectories(directory);
        }

        return new InstallResult(plan.Download.Count, networkBytes, plan.Reuse.Count, removed);
    }

    public async Task<string?> VerifyAsync(
        ModpackManifest manifest,
        InstallTarget target,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var index = 0;
        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;
            progress?.Report(new InstallProgress(InstallPhase.Scanning, file.Path, 0, 0, index, manifest.Files.Count, 0));
            var destination = target.Absolute(file.Path);
            if (!File.Exists(destination) || new FileInfo(destination).Length != file.Size)
                return file.Path;
            var hash = await Hashing.Sha256FileAsync(destination, cancellationToken).ConfigureAwait(false);
            if (!hash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                return file.Path;
        }

        return null;
    }

    private async Task DownloadPartialAsync(
        ModpackFile file,
        Uri manifestUri,
        string partial,
        Action<long, bool> onBytes,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(partial)!);
        long existing = 0;
        if (File.Exists(partial))
        {
            existing = new FileInfo(partial).Length;
            if (existing > file.Size)
            {
                File.Delete(partial);
                existing = 0;
            }
            else if (existing == file.Size)
            {
                var hash = await Hashing.Sha256FileAsync(partial, cancellationToken).ConfigureAwait(false);
                if (hash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    onBytes(file.Size, false);
                    return;
                }

                File.Delete(partial);
                existing = 0;
            }
        }

        var blob = FeedAddress.ResolveBlob(manifestUri, file.Url);
        if (blob.IsFile)
        {
            if (existing > 0)
                File.Delete(partial);

            using var input = new FileStream(blob.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await WriteVerifiedAsync(file, partial, input, 0, bytes => onBytes(bytes, false), cancellationToken).ConfigureAwait(false);
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, blob);
        if (existing > 0)
            request.Headers.Range = new RangeHeaderValue(existing, null);

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        var resume = existing > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        if (existing > 0 && response.StatusCode == HttpStatusCode.OK)
        {
            existing = 0;
        }
        else if (existing > 0 && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            File.Delete(partial);
            await DownloadPartialAsync(file, manifestUri, partial, onBytes, cancellationToken).ConfigureAwait(false);
            return;
        }
        else if (!response.IsSuccessStatusCode)
        {
            throw new IOException($"Download failed for {file.Path} ({(int)response.StatusCode}).");
        }

        if (resume)
            onBytes(existing, false);

        using var network = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await WriteVerifiedAsync(file, partial, network, resume ? existing : 0, bytes => onBytes(bytes, true), cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteVerifiedAsync(
        ModpackFile file,
        string partial,
        Stream input,
        long alreadyOnDisk,
        Action<long> onChunk,
        CancellationToken cancellationToken)
    {
        var mode = alreadyOnDisk > 0 ? FileMode.Append : FileMode.Create;
        var written = alreadyOnDisk;
        try
        {
            using var output = new FileStream(partial, mode, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.Asynchronous);
            var buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);
            try
            {
                int read;
                while ((read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    written += read;
                    if (written > file.Size)
                        throw new IOException($"Download for {file.Path} was larger than the feed declared.");

                    await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                    onChunk(read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch
        {
            if (File.Exists(partial))
                File.Delete(partial);
            throw;
        }

        if (written != file.Size)
        {
            File.Delete(partial);
            throw new IOException($"Download for {file.Path} ended early.");
        }

        var hash = await Hashing.Sha256FileAsync(partial, cancellationToken).ConfigureAwait(false);
        if (!hash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(partial);
            throw new IOException($"Download for {file.Path} failed verification. It will be fetched again.");
        }
    }

    private static void EnsureDiskSpace(string gamePath, InstallPlan plan)
    {
        var needed = plan.Download.Sum(file => file.Size);
        if (needed <= 0)
            return;
        var root = Path.GetPathRoot(Path.GetFullPath(gamePath));
        if (string.IsNullOrEmpty(root))
            return;
        var drive = new DriveInfo(root);
        if (drive.AvailableFreeSpace < needed + (64L * 1024 * 1024))
        {
            var megabytes = (needed + (64L * 1024 * 1024)) / (1024 * 1024);
            throw new IOException($"Not enough free space on {root}. Need about {megabytes} MB.");
        }
    }

    private static void DeleteEmptyDirectories(string root)
    {
        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);
        }
    }
}
