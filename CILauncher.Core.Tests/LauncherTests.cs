using System.Net;
using System.Security.Cryptography;
using CILauncher.Core;
using Xunit;

namespace CILauncher.Core.Tests;

public class LauncherTests
{
    [Fact]
    public void Rejects_paths_that_escape_the_module_folder()
    {
        Assert.Throws<ManifestException>(() => SafePaths.NormalizeModulePath(@"Modules/CI_EVENT/../../Windows/system.ini"));
        Assert.Throws<ManifestException>(() => SafePaths.NormalizeModulePath(@"C:\Windows\system.ini"));
        Assert.Equal("Modules/CI_EVENT/SubModule.xml", SafePaths.NormalizeModulePath(@"Modules\CI_EVENT\SubModule.xml"));
    }

    [Fact]
    public void Feed_must_be_https_except_on_this_machine()
    {
        Assert.Throws<ManifestException>(() => FeedAddress.Parse("http://updates.example/manifest.json"));
        Assert.Equal("https", FeedAddress.Parse("https://updates.example/manifest.json").Scheme);
        Assert.True(FeedAddress.Parse("http://127.0.0.1:9/manifest.json").IsLoopback);
    }

    [Fact]
    public void Launch_line_only_allows_a_multiplayer_module_list()
    {
        var args = ModpackPublisher.BuildLaunchArguments(new[] { "CI_EVENT", "Bannerlord.Harmony", "CI_EVENT" });
        Assert.Equal("/multiplayer _MODULES_*Native*Bannerlord.Harmony*Multiplayer*CI_EVENT*_MODULES_", args);
        ManifestValidator.ValidateLaunch(new LaunchSpec { Arguments = args });
        Assert.Throws<ManifestException>(() => ManifestValidator.ValidateLaunch(new LaunchSpec
        {
            Executable = "bin/Win64_Shipping_Client/Bannerlord.exe",
            Arguments = "/multiplayer cmd.exe /c calc"
        }));
    }

    [Fact]
    public void Website_download_is_not_treated_as_the_steam_copy()
    {
        Assert.False(GameLocator.IsBundledInSteam(@"C:\Users\sam\Downloads\CIModpackLauncher.exe"));
        Assert.True(GameLocator.IsBundledInSteam(@"D:\Steam\steamapps\workshop\content\261550\3245522442\Launcher\CIModpackLauncher.exe"));
        Assert.True(GameLocator.IsBundledInSteam(@"D:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\CI_EVENT\Launcher\CIModpackLauncher.exe"));
    }

    [Fact]
    public void Steam_library_file_yields_install_folders()
    {
        var paths = GameLocator.ParseLibraryPaths("""
            "libraryfolders"
            {
              "0" { "path" "C:\\Steam" }
              "1" { "path" "D:\\Games\\Steam" }
            }
            """);
        Assert.Equal(new[] { @"C:\Steam", @"D:\Games\Steam" }, paths);
    }

    [Fact]
    public void Steam_folder_finds_bannerlord_and_pulls_its_modules()
    {
        using var tree = new TempTree();
        var steam = Path.Combine(tree.Path, "Steam");
        var other = Path.Combine(tree.Path, "LibraryWithoutGame");
        var library = Path.Combine(tree.Path, "Library");
        Directory.CreateDirectory(steam);
        File.WriteAllText(Path.Combine(steam, "steam.exe"), "");
        WriteGame(Path.Combine(other, "steamapps", "common", "Mount & Blade II Bannerlord"), "OtherMod");
        WriteGame(Path.Combine(library, "steamapps", "common", "Mount & Blade II Bannerlord"), "UserMod");

        var vdf = $$"""
            "libraryfolders"
            {
              "0"
              {
                "path" "{{VdfPath(steam)}}"
                "apps" { "228980" "1" }
              }
              "1"
              {
                "path" "{{VdfPath(other)}}"
                "apps" { "730" "1" }
              }
              "2"
              {
                "path" "{{VdfPath(library)}}"
                "apps" { "261550" "500" }
              }
            }
            """;
        Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
        File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"), vdf);

        var missing = GameLocator.Locate(Path.Combine(tree.Path, "nope"));
        Assert.False(missing.Ok);

        var located = GameLocator.Locate(steam);
        Assert.True(located.Ok);
        Assert.Equal(steam, located.Location!.SteamPath);
        Assert.EndsWith(Path.Combine("Library", "steamapps", "common", "Mount & Blade II Bannerlord"), located.Location.GamePath);
        Assert.Equal(Path.Combine(located.Location.GamePath, "Modules"), located.Location.ModulesPath);
        Assert.Contains(located.Location.Modules, module => module.Id == "UserMod" && module.Name == "My Module");
        Assert.DoesNotContain(located.Location.Modules, module => module.Id == "OtherMod");

        var direct = GameLocator.Locate(located.Location.GamePath);
        Assert.True(direct.Ok);
        Assert.Equal(located.Location.GamePath, direct.Location!.GamePath);
        Assert.Contains(direct.Location.Modules, module => module.Id == "UserMod");
    }

