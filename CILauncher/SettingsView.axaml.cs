using System.Windows;
using CILauncher.Core;

namespace CILauncher;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    private LauncherSettings _settings = new();

    public string FormError { get; private set; } = "";

    public event EventHandler? Saved;
    public event EventHandler? Dismissed;
    public event EventHandler? ErrorChanged;

    public SettingsView()
    {
        InitializeComponent();
        UiText.S.PropertyChanged += (_, _) => ApplyLanguage();
    }

    private void OnThreadsChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThreadsLabel != null)
            ThreadsLabel.Text = LabelFor(e.NewValue);
    }

    private void ApplyLanguage()
    {
        if (ThreadsLabel != null)
            ThreadsLabel.Text = LabelFor(Threads.Value);
    }

    public void Load(LauncherSettings settings)
    {
        _settings = settings;
        SteamBox.Text = settings.SteamPath ?? settings.GamePath ?? "";
        FeedBox.Text = settings.ManifestUrl ?? "";
        var threads = settings.DownloadConcurrency;
        if (threads < 1)
            threads = 1;
        if (threads > 16)
            threads = 16;
        Threads.Value = threads;
        OnlineCheck.IsChecked = settings.RequireOnlineCheck;
        ThreadsLabel.Text = LabelFor(Threads.Value);
        SetError("");
    }

    public void CancelWork()
    {
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = UiText.S.Get("selectSteam"),
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;
        if (!string.IsNullOrWhiteSpace(dialog.SelectedPath))
            SteamBox.Text = dialog.SelectedPath;
    }

    public void Dismiss() => Dismissed?.Invoke(this, EventArgs.Empty);

    public void Commit()
    {
        var feed = FeedBox.Text?.Trim();
        if (string.IsNullOrEmpty(feed))
        {
            _settings.ManifestUrl = null;
        }
        else
        {
            try
            {
                FeedAddress.Parse(feed);
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
                return;
            }

            _settings.ManifestUrl = feed;
        }

        var steam = SteamBox.Text?.Trim();
        if (string.IsNullOrEmpty(steam))
        {
            _settings.SteamPath = null;
            _settings.GamePath = null;
        }
        else
        {
            var located = GameLocator.Locate(steam);
            if (!located.Ok || located.Location == null)
            {
                SetError(UiText.S.LocateError(located.Error));
                return;
            }

            _settings.SteamPath = located.Location.SteamPath ?? steam;
            _settings.GamePath = located.Location.GamePath;
        }

        _settings.DownloadConcurrency = (int)Math.Round(Threads.Value);
        _settings.RequireOnlineCheck = OnlineCheck.IsChecked == true;
        Saved?.Invoke(this, EventArgs.Empty);
    }

    private void SetError(string message)
    {
        FormError = message;
        ErrorChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string LabelFor(double value) => UiText.S.Format("parallel", (int)Math.Round(value));
}
