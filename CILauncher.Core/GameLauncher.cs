using System.Diagnostics;

namespace CILauncher.Core;

public static class GameLauncher
{
    public const string ClientExecutable = "bin/Win64_Shipping_Client/Bannerlord.exe";

    /// <summary>
    /// Steam's Stop button is the official launcher, not Bannerlord.exe. That process and its
    /// Watchdog keep the Workshop folder open until both are gone.
    /// </summary>
    public static readonly string[] ProcessNames =
    [
        "Bannerlord",
        "Bannerlord.Native",
        "Bannerlord.exe",
        "Bannerlord.Native.exe",
        "TaleWorlds.MountAndBlade.Launcher",
        "Watchdog"
    ];

    public static bool IsGameProcessName(string? name)
        => name is "Bannerlord" or "Bannerlord.Native" or "TaleWorlds.MountAndBlade.Launcher";

    public static Process Launch(string gamePath, LaunchSpec launch, DirectJoinRequest? join = null)
    {
        ManifestValidator.ValidateLaunch(launch);
        if (IsBannerlordRunning())
            throw new InvalidOperationException("Close Bannerlord before starting it again.");

        var plan = GameLocator.CreatePlan(gamePath);
        var start = StartInfo(plan, launch.Arguments);
        if (join == null)
            ClearJoin(gamePath);
        else
        {
            DirectJoin.Write(join);
            MirrorJoin(gamePath);
        }

        try
        {
            var process = Process.Start(start);
            if (process == null)
                throw new InvalidOperationException("The system didn't start Bannerlord.");
            return process;
        }
        catch
        {
            if (join != null)
                ClearJoin(gamePath);
            throw;
        }
    }

    private static ProcessStartInfo StartInfo(GameLaunchPlan plan, string arguments)
    {
        var workingDirectory = Path.GetDirectoryName(plan.Executable)!;
        ProcessStartInfo start = plan.Kind switch
        {
            GameLaunchKind.Proton => ProtonStart(plan, arguments, workingDirectory),
            GameLaunchKind.SteamAppLaunch => SteamStart(plan, arguments),
            GameLaunchKind.Wine => WineStart(plan, arguments, workingDirectory),
            _ => new ProcessStartInfo(plan.Executable)
            {
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false
            }
        };
        start.Environment["CI_LAUNCHER_CHECKED"] = "1";
        return start;
    }

    private static void AddArg(ProcessStartInfo start, string value)
    {
        if (value.IndexOfAny(new[] { ' ', '"' }) >= 0)
            value = "\"" + value.Replace("\"", "\\\"") + "\"";
        start.Arguments = string.IsNullOrEmpty(start.Arguments) ? value : start.Arguments + " " + value;
    }