    [Fact]
    public void Linux_steam_folder_and_proton_can_launch_the_windows_client()
    {
        using var tree = new TempTree();
        var steam = Path.Combine(tree.Path, "Steam");
        var game = Path.Combine(steam, "steamapps", "common", "Mount & Blade II Bannerlord");
        Directory.CreateDirectory(steam);
        File.WriteAllText(Path.Combine(steam, "steam.sh"), "");
        WriteGame(game, "CI_EVENT");
        File.Delete(Path.Combine(game, "bin", "Win64_Shipping_Client", "Bannerlord.exe"));
        File.WriteAllText(Path.Combine(game, "bin", "Win64_Shipping_Client", "Bannerlord.Native.exe"), "");
        var proton = Path.Combine(steam, "steamapps", "common", "Proton - Experimental", "proton");
        Directory.CreateDirectory(Path.GetDirectoryName(proton)!);
        File.WriteAllText(proton, "");
        Directory.CreateDirectory(Path.Combine(steam, "steamapps", "compatdata", "261550", "pfx", "drive_c", "users", "steamuser", "AppData", "Local"));

        Assert.True(GameLocator.IsSteamInstall(steam));
        Assert.Equal(steam, GameLocator.FirstExistingSteamRoot(new[] { steam }));
        var located = GameLocator.Locate(steam);
        Assert.True(located.Ok);

        var plan = GameLocator.CreatePlan(game, GameHostKind.Linux);
        Assert.Equal(GameLaunchKind.Proton, plan.Kind);
        Assert.Equal(proton, plan.Host);
        Assert.EndsWith(Path.Combine("compatdata", "261550"), plan.CompatData);
        Assert.EndsWith(Path.Combine("AppData", "Local"), GameLocator.ProtonLocalAppData(game));

        Assert.Throws<InvalidOperationException>(() => GameLocator.CreatePlan(game, GameHostKind.Mac));
    }

    [Fact]
    public void Installed_modpack_version_comes_from_the_ci_event_submodule()
    {
        using var tree = new TempTree();
        var game = Path.Combine(tree.Path, "game");
        WriteGame(game, "SomeOtherMod");
        var modpack = Path.Combine(game, "Modules", "CI_EVENT");
        Directory.CreateDirectory(modpack);
        File.WriteAllText(Path.Combine(modpack, "SubModule.xml"), """
            <Module>
              <Name value="CI Modpack"/>
              <Id value="CI_EVENT"/>
              <Version value="v1.9.0.1"/>
            </Module>
            """);

        var found = GameLocator.FindModpack(game);
        Assert.NotNull(found);
        Assert.Equal("CI Modpack", found.Name);
        Assert.Equal("CI_EVENT", found.Id);
        Assert.Equal("v1.9.0.1", found.Version);
    }

