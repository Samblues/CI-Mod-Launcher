using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows.Input;
using CILauncher.Core;

namespace CILauncher;

public sealed class MainWindowViewModel : Observable
{
    private readonly HttpClient _http;
    private readonly ManifestClient _manifests;
    private readonly ModpackInstaller _installer;
    private CancellationTokenSource? _operation;
    private int _running;
    private UiAction _action = UiAction.None;
    private ModpackManifest? _manifest;
    private Uri? _manifestUri;
    private InstallRecord? _record;
    private bool _checkCompleted;
    private bool _checkSucceeded;
    private string? _checkError;
    private bool? _filesIntact;
    private bool _justLaunched;

    public MainWindowViewModel()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("CIModpackLauncher/1.0");
        _http = http;
        _manifests = new ManifestClient(http);
        _installer = new ModpackInstaller(http);
        PrimaryCommand = new AsyncCommand(PrimaryAsync);
        SettingsCommand = new AsyncCommand(OpenSettingsAsync);
        SetLanguageCommand = new AsyncCommand(parameter =>
        {
            SetLanguage(parameter as string);
            return Task.CompletedTask;
        });
        BrowseCommand = new AsyncCommand(OpenBrowseAsync);
        ShowModpackCommand = new AsyncCommand(() => SelectTabAsync(false));
        ShowServersCommand = new AsyncCommand(() => SelectTabAsync(true));
        RefreshServersCommand = new AsyncCommand(RefreshServersAsync);
        AddServerCommand = new AsyncCommand(() =>
        {
            AddServer();
            return Task.CompletedTask;
        });
        RemoveServerCommand = new AsyncCommand(() =>
        {
            RemoveServer();
            return Task.CompletedTask;
        });
        JoinCommand = new AsyncCommand(JoinAsync);
        UseSuggestedSteamCommand = new AsyncCommand(UseSuggestedSteamAsync);
    }

    public Func<Task>? Browse { get; set; }
    public Func<Task>? EditSettings { get; set; }
    public LauncherSettings Settings { get; private set; } = new();

    public ICommand PrimaryCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand SetLanguageCommand { get; }
    public ICommand BrowseCommand { get; }
    public ICommand ShowModpackCommand { get; }
    public ICommand ShowServersCommand { get; }
    public ICommand RefreshServersCommand { get; }
    public ICommand AddServerCommand { get; }
    public ICommand RemoveServerCommand { get; }
    public ICommand JoinCommand { get; }
    public ICommand UseSuggestedSteamCommand { get; }

    private string _headline = "";
    private string _detail = "";
    private string _statusLabel = "";
    private string _installedVersionText = "—";
    private string _remoteVersionText = "—";
    private string _gameVersionText = "—";
    private string _gamePathText = "Not selected";
    private string _feedHost = "Not set";
    private string _packSizeText = "—";
    private string _publishedText = "—";
    private string _notes = "";
    private string _progressText = "";
    private string _error = "";
    private string _primaryLabel = "Please wait";
    private double _progressValue;
    private bool _showProgress;
    private bool _hasNotes;
    private bool _hasError;
    private bool _primaryEnabled;
    private bool _sideEnabled = true;
    private bool _isOk;
    private bool _isWarn;
    private bool _isBad;
    private bool _isIdle = true;
    private bool _busy;
    private bool _versionsMatch;
    private bool _showVersions;
    private bool _showFound;
    private string _foundText = "";
    private string _unpackPathText = "";
    private string _steamPathText = "Not selected";
    private bool _showModpack = true;
    private bool _showServerTab = true;
    private bool _showServers;
    private bool _showSettings;
    private bool _showSetup;
    private bool _hasSuggestedSteam;
    private bool _hasSetupError;
    private string? _setupRawError;
    private string _suggestedSteamText = "";
    private string _setupError = "";
    private bool _settingsFromServers;
    private string _serverStatus = "";
    private bool _feedTimedOut;
    private int _serverOnline;
    private int _serverTotal;
    private int _serverAdded;
    private string? _serverFail;
    private bool _serversLoaded;
    private string _addName = "";
    private string _addAddress = "";
    private string _addPort = "";
    private string _addPassword = "";
    private string _joinPassword = "";
    private ServerEntry? _selectedServer;
    private int _refreshingServers;

    public string Headline { get => _headline; private set => Set(ref _headline, value); }
    public string Detail { get => _detail; private set => Set(ref _detail, value); }
    public string StatusLabel { get => _statusLabel; private set => Set(ref _statusLabel, value); }
    public string InstalledVersionText { get => _installedVersionText; private set => Set(ref _installedVersionText, value); }
    public string RemoteVersionText { get => _remoteVersionText; private set => Set(ref _remoteVersionText, value); }
    public string GameVersionText { get => _gameVersionText; private set => Set(ref _gameVersionText, value); }
    public string GamePathText { get => _gamePathText; private set => Set(ref _gamePathText, value); }
    public string FeedHost { get => _feedHost; private set => Set(ref _feedHost, value); }
    public string PackSizeText { get => _packSizeText; private set => Set(ref _packSizeText, value); }
    public string PublishedText { get => _publishedText; private set => Set(ref _publishedText, value); }
    public string Notes { get => _notes; private set => Set(ref _notes, value); }
    public string ProgressText { get => _progressText; private set => Set(ref _progressText, value); }
    public string Error { get => _error; private set => Set(ref _error, value); }
    public string PrimaryLabel { get => _primaryLabel; private set => Set(ref _primaryLabel, value); }
    public double ProgressValue { get => _progressValue; private set => Set(ref _progressValue, value); }
    public bool ShowProgress { get => _showProgress; private set => Set(ref _showProgress, value); }
    public bool HasNotes { get => _hasNotes; private set => Set(ref _hasNotes, value); }
    public bool HasError
    {
        get => _hasError;
        private set
        {
            Set(ref _hasError, value);
            Notify(nameof(ShowLauncherError));
        }
    }

    public bool ShowLauncherError => _hasError && !_showSettings;
    public bool PrimaryEnabled { get => _primaryEnabled; private set => Set(ref _primaryEnabled, value); }
    public bool SideEnabled { get => _sideEnabled; private set => Set(ref _sideEnabled, value); }
    public bool IsOk { get => _isOk; private set => Set(ref _isOk, value); }
    public bool IsWarn { get => _isWarn; private set => Set(ref _isWarn, value); }
    public bool IsBad { get => _isBad; private set => Set(ref _isBad, value); }
    public bool IsIdle { get => _isIdle; private set => Set(ref _isIdle, value); }
    public bool VersionsMatch { get => _versionsMatch; private set => Set(ref _versionsMatch, value); }
    public bool ShowVersions { get => _showVersions; private set => Set(ref _showVersions, value); }
    public bool LangEn => UiText.S.Language == "en";
    public bool LangEs => UiText.S.Language == "es";
    public bool LangFr => UiText.S.Language == "fr";
    public bool LangDe => UiText.S.Language == "de";
    public bool LangRu => UiText.S.Language == "ru";
    public bool ShowFound { get => _showFound; private set => Set(ref _showFound, value); }
    public string FoundText { get => _foundText; private set => Set(ref _foundText, value); }
    public string UnpackPathText { get => _unpackPathText; private set => Set(ref _unpackPathText, value); }
    public string SteamPathText { get => _steamPathText; private set => Set(ref _steamPathText, value); }
    public bool ShowModpack { get => _showModpack; private set => Set(ref _showModpack, value); }
    public bool ShowServerTab { get => _showServerTab; private set => Set(ref _showServerTab, value); }
    public bool ShowServers { get => _showServers; private set => Set(ref _showServers, value); }
    public bool ShowSettings
    {
        get => _showSettings;
        private set
        {
            Set(ref _showSettings, value);
            Notify(nameof(ShowLauncherError), nameof(ShowPlayFooter));
        }
    }

    public bool ShowSetup
    {
        get => _showSetup;
        private set
        {
            Set(ref _showSetup, value);
            Notify(nameof(ShowPlayFooter));
        }
    }

    public bool ShowPlayFooter => !_showSettings && !_showSetup;
    public bool HasSuggestedSteam { get => _hasSuggestedSteam; private set => Set(ref _hasSuggestedSteam, value); }
    public bool HasSetupError { get => _hasSetupError; private set => Set(ref _hasSetupError, value); }
    public string SuggestedSteamText { get => _suggestedSteamText; private set => Set(ref _suggestedSteamText, value); }
    public string SetupError { get => _setupError; private set => Set(ref _setupError, value); }

    public void ShowSettingsPage()
    {
        if (!ShowSettings)
            _settingsFromServers = ShowServers;
        ShowSettings = true;
        ShowModpack = false;
        ShowServers = false;
    }

    public void HideSettingsPage()
    {
        ShowSettings = false;
        var servers = _settingsFromServers && ShowServerTab;
        ShowServers = servers;
        ShowModpack = !servers;
    }

    public void UnlockServerBrowser()
    {
        ShowServerTab = true;
    }
    public string ServerStatus { get => _serverStatus; private set => Set(ref _serverStatus, value); }
    public string AddName { get => _addName; set => Set(ref _addName, value); }
    public string AddAddress { get => _addAddress; set => Set(ref _addAddress, value); }
    public string AddPort { get => _addPort; set => Set(ref _addPort, value); }
    public string AddPassword { get => _addPassword; set => Set(ref _addPassword, value); }
    public string JoinPassword { get => _joinPassword; set => Set(ref _joinPassword, value); }
    public bool HasSelectedServer => SelectedServer != null;
    public string SelectedTitle => SelectedServer?.Name ?? "";
    public string SelectedEndpoint => SelectedServer?.Endpoint ?? "";
    public string SelectedMeta => SelectedServer?.Meta ?? "";
    public bool HasSelectedMeta => SelectedMeta.Length > 0;
    public bool CanJoin => SelectedServer != null && !_busy && CurrentDecision().CanPlay && (SelectedServer.Local || SelectedServer.Online);
    public bool CanRemoveServer => SelectedServer is { Local: true } && !_busy;
    public string JoinHint
    {
        get
        {
            if (SelectedServer == null)
                return T("joinSelect");
            var decision = CurrentDecision();
            if (!decision.CanPlay)
                return Describe(decision).Detail;
            if (!SelectedServer.Local && !SelectedServer.Online)
                return T("joinOffline", SelectedServer.Name);
            return T("joinGoes", SelectedServer.Name);
        }
    }

    public ServerEntry? SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (ReferenceEquals(_selectedServer, value))
                return;
            _selectedServer = value;
            _joinPassword = value?.Password ?? "";
            Notify(
                nameof(SelectedServer),
                nameof(JoinPassword),
                nameof(HasSelectedServer),
                nameof(SelectedTitle),
                nameof(SelectedEndpoint),
                nameof(SelectedMeta),
                nameof(HasSelectedMeta),
                nameof(CanJoin),
                nameof(CanRemoveServer),
                nameof(JoinHint));
        }
    }

    public ObservableCollection<InstalledModule> Modules { get; } = new();
    public ObservableCollection<LaunchModChoice> LaunchMods { get; } = new();
    public ObservableCollection<ServerEntry> Servers { get; } = new();

    public string? SuggestedSteamFolder =>
        !string.IsNullOrWhiteSpace(Settings.SteamPath) ? Settings.SteamPath : GameLocator.TryReadSteamInstall();

    public Task StartAsync()
    {
        Settings = LauncherSettingsStore.Load();
        UiText.S.Set(Settings.Language);
        var savedGame = GameLocator.IsGameRoot(Settings.GamePath);
        // A website download has no Steam path yet. Walk the player to it instead of guessing.
        if (!GameLocator.IsBundledInSteam(LauncherPaths.CurrentExecutable()) && !savedGame)
        {
            ShowSetup = true;
            SideEnabled = false;
            RefreshSetupOffer();
            Log.Info("setup guide");
            return Task.CompletedTask;
        }

        if (!savedGame)
        {
            var found = !string.IsNullOrWhiteSpace(Settings.SteamPath)
                ? GameLocator.Locate(Settings.SteamPath).Location
                : null;
            found ??= GameLocator.TryFind();
            if (found != null)
            {
                Settings.GamePath = found.GamePath;
                Settings.SteamPath = found.SteamPath ?? Settings.SteamPath;
                LauncherSettingsStore.Save(Settings);
            }
        }

        Log.Info("launcher start");
        return CheckAsync();
    }

    private void RefreshSetupOffer()
    {
        var hint = SuggestedSteamFolder;
        var usable = !string.IsNullOrWhiteSpace(hint) && Directory.Exists(hint);
        HasSuggestedSteam = usable;
        SuggestedSteamText = usable ? hint! : "";
    }

    private Task UseSuggestedSteamAsync()
    {
        if (_busy)
            return Task.CompletedTask;
        var hint = SuggestedSteamFolder;
        if (string.IsNullOrWhiteSpace(hint) || !Directory.Exists(hint))
            return OpenBrowseAsync();
        return SetSteamDirectoryAsync(hint);
    }

    public Task SetSteamDirectoryAsync(string path)
    {
        var located = GameLocator.Locate(path);
        if (!located.Ok || located.Location == null)
        {
            if (ShowSetup)
            {
                _setupRawError = located.Error;
                SetupError = UiText.S.LocateError(located.Error);
                HasSetupError = true;
                return Task.CompletedTask;
            }

            Error = UiText.S.LocateError(located.Error);
            HasError = true;
            return Task.CompletedTask;
        }

        ShowSetup = false;
        _setupRawError = null;
        SetupError = "";
        HasSetupError = false;
        Settings.SteamPath = located.Location.SteamPath ?? path;
        Settings.GamePath = located.Location.GamePath;
        LauncherSettingsStore.Save(Settings);
        Log.Info($"steam {Settings.SteamPath}; game {Settings.GamePath}; modules {located.Location.Modules.Count}");
        return CheckAsync();
    }

    public async Task SettingsClosedAsync(bool saved)
    {
        if (!saved)
            return;
        LauncherSettingsStore.Save(Settings);
        await CheckAsync().ConfigureAwait(true);
    }

    public void Cancel() => _operation?.Cancel();

    private Task CheckAsync() => RunAsync(async cancellationToken =>
    {
        _justLaunched = false;
        ShowProgress = false;
        ShowWorking(T("checking"), T("checkingDetail"));
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    });

    private Task UpdateAsync() => RunAsync(async cancellationToken =>
    {
        if (GameLauncher.IsBannerlordRunning())
        {
            ShowWorking(T("closingGame"), T("closingGameDetail"));
            await GameLauncher.CloseGameAsync(cancellationToken).ConfigureAwait(true);
        }

        await RefreshAsync(cancellationToken).ConfigureAwait(true);
        if (_manifest == null || _manifestUri == null || string.IsNullOrWhiteSpace(Settings.GamePath))
            return;

        ShowProgress = true;
        var progress = new Progress<InstallProgress>(OnProgress);
        var target = CurrentInstallTarget();
        var result = await _installer.InstallAsync(
            _manifest,
            _manifestUri,
            target,
            Settings.DownloadConcurrency,
            progress,
            cancellationToken).ConfigureAwait(true);

        InstallRecordStore.Save(RecordFromManifest(target));
        _record = InstallRecordStore.TryLoad(target.Root);
        _filesIntact = true;
        _checkSucceeded = true;
        ShowProgress = false;
        var installed = InstalledModpackVersion();
        if (!string.Equals(installed, _manifest.Version, StringComparison.Ordinal))
            throw new InvalidOperationException(T("workshopStill", string.IsNullOrEmpty(installed) ? T("noModpack") : installed));
        Log.Info($"installed {_manifest.Version}; downloaded {result.DownloadedFiles}; reused {result.ReusedFiles}; removed {result.RemovedFiles}");
    });

    private Task PlayAsync() => LaunchGameAsync(null);

    private Task JoinAsync()
    {
        var server = SelectedServer;
        if (server == null)
            return Task.CompletedTask;

        var password = string.IsNullOrEmpty(JoinPassword) ? null : JoinPassword;
        return LaunchGameAsync(new DirectJoinRequest
        {
            Name = server.Name,
            Address = server.Address,
            Port = server.Port,
            Password = password
        });
    }

    private Task LaunchGameAsync(DirectJoinRequest? join) => RunAsync(async cancellationToken =>
    {
        ShowWorking(T("checking"), join == null ? T("playLocked") : T("joinLocked"));
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
        var decision = CurrentDecision();
        if (!decision.CanPlay || _manifest == null || string.IsNullOrWhiteSpace(Settings.GamePath))
        {
            ApplyDecision(decision);
            return;
        }

        ShowWorking(T("startingGame"), join == null ? T("startingPlayDetail") : T("startingJoinDetail", join.Name));
        var extras = LaunchMods.Where(mod => mod.Selected && !mod.Required).Select(mod => mod.Id);
        var process = GameLauncher.Launch(Settings.GamePath, new LaunchSpec { Arguments = LaunchSelection.Build(extras) }, join);
        _justLaunched = true;
        try
        {
            await Task.Delay(2000, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (process.HasExited)
        {
            DirectJoin.Clear();
            throw new InvalidOperationException(T("gameClosed"));
        }

        ShowWorking(
            T("starting"),
            join == null ? T("startedPlayDetail", _manifest.Version) : T("startedJoinDetail", join.Name, _manifest.Version));
        if (join != null)
            Log.Info("join " + join.Name + " " + join.Address + ":" + join.Port);
    });

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        _checkCompleted = false;
        var url = FeedAddress.Resolve(Settings.ManifestUrl);
        var gameFound = GameLocator.IsGameRoot(Settings.GamePath);
        var target = gameFound ? CurrentInstallTarget() : null;
        _record = target == null ? null : InstallRecordStore.TryLoad(target.Root);
        if (target != null && _record != null)
            _filesIntact = InstallRecordStore.FilesMatchSizes(target.Root, _record);
        else
            _filesIntact = _record == null ? null : false;

        if (url == null)
        {
            _manifest = null;
            _manifestUri = null;
            _checkSucceeded = false;
            _feedTimedOut = false;
            _checkError = null;
            _checkCompleted = true;
            return;
        }

        try
        {
            var uri = FeedAddress.Parse(url);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            _manifest = await _manifests.GetAsync(uri, timeout.Token).ConfigureAwait(true);
            _manifestUri = uri;
            _checkSucceeded = true;
            _feedTimedOut = false;
            _checkError = null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _checkSucceeded = false;
            _feedTimedOut = true;
            _checkError = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _checkSucceeded = false;
            _feedTimedOut = false;
            _checkError = ex.Message;
            Log.Error("feed", ex);
        }
        finally
        {
            _checkCompleted = true;
        }
    }

    private async Task RunAsync(Func<CancellationToken, Task> work)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            return;

        _operation = new CancellationTokenSource();
        _busy = true;
        Error = "";
        HasError = false;
        ApplyChrome();
        try
        {
            await work(_operation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            Detail = T("cancelled");
            ShowProgress = false;
        }
        catch (Exception ex)
        {
            Error = Friendly(ex);
            HasError = true;
            ShowProgress = false;
            Log.Error(Error, ex);
        }
        finally
        {
            _busy = false;
            Interlocked.Exchange(ref _running, 0);
            ApplyGate();
        }
    }

    private async Task PrimaryAsync()
    {
        switch (_action)
        {
            case UiAction.Play:
                await PlayAsync().ConfigureAwait(true);
                break;
            case UiAction.Update:
                await UpdateAsync().ConfigureAwait(true);
                break;
            case UiAction.Check:
                await CheckAsync().ConfigureAwait(true);
                break;
            case UiAction.Settings:
                await OpenSettingsAsync().ConfigureAwait(true);
                break;
            case UiAction.Browse:
                await OpenBrowseAsync().ConfigureAwait(true);
                break;
            case UiAction.Cancel:
                Cancel();
                break;
        }
    }

    private async Task OpenSettingsAsync()
    {
        if (_busy || EditSettings == null)
            return;
        await EditSettings().ConfigureAwait(true);
    }

    private async Task OpenBrowseAsync()
    {
        if (_busy || Browse == null)
            return;
        await Browse().ConfigureAwait(true);
    }

    private void OnProgress(InstallProgress progress)
    {
        ShowProgress = true;
        ProgressValue = progress.Fraction;
        var name = progress.Detail.Replace('\\', '/');
        var slash = name.LastIndexOf('/');
        if (slash >= 0 && slash < name.Length - 1)
            name = name.Substring(slash + 1);

        if (progress.Detail == "Copying into the game folder")
            name = T("copying");
        else if (progress.Detail == "Removing old files")
            name = T("removingOld");

        if (progress.Phase == InstallPhase.Downloading)
        {
            ShowWorking(T("downloading"), name);
            ProgressText = DownloadStatus(progress);
            return;
        }

        ShowWorking(progress.Phase switch
        {
            InstallPhase.Scanning => T("checkingFiles"),
            InstallPhase.Cleaning => T("cleaning"),
            _ => CurrentInstallTarget().Workshop ? T("unpackingWorkshop") : T("unpackingModules")
        }, name);
        ProgressText = progress.TotalFiles > 0
            ? $"{progress.CompletedFiles} / {progress.TotalFiles}"
            : "";
    }

    private void ApplyChrome()
    {
        _action = UiAction.Cancel;
        PrimaryLabel = T("cancel");
        PrimaryEnabled = true;
        SideEnabled = false;
        Notify(nameof(CanJoin), nameof(CanRemoveServer), nameof(JoinHint));
    }

    private void ApplyGate()
    {
        var decision = CurrentDecision();
        ApplyDecision(decision);
    }

    private void ApplyDecision(GateDecision decision)
    {
        var installedVersion = InstalledModpackVersion();
        var remoteVersion = _manifest?.Version;
        InstalledVersionText = string.IsNullOrEmpty(installedVersion) ? T("notInstalledShort") : installedVersion;
        RemoteVersionText = string.IsNullOrEmpty(remoteVersion) ? "—" : remoteVersion;
        VersionsMatch = !string.IsNullOrEmpty(installedVersion) && string.Equals(installedVersion, remoteVersion, StringComparison.Ordinal);
        ShowVersions = !string.IsNullOrEmpty(installedVersion) || !string.IsNullOrEmpty(remoteVersion);
        if (!_busy)
        {
            var copy = Describe(decision);
            Headline = copy.Headline;
            Detail = copy.Detail;
        }

        var gameVersion = GameLocator.IsGameRoot(Settings.GamePath) ? GameLocator.ReadNativeVersion(Settings.GamePath!) : null;
        GameVersionText = string.IsNullOrEmpty(gameVersion) ? "—" : gameVersion!;
        GamePathText = string.IsNullOrWhiteSpace(Settings.GamePath) ? T("notSelected") : Settings.GamePath!;
        SteamPathText = string.IsNullOrWhiteSpace(Settings.SteamPath) ? T("notSelected") : Settings.SteamPath!;
        RefreshModules();
        FeedHost = FeedLabel();
        PackSizeText = _manifest == null ? "—" : FormatBytes(_manifest.Files.Sum(file => file.Size));
        PublishedText = _manifest?.PublishedAt?.ToLocalTime().ToString("dd MMM yyyy HH:mm", UiText.S.Culture) ?? "—";
        Notes = _manifest?.Notes ?? "";
        HasNotes = Notes.Length > 0;
        IsOk = decision.CanPlay;
        IsWarn = decision.Block is PlayBlock.UpdateRequired or PlayBlock.NotInstalled or PlayBlock.FilesChanged or PlayBlock.AlreadyRunning or PlayBlock.GameVersionMismatch;
        IsBad = !decision.CanPlay && !IsWarn && decision.Block is not PlayBlock.Checking and not PlayBlock.Busy;
        IsIdle = !IsOk && !IsWarn && !IsBad;
        if (!_busy)
            StatusLabel = IsOk ? T("statusReady") : IsWarn ? T("statusAction") : IsBad ? T("statusBlocked") : T("statusWorking");

        if (_busy)
        {
            Notify(nameof(CanJoin), nameof(CanRemoveServer), nameof(JoinHint));
            return;
        }

        SideEnabled = true;
        (_action, PrimaryLabel) = decision.Block switch
        {
            PlayBlock.None => (UiAction.Play, T("play")),
            PlayBlock.NotInstalled => (UiAction.Update, T("download")),
            PlayBlock.UpdateRequired => (UiAction.Update, T("update")),
            PlayBlock.FilesChanged => (UiAction.Update, T("repair")),
            PlayBlock.NoFeed => (UiAction.Settings, T("settings")),
            PlayBlock.NoGame => (UiAction.Browse, T("selectSteamBtn")),
            PlayBlock.Offline or PlayBlock.GameVersionMismatch or PlayBlock.AlreadyRunning => (UiAction.Check, T("retry")),
            _ => (UiAction.None, T("pleaseWait"))
        };
        PrimaryEnabled = _action != UiAction.None;
        Notify(nameof(CanJoin), nameof(CanRemoveServer), nameof(JoinHint));
    }

    private GateDecision CurrentDecision()
    {
        var url = FeedAddress.Resolve(Settings.ManifestUrl);
        return VersionGate.Decide(new GateInput
        {
            FeedConfigured = !string.IsNullOrWhiteSpace(url),
            GameFound = GameLocator.IsGameRoot(Settings.GamePath),
            CheckCompleted = _checkCompleted,
            CheckSucceeded = _checkSucceeded,
            CheckError = _checkError,
            RequireOnlineCheck = Settings.RequireOnlineCheck,
            InstalledVersion = InstalledModpackVersion(),
            RemoteVersion = _manifest?.Version,
            FilesIntact = FilesLookIntact(),
            InstalledGameVersion = null,
            RequiredGameVersion = null,
            Busy = false,
            GameRunning = GameLauncher.IsBannerlordRunning(),
            JustLaunched = _justLaunched
        });
    }

    private InstallRecord RecordFromManifest(InstallTarget target)
    {
        return new InstallRecord
        {
            Version = _manifest!.Version,
            ManifestUrl = _manifestUri?.ToString() ?? "",
            GamePath = target.Root,
            Files = _manifest.Files.Select(file => new RecordedFile
            {
                Path = target.Map(file.Path),
                Size = file.Size,
                Sha256 = file.Sha256
            }).ToList()
        };
    }

    private InstallTarget CurrentInstallTarget()
        => GameLocator.ChooseInstallTarget(Settings.GamePath!, Settings.SteamPath);

    private void RefreshModules()
    {
        Modules.Clear();
        LaunchMods.Clear();
        if (!GameLocator.IsGameRoot(Settings.GamePath))
        {
            ShowFound = false;
            FoundText = "";
            UnpackPathText = T("selectSteamFirst");
            return;
        }

        var home = GameLocator.LocateModpack(Settings.GamePath!, Settings.SteamPath);
        var target = CurrentInstallTarget();
        ShowFound = true;
        UnpackPathText = target.Workshop ? target.Root : Path.Combine(Settings.GamePath!, "Modules");
        var chosen = new HashSet<string>(Settings.SelectedModuleIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        var favourites = new HashSet<string>(Settings.FavouriteModuleIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        var order = 0;
        foreach (var module in GameLocator.ListLaunchModules(Settings.GamePath!, Settings.SteamPath))
        {
            var required = module.Id.Equals(GameLocator.ModpackModuleId, StringComparison.OrdinalIgnoreCase);
            LaunchMods.Add(new LaunchModChoice(module.Id, module.Name, required, chosen.Contains(module.Id), favourites.Contains(module.Id), order++, SaveLaunchSelection));
        }

        SortLaunchMods();

        if (home == null)
        {
            FoundText = target.Workshop ? T("foundWorkshopPending") : T("foundNotInstalled");
            return;
        }

        Modules.Add(home.Module);
        FoundText = T("foundTick");
    }

    private void SaveLaunchSelection()
    {
        Settings.SelectedModuleIds = LaunchMods.Where(mod => mod.Selected && !mod.Required).Select(mod => mod.Id).ToList();
        Settings.FavouriteModuleIds = LaunchMods.Where(mod => mod.Favourite && !mod.Required).Select(mod => mod.Id).ToList();
        LauncherSettingsStore.Save(Settings);
        SortLaunchMods();
    }

    private void SortLaunchMods()
    {
        var ordered = LaunchMods
            .OrderByDescending(mod => mod.Required)
            .ThenByDescending(mod => mod.Favourite)
            .ThenBy(mod => mod.Order)
            .ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            var current = LaunchMods.IndexOf(ordered[index]);
            if (current != index)
                LaunchMods.Move(current, index);
        }
    }

    private string? InstalledModpackVersion()
    {
        if (!GameLocator.IsGameRoot(Settings.GamePath))
            return null;
        return GameLocator.LocateModpack(Settings.GamePath!, Settings.SteamPath)?.Module.Version;
    }

    private bool? FilesLookIntact()
    {
        if (_filesIntact == false)
            return false;
        if (_manifest != null && InstalledModpackVersion() != null && _record == null)
            return false;
        return _filesIntact;
    }

    private string FeedLabel()
    {
        var url = FeedAddress.Resolve(Settings.ManifestUrl);
        if (string.IsNullOrWhiteSpace(url))
            return "Not set";
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;
    }

    private static string DownloadStatus(InstallProgress progress)
    {
        var rate = progress.BytesPerSecond >= 1024 * 1024
            ? $"{progress.BytesPerSecond / (1024 * 1024):0.0} MB/s"
            : progress.BytesPerSecond >= 1024
                ? $"{progress.BytesPerSecond / 1024:0} KB/s"
                : "";
        var eta = "";
        if (progress.BytesPerSecond > 1 && progress.TotalBytes > progress.CompletedBytes)
        {
            var left = TimeSpan.FromSeconds((progress.TotalBytes - progress.CompletedBytes) / progress.BytesPerSecond);
            var clock = left.TotalHours >= 1
                ? $"{(int)left.TotalHours}h {left.Minutes:00}m"
                : left.TotalMinutes >= 1
                    ? $"{(int)left.TotalMinutes}m {left.Seconds:00}s"
                    : $"{left.Seconds}s";
            eta = T("timeLeft", clock);
        }

        return $"{FormatBytes(progress.CompletedBytes)} / {FormatBytes(progress.TotalBytes)}"
               + (rate.Length == 0 ? "" : " · " + rate)
               + eta
               + $" · {progress.CompletedFiles}/{progress.TotalFiles}";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024)
            return $"{bytes / (1024d * 1024 * 1024):0.00} GB";
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024d * 1024):0.0} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024d:0} KB";
        return $"{bytes} B";
    }

    private static string Friendly(Exception exception)
    {
        while (exception is AggregateException aggregate && aggregate.InnerExceptions.Count == 1 && aggregate.InnerException != null)
            exception = aggregate.InnerException;

        return exception switch
        {
            ManifestException => exception.Message,
            HttpRequestException => T("downloadFailed", exception.Message),
            UnauthorizedAccessException => T("folderLocked"),
            IOException io when InstalledFile.IsLocked(io) => T("folderLocked"),
            _ => exception.Message
        };
    }

    private Task SelectTabAsync(bool servers)
    {
        if (servers && !ShowServerTab)
            return Task.CompletedTask;
        ShowSettings = false;
        ShowServers = servers;
        ShowModpack = !servers;
        return servers ? RefreshServersAsync() : Task.CompletedTask;
    }

    private async Task RefreshServersAsync()
    {
        if (_busy || Interlocked.CompareExchange(ref _refreshingServers, 1, 0) != 0)
            return;

        try
        {
            await LoadServersAsync().ConfigureAwait(true);
        }
        finally
        {
            Interlocked.Exchange(ref _refreshingServers, 0);
        }
    }

    private async Task LoadServersAsync()
    {

        IReadOnlyList<ServerEntry> remote = Array.Empty<ServerEntry>();
        _serverFail = null;
        try
        {
            if (_manifestUri != null)
                remote = await ServerCatalog.FetchAsync(_http, _manifestUri, CancellationToken.None).ConfigureAwait(true);
            _serverOnline = remote.Count(server => server.Online);
            _serverTotal = remote.Count;
            _serversLoaded = true;
        }
        catch (Exception ex)
        {
            _serverFail = ex.Message;
            _serversLoaded = true;
            Log.Error("servers", ex);
        }

        var selected = SelectedServer;
        var merged = ServerCatalog.Merge(remote, ServerCatalog.LoadLocal());
        Servers.Clear();
        foreach (var server in merged)
            Servers.Add(server);

        SelectedServer = selected == null
            ? Servers.FirstOrDefault()
            : Servers.FirstOrDefault(server => server.Port == selected.Port && ServerEndpoint.SameHost(server.Address, selected.Address))
              ?? Servers.FirstOrDefault();

        _serverAdded = Servers.Count(server => server.Local);
        PublishServerStatus();
    }

    private void AddServer()
    {
        if (!ServerCatalog.TryCreate(AddName, AddAddress, AddPort, AddPassword, out var server, out var error) || server == null)
        {
            Error = UiText.S.ServerError(error);
            HasError = true;
            return;
        }

        if (Servers.Any(existing => ServerCatalog.Same(existing, server)))
        {
            Error = T("serverExists");
            HasError = true;
            SelectedServer = Servers.First(existing => ServerCatalog.Same(existing, server));
            return;
        }

        Servers.Add(server);
        ServerCatalog.SaveLocal(Servers);
        SelectedServer = server;
        AddName = "";
        AddAddress = "";
        AddPort = "";
        AddPassword = "";
        Error = "";
        HasError = false;
        _serverAdded = Servers.Count(entry => entry.Local);
        PublishServerStatus();
    }

    private void RemoveServer()
    {
        var server = SelectedServer;
        if (server is not { Local: true })
            return;

        Servers.Remove(server);
        ServerCatalog.SaveLocal(Servers);
        SelectedServer = Servers.FirstOrDefault();
        Error = "";
        HasError = false;
        _serverAdded = Servers.Count(entry => entry.Local);
        PublishServerStatus();
    }

    private void SetLanguage(string? code)
    {
        var next = UiText.Normalize(code);
        if (next == UiText.S.Language)
            return;
        Settings.Language = next;
        LauncherSettingsStore.Save(Settings);
        UiText.S.Set(next);
        Notify(nameof(LangEn), nameof(LangEs), nameof(LangFr), nameof(LangDe), nameof(LangRu));
        foreach (var mod in LaunchMods)
            mod.RefreshTip();
        PublishServerStatus();
        if (ShowSetup)
        {
            if (_setupRawError != null)
                SetupError = UiText.S.LocateError(_setupRawError);
            return;
        }
        if (_busy)
        {
            PrimaryLabel = T("cancel");
            StatusLabel = T("statusWorking");
            return;
        }

        ApplyGate();
    }

    private void ShowWorking(string headline, string detail)
    {
        Headline = headline;
        Detail = detail;
        IsOk = false;
        IsWarn = false;
        IsBad = false;
        IsIdle = true;
        StatusLabel = T("statusWorking");
    }

    private (string Headline, string Detail) Describe(GateDecision decision)
    {
        var installed = InstalledModpackVersion();
        var remote = _manifest?.Version ?? "";
        return decision.Block switch
        {
            PlayBlock.Busy => (T("working"), T("workingDetail")),
            PlayBlock.NoGame => (T("selectSteam"), T("selectSteamDetail")),
            PlayBlock.NoFeed => (T("feedMissing"), T("feedMissingDetail")),
            PlayBlock.Checking => (T("checking"), T("checkingDetail")),
            PlayBlock.Offline => (T("offline"), _feedTimedOut ? T("feedTimeout") : string.IsNullOrEmpty(_checkError) ? T("offlineFallback") : _checkError!),
            PlayBlock.NotInstalled => (T("notInstalled"), T("notInstalledDetail", remote)),
            PlayBlock.UpdateRequired => (T("updateRequired"), T("updateRequiredDetail", installed ?? "", remote)),
            PlayBlock.FilesChanged when !_checkSucceeded => (T("filesChangedOffline"), T("filesChangedOfflineDetail")),
            PlayBlock.FilesChanged => (T("filesChanged"), T("filesChangedDetail")),
            PlayBlock.AlreadyRunning when _justLaunched => (T("starting"), T("startingDetail")),
            PlayBlock.AlreadyRunning => (T("gameOpen"), T("gameOpenDetail")),
            PlayBlock.None when !_checkSucceeded => (T("readyOffline"), T("readyOfflineDetail", installed ?? "")),
            _ => (T("ready"), T("readyDetail", string.IsNullOrEmpty(remote) ? installed ?? "" : remote))
        };
    }

    private void PublishServerStatus()
    {
        var added = _serverAdded > 0 ? " " + T("serversAdded", _serverAdded) : "";
        if (!_serversLoaded)
        {
            ServerStatus = T("serversPending");
            return;
        }

        ServerStatus = _serverFail != null
            ? T("serversFailDetail", _serverFail) + added
            : (_serverTotal == 0 ? T("serversEmpty") : T("serversOnline", _serverOnline, _serverTotal)) + added;
    }

    private static string T(string key, params object[] args) => UiText.S.Format(key, args);

    private enum UiAction
    {
        None,
        Play,
        Update,
        Check,
        Settings,
        Browse,
        Cancel
    }
}

