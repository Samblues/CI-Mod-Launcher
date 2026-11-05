namespace CILauncher.Core;

/// <summary>
/// Where the modpack files are written. The feed always lists them as Modules/CI_EVENT/...
/// A Steam Workshop item stores those same files at the root of its folder.
/// </summary>
public sealed class InstallTarget
{
    public required string Root { get; init; }
    public bool Workshop { get; init; }

    public static InstallTarget Game(string gamePath)
        => new() { Root = Path.GetFullPath(gamePath), Workshop = false };

    public static InstallTarget WorkshopItem(string itemDirectory)
        => new() { Root = Path.GetFullPath(itemDirectory), Workshop = true };

    public string Map(string manifestPath)
    {
        var path = SafePaths.NormalizeModulePath(manifestPath);
        if (!Workshop)
            return path;

        var prefix = "Modules/" + GameLocator.ModpackModuleId + "/";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The workshop folder only receives CI Modpack files, not {path}.");
        return path[prefix.Length..];
    }

    public string Absolute(string manifestPath)
        => SafePaths.CombineUnderRoot(Root, Map(manifestPath));

    public IReadOnlyList<string> ManagedDirectories(ModpackManifest manifest)
    {
        if (Workshop)
            return new[] { Root };

        return manifest.Files
            .Select(file => SafePaths.ModuleRoot(file.Path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(relative => SafePaths.CombineUnderRoot(Root, relative))
            .ToList();
    }
}