    [Fact]
    public void Workshop_copy_is_the_installed_modpack()
    {
        using var tree = new TempTree();
        var game = Path.Combine(tree.Path, "steamapps", "common", "Mount & Blade II Bannerlord");
        WriteGame(game, "SomeOtherMod");
        var local = Path.Combine(game, "Modules", "CI_EVENT");
        Directory.CreateDirectory(local);
        File.WriteAllText(Path.Combine(local, "SubModule.xml"), """
            <Module>
              <Name value="CI Modpack"/>
              <Id value="CI_EVENT"/>
              <Version value="v1.0.0.0"/>
            </Module>
            """);
        var item = Path.Combine(tree.Path, "steamapps", "workshop", "content", "261550", GameLocator.WorkshopModpackItemId);
        Directory.CreateDirectory(item);
        File.WriteAllText(Path.Combine(item, "SubModule.xml"), """
            <Module>
              <Name value="CI Modpack"/>
              <Id value="CI_EVENT"/>
              <Version value="v1.9.0.1"/>
            </Module>
            """);
        var other = Path.Combine(tree.Path, "steamapps", "workshop", "content", "261550", "999");
        Directory.CreateDirectory(other);
        File.WriteAllText(Path.Combine(other, "SubModule.xml"), """
            <Module>
              <Name value="Other"/>
              <Id value="OtherMod"/>
              <Version value="v0.0.1"/>
            </Module>
            """);

        var home = GameLocator.LocateModpack(game, tree.Path);
        Assert.NotNull(home);
        Assert.True(home.Workshop);
        Assert.Equal(GameLocator.WorkshopModpackItemId, home.ItemId);
        Assert.Equal("v1.9.0.1", home.Module.Version);
        var target = GameLocator.ChooseInstallTarget(game, tree.Path);
        Assert.True(target.Workshop);
        Assert.Equal(Path.GetFullPath(item), target.Root);
        Assert.Equal("AssetPackages/a.tpac", target.Map("Modules/CI_EVENT/AssetPackages/a.tpac"));

        var choices = GameLocator.ListLaunchModules(game, tree.Path);
        Assert.Contains(choices, module => module.Id == "CI_EVENT");
        Assert.Contains(choices, module => module.Id == "SomeOtherMod");
        Assert.Contains(choices, module => module.Id == "OtherMod");
        Assert.DoesNotContain(choices, module => module.Id == "Native");
    }

    [Fact]
    public void Launch_always_includes_the_modpack_and_the_mods_the_player_ticked()
    {
        var args = LaunchSelection.Build(new[] { "Bannerlord.Harmony", "CI_EVENT", "Native", "My-Mod", "../bad" });
        Assert.Equal("/multiplayer _MODULES_*Native*Bannerlord.Harmony*Multiplayer*CI_EVENT*My-Mod*_MODULES_", args);
        ManifestValidator.ValidateLaunch(new LaunchSpec { Arguments = args });
    }

    [Fact]
    public async Task Workshop_install_writes_files_at_the_item_root()
    {
        using var source = new TempTree();
        using var feed = new TempTree();
        using var item = new TempTree();
        var module = Path.Combine(source.Path, "CI_EVENT");
        WriteModule(module, RandomBytes(1024));
        File.WriteAllText(Path.Combine(item.Path, "leftover.txt"), "old");
        var manifest = await ModpackPublisher.PublishAsync(new PublishRequest
        {
            OutputDirectory = feed.Path,
            GameVersion = "v1.2.8",
            Maps = new[] { new ModuleMap { Destination = "Modules/CI_EVENT", SourceDirectory = module } }
        });

        var installer = new ModpackInstaller(new HttpClient());
        var uri = new Uri(Path.Combine(feed.Path, "manifest.json"));
        var result = await installer.InstallAsync(manifest, uri, InstallTarget.WorkshopItem(item.Path), 2, null, CancellationToken.None);

        Assert.Equal(2, result.DownloadedFiles);
        Assert.Equal(1, result.RemovedFiles);
        Assert.True(File.Exists(Path.Combine(item.Path, "data.bin")));
        Assert.True(File.Exists(Path.Combine(item.Path, "SubModule.xml")));
        Assert.False(Directory.Exists(Path.Combine(item.Path, "Modules")));
        Assert.False(File.Exists(Path.Combine(item.Path, "leftover.txt")));
    }

    [Fact]
    public void Play_is_blocked_until_the_live_version_matches()
    {
        var mismatch = VersionGate.Decide(Ready with
        {
            InstalledVersion = "2026.10.01.1",
            RemoteVersion = "2026.10.30.1"
        });
        Assert.False(mismatch.CanPlay);
        Assert.Equal(PlayBlock.UpdateRequired, mismatch.Block);

        var offline = VersionGate.Decide(Ready with
        {
            CheckSucceeded = false,
            CheckError = "timeout"
        });
        Assert.False(offline.CanPlay);
        Assert.Equal(PlayBlock.Offline, offline.Block);

        var match = VersionGate.Decide(Ready with { });
        Assert.True(match.CanPlay);

        var differentGame = VersionGate.Decide(Ready with
        {
            InstalledGameVersion = "v1.2.11",
            RequiredGameVersion = "v1.2.12"
        });
        Assert.True(differentGame.CanPlay);
    }

