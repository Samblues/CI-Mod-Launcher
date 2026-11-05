using System.Net;
using System.Text.Json.Serialization;

namespace CILauncher.Core;

public sealed class ServerEntry
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public int Port { get; set; }
    public string? GameType { get; set; }
    public string? Map { get; set; }
    public int? Players { get; set; }
    public int? MaxPlayers { get; set; }
    public string? Password { get; set; }

    [JsonIgnore]
    public bool Local { get; set; }

    [JsonIgnore]
    public string Endpoint => Address + ":" + Port;

    public bool Online { get; set; }
    public bool Live { get; set; }

    [JsonIgnore]
    public string SourceLabel => Local ? "ADDED" : Live ? Online ? "LIVE" : "OFFLINE" : "FEED";

    [JsonIgnore]
    public string Meta
    {
        get
        {
            var parts = new List<string>();
            if (Live)
                parts.Add(Online ? "Online" : "Offline");
            if (!string.IsNullOrWhiteSpace(GameType))
                parts.Add(GameType.Trim());
            if (!string.IsNullOrWhiteSpace(Map))
                parts.Add(Map.Trim());
            if (Players is int players)
                parts.Add(MaxPlayers is int max ? players + " / " + max : players + " playing");
            return string.Join("   ", parts);
        }
    }

    [JsonIgnore]
    public bool HasMeta => Meta.Length > 0;
}

public sealed class ServerListDocument
{
    public List<ServerEntry> Servers { get; set; } = new();
}

public sealed class DirectJoinRequest
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public int Port { get; set; }
    public string? Password { get; set; }
}

public static class ServerEndpoint
{
    public static bool SameHost(string? registered, string? wanted)
    {
        var left = HostOnly(registered);
        var right = HostOnly(wanted);
        return left.Length > 0 && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    public static string HostOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var host = value.Trim().TrimEnd('.');
        if (host.StartsWith('[') && host.EndsWith(']') && host.Length > 2)
            host = host[1..^1];

        var colon = host.LastIndexOf(':');
        if (colon > 0 && host.IndexOf(':') == colon && int.TryParse(host[(colon + 1)..], out _))
            host = host[..colon];

        return host.Trim();
    }

