namespace CILauncher.Core;

public enum GameHostKind
{
    Windows,
    Linux,
    Mac
}

public enum GameLaunchKind
{
    Direct,
    Proton,
    SteamAppLaunch,
    Wine
}

public sealed class GameLaunchPlan
{
    public required GameLaunchKind Kind { get; init; }
    public required string Executable { get; init; }
    public string? Host { get; init; }
    public string? Bottle { get; init; }
    public string? CompatData { get; init; }
    public string? SteamRoot { get; init; }
}

public static partial class GameLocator
{
    public static GameHostKind CurrentHost
        => OperatingSystem.IsMacOS() ? GameHostKind.Mac
            : OperatingSystem.IsLinux() ? GameHostKind.Linux
            : GameHostKind.Windows;

    public static GameLaunchPlan CreatePlan(string gamePath)
        => CreatePlan(gamePath, CurrentHost);

    public static GameLaunchPlan CreatePlan(string gamePath, GameHostKind host)
    {
        var executable = ResolveClientExecutable(gamePath);
        if (executable == null)
            throw new FileNotFoundException("Couldn't find Bannerlord in this folder.", gamePath);

        if (host == GameHostKind.Windows)
            return new GameLaunchPlan { Kind = GameLaunchKind.Direct, Executable = executable };

        var steam = FindSteamInstallAbove(gamePath);
        if (host == GameHostKind.Linux)
        {
            var compat = ExpectedCompatData(gamePath, steam);
            var proton = FindProtonScript(steam, gamePath);
            if (proton != null && compat != null && steam != null)
            {
                return new GameLaunchPlan
                {
                    Kind = GameLaunchKind.Proton,
                    Executable = executable,
                    Host = proton,
                    CompatData = compat,
                    SteamRoot = steam
                };
            }

            return new GameLaunchPlan
            {
                Kind = GameLaunchKind.SteamAppLaunch,
                Executable = executable,
                Host = SteamCommand(steam),
                SteamRoot = steam
            };
        }

        var wine = FindMacWine(gamePath);
        if (wine == null)
        {
            throw new InvalidOperationException(
                "Bannerlord has no Mac version. Install it with CrossOver, then choose the Steam folder inside that bottle — the one that contains steam.exe.");
        }

        return new GameLaunchPlan
        {
            Kind = GameLaunchKind.Wine,
            Executable = executable,
            Host = wine.Value.Path,
            Bottle = wine.Value.Bottle
        };
    }

    public static string? ProtonLocalAppData(string gamePath)
    {
        var compat = ExpectedCompatData(gamePath, FindSteamInstallAbove(gamePath));
        if (compat == null)
            return null;
        return UsersLocalAppData(Path.Combine(compat, "pfx", "drive_c", "users"));
    }

    public static string? MacBottleLocalAppData(string gamePath)
    {
        var bottle = FindBottleRoot(gamePath);
        if (bottle == null)
            return null;
        return UsersLocalAppData(Path.Combine(bottle, "drive_c", "users"));
    }

    public static string? FindProtonScript(string? steamRoot, string gamePath)
    {
        foreach (var library in LibraryRoots(steamRoot, gamePath))
        {
            var fromInfo = ReadConfigInfoProton(Path.Combine(library, "steamapps", "compatdata", BannerlordAppId));
            if (fromInfo != null)
                return fromInfo;
        }

        string? fallback = null;
        foreach (var library in LibraryRoots(steamRoot, gamePath))
        {
            foreach (var script in ProtonScripts(library))
            {
                if (script.Contains("Experimental", StringComparison.OrdinalIgnoreCase))
                    return script;
                fallback = script;
            }
        }

        return fallback;
    }

    private static string? ExpectedCompatData(string gamePath, string? steamRoot)
    {
        var library = LibraryRootFromGame(gamePath) ?? steamRoot;
        if (string.IsNullOrWhiteSpace(library))
            return null;
        return Path.Combine(library, "steamapps", "compatdata", BannerlordAppId);
    }

