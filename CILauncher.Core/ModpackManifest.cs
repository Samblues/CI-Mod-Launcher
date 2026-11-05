using System.Text.Json;
using System.Text.Json.Serialization;

namespace CILauncher.Core;

public sealed class ModpackManifest
{
    public int Format { get; set; } = 1;
    public string Version { get; set; } = "";
    public string? GameVersion { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public LaunchSpec Launch { get; set; } = new();
    public List<ModpackFile> Files { get; set; } = new();
}

public sealed class LaunchSpec
{
    public string Executable { get; set; } = GameLauncher.ClientExecutable;
    public string Arguments { get; set; } = "";
}

public sealed class ModpackFile
{
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public string Url { get; set; } = "";
}

public static class ManifestJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static ModpackManifest Parse(string json)
    {
        ModpackManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ModpackManifest>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new ManifestException("The modpack feed isn't valid JSON. " + ex.Message);
        }

        if (manifest == null)
            throw new ManifestException("The modpack feed was empty.");
        ManifestValidator.Validate(manifest);
        return manifest;
    }

    public static string Write(ModpackManifest manifest)
    {
        ManifestValidator.Validate(manifest);
        return JsonSerializer.Serialize(manifest, Options);
    }
}

public sealed class ManifestException : Exception
{
    public ManifestException(string message) : base(message)
    {
    }
}
