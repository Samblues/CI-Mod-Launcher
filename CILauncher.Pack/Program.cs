using CILauncher.Core;

if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
{
    PrintHelp();
    return 0;
}

try
{
    var version = Optional(args, "--version");
    var output = Required(args, "--output");
    var maps = ReadMaps(args);
    var gameVersion = Optional(args, "--game-version");
    var notes = Optional(args, "--notes");
    var launchArgs = Optional(args, "--launch-args");
    var manifest = await ModpackPublisher.PublishAsync(new PublishRequest
    {
        Version = version,
        OutputDirectory = output,
        GameVersion = gameVersion,
        Notes = notes,
        LaunchArguments = launchArgs,
        Maps = maps
    });

    var bytes = manifest.Files.Sum(file => file.Size);
    Console.WriteLine($"Published {manifest.Version}");
    Console.WriteLine($"Files: {manifest.Files.Count}");
    Console.WriteLine($"Size: {bytes / (1024d * 1024d):0.0} MB");
    Console.WriteLine($"Game: {manifest.GameVersion ?? "(not set)"}");
    Console.WriteLine($"Launch: {manifest.Launch.Arguments}");
    Console.WriteLine($"Feed: {Path.GetFullPath(output)}");
    Console.WriteLine("Upload the blobs first, then manifest.json, to an HTTPS static host.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static string Required(string[] args, string name)
{
    var value = Optional(args, name);
    if (string.IsNullOrWhiteSpace(value))
        throw new ArgumentException($"Missing {name}.");
    return value;
}

static string? Optional(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }

    return null;
}

static List<ModuleMap> ReadMaps(string[] args)
{
    var maps = new List<ModuleMap>();
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (!args[i].Equals("--map", StringComparison.OrdinalIgnoreCase))
            continue;
        var value = args[i + 1];
        var split = value.IndexOf('=');
        if (split <= 0 || split == value.Length - 1)
            throw new ArgumentException("--map must look like Modules/CI_EVENT=D:\\path\\to\\CI_EVENT");
        maps.Add(new ModuleMap
        {
            Destination = value[..split],
            SourceDirectory = value[(split + 1)..]
        });
    }

    if (maps.Count == 0)
        throw new ArgumentException("Add at least one --map Modules/CI_EVENT=<folder with SubModule.xml>.");
    return maps;
}

static void PrintHelp()
{
    Console.WriteLine("""
        CI modpack publisher

        Builds manifest.json plus a blobs folder you can host over HTTPS.
        Point --map at the deployed client module (the folder that contains
        SubModule.xml, bin, GUI, ModuleData and AssetPackages), not the git repo.

        dotnet run --project launcher/CILauncher.Pack -- --output D:\ci-feed --map "Modules/CI_EVENT=F:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\CI_EVENT"

        The published version is the <Version> of the module whose Id is CI_EVENT
        (Name "CI Modpack"). Bump that value in SubModule.xml on every publish.
        Players are compared against the same value in their Modules folder.
        Launch arguments load Native, Multiplayer, and CI_EVENT.

        --version v1.9.0.1       Optional. Must match SubModule.xml if you pass it.
        --notes "text"           Shown in the launcher.
        --launch-args "..."      Optional. Defaults to CI_EVENT.

        Upload the blobs, then manifest.json.
        """);
}
