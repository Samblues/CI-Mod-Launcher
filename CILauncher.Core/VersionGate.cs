namespace CILauncher.Core;

public enum PlayBlock
{
    None,
    NoFeed,
    NoGame,
    Checking,
    NotInstalled,
    UpdateRequired,
    FilesChanged,
    GameVersionMismatch,
    Offline,
    Busy,
    AlreadyRunning
}

public sealed record GateDecision(bool CanPlay, PlayBlock Block, string Headline, string Detail);

public sealed record GateInput
{
    public bool FeedConfigured { get; init; }
    public bool GameFound { get; init; }
    public bool CheckCompleted { get; init; }
    public bool CheckSucceeded { get; init; }
    public string? CheckError { get; init; }
    public bool RequireOnlineCheck { get; init; } = true;
    public string? InstalledVersion { get; init; }
    public string? RemoteVersion { get; init; }
    public bool? FilesIntact { get; init; }
    public string? InstalledGameVersion { get; init; }
    public string? RequiredGameVersion { get; init; }
    public bool Busy { get; init; }
    public bool GameRunning { get; init; }
    public bool JustLaunched { get; init; }
}

public static class VersionGate
{
    public static GateDecision Decide(GateInput input)
    {
        if (input.Busy)
            return new GateDecision(false, PlayBlock.Busy, "Working", "Hang on a moment.");
        if (!input.GameFound)
            return new GateDecision(false, PlayBlock.NoGame, "Select your Steam folder", "We'll find Bannerlord and unpack CI Modpack into its Modules folder, or into the Steam Workshop folder if you already have it there.");
        if (!input.FeedConfigured)
            return new GateDecision(false, PlayBlock.NoFeed, "Modpack feed is missing", "This build has no modpack address.");
        if (!input.CheckCompleted)
            return new GateDecision(false, PlayBlock.Checking, "Checking the live modpack", "Comparing this PC with the current version.");
        if (!input.CheckSucceeded)
        {
            if (input.RequireOnlineCheck || string.IsNullOrEmpty(input.InstalledVersion))
                return new GateDecision(false, PlayBlock.Offline, "Can't reach the modpack feed", input.CheckError ?? "Check the connection and try again.");
            if (input.FilesIntact == false)
                return new GateDecision(false, PlayBlock.FilesChanged, "Modpack files changed", "The feed didn't answer, and the local files don't match the last install.");
            return new GateDecision(true, PlayBlock.None, "Ready to play", "The feed didn't answer. This PC is still on " + input.InstalledVersion + ".");
        }

        if (string.IsNullOrEmpty(input.InstalledVersion))
            return new GateDecision(false, PlayBlock.NotInstalled, "Modpack not installed", "Download " + input.RemoteVersion + " into the Modules folder, or into the Steam Workshop folder if CI Modpack is already there.");
        if (!string.Equals(input.InstalledVersion, input.RemoteVersion, StringComparison.Ordinal))
            return new GateDecision(false, PlayBlock.UpdateRequired, "Update required", "You have " + input.InstalledVersion + ". Live is " + input.RemoteVersion + ". Update before you join.");
        if (input.FilesIntact == false)
            return new GateDecision(false, PlayBlock.FilesChanged, "Files don't match", "Something in the modpack folder changed. Repair it before you join.");

        if (input.GameRunning && input.JustLaunched)
            return new GateDecision(false, PlayBlock.AlreadyRunning, "Bannerlord is starting", "Join from the multiplayer list. Close the game before updating the modpack.");
        if (input.GameRunning)
            return new GateDecision(false, PlayBlock.AlreadyRunning, "Bannerlord is open", "Close it before launching again.");
        return new GateDecision(true, PlayBlock.None, "Ready to play", "Your modpack matches " + input.RemoteVersion + ". You can join.");
    }
}