public sealed class LaunchModChoice : Observable
{
    private readonly Action _persist;
    private bool _selected;
    private bool _favourite;

    public LaunchModChoice(string id, string label, bool required, bool selected, bool favourite, int order, Action persist)
    {
        Id = id;
        Label = string.IsNullOrWhiteSpace(label) ? id : label;
        Required = required;
        Order = order;
        _selected = required || selected;
        _favourite = !required && favourite;
        _persist = persist;
        ToggleFavouriteCommand = new AsyncCommand(() =>
        {
            Favourite = !Favourite;
            return Task.CompletedTask;
        });
    }

    public string Id { get; }
    public string Label { get; }
    public bool Required { get; }
    public int Order { get; }
    public bool CanToggle => !Required;
    public bool CanFavourite => !Required;
    public ICommand ToggleFavouriteCommand { get; }
    public string FavouriteTip => _favourite ? UiText.S.Get("removeTop") : UiText.S.Get("keepTop");

    public void RefreshTip() => Notify(nameof(FavouriteTip));

    public bool Selected
    {
        get => _selected;
        set
        {
            if (Required)
            {
                Set(ref _selected, true);
                return;
            }

            var before = _selected;
            Set(ref _selected, value);
            if (before != _selected)
                _persist();
        }
    }

    public bool Favourite
    {
        get => _favourite;
        set
        {
            if (!CanFavourite)
                return;
            var before = _favourite;
            Set(ref _favourite, value);
            if (before == _favourite)
                return;
            Notify(nameof(FavouriteTip));
            _persist();
        }
    }
}
