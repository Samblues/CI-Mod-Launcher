using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Win32;

namespace CILauncher.Core;

public sealed class ModpackHome
{
    public required InstalledModule Module { get; init; }
    public required string Directory { get; init; }
    public required bool Workshop { get; init; }
    public string? ItemId { get; init; }
}

public sealed record InstalledModule
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Folder { get; init; }
    public string? Version { get; init; }

    public string Display
    {
        get
        {
            var label = string.IsNullOrWhiteSpace(Name) || Name.Equals(Id, StringComparison.OrdinalIgnoreCase)
                ? Id
                : Name + "    " + Id;
            return string.IsNullOrWhiteSpace(Version) ? label : label + "    " + Version;
        }
    }
}

public sealed class GameLocation
{
    public required string GamePath { get; init; }
    public string? SteamPath { get; init; }
    public IReadOnlyList<InstalledModule> Modules { get; init; } = Array.Empty<InstalledModule>();
    public string ModulesPath => Path.Combine(GamePath, "Modules");
}

public sealed class LocateResult
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public GameLocation? Location { get; init; }

    public static LocateResult Found(GameLocation location) => new() { Ok = true, Location = location };

    public static LocateResult Missing(string error) => new() { Error = error };
}

public readonly record struct SteamLibraryFolder(string Path, bool HasBannerlord);

public static partial class GameLocator
{
    public const string BannerlordFolderName = "Mount & Blade II Bannerlord";
    public const string BannerlordAppId = "261550";

    /// <summary>The only module the launcher starts. Name in SubModule.xml is "CI Modpack".</summary>
    public const string ModpackModuleId = "CI_EVENT";

    /// <summary>Steam Workshop file id of CI Modpack. The folder is workshop/content/261550/3245522442.</summary>
    public const string WorkshopModpackItemId = "3245522442";

