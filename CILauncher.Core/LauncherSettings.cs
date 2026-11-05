using System.Text.Json;

namespace CILauncher.Core;

public sealed class LauncherSettings
{
    public string? ManifestUrl { get; set; }
    public string? SteamPath { get; set; }
    public string? GamePath { get; set; }
    public int DownloadConcurrency { get; set; } = 8;
    public bool RequireOnlineCheck { get; set; } = true;
    public List<string> SelectedModuleIds { get; set; } = new();
    public List<string> FavouriteModuleIds { get; set; } = new();
    public string Language { get; set; } = "en";
}

public static class LauncherPaths
{
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CILauncher");
    public static string SettingsPath => Path.Combine(Root, "settings.json");
    public static string LogPath => Path.Combine(Root, "launcher.log");
    public static string ExecutablePathFile => Path.Combine(Root, "launcher.path");
    public static string ServersPath => Path.Combine(Root, "servers.json");
    public static string JoinRequestPath => Path.Combine(Root, "join.json");

    public static string? CurrentExecutable() => IoCompat.ProcessPath();

    public static void RememberExecutable()
    {
        var path = IoCompat.ProcessPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;
        Directory.CreateDirectory(Root);
        File.WriteAllText(ExecutablePathFile, path);
    }
}

public static class LauncherSettingsStore
{
    public static LauncherSettings Load()
    {
        try
        {
            if (!File.Exists(LauncherPaths.SettingsPath))
                return new LauncherSettings();
            var settings = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(LauncherPaths.SettingsPath), ManifestJson.Options)
                           ?? new LauncherSettings();
            settings.SelectedModuleIds ??= new List<string>();
            settings.FavouriteModuleIds ??= new List<string>();
            if (string.IsNullOrWhiteSpace(settings.Language))
                settings.Language = "en";
            return settings;
        }
        catch
        {
            return new LauncherSettings();
        }
    }

    public static void Save(LauncherSettings settings)
    {
        settings.DownloadConcurrency = IoCompat.Clamp(settings.DownloadConcurrency, 1, 16);
        Directory.CreateDirectory(LauncherPaths.Root);
        File.WriteAllText(LauncherPaths.SettingsPath, JsonSerializer.Serialize(settings, ManifestJson.Options));
    }
}

public static class Log
{
    public static void Info(string message) => Write("info", message);

    public static void Error(string message, Exception? exception = null)
        => Write("error", exception == null ? message : message + " " + exception);

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(LauncherPaths.Root);
            File.AppendAllText(LauncherPaths.LogPath, $"{DateTimeOffset.Now:O} {level} {message}{Environment.NewLine}");
            var info = new FileInfo(LauncherPaths.LogPath);
            if (info.Length <= 256 * 1024)
                return;
            var text = File.ReadAllText(LauncherPaths.LogPath);
            var keep = text.Length > 100_000 ? text[^100_000..] : text;
            File.WriteAllText(LauncherPaths.LogPath, keep);
        }
        catch
        {
            // Logging must not take the launcher down.
        }
    }
}