    [Fact]
    public async Task Publish_and_install_skips_files_that_already_match()
    {
        using var source = new TempTree();
        using var feed = new TempTree();
        using var game = new TempTree();
        var module = Path.Combine(source.Path, "CI_EVENT");
        WriteModule(module, RandomBytes(32 * 1024));
        WriteNative(Path.Combine(source.Path, "game"), "v1.2.12");
        Directory.Move(module, Path.Combine(source.Path, "game", "Modules", "CI_EVENT"));
        var moduleDir = Path.Combine(source.Path, "game", "Modules", "CI_EVENT");
        Directory.CreateDirectory(Path.Combine(moduleDir, "Launcher"));
        File.WriteAllText(Path.Combine(moduleDir, "Launcher", "CIModpackLauncher.exe"), "launcher");

        var manifest = await ModpackPublisher.PublishAsync(new PublishRequest
        {
            OutputDirectory = feed.Path,
            Maps = new[]
            {
                new ModuleMap
                {
                    Destination = "Modules/CI_EVENT",
                    SourceDirectory = Path.Combine(source.Path, "game", "Modules", "CI_EVENT")
                }
            }
        });

        Assert.Equal("v1.0.0.0", manifest.Version);
        Assert.DoesNotContain(manifest.Files, file => file.Path.Contains("Launcher", StringComparison.OrdinalIgnoreCase));
        Assert.Null(manifest.GameVersion);
        Assert.Equal("/multiplayer _MODULES_*Native*Multiplayer*CI_EVENT*_MODULES_", manifest.Launch.Arguments);
        var uri = new Uri(Path.Combine(feed.Path, "manifest.json"));
        var http = new HttpClient();
        var installer = new ModpackInstaller(http);

        var first = await installer.InstallAsync(manifest, uri, InstallTarget.Game(game.Path), 4, null, CancellationToken.None);
        Assert.Equal(2, first.DownloadedFiles);
        Assert.True(File.Exists(Path.Combine(game.Path, "Modules", "CI_EVENT", "data.bin")));

        var second = await installer.InstallAsync(manifest, uri, InstallTarget.Game(game.Path), 4, null, CancellationToken.None);
        Assert.Equal(0, second.DownloadedFiles);
        Assert.Equal(2, second.ReusedFiles);

        var dataPath = Path.Combine(game.Path, "Modules", "CI_EVENT", "data.bin");
        File.WriteAllBytes(dataPath, new byte[new FileInfo(dataPath).Length]);
        var repaired = await installer.InstallAsync(manifest, uri, InstallTarget.Game(game.Path), 4, null, CancellationToken.None);
        Assert.Equal(1, repaired.DownloadedFiles);

        File.WriteAllText(Path.Combine(game.Path, "Modules", "CI_EVENT", "leftover.txt"), "old");
        var bundled = Path.Combine(game.Path, "Modules", "CI_EVENT", "Launcher");
        Directory.CreateDirectory(bundled);
        File.WriteAllText(Path.Combine(bundled, "CIModpackLauncher.exe"), "launcher");
        await ModpackPublisher.PublishAsync(new PublishRequest
        {
            GameVersion = "v1.2.12",
            OutputDirectory = feed.Path,
            Maps = new[]
            {
                new ModuleMap
                {
                    Destination = "Modules/CI_EVENT",
                    SourceDirectory = Path.Combine(source.Path, "game", "Modules", "CI_EVENT")
                }
            }
        });
        var updated = ManifestJson.Parse(await File.ReadAllTextAsync(Path.Combine(feed.Path, "manifest.json")));
        var third = await installer.InstallAsync(updated, uri, InstallTarget.Game(game.Path), 4, null, CancellationToken.None);
        Assert.Equal(0, third.DownloadedFiles);
        Assert.Equal(1, third.RemovedFiles);
        Assert.False(File.Exists(Path.Combine(game.Path, "Modules", "CI_EVENT", "leftover.txt")));
        Assert.True(File.Exists(Path.Combine(game.Path, "Modules", "CI_EVENT", "Launcher", "CIModpackLauncher.exe")));
    }

