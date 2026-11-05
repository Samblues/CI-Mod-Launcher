using System.Text.Json;

namespace CILauncher.Core;

public sealed class InstallRecord
{
    public string Version { get; set; } = "";
    public string ManifestUrl { get; set; } = "";
    public string GamePath { get; set; } = "";
    public List<RecordedFile> Files { get; set; } = new();
}

public sealed class RecordedFile
{
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
}

public static class InstallRecordStore
{
    public static InstallRecord? TryLoad(string? gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
            return null;
        var path = PathFor(gamePath);
        if (!File.Exists(path))
            return null;
        try
        {
            var record = JsonSerializer.Deserialize<InstallRecord>(File.ReadAllText(path), ManifestJson.Options);
            if (record == null || record.Files.Count == 0)
                return null;
            return record;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(InstallRecord record)
    {
        var path = PathFor(record.GamePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(record, ManifestJson.Options));
    }

    public static bool FilesMatchSizes(string gamePath, InstallRecord record)
    {
        foreach (var file in record.Files)
        {
            string absolute;
            try
            {
                absolute = SafePaths.CombineUnderRoot(gamePath, file.Path);
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            if (!File.Exists(absolute) || new FileInfo(absolute).Length != file.Size)
                return false;
        }

        return true;
    }

    public static string PathFor(string gamePath)
    {
        var canonical = Path.GetFullPath(gamePath).TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant();
        var hash = IoCompat.ToHex(IoCompat.Sha256(System.Text.Encoding.UTF8.GetBytes(canonical))).Substring(0, 16).ToLowerInvariant();
        return Path.Combine(LauncherPaths.Root, "installs", hash + ".json");
    }
}
