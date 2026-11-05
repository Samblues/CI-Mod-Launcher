namespace CILauncher.Core;

public static class SafePaths
{
    public static string NormalizeModulePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ManifestException("A modpack file has an empty path.");

        var slash = path.Replace('\\', '/').Trim();
        if (slash.StartsWith('/') || Path.IsPathRooted(path))
            throw new ManifestException($"Path must stay inside Modules: {path}");

        var parts = slash.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(part => part is "." or ".."))
            throw new ManifestException($"Path must stay inside Modules: {path}");
        if (parts.Length < 3 || !parts[0].Equals("Modules", StringComparison.OrdinalIgnoreCase))
            throw new ManifestException($"Path must be a file under Modules/<module>/: {path}");

        foreach (var part in parts)
        {
            if (part.IndexOfAny(InvalidNameChars) >= 0)
                throw new ManifestException($"Path has invalid characters: {path}");
        }

        return "Modules/" + string.Join("/", parts.Skip(1));
    }

    public static string NormalizeModuleDirectory(string path)
    {
        var slash = path.Replace('\\', '/').Trim().Trim('/');
        var parts = slash.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2
            || !parts[0].Equals("Modules", StringComparison.OrdinalIgnoreCase)
            || parts.Any(part => part is "." or "..")
            || parts.Any(part => part.IndexOfAny(InvalidNameChars) >= 0))
        {
            throw new ManifestException($"Destination must look like Modules/CI_EVENT, not '{path}'.");
        }

        return "Modules/" + parts[1];
    }

    public static string CombineUnderRoot(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root);
        var combined = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to write outside the game folder.");
        return combined;
    }

    public static string ModuleRoot(string normalizedPath)
    {
        var parts = normalizedPath.Split('/');
        return parts[0] + "/" + parts[1];
    }

    private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars();
}
