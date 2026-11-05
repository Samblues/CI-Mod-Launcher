using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CILauncher.Core;

namespace CILauncher;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.Browse = BrowseAsync;
        _viewModel.EditSettings = EditSettingsAsync;
        SettingsPane.Saved += async (_, _) =>
        {
            _viewModel.HideSettingsPage();
            await _viewModel.SettingsClosedAsync(true);
        };
        SettingsPane.Dismissed += (_, _) => _viewModel.HideSettingsPage();
        SettingsPane.ErrorChanged += (_, _) => SettingsFooterError.Text = SettingsPane.FormError;
        Loaded += async (_, _) => await _viewModel.StartAsync();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SettingsPane.CancelWork();
        _viewModel.Cancel();
        base.OnClosing(e);
    }

    private void OnHeaderPressed(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource as DependencyObject))
            return;
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is System.Windows.Controls.Button)
                return true;
            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private async Task BrowseAsync()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = UiText.S.Get("selectSteam"),
            ShowNewFolderButton = false
        };
        var hint = _viewModel.SuggestedSteamFolder;
        if (!string.IsNullOrWhiteSpace(hint) && System.IO.Directory.Exists(hint))
            dialog.SelectedPath = hint;
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;
        if (!string.IsNullOrWhiteSpace(dialog.SelectedPath))
            await _viewModel.SetSteamDirectoryAsync(dialog.SelectedPath);
    }

    private void OnOpenSite(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://coronasibericas.com/",
            UseShellExecute = true
        });
    }

    private void OnOpenWorkshop(object sender, MouseButtonEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://steamcommunity.com/sharedfiles/filedetails/?id=" + GameLocator.WorkshopModpackItemId,
            UseShellExecute = true
        });
    }

    private void OnSettingsCancel(object sender, RoutedEventArgs e) => SettingsPane.Dismiss();

    private void OnSettingsSave(object sender, RoutedEventArgs e) => SettingsPane.Commit();

    private Task EditSettingsAsync()
    {
        if (_viewModel.ShowSettings)
            return Task.CompletedTask;
        SettingsPane.Load(_viewModel.Settings);
        _viewModel.ShowSettingsPage();
        return Task.CompletedTask;
    }
}