    [Fact]
    public async Task Http_download_resumes_and_rejects_a_bad_hash()
    {
        using var source = new TempTree();
        using var feed = new TempTree();
        using var game = new TempTree();
        var module = Path.Combine(source.Path, "CI_EVENT");
        var payload = RandomBytes(256 * 1024);
        WriteModule(module, payload);
        var manifest = await ModpackPublisher.PublishAsync(new PublishRequest
        {
            Version = "v1.0.0.0",
            GameVersion = "v1.2.12",
            OutputDirectory = feed.Path,
            Maps = new[] { new ModuleMap { Destination = "Modules/CI_EVENT", SourceDirectory = module } }
        });

        var data = manifest.Files.Single(file => file.Path.EndsWith("data.bin", StringComparison.OrdinalIgnoreCase));
        var partial = Path.Combine(game.Path, "Modules", "CI_EVENT", "data.bin.ci-partial");
        Directory.CreateDirectory(Path.GetDirectoryName(partial)!);
        var blob = await File.ReadAllBytesAsync(Path.Combine(feed.Path, "blobs", data.Sha256));
        await File.WriteAllBytesAsync(partial, blob[..(100 * 1024)]);

        var handler = new FeedHandler(feed.Path);
        var installer = new ModpackInstaller(new HttpClient(handler));
        var uri = new Uri("http://127.0.0.1/manifest.json");
        var result = await installer.InstallAsync(manifest, uri, InstallTarget.Game(game.Path), 4, null, CancellationToken.None);

        Assert.True(handler.SawRange);
        Assert.True(result.DownloadedBytes > 0);
        Assert.True(result.DownloadedBytes < payload.Length);
        Assert.Equal(payload, await File.ReadAllBytesAsync(Path.Combine(game.Path, "Modules", "CI_EVENT", "data.bin")));

        blob[0] ^= 0xFF;
        await File.WriteAllBytesAsync(Path.Combine(feed.Path, "blobs", data.Sha256), blob);
        File.Delete(Path.Combine(game.Path, "Modules", "CI_EVENT", "data.bin"));
        await Assert.ThrowsAsync<IOException>(() => installer.InstallAsync(manifest, uri, InstallTarget.Game(game.Path), 2, null, CancellationToken.None));
        Assert.False(File.Exists(Path.Combine(game.Path, "Modules", "CI_EVENT", "data.bin")));
    }

    private static GateInput Ready => new()
    {
        FeedConfigured = true,
        GameFound = true,
        CheckCompleted = true,
        CheckSucceeded = true,
        RequireOnlineCheck = true,
        InstalledVersion = "2026.10.30.1",
        RemoteVersion = "2026.10.30.1",
        FilesIntact = true,
        InstalledGameVersion = "v1.2.12",
        RequiredGameVersion = "v1.2.12"
    };

    private static void WriteGame(string game, string moduleId)
    {
        Directory.CreateDirectory(Path.Combine(game, "bin", "Win64_Shipping_Client"));
        File.WriteAllText(Path.Combine(game, "bin", "Win64_Shipping_Client", "Bannerlord.exe"), "");
        var native = Path.Combine(game, "Modules", "Native");
        Directory.CreateDirectory(native);
        File.WriteAllText(Path.Combine(native, "SubModule.xml"), """
            <Module>
              <Name value="Native"/>
              <Id value="Native"/>
              <Version value="v1.2.12"/>
            </Module>
            """);
        var user = Path.Combine(game, "Modules", moduleId);
        Directory.CreateDirectory(user);
        File.WriteAllText(Path.Combine(user, "SubModule.xml"), $"""
            <Module>
              <Name value="My Module"/>
              <Id value="{moduleId}"/>
            </Module>
            """);
        Directory.CreateDirectory(Path.Combine(game, "Modules", "NotAModule"));
    }

    private static string VdfPath(string path) => path.Replace("\\", "\\\\");

