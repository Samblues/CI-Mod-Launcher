using System.Globalization;
using System.Windows.Data;
using CILauncher.Core;

namespace CILauncher;

public sealed class ServerBadgeConverter : IMultiValueConverter
{
    public static ServerBadgeConverter Instance { get; } = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 3)
            return "";
        var local = values[0] is true;
        var live = values[1] is true;
        var online = values[2] is true;
        if (local)
            return UiText.S.Get("added");
        if (live)
            return online ? UiText.S.Live : UiText.S.Get("offlineBadge");
        return UiText.S.Get("feedBadge");
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class ServerMetaConverter : IMultiValueConverter
{
    public static ServerMetaConverter Instance { get; } = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length == 0 || values[0] is not ServerEntry server)
            return "";
        var parts = new List<string>();
        if (server.Live)
            parts.Add(server.Online ? UiText.S.Get("onlineWord") : UiText.S.Get("offlineWord"));
        if (!string.IsNullOrWhiteSpace(server.GameType))
            parts.Add(server.GameType.Trim());
        if (!string.IsNullOrWhiteSpace(server.Map))
            parts.Add(server.Map.Trim());
        if (server.Players is int players)
            parts.Add(server.MaxPlayers is int max ? players + " / " + max : UiText.S.Format("playing", players));
        return string.Join("   ", parts);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
