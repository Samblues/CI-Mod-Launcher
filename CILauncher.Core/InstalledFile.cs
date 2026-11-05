namespace CILauncher.Core;

public static class InstalledFile
{
    public static async Task ReplaceAsync(string partial, string destination, CancellationToken cancellationToken)
    {
        MakeWritable(destination);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                IoCompat.MoveReplace(partial, destination);
                return;
            }
            catch (Exception ex) when (attempt < 10 && IsLocked(ex))
            {
                await Task.Delay(400, cancellationToken).ConfigureAwait(false);
                MakeWritable(destination);
            }
        }
    }

    public static void Delete(string path)
    {
        if (!File.Exists(path))
            return;

        MakeWritable(path);
        File.Delete(path);
    }

    public static bool IsLocked(Exception exception)
    {
        const int accessDenied = unchecked((int)0x80070005);
        const int sharingViolation = unchecked((int)0x80070020);
        const int lockViolation = unchecked((int)0x80070021);
        if (exception is UnauthorizedAccessException)
            return true;
        if (exception.HResult is accessDenied or sharingViolation or lockViolation)
            return true;
        return exception.Message.Contains("being used", StringComparison.OrdinalIgnoreCase)
               || exception.Message.Contains("used by another process", StringComparison.OrdinalIgnoreCase);
    }

    private static void MakeWritable(string path)
    {
        if (!File.Exists(path))
            return;

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) == 0)
            return;
        File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
    }
}
