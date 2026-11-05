using System.Text.RegularExpressions;

namespace CILauncher.Core;

public static partial class LaunchSelection
{
    public static bool IsSafeId(string? id)
        => !string.IsNullOrWhiteSpace(id) && ModuleIdPattern.IsMatch(id);

    /// <summary>
    /// Native, Multiplayer and CI Modpack are always loaded. Extra ids are the mods the player ticked.
    /// </summary>
    public static string Build(IEnumerable<string>? extraModuleIds)
    {
        var ids = new List<string> { GameLocator.ModpackModuleId };
        if (extraModuleIds != null)
        {
            foreach (var raw in extraModuleIds)
            {
                var id = raw?.Trim();
                if (!IsSafeId(id))
                    continue;
                if (id!.Equals("Native", StringComparison.OrdinalIgnoreCase)
                    || id.Equals("Multiplayer", StringComparison.OrdinalIgnoreCase)
                    || id.Equals(GameLocator.ModpackModuleId, StringComparison.OrdinalIgnoreCase))
                    continue;
                ids.Add(id);
            }
        }

        return ModpackPublisher.BuildLaunchArguments(ids);
    }

    private static readonly Regex ModuleIdPattern = new("^[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
}
