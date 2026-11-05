namespace CILauncher.Core;

public enum InstallPhase
{
    Scanning,
    Downloading,
    Installing,
    Cleaning
}

public sealed record InstallProgress(
    InstallPhase Phase,
    string Detail,
    long CompletedBytes,
    long TotalBytes,
    int CompletedFiles,
    int TotalFiles,
    double BytesPerSecond)
{
    public double Fraction
    {
        get
        {
            double value = Phase == InstallPhase.Downloading && TotalBytes > 0
                ? (double)CompletedBytes / TotalBytes
                : TotalFiles > 0 ? (double)CompletedFiles / TotalFiles : 0;
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }
    }
}

public sealed record InstallPlan(
    IReadOnlyList<ModpackFile> Download,
    IReadOnlyList<ModpackFile> Reuse,
    IReadOnlyList<string> Remove);

public sealed record InstallResult(int DownloadedFiles, long DownloadedBytes, int ReusedFiles, int RemovedFiles);

public static class InstallPlanner
{
    public static async Task<InstallPlan> BuildAsync(
        ModpackManifest manifest,
        InstallTarget target,
        InstallRecord? record,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        _ = record;
        var reuse = new List<ModpackFile>();
        var download = new List<ModpackFile>();
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;

        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;
            progress?.Report(new InstallProgress(InstallPhase.Scanning, file.Path, 0, 0, index, manifest.Files.Count, 0));
            var placed = target.Map(file.Path);
            known.Add(placed);

            var destination = SafePaths.CombineUnderRoot(target.Root, placed);
            if (File.Exists(destination) && new FileInfo(destination).Length == file.Size)
            {
                var hash = await Hashing.Sha256FileAsync(destination, cancellationToken).ConfigureAwait(false);
                if (hash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    reuse.Add(file);
                    continue;
                }
            }

            download.Add(file);
        }

        var remove = new List<string>();
        foreach (var directory in target.ManagedDirectories(manifest))
        {
            if (!Directory.Exists(directory))
                continue;
            foreach (var absolute in IoCompat.Walk(directory, ignoreInaccessible: true))
            {
                var name = Path.GetFileName(absolute);
                if (name.EndsWith(".ci-partial", StringComparison.OrdinalIgnoreCase))
                    continue;
                var relative = IoCompat.Relative(target.Root, absolute).Replace('\\', '/');
                if (ModpackLauncher.IsBundledPath(relative) || known.Contains(relative))
                    continue;
                remove.Add(relative);
            }
        }

        return new InstallPlan(download, reuse, remove);
    }
}