    private static ProcessStartInfo ProtonStart(GameLaunchPlan plan, string arguments, string workingDirectory)
    {
        var start = new ProcessStartInfo(plan.Host!)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };
        AddArg(start, "run");
        AddArg(start, plan.Executable);
        if (!string.IsNullOrWhiteSpace(arguments))
            AddArg(start, arguments);
        start.Environment["STEAM_COMPAT_DATA_PATH"] = plan.CompatData!;
        start.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = plan.SteamRoot!;
        start.Environment["SteamGameId"] = GameLocator.BannerlordAppId;
        return start;
    }

    private static ProcessStartInfo SteamStart(GameLaunchPlan plan, string arguments)
    {
        var start = new ProcessStartInfo(plan.Host!)
        {
            UseShellExecute = false
        };
        AddArg(start, "-applaunch");
        AddArg(start, GameLocator.BannerlordAppId);
        if (!string.IsNullOrWhiteSpace(arguments))
            AddArg(start, arguments);
        return start;
    }

    private static ProcessStartInfo WineStart(GameLaunchPlan plan, string arguments, string workingDirectory)
    {
        var start = new ProcessStartInfo(plan.Host!)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };
        if (!string.IsNullOrWhiteSpace(plan.Bottle))
        {
            AddArg(start, "--bottle");
            AddArg(start, plan.Bottle);
        }

        AddArg(start, plan.Executable);
        if (!string.IsNullOrWhiteSpace(arguments))
            AddArg(start, arguments);
        return start;
    }

    private static void MirrorJoin(string gamePath)
    {
        if (!File.Exists(LauncherPaths.JoinRequestPath))
            return;
        CopyJoin(GameLocator.ProtonLocalAppData(gamePath));
        CopyJoin(GameLocator.MacBottleLocalAppData(gamePath));
    }

    private static void CopyJoin(string? localAppData)
    {
        if (string.IsNullOrWhiteSpace(localAppData))
            return;
        try
        {
            var directory = Path.Combine(localAppData, "CILauncher");
            Directory.CreateDirectory(directory);
            File.Copy(LauncherPaths.JoinRequestPath, Path.Combine(directory, "join.json"), overwrite: true);
        }
        catch
        {
            // The host copy is still there. Play continues.
        }
    }

    private static void ClearJoin(string gamePath)
    {
        DirectJoin.Clear();
        DeleteJoin(GameLocator.ProtonLocalAppData(gamePath));
        DeleteJoin(GameLocator.MacBottleLocalAppData(gamePath));
    }

    private static void DeleteJoin(string? localAppData)
    {
        if (string.IsNullOrWhiteSpace(localAppData))
            return;
        try
        {
            var path = Path.Combine(localAppData, "CILauncher", "join.json");
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // A locked prefix file must not block the next launch.
        }
    }

    public static bool IsBannerlordRunning()
    {
        foreach (var name in ProcessNames)
        {
            Process[] found;
            try
            {
                found = Process.GetProcessesByName(name);
            }
            catch
            {
                continue;
            }

            var alive = false;
            foreach (var process in found)
            {
                try
                {
                    alive |= IsGameProcess(process);
                }
                catch
                {
                    alive = true;
                }
                finally
                {
                    process.Dispose();
                }
            }

            if (alive)
                return true;
        }

        return false;
    }

    public static async Task CloseGameAsync(CancellationToken cancellationToken)
    {
        if (!IsBannerlordRunning())
            return;

        RequestClose();
        if (!await WaitUntilClosedAsync(TimeSpan.FromSeconds(8), cancellationToken).ConfigureAwait(false))
        {
            foreach (var process in EnumerateGameProcesses())
            {
                try
                {
                    if (IsGameProcess(process))
                        process.Kill();
                }
                catch
                {
                    // A protected process is reported below if it is still there.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        if (!await WaitUntilClosedAsync(TimeSpan.FromSeconds(8), cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Bannerlord is still open in Steam. Close it, then update again.");
    }

    private static void RequestClose()
    {
        foreach (var process in EnumerateGameProcesses())
        {
            try
            {
                if (IsGameProcess(process))
                    process.CloseMainWindow();
            }
            catch
            {
                // Kill follows if the window does not close.
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static async Task<bool> WaitUntilClosedAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (IsBannerlordRunning())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTime.UtcNow >= deadline)
                return false;
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private static List<Process> EnumerateGameProcesses()
    {
        var processes = new List<Process>();
        foreach (var name in ProcessNames)
        {
            try
            {
                processes.AddRange(Process.GetProcessesByName(name));
            }
            catch
            {
                // Skip a name we cannot enumerate.
            }
        }

        return processes;
    }

    private static bool IsGameProcess(Process process)
    {
        try
        {
            if (process.HasExited)
                return false;
        }
        catch
        {
            return false;
        }

        var name = process.ProcessName;
        if (name is "Bannerlord" or "Bannerlord.Native" or "Bannerlord.exe" or "Bannerlord.Native.exe" or "TaleWorlds.MountAndBlade.Launcher")
            return true;
        if (!string.Equals(name, "Watchdog", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var path = process.MainModule?.FileName;
            return path != null && path.Contains("Bannerlord", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