    public static bool IsValidHost(string? host)
    {
        host = HostOnly(host);
        if (host.Length is < 1 or > 253 || host.Contains("..", StringComparison.Ordinal))
            return false;

        foreach (var label in host.Split('.'))
        {
            if (label.Length is < 1 or > 63)
                return false;
            if (label[0] == '-' || label[^1] == '-')
                return false;
            foreach (var character in label)
            {
                if (character is not ((>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_'))
                    return false;
            }
        }

        return true;
    }

    public static bool TrySplit(string? text, out string host, out int port)
    {
        host = "";
        port = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        var colon = trimmed.LastIndexOf(':');
        if (colon > 0 && trimmed.IndexOf(':') == colon && int.TryParse(trimmed[(colon + 1)..], out var parsed) && parsed is >= 1 and <= 65535)
        {
            host = HostOnly(trimmed[..colon]);
            port = parsed;
            return IsValidHost(host);
        }

        host = HostOnly(trimmed);
        return IsValidHost(host);
    }
}

public static class ServerCatalog
{
    public const int MaxServers = 100;
    public const int MaxNameLength = 80;
    public const int MaxDocumentChars = 65_536;

    public static Uri ListUri(Uri manifestUri) => new(manifestUri, "servers.json");

    public static async Task<IReadOnlyList<ServerEntry>> FetchAsync(HttpClient http, Uri manifestUri, CancellationToken cancellationToken)
    {
        var uri = ListUri(manifestUri);
        FeedAddress.EnsureAllowed(uri);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return Array.Empty<ServerEntry>();

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        return ParseFeed(json);
    }

    public static IReadOnlyList<ServerEntry> ParseFeed(string json)
    {
        if (json.Length > MaxDocumentChars)
            throw new InvalidOperationException("The server list is larger than 64 KB.");

        ServerListDocument? document;
        try
        {
            document = System.Text.Json.JsonSerializer.Deserialize<ServerListDocument>(json, ManifestJson.Options);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new InvalidOperationException("The server list is not valid JSON. " + ex.Message);
        }

        return Clean(document?.Servers, local: false);
    }

    public static List<ServerEntry> LoadLocal()
    {
        try
        {
            var path = LauncherPaths.ServersPath;
            if (!File.Exists(path))
                return new List<ServerEntry>();
            var json = File.ReadAllText(path);
            if (json.Length > MaxDocumentChars)
                return new List<ServerEntry>();
            var document = System.Text.Json.JsonSerializer.Deserialize<ServerListDocument>(json, ManifestJson.Options);
            return Clean(document?.Servers, local: true).ToList();
        }
        catch
        {
            return new List<ServerEntry>();
        }
    }

    public static void SaveLocal(IEnumerable<ServerEntry> servers)
    {
        var document = new ServerListDocument
        {
            Servers = servers.Where(server => server.Local).Take(MaxServers).Select(server => Copy(server)).ToList()
        };
        Directory.CreateDirectory(LauncherPaths.Root);
        var path = LauncherPaths.ServersPath;
        var temp = path + ".tmp";
        File.WriteAllText(temp, System.Text.Json.JsonSerializer.Serialize(document, ManifestJson.Options));
        IoCompat.MoveReplace(temp, path);
    }

    public static IReadOnlyList<ServerEntry> Merge(IEnumerable<ServerEntry> remote, IEnumerable<ServerEntry> local)
    {
        var merged = new List<ServerEntry>();
        foreach (var server in remote)
        {
            if (merged.Count >= MaxServers || !IsListed(server))
                continue;
            if (merged.Any(existing => Same(existing, server)))
                continue;
            merged.Add(Copy(server, local: false));
        }

        foreach (var server in local)
        {
            if (merged.Count >= MaxServers || !IsListed(server))
                continue;
            if (merged.Any(existing => Same(existing, server)))
                continue;
            merged.Add(Copy(server, local: true));
        }

        return merged;
    }

    public static bool TryCreate(string? name, string? address, string? portText, string? password, out ServerEntry? server, out string? error)
    {
        server = null;
        error = null;
        var trimmedName = Collapse(name);
        if (trimmedName.Length is < 1 or > MaxNameLength)
        {
            error = "Give the server a name, up to 80 characters.";
            return false;
        }

        if (!ServerEndpoint.TrySplit(address, out var host, out var embeddedPort))
        {
            error = "Enter the server address, like 1.2.3.4 or 1.2.3.4:7210.";
            return false;
        }

        int port;
        if (string.IsNullOrWhiteSpace(portText))
        {
            if (embeddedPort == 0)
            {
                error = "Enter the server port.";
                return false;
            }

            port = embeddedPort;
        }
        else if (!int.TryParse(portText.Trim(), out port) || port is < 1 or > 65535)
        {
            error = "The port has to be a number from 1 to 65535.";
            return false;
        }
        else if (embeddedPort != 0 && embeddedPort != port)
        {
            error = "The address and the port don't match.";
            return false;
        }

        if (!string.IsNullOrEmpty(password) && password.Length > 64)
        {
            error = "The password is too long.";
            return false;
        }

        server = new ServerEntry
        {
            Name = trimmedName,
            Address = host,
            Port = port,
            Password = string.IsNullOrEmpty(password) ? null : password,
            Local = true
        };
        return true;
    }

    public static bool Same(ServerEntry left, ServerEntry right)
        => left.Port == right.Port && ServerEndpoint.SameHost(left.Address, right.Address);

    private static List<ServerEntry> Clean(List<ServerEntry>? servers, bool local)
    {
        var cleaned = new List<ServerEntry>();
        if (servers == null)
            return cleaned;

        foreach (var server in servers)
        {
            if (server == null)
                continue;
            if (ServerEndpoint.TrySplit(server.Address, out var host, out var embedded))
            {
                server.Address = host;
                if (server.Port == 0)
                    server.Port = embedded;
            }

            if (cleaned.Count >= MaxServers || !IsListed(server))
                continue;
            if (!local)
                server.Password = null;
            server.Local = local;
            if (string.IsNullOrWhiteSpace(server.Name))
                server.Name = server.Address;
            server.Name = Collapse(server.Name);
            server.Address = ServerEndpoint.HostOnly(server.Address);
            if (cleaned.Any(existing => Same(existing, server)))
                continue;
            cleaned.Add(server);
        }

        return cleaned;
    }

    private static bool IsListed(ServerEntry? server)
        => server != null
           && server.Port is >= 1 and <= 65535
           && ServerEndpoint.IsValidHost(server.Address)
           && Collapse(server.Name).Length <= MaxNameLength;

    private static ServerEntry Copy(ServerEntry server, bool? local = null) => new()
    {
        Name = server.Name,
        Address = server.Address,
        Port = server.Port,
        GameType = server.GameType,
        Map = server.Map,
        Players = server.Players,
        MaxPlayers = server.MaxPlayers,
        Online = server.Online,
        Live = server.Live,
        Password = server.Password,
        Local = local ?? server.Local
    };

    private static string Collapse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts);
    }
}

public static class DirectJoin
{
    public static void Write(DirectJoinRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > ServerCatalog.MaxNameLength)
            throw new InvalidOperationException("That server name is not valid.");
        if (!ServerEndpoint.IsValidHost(request.Address) || request.Port is < 1 or > 65535)
            throw new InvalidOperationException("That server address is not valid.");
        if (request.Password is { Length: > 64 })
            throw new InvalidOperationException("That server password is too long.");

        Directory.CreateDirectory(LauncherPaths.Root);
        var path = LauncherPaths.JoinRequestPath;
        var temp = path + ".tmp";
        File.WriteAllText(temp, System.Text.Json.JsonSerializer.Serialize(request, ManifestJson.Options));
        IoCompat.MoveReplace(temp, path);
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(LauncherPaths.JoinRequestPath))
                File.Delete(LauncherPaths.JoinRequestPath);
        }
        catch
        {
            // Play still starts if a previous join file is locked.
        }
    }
}
