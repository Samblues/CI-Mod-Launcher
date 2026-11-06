Publishing this so you can run your AI stuff and find out whats its doing for trustworthy points.

If you're looking at it to use as your own launcher with your own tweaks feel free to fork the codebase or copy/use it just note you'll need to change:

1) You'll need a Feed for your modpack location (Like cloudflare R2), new admin pass, new secrets etc for your feed to upload locally.
2) Change of all the CI logos, CI workshop folder, Links to the website and Links to the workshop file
3) The module thats built will need to be for your mod if you want to link them into your mods sln's.

**DOTNET 4.8 is used not 4.9 because it flags false positive if you use icons or atleast did for us.**

- If you need help or advice ask, im [CI] Sam. 

# CI Modpack Launcher

Windows launcher for a Mount & Blade II: Bannerlord modpack. It reads a manifest you supply, downloads the listed files, and starts the game.

No feed address is built into this copy. In Settings, set **Feed** to the HTTPS address of your `manifest.json`. A `servers.json` file beside that manifest fills the server list. Until a feed is set, the launcher does not download anything.

## Build

You need the .NET SDK and the .NET Framework 4.8 developer pack.

```powershell
dotnet build CILauncher\CILauncher.csproj -c Release
```

The program is `CILauncher\bin\Release\net48\CIModpackLauncher.exe`. `publish.ps1` copies it to `CILauncher\bin\publish`.

## Tests

```powershell
dotnet test CILauncher.Core.Tests\CILauncher.Core.Tests.csproj -c Release
```

Add a license before you publish this folder.
