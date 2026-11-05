namespace CILauncher.Core;

/// <summary>
/// The launcher shipped inside the modpack, next to SubModule.xml, so a Workshop upload
/// can start it without a separate install. The feed does not manage this folder.
/// </summary>
public static class ModpackLauncher
{
    public const string FolderName = "Launcher";
    public const string FileName = "CIModpackLauncher.exe";

    public static bool IsBundledPath(string relativePath)
    {
        var path = relativePath.Replace('\\', '/').Trim('/');
        return path.Equals(FolderName, StringComparison.OrdinalIgnoreCase)
               || path.StartsWith(FolderName + "/", StringComparison.OrdinalIgnoreCase)
               || path.Contains("/" + FolderName + "/", StringComparison.OrdinalIgnoreCase);
    }
}
