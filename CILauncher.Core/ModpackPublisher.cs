using System.Xml.Linq;

namespace CILauncher.Core;

public sealed class PublishRequest
{
    /// <summary>Optional check. The published version is always the CI_EVENT SubModule.xml Version.</summary>
    public string? Version { get; init; }
    public string? GameVersion { get; init; }
    public string? Notes { get; init; }
    public string? LaunchArguments { get; init; }
    public required string OutputDirectory { get; init; }
    public required IReadOnlyList<ModuleMap> Maps { get; init; }
}

public sealed class ModuleMap
{
    public required string Destination { get; init; }
    public required string SourceDirectory { get; init; }
}

public sealed class StagedFile
{
    public required ModpackFile File { get; init; }
    public required string SourcePath { get; init; }
}

public sealed class StagedModpack
{
    public required ModpackManifest Manifest { get; init; }
    public required IReadOnlyList<StagedFile> Files { get; init; }
}

public static class ModpackPublisher
{
    public static async Task<ModpackManifest> PublishAsync(PublishRequest request, CancellationToken cancellationToken = default)
    {
        var staged = await StageAsync(request, cancellationToken).ConfigureAwait(false);
        var output = Path.GetFullPath(request.OutputDirectory);
        var blobDirectory = Path.Combine(output, "blobs");
        Directory.CreateDirectory(blobDirectory);

        foreach (var stagedFile in staged.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var blob = Path.Combine(blobDirectory, stagedFile.File.Sha256);
            var info = new FileInfo(stagedFile.SourcePath);
            if (!File.Exists(blob) || new FileInfo(blob).Length != info.Length)
            {
                if (File.Exists(blob))
                    File.Delete(blob);
                File.Copy(stagedFile.SourcePath, blob, overwrite: false);
            }
        }

        var json = ManifestJson.Write(staged.Manifest);
        await IoCompat.WriteTextAsync(Path.Combine(output, "manifest.json"), json, cancellationToken).ConfigureAwait(false);

        var keep = staged.Files.Select(file => file.File.Sha256).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var blob in Directory.EnumerateFiles(blobDirectory))
        {
            if (!keep.Contains(Path.GetFileName(blob)))
                File.Delete(blob);
        }

        return staged.Manifest;
    }

    public static async Task<StagedModpack> StageAsync(PublishRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Maps.Count == 0)
            throw new ArgumentException("Map at least one module folder.");

        var files = new List<StagedFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? modpackVersion = null;

        foreach (var map in request.Maps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(map.SourceDirectory))
                throw new DirectoryNotFoundException($"Module folder not found: {map.SourceDirectory}");

            var destinationRoot = SafePaths.NormalizeModuleDirectory(map.Destination);
            var identity = ReadModuleIdentity(map.SourceDirectory);
            if (identity.Id != null && identity.Id.Equals(GameLocator.ModpackModuleId, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(identity.Version))
                    throw new InvalidOperationException("CI Modpack SubModule.xml has no Version. Set <Version value=\"v1.9.0.1\"/> on the module whose Id is CI_EVENT.");
                if (modpackVersion != null && !modpackVersion.Equals(identity.Version, StringComparison.Ordinal))
                    throw new InvalidOperationException("Two CI_EVENT folders declare different Version values.");
                modpackVersion = identity.Version;
            }

            foreach (var absolute in IoCompat.Walk(map.SourceDirectory, ignoreInaccessible: false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ShouldSkip(absolute))
                    continue;

                var underModule = IoCompat.Relative(map.SourceDirectory, absolute).Replace('\\', '/');
                var relative = SafePaths.NormalizeModulePath(destinationRoot + "/" + underModule);
                if (!seen.Add(relative))
                    throw new InvalidOperationException($"Duplicate path {relative}.");

                var info = new FileInfo(absolute);
                if (info.Length > ManifestValidator.MaxFileBytes)
                    throw new InvalidOperationException($"{relative} is larger than 8 GB.");

                var hash = await Hashing.Sha256FileAsync(absolute, cancellationToken).ConfigureAwait(false);
                files.Add(new StagedFile
                {
                    SourcePath = absolute,
                    File = new ModpackFile
                    {
                        Path = relative,
                        Size = info.Length,
                        Sha256 = hash,
                        Url = "blobs/" + hash
                    }
                });
            }
        }

        if (files.Count == 0)
            throw new InvalidOperationException("The mapped folders had no files to publish.");
        if (string.IsNullOrWhiteSpace(modpackVersion))
            throw new InvalidOperationException("The publish must include the CI Modpack module, the folder whose SubModule.xml Id is CI_EVENT.");
        if (!string.IsNullOrWhiteSpace(request.Version)
            && !request.Version.Trim().Equals(modpackVersion, StringComparison.Ordinal))
            throw new InvalidOperationException($"SubModule.xml says {modpackVersion}, but --version is {request.Version.Trim()}. Bump the Version in that file and publish it.");

        var arguments = string.IsNullOrWhiteSpace(request.LaunchArguments)
            ? BuildLaunchArguments(new[] { GameLocator.ModpackModuleId })
            : request.LaunchArguments.Trim();

        var manifest = new ModpackManifest
        {
            Format = 1,
            Version = modpackVersion,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            PublishedAt = DateTimeOffset.UtcNow,
            Launch = new LaunchSpec { Arguments = arguments },
            Files = files.Select(file => file.File).OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase).ToList()
        };

        return new StagedModpack { Manifest = manifest, Files = files };
    }

    public static string BuildLaunchArguments(IEnumerable<string> moduleIds)
    {
        var packed = new List<string>();
        foreach (var moduleId in moduleIds)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                continue;
            if (packed.Any(existing => existing.Equals(moduleId, StringComparison.OrdinalIgnoreCase)))
                continue;
            packed.Add(moduleId.Trim());
        }

        var order = new List<string> { "Native" };
        if (packed.Any(id => id.Equals("Bannerlord.Harmony", StringComparison.OrdinalIgnoreCase)))
            order.Add("Bannerlord.Harmony");
        order.Add("Multiplayer");
        foreach (var id in packed)
        {
            if (id.Equals("Bannerlord.Harmony", StringComparison.OrdinalIgnoreCase)
                || id.Equals("Native", StringComparison.OrdinalIgnoreCase)
                || id.Equals("Multiplayer", StringComparison.OrdinalIgnoreCase))
                continue;
            order.Add(id);
        }

        return "/multiplayer _MODULES_*" + string.Join("*", order) + "*_MODULES_";
    }

    public static (string? Id, string? Version) ReadModuleIdentity(string moduleDirectory)
    {
        var xmlPath = Path.Combine(moduleDirectory, "SubModule.xml");
        if (!File.Exists(xmlPath))
            return (null, null);
        var document = XDocument.Load(xmlPath);
        var id = document.Root?.Element("Id")?.Attribute("value")?.Value?.Trim();
        var version = document.Root?.Element("Version")?.Attribute("value")?.Value?.Trim();
        return (string.IsNullOrWhiteSpace(id) ? null : id, string.IsNullOrWhiteSpace(version) ? null : version);
    }

    private static bool ShouldSkip(string absolute)
    {
        var name = Path.GetFileName(absolute);
        if (name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase)
            || name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase))
            return true;
        if (name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            return true;

        var relative = absolute.Replace('\\', '/');
        if (ModpackLauncher.IsBundledPath(relative))
            return true;
        return relative.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
               || relative.Contains("/.git/", StringComparison.OrdinalIgnoreCase)
               || relative.Contains("/.vs/", StringComparison.OrdinalIgnoreCase);
    }
}