    private static void WriteModule(string directory, byte[] data)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "SubModule.xml"), """
            <Module>
              <Name value="CI Modpack"/>
              <Id value="CI_EVENT"/>
              <Version value="v1.0.0.0"/>
            </Module>
            """);
        File.WriteAllBytes(Path.Combine(directory, "data.bin"), data);
    }

    private static void WriteNative(string game, string version)
    {
        var native = Path.Combine(game, "Modules", "Native");
        Directory.CreateDirectory(native);
        File.WriteAllText(Path.Combine(native, "SubModule.xml"), $"""
            <Module>
              <Version value="{version}"/>
            </Module>
            """);
    }

    private static byte[] RandomBytes(int count)
    {
        var data = new byte[count];
        RandomNumberGenerator.Fill(data);
        return data;
    }

    [Fact]
    public void Server_list_comes_from_beside_the_manifest_and_drops_feed_passwords()
    {
        var list = ServerCatalog.ListUri(FeedAddress.Parse("https://updates.example/manifest.json"));
        Assert.Equal("https://updates.example/servers.json", list.ToString());

        var remote = ServerCatalog.ParseFeed("""
            {
              "servers": [
                { "name": "Coronas", "address": "203.0.113.10:7210", "port": 7210, "password": "secret", "gameType": "Frontline", "players": 40, "maxPlayers": 200 },
                { "name": "Broken", "address": "not a host", "port": 1 }
              ]
            }
            """);

        var server = Assert.Single(remote);
        Assert.Equal("Coronas", server.Name);
        Assert.Equal("203.0.113.10", server.Address);
        Assert.Equal(7210, server.Port);
        Assert.Null(server.Password);
        Assert.False(server.Local);
        Assert.Equal("Frontline   40 / 200", server.Meta);
    }

    [Fact]
    public void Added_servers_stay_when_the_feed_does_not_list_them()
    {
        var remote = new[]
        {
            new ServerEntry { Name = "Feed", Address = "203.0.113.10", Port = 7210 }
        };
        var local = new[]
        {
            new ServerEntry { Name = "Feed copy", Address = "203.0.113.10", Port = 7210, Password = "nope", Local = true },
            new ServerEntry { Name = "Practice", Address = "203.0.113.11", Port = 7211, Local = true }
        };

        var merged = ServerCatalog.Merge(remote, local);
        Assert.Equal(2, merged.Count);
        Assert.Equal("Feed", merged[0].Name);
        Assert.False(merged[0].Local);
        Assert.Null(merged[0].Password);
        Assert.Equal("Practice", merged[1].Name);
        Assert.True(merged[1].Local);
    }

    [Fact]
    public async Task Update_replaces_a_read_only_workshop_file()
    {
        using var directory = new TempTree();
        var destination = Path.Combine(directory.Path, "SubModule.xml");
        var partial = destination + ".ci-partial";
        File.WriteAllText(destination, "old");
        File.SetAttributes(destination, FileAttributes.ReadOnly);
        File.WriteAllText(partial, "new");

        await InstalledFile.ReplaceAsync(partial, destination, CancellationToken.None);

        Assert.Equal("new", File.ReadAllText(destination));
        Assert.False(File.GetAttributes(destination).HasFlag(FileAttributes.ReadOnly));
        Assert.Contains("TaleWorlds.MountAndBlade.Launcher", GameLauncher.ProcessNames);
    }

    [Fact]
    public void A_server_address_can_include_the_port()
    {
        Assert.True(ServerCatalog.TryCreate("Practice", "203.0.113.10:7211", "", null, out var server, out var error));
        Assert.Null(error);
        Assert.NotNull(server);
        Assert.Equal("203.0.113.10", server.Address);
        Assert.Equal(7211, server.Port);
        Assert.True(ServerEndpoint.SameHost("203.0.113.10:7211", "203.0.113.10"));

        Assert.False(ServerCatalog.TryCreate("Practice", "203.0.113.10:7211", "7210", null, out _, out error));
        Assert.Equal("The address and the port don't match.", error);
        Assert.False(ServerCatalog.TryCreate("Practice", "http://203.0.113.10", "7210", null, out _, out _));
    }

    private sealed class TempTree : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ci-launcher-" + Guid.NewGuid().ToString("N"));

        public TempTree() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
    }

    private sealed class FeedHandler : HttpMessageHandler
    {
        private readonly string _root;
        public bool SawRange { get; private set; }

        public FeedHandler(string root) => _root = root;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var relative = request.RequestUri!.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var file = Path.Combine(_root, relative);
            if (!File.Exists(file))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            var bytes = File.ReadAllBytes(file);
            var status = HttpStatusCode.OK;
            if (request.Headers.Range?.Ranges.FirstOrDefault() is { } range && range.From is long start && start > 0 && start < bytes.Length)
            {
                SawRange = true;
                status = HttpStatusCode.PartialContent;
                bytes = bytes[(int)start..];
            }

            var response = new HttpResponseMessage(status)
            {
                Content = new ByteArrayContent(bytes)
            };
            response.Content.Headers.ContentLength = bytes.Length;
            return Task.FromResult(response);
        }
    }
}
