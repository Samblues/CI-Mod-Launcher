using System.Text.RegularExpressions;

namespace CILauncher.Core;

public static partial class ManifestValidator
{
    public const int MaxNotesLength = 4000;
    public const long MaxFileBytes = 8L * 1024 * 1024 * 1024;

    public static void Validate(ModpackManifest manifest)
    {
        if (manifest.Format != 1)
            throw new ManifestException("This launcher is too old for the modpack feed. Download the latest launcher.");
        if (string.IsNullOrWhiteSpace(manifest.Version) || manifest.Version.Length > 64 || manifest.Version.Any(char.IsWhiteSpace))
            throw new ManifestException("Modpack version is missing or invalid.");
        if (manifest.Notes != null && manifest.Notes.Length > MaxNotesLength)
            throw new ManifestException("Patch notes are too long.");
        if (!string.IsNullOrWhiteSpace(manifest.GameVersion) && !GameVersionPattern.IsMatch(manifest.GameVersion))
            throw new ManifestException("Game version must look like v1.2.12.");
        if (manifest.Launch == null)
            throw new ManifestException("Modpack feed is missing launch settings.");
        ValidateLaunch(manifest.Launch);
        if (manifest.Files == null || manifest.Files.Count == 0)
            throw new ManifestException("Modpack feed has no files.");

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sizesByHash = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            file.Path = SafePaths.NormalizeModulePath(file.Path);
            if (!paths.Add(file.Path))
                throw new ManifestException($"Duplicate modpack path: {file.Path}");
            if (file.Size < 0 || file.Size > MaxFileBytes)
                throw new ManifestException($"Unexpected size for {file.Path}.");

            file.Sha256 = (file.Sha256 ?? "").Trim().ToLowerInvariant();
            if (!Sha256Pattern.IsMatch(file.Sha256))
                throw new ManifestException($"Missing hash for {file.Path}.");
            if (sizesByHash.TryGetValue(file.Sha256, out var size) && size != file.Size)
                throw new ManifestException("The same hash is listed with two sizes.");
            sizesByHash[file.Sha256] = file.Size;

            if (string.IsNullOrWhiteSpace(file.Url) || file.Url.Contains("..", StringComparison.Ordinal))
                throw new ManifestException($"Download URL is not allowed for {file.Path}.");
            if (Uri.TryCreate(file.Url, UriKind.Absolute, out var absolute))
                FeedAddress.EnsureAllowed(absolute);
            else if (file.Url.Contains("://", StringComparison.Ordinal) || file.Url.StartsWith('/') || file.Url.StartsWith('\\'))
                throw new ManifestException($"Download URL is not allowed for {file.Path}.");
        }
    }

    public static void ValidateLaunch(LaunchSpec launch)
    {
        var executable = (launch.Executable ?? "").Replace('\\', '/').Trim();
        if (!executable.Equals(GameLauncher.ClientExecutable, StringComparison.OrdinalIgnoreCase))
            throw new ManifestException("The feed is only allowed to start Bannerlord.exe.");
        launch.Executable = GameLauncher.ClientExecutable;

        var args = (launch.Arguments ?? "").Trim();
        if (!LaunchArgumentsPattern.IsMatch(args))
            throw new ManifestException("Launch arguments must be a multiplayer module list.");
        launch.Arguments = args;
    }

    private static readonly Regex Sha256Pattern = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GameVersionPattern = new(@"^v\d+(\.\d+){1,3}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LaunchArgumentsPattern = new(@"^/multiplayer _MODULES_\*(?:[A-Za-z0-9_.-]+\*)+_MODULES_$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
}