    public static bool IsGameRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return false;
        var bin = Path.Combine(path, "bin", "Win64_Shipping_Client");
        var hasClient = File.Exists(Path.Combine(bin, "Bannerlord.exe"))
                        || File.Exists(Path.Combine(bin, "Bannerlord.Native.exe"));
        return hasClient && File.Exists(Path.Combine(path, "Modules", "Native", "SubModule.xml"));
    }

    /// <summary>
    /// Windows Steam has steam.exe. The Linux and Mac clients have steam.sh, and the Mac app
    /// also has Steam.app next to steamapps. A CrossOver bottle still has steam.exe.
    /// </summary>
    public static bool IsSteamInstall(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return false;
        if (File.Exists(Path.Combine(path, "steam.exe")) || File.Exists(Path.Combine(path, "steam.sh")))
            return true;
        return Directory.Exists(Path.Combine(path, "Steam.app"))
               && Directory.Exists(Path.Combine(path, "steamapps"));
    }

    public static string? ResolveClientExecutable(string gamePath)
    {
        var bin = Path.Combine(gamePath, "bin", "Win64_Shipping_Client");
        var exe = Path.Combine(bin, "Bannerlord.exe");
        if (File.Exists(exe))
            return exe;
        var native = Path.Combine(bin, "Bannerlord.Native.exe");
        return File.Exists(native) ? native : null;
    }

    /// <summary>
    /// True when this copy of the launcher lives inside a Steam library (Workshop or the game folder).
    /// A download from the website does not.
    /// </summary>
    public static bool IsBundledInSteam(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            return false;
        var full = Path.GetFullPath(executablePath);
        var needle = Path.DirectorySeparatorChar + "steamapps" + Path.DirectorySeparatorChar;
        return full.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    public static GameLocation? TryFind()
    {
        var steam = TryReadSteamInstall();
        if (!string.IsNullOrWhiteSpace(steam))
        {
            var located = Locate(steam);
            if (located.Ok && located.Location != null)
                return located.Location;
        }

        var uninstall = ReadUninstallLocation();
        if (!IsGameRoot(uninstall))
            return null;

        return new GameLocation
        {
            GamePath = Path.GetFullPath(uninstall!),
            SteamPath = FindSteamInstallAbove(uninstall!) ?? steam,
            Modules = ReadModules(uninstall!)
        };
    }

    public static LocateResult Locate(string selectedFolder)
    {
        if (string.IsNullOrWhiteSpace(selectedFolder) || !Directory.Exists(selectedFolder))
            return LocateResult.Missing("That folder doesn't exist.");

        var full = Path.GetFullPath(selectedFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (full.EndsWith("steamapps", StringComparison.OrdinalIgnoreCase))
        {
            var parent = Directory.GetParent(full);
            if (parent != null)
                full = parent.FullName;
        }

        if (IsGameRoot(full))
        {
            return LocateResult.Found(new GameLocation
            {
                GamePath = full,
                SteamPath = FindSteamInstallAbove(full),
                Modules = ReadModules(full)
            });
        }

        if (!IsSteamInstall(full) && !Directory.Exists(Path.Combine(full, "steamapps")))
            return LocateResult.Missing("Select your Steam folder — the one that contains steam.exe.");

        var game = ChooseBannerlord(full);
        if (game == null)
            return LocateResult.Missing("Steam is there, but Bannerlord isn't installed in its libraries. Install the game on Steam, then select the folder again.");

        return LocateResult.Found(new GameLocation
        {
            GamePath = game,
            SteamPath = IsSteamInstall(full) ? full : FindSteamInstallAbove(full) ?? FindSteamInstallAbove(game),
            Modules = ReadModules(game)
        });
    }

    public static IReadOnlyList<InstalledModule> ReadModules(string gamePath)
    {
        var modulesDirectory = Path.Combine(gamePath, "Modules");
        if (!Directory.Exists(modulesDirectory))
            return Array.Empty<InstalledModule>();

        var modules = new List<InstalledModule>();
        foreach (var directory in Directory.EnumerateDirectories(modulesDirectory))
        {
            var module = TryReadModule(directory);
            if (module != null)
                modules.Add(module);
        }

        return modules
            .OrderBy(module => module.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Mods the player can add to a multiplayer launch. Native and Multiplayer are omitted because they always load.
    /// Includes Modules and Steam Workshop items. CI Modpack is included so the list can show it as required.
    /// </summary>
    public static IReadOnlyList<InstalledModule> ListLaunchModules(string gamePath, string? steamPath = null)
    {
        var found = new Dictionary<string, InstalledModule>(StringComparer.OrdinalIgnoreCase);
        void Consider(InstalledModule? module)
        {
            if (module == null || !LaunchSelection.IsSafeId(module.Id))
                return;
            if (module.Id.Equals("Native", StringComparison.OrdinalIgnoreCase)
                || module.Id.Equals("Multiplayer", StringComparison.OrdinalIgnoreCase))
                return;
            found.TryAdd(module.Id, module);
        }

        foreach (var module in ReadModules(gamePath))
            Consider(module);
        foreach (var module in ReadWorkshopModules(gamePath, steamPath))
            Consider(module);

        return found.Values
            .OrderBy(module => module.Id.Equals(ModpackModuleId, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(module => module.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static InstalledModule? FindModpack(string gamePath)
    {
        var matches = ReadModules(gamePath)
            .Where(module => module.Id.Equals(ModpackModuleId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return matches.FirstOrDefault(module => module.Folder.Equals(ModpackModuleId, StringComparison.OrdinalIgnoreCase))
               ?? matches.FirstOrDefault();
    }

    /// <summary>
    /// The CI Modpack the player is actually running. A subscribed Workshop copy wins over Modules\CI_EVENT,
    /// because Steam loads workshop/content/261550/&lt;item&gt; with SubModule.xml at the root of that folder.
    /// </summary>
    public static ModpackHome? LocateModpack(string gamePath, string? steamPath = null)
    {
        var workshop = FindWorkshopCopies(gamePath, steamPath)
            .OrderByDescending(copy => copy.ItemId != null && copy.ItemId.Equals(WorkshopModpackItemId, StringComparison.Ordinal))
            .FirstOrDefault();
        if (workshop != null)
            return workshop;

        var local = FindModpack(gamePath);
        if (local == null)
            return null;
        return new ModpackHome
        {
            Module = local,
            Directory = Path.Combine(gamePath, "Modules", local.Folder),
            Workshop = false
        };
    }

    public static InstallTarget ChooseInstallTarget(string gamePath, string? steamPath = null)
    {
        var home = LocateModpack(gamePath, steamPath);
        if (home?.Workshop == true)
            return InstallTarget.WorkshopItem(home.Directory);
        if (home != null)
            return InstallTarget.Game(gamePath);

        var known = FindWorkshopItemDirectory(gamePath, steamPath, WorkshopModpackItemId);
        if (known != null && WorkshopFolderAcceptsModpack(known))
            return InstallTarget.WorkshopItem(known);
        return InstallTarget.Game(gamePath);
    }

    public static IReadOnlyList<string> WorkshopContentRoots(string gamePath, string? steamPath)
    {
        var roots = new List<string>();
        void AddLibrary(string? library)
        {
            if (string.IsNullOrWhiteSpace(library))
                return;
            var content = Path.Combine(library, "steamapps", "workshop", "content", BannerlordAppId);
            if (!Directory.Exists(content))
                return;
            var full = Path.GetFullPath(content);
            if (roots.Any(existing => existing.Equals(full, StringComparison.OrdinalIgnoreCase)))
                return;
            roots.Add(full);
        }

        AddLibrary(LibraryRootFromGame(gamePath));
        AddLibrary(steamPath);
        if (!string.IsNullOrWhiteSpace(steamPath))
        {
            var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                foreach (var library in ParseLibraryPaths(File.ReadAllText(vdf)))
                    AddLibrary(library);
            }
        }

        return roots;
    }

    public static string? ReadNativeVersion(string gamePath)
    {
        var xml = Path.Combine(gamePath, "Modules", "Native", "SubModule.xml");
        if (!File.Exists(xml))
            return null;
        try
        {
            var document = XDocument.Load(xml);
            return document.Root?.Element("Version")?.Attribute("value")?.Value;
        }
        catch
        {
            return null;
        }
    }

    public static string? TryReadGameVersionAbove(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Modules", "Native", "SubModule.xml")))
                return ReadNativeVersion(directory.FullName);
            directory = directory.Parent;
        }

        return null;
    }

    public static string? TryReadSteamInstall()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                var path = key?.GetValue("SteamPath") as string;
                return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
            }
            catch
            {
                return null;
            }
        }

        return FirstExistingSteamRoot(UnixSteamCandidates());
    }

    public static string? FirstExistingSteamRoot(IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;
            if (IsSteamInstall(candidate) || Directory.Exists(Path.Combine(candidate, "steamapps")))
                return Path.GetFullPath(candidate);
        }

        return null;
    }

    public static IEnumerable<string> UnixSteamCandidates()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            yield break;

        if (OperatingSystem.IsMacOS())
        {
            yield return Path.Combine(home, "Library", "Application Support", "Steam");
            yield break;
        }

        yield return Path.Combine(home, ".steam", "steam");
        yield return Path.Combine(home, ".steam", "root");
        yield return Path.Combine(home, ".local", "share", "Steam");
        yield return Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
        yield return Path.Combine(home, "snap", "steam", "common", ".local", "share", "Steam");
    }

    public static IReadOnlyList<string> ParseLibraryPaths(string vdf)
        => ParseLibraries(vdf).Select(library => library.Path).ToList();

    public static IReadOnlyList<SteamLibraryFolder> ParseLibraries(string vdf)
    {
        var matches = LibraryPathPattern.Matches(vdf);
        var libraries = new List<SteamLibraryFolder>(matches.Count);
        for (var index = 0; index < matches.Count; index++)
        {
            var path = matches[index].Groups[1].Value.Replace("\\\\", "\\");
            if (string.IsNullOrWhiteSpace(path))
                continue;
            var start = matches[index].Index + matches[index].Length;
            var end = index + 1 < matches.Count ? matches[index + 1].Index : vdf.Length;
            var slice = vdf[start..end];
            var hasBannerlord = slice.Contains("\"" + BannerlordAppId + "\"", StringComparison.Ordinal);
            libraries.Add(new SteamLibraryFolder(path, hasBannerlord));
        }

        return libraries;
    }

    private static string? ChooseBannerlord(string steamOrLibrary)
    {
        var candidates = new List<(string Path, bool Preferred)>();
        var direct = Path.Combine(steamOrLibrary, "steamapps", "common", BannerlordFolderName);
        var vdfPath = Path.Combine(steamOrLibrary, "steamapps", "libraryfolders.vdf");
        IReadOnlyList<SteamLibraryFolder> libraries = File.Exists(vdfPath)
            ? ParseLibraries(File.ReadAllText(vdfPath))
            : Array.Empty<SteamLibraryFolder>();

        var directPreferred = libraries.Any(library =>
            library.HasBannerlord
            && PathsEqual(library.Path, steamOrLibrary));
        candidates.Add((direct, directPreferred));

        foreach (var library in libraries)
            candidates.Add((Path.Combine(library.Path, "steamapps", "common", BannerlordFolderName), library.HasBannerlord));

        string? preferred = null;
        string? any = null;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (!IsGameRoot(candidate.Path))
                continue;
            var full = Path.GetFullPath(candidate.Path);
            if (!seen.Add(full))
            {
                if (candidate.Preferred)
                    preferred = full;
                continue;
            }

            any ??= full;
            if (candidate.Preferred)
                preferred = full;
        }

        return preferred ?? any;
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string? LibraryRootFromGame(string gamePath)
    {
        var steamapps = Directory.GetParent(gamePath)?.Parent;
        if (steamapps == null || !steamapps.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
            return null;
        return steamapps.Parent?.FullName;
    }

    private static IEnumerable<InstalledModule> ReadWorkshopModules(string gamePath, string? steamPath)
    {
        foreach (var root in WorkshopContentRoots(gamePath, steamPath))
        {
            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(root);
            }
            catch
            {
                continue;
            }

            foreach (var directory in directories)
            {
                var module = TryReadModule(directory);
                if (module != null)
                    yield return module;
            }
        }
    }

    private static IEnumerable<ModpackHome> FindWorkshopCopies(string gamePath, string? steamPath)
    {
        foreach (var root in WorkshopContentRoots(gamePath, steamPath))
        {
            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(root);
            }
            catch
            {
                continue;
            }

            foreach (var directory in directories)
            {
                var module = TryReadModule(directory);
                if (module == null || !module.Id.Equals(ModpackModuleId, StringComparison.OrdinalIgnoreCase))
                    continue;
                yield return new ModpackHome
                {
                    Module = module,
                    Directory = Path.GetFullPath(directory),
                    Workshop = true,
                    ItemId = Path.GetFileName(directory)
                };
            }
        }
    }

    private static string? FindWorkshopItemDirectory(string gamePath, string? steamPath, string itemId)
    {
        foreach (var root in WorkshopContentRoots(gamePath, steamPath))
        {
            var directory = Path.Combine(root, itemId);
            if (Directory.Exists(directory))
                return Path.GetFullPath(directory);
        }

        return null;
    }

    private static bool WorkshopFolderAcceptsModpack(string directory)
    {
        var module = TryReadModule(directory);
        return module == null || module.Id.Equals(ModpackModuleId, StringComparison.OrdinalIgnoreCase);
    }

    private static InstalledModule? TryReadModule(string directory)
    {
        var xmlPath = Path.Combine(directory, "SubModule.xml");
        if (!File.Exists(xmlPath))
            return null;

        var folder = Path.GetFileName(directory);
        var id = folder;
        var name = folder;
        string? version = null;
        try
        {
            var document = XDocument.Load(xmlPath);
            id = document.Root?.Element("Id")?.Attribute("value")?.Value;
            name = document.Root?.Element("Name")?.Attribute("value")?.Value;
            version = document.Root?.Element("Version")?.Attribute("value")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(id))
                id = folder;
            if (string.IsNullOrWhiteSpace(name))
                name = id;
        }
        catch
        {
            id = folder;
            name = folder;
            version = null;
        }

        return new InstalledModule
        {
            Id = id,
            Name = name,
            Folder = folder,
            Version = string.IsNullOrWhiteSpace(version) ? null : version
        };
    }

    private static string? FindSteamInstallAbove(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory != null)
        {
            if (IsSteamInstall(directory.FullName))
                return directory.FullName;
            directory = directory.Parent;
        }

        return null;
    }

    private static string? ReadUninstallLocation()
    {
        if (!OperatingSystem.IsWindows())
            return null;
        try
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 261550");
                if (key?.GetValue("InstallLocation") is string location && !string.IsNullOrWhiteSpace(location))
                    return location;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static readonly Regex LibraryPathPattern = new("\"path\"\\s+\"([^\"]*)\"", RegexOptions.CultureInvariant | RegexOptions.Compiled);
}