    private static IEnumerable<string> LibraryRoots(string? steamRoot, string gamePath)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<string>();
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            var full = Path.GetFullPath(path);
            if (seen.Add(full))
                roots.Add(full);
        }

        Add(LibraryRootFromGame(gamePath));
        Add(steamRoot);
        if (!string.IsNullOrWhiteSpace(steamRoot))
        {
            var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                foreach (var library in ParseLibraryPaths(File.ReadAllText(vdf)))
                    Add(library);
            }
        }

        return roots;
    }

    private static string? ReadConfigInfoProton(string compatData)
    {
        var info = Path.Combine(compatData, "config_info");
        if (!File.Exists(info))
            return null;
        string? first;
        try
        {
            using var reader = new StreamReader(info);
            first = reader.ReadLine()?.Trim();
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(first))
            return null;
        var script = Path.Combine(first, "proton");
        return File.Exists(script) ? script : null;
    }

    private static IEnumerable<string> ProtonScripts(string library)
    {
        var roots = new[]
        {
            Path.Combine(library, "steamapps", "common"),
            Path.Combine(library, "steamapps", "compatibilitytools.d")
        };
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
                continue;
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
                var name = Path.GetFileName(directory);
                if (!name.StartsWith("Proton", StringComparison.OrdinalIgnoreCase)
                    && !File.Exists(Path.Combine(directory, "proton")))
                    continue;
                var script = Path.Combine(directory, "proton");
                if (File.Exists(script))
                    yield return script;
                foreach (var nested in NestedProton(directory))
                    yield return nested;
            }
        }
    }

    private static IEnumerable<string> NestedProton(string directory)
    {
        IEnumerable<string> children;
        try
        {
            children = Directory.EnumerateDirectories(directory);
        }
        catch
        {
            yield break;
        }

        foreach (var child in children)
        {
            var script = Path.Combine(child, "proton");
            if (File.Exists(script))
                yield return script;
        }
    }

    private static string SteamCommand(string? steamRoot)
    {
        if (!string.IsNullOrWhiteSpace(steamRoot))
        {
            var script = Path.Combine(steamRoot, "steam.sh");
            if (File.Exists(script))
                return script;
            var exe = Path.Combine(steamRoot, "steam.exe");
            if (File.Exists(exe))
                return exe;
        }

        return "steam";
    }

    private readonly record struct WineHost(string Path, string? Bottle);

    private static WineHost? FindMacWine(string gamePath)
    {
        var bottleRoot = FindBottleRoot(gamePath);
        var bottle = bottleRoot == null ? null : Path.GetFileName(bottleRoot);
        foreach (var wine in WineCandidates())
        {
            if (File.Exists(wine))
                return new WineHost(wine, bottle);
        }

        return null;
    }

    private static IEnumerable<string> WineCandidates()
    {
        yield return "/Applications/CrossOver.app/Contents/SharedSupport/CrossOver/bin/wine";
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
            yield return Path.Combine(home, "Applications", "CrossOver.app", "Contents", "SharedSupport", "CrossOver", "bin", "wine");
        yield return "/opt/homebrew/bin/wine";
        yield return "/usr/local/bin/wine";
    }

    private static string? FindBottleRoot(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "cxbottle.conf")))
                return directory.FullName;
            directory = directory.Parent;
        }

        return null;
    }

    private static string? UsersLocalAppData(string usersDirectory)
    {
        if (!Directory.Exists(usersDirectory))
            return null;
        foreach (var user in Directory.EnumerateDirectories(usersDirectory))
        {
            if (Path.GetFileName(user).Equals("Public", StringComparison.OrdinalIgnoreCase))
                continue;
            var local = Path.Combine(user, "AppData", "Local");
            if (Directory.Exists(local))
                return local;
        }

        return null;
    }
}
