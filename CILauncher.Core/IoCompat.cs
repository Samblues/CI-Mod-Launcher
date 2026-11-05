using System.Security.Cryptography;
using System.Text;

namespace CILauncher.Core;

internal static class IoCompat
{
    public static void MoveReplace(string source, string destination)
    {
        if (File.Exists(destination))
            File.Delete(destination);
        File.Move(source, destination);
    }

    public static string Relative(string root, string path)
    {
        var from = Path.GetFullPath(root);
        if (!from.EndsWith(Path.DirectorySeparatorChar.ToString()))
            from += Path.DirectorySeparatorChar;
        var to = Path.GetFullPath(path);
        if (to.StartsWith(from, StringComparison.OrdinalIgnoreCase))
            return to.Substring(from.Length);
        return to;
    }

    public static IEnumerable<string> Walk(string directory, bool ignoreInaccessible)
    {
        var stack = new Stack<string>();
        stack.Push(directory);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            string[] files;
            string[] dirs;
            try
            {
                files = Directory.GetFiles(current);
                dirs = Directory.GetDirectories(current);
            }
            catch (Exception) when (ignoreInaccessible)
            {
                continue;
            }

            foreach (var file in files)
                yield return file;
            foreach (var dir in dirs)
            {
                try
                {
                    if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0)
                        continue;
                }
                catch (Exception) when (ignoreInaccessible)
                {
                    continue;
                }

                stack.Push(dir);
            }
        }
    }

    public static string ToHex(byte[] data)
    {
        var chars = new char[data.Length * 2];
        for (var i = 0; i < data.Length; i++)
        {
            var value = data[i];
            chars[i * 2] = "0123456789ABCDEF"[value >> 4];
            chars[i * 2 + 1] = "0123456789ABCDEF"[value & 0xF];
        }

        return new string(chars);
    }

    public static byte[] FromHex(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }

    public static byte[] Sha256(byte[] data)
    {
        using var hasher = SHA256.Create();
        return hasher.ComputeHash(data);
    }

    public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

    public static string? ProcessPath()
    {
        try
        {
            return System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    public static Task<string> ReadTextAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.ReadAllText(path, new UTF8Encoding(false)));
    }

    public static Task WriteTextAsync(string path, string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.WriteAllText(path, text, new UTF8Encoding(false));
        return Task.CompletedTask;
    }
}
