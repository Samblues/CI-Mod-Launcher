using System.ComponentModel;
using System.Globalization;

namespace CILauncher;

public sealed class UiText : INotifyPropertyChanged
{
    public static UiText S { get; } = new();

    static UiText()
    {
        foreach (var pair in Map)
        {
            if (pair.Value.Length != 5)
                throw new InvalidOperationException(pair.Key);
        }
    }

    private static readonly string[] Codes = ["en", "es", "fr", "de", "ru"];
    private static readonly CultureInfo[] Cultures =
    [
        CultureInfo.GetCultureInfo("en-GB"),
        CultureInfo.GetCultureInfo("es-ES"),
        CultureInfo.GetCultureInfo("fr-FR"),
        CultureInfo.GetCultureInfo("de-DE"),
        CultureInfo.GetCultureInfo("ru-RU")
    ];

    private int _index;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language { get; private set; } = "en";
    public CultureInfo Culture => Cultures[_index];

    public void Set(string? code)
    {
        var next = Normalize(code);
        var index = Array.IndexOf(Codes, next);
        if (index < 0)
            index = 0;
        if (index == _index && Language == Codes[index])
            return;
        _index = index;
        Language = Codes[index];
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }

    public static string Normalize(string? code)
    {
        var value = (code ?? "").Trim().ToLowerInvariant();
        return value is "es" or "fr" or "de" or "ru" or "en" ? value : "en";
    }

    public string Get(string key) => Map.TryGetValue(key, out var row) ? row[_index] : key;

    public string Format(string key, params object[] args)
    {
        var text = Get(key);
        return args.Length == 0 ? text : string.Format(Culture, text, args);
    }

    public string LocateError(string? error) => error switch
    {
        "That folder doesn't exist." => Get("errFolderMissing"),
        "Select your Steam folder — the one that contains steam.exe." => OperatingSystem.IsWindows() ? Get("errSteamExe") : Get("errSteamUnix"),
        "Steam is there, but Bannerlord isn't installed in its libraries. Install the game on Steam, then select the folder again." => Get("errNoGame"),
        null or "" => Get("errSteamExe"),
        _ => error
    };

    public string ServerError(string? error) => error switch
    {
        "Give the server a name, up to 80 characters." => Get("errName"),
        "Enter the server address, like 1.2.3.4 or 1.2.3.4:7210." => Get("errAddress"),
        "Enter the server port." => Get("errPort"),
        "The port has to be a number from 1 to 65535." => Get("errPortRange"),
        "The address and the port don't match." => Get("errPortMismatch"),
        "The password is too long." => Get("errPassword"),
        null or "" => Get("serverAddFail"),
        _ => error
    };

    public string LauncherSubtitle => Get("launcherSubtitle");
    public string Modpack => Get("modpack");
    public string Servers => Get("servers");
    public string Settings => Get("settings");
    public string Minimize => Get("minimize");
    public string Close => Get("close");
    public string Steam => Get("steam");
    public string ChangeFolder => Get("changeFolder");
    public string Bannerlord => Get("bannerlord");
    public string UnpacksInto => Get("unpacksInto");
    public string LastUpdated => Get("lastUpdated");
    public string LaunchWith => Get("launchWith");
    public string Installed => Get("installed");
    public string Live => Get("live");
    public string UpToDate => Get("upToDate");
    public string PatchNotes => Get("patchNotes");
    public string Website => Get("website");
    public string ServersTitle => Get("serversTitle");
    public string Refresh => Get("refresh");
    public string AddServer => Get("addServer");
    public string NamePh => Get("namePh");
    public string AddressPh => Get("addressPh");
    public string PortPh => Get("portPh");
    public string PasswordPh => Get("passwordPh");
    public string Add => Get("add");
    public string Join => Get("join");
    public string SelectServer => Get("selectServer");
    public string PasswordLabel => Get("passwordLabel");
    public string PasswordHint => Get("passwordHint");
    public string Remove => Get("remove");
    public string SettingsTitle => Get("settingsTitle");
    public string SettingsLead => Get("settingsLead");
    public string FeedUrl => Get("feedUrl");
    public string FeedHelp => Get("feedHelp");
    public string SteamFolder => Get("steamFolder");
    public string Browse => Get("browse");
    public string SteamHelp => Get(OperatingSystem.IsMacOS() ? "steamHelpMac" : OperatingSystem.IsLinux() ? "steamHelpLinux" : "steamHelp");
    public string Downloads => Get("downloads");
    public string RequireCheck => Get("requireCheck");
    public string RequireHelp => Get("requireHelp");
    public string Admin => Get("admin");
    public string AdminPasswordPh => Get("adminPasswordPh");
    public string LogIn => Get("logIn");
    public string R2Key => Get("r2Key");
    public string AccessKeyPh => Get("accessKeyPh");
    public string R2Secret => Get("r2Secret");
    public string SecretKeyPh => Get("secretKeyPh");
    public string SaveKey => Get("saveKey");
    public string Upload => Get("upload");
    public string Cancel => Get("cancel");
    public string Save => Get("save");
    public string SetupTitle => Get("setupTitle");
    public string SetupLead => Get("setupLead");
    public string SetupWorkshopk => Get("setupWorkshopk");
    public string SetupWorkshop => Get("setupWorkshop");
    public string SetupWorkshopLink => Get("setupWorkshopLink");
    public string SetupStep1k => Get("setupStep1k");
    public string SetupStep1 => Get(OperatingSystem.IsMacOS() ? "setupStep1Mac" : "setupStep1");
    public string SetupStep2k => Get(OperatingSystem.IsMacOS() ? "setupStep2kMac" : OperatingSystem.IsLinux() ? "setupStep2kLinux" : "setupStep2k");
    public string SetupStep2 => Get(OperatingSystem.IsMacOS() ? "setupStep2Mac" : OperatingSystem.IsLinux() ? "setupStep2Linux" : "setupStep2");
    public string SetupStep3k => Get("setupStep3k");
    public string SetupStep3 => Get("setupStep3");
    public string SetupFound => Get("setupFound");
    public string SetupUse => Get("setupUse");
    public string SetupChooseOther => Get("setupChooseOther");
    public string SetupChoose => Get("setupChoose");

    private static readonly Dictionary<string, string[]> Map = new()
    {
        ["launcherSubtitle"] = ["MODPACK LAUNCHER", "LANZADOR DEL MODPACK", "LANCEUR DU MODPACK", "MODPACK-STARTER", "ЛАУНЧЕР МОДПАКА"],
        ["modpack"] = ["MODPACK", "MODPACK", "MODPACK", "MODPACK", "МОДПАК"],
        ["servers"] = ["SERVERS", "SERVIDORES", "SERVEURS", "SERVER", "СЕРВЕРЫ"],
        ["settings"] = ["SETTINGS", "AJUSTES", "RÉGLAGES", "EINSTELLUNGEN", "НАСТРОЙКИ"],
        ["minimize"] = ["Minimize", "Minimizar", "Réduire", "Minimieren", "Свернуть"],
        ["close"] = ["Close", "Cerrar", "Fermer", "Schließen", "Закрыть"],
        ["steam"] = ["STEAM", "STEAM", "STEAM", "STEAM", "STEAM"],
        ["changeFolder"] = ["CHANGE FOLDER", "CAMBIAR CARPETA", "CHANGER DE DOSSIER", "ORDNER ÄNDERN", "СМЕНИТЬ ПАПКУ"],
        ["bannerlord"] = ["BANNERLORD", "BANNERLORD", "BANNERLORD", "BANNERLORD", "BANNERLORD"],
        ["unpacksInto"] = ["UNPACKS INTO", "SE DESCOMPRIME EN", "SE DÉCOMPRESSE DANS", "WIRD ENTPACKT NACH", "РАСПАКОВКА В"],
        ["lastUpdated"] = ["LAST UPDATED", "ÚLTIMA ACTUALIZACIÓN", "DERNIÈRE MISE À JOUR", "ZULETZT AKTUALISIERT", "ОБНОВЛЕНО"],
        ["launchWith"] = ["LAUNCH WITH", "INICIAR CON", "LANCER AVEC", "STARTEN MIT", "ЗАПУСК С"],
        ["installed"] = ["INSTALLED", "INSTALADO", "INSTALLÉ", "INSTALLIERT", "УСТАНОВЛЕНО"],
        ["live"] = ["LIVE", "EN VIVO", "EN LIGNE", "LIVE", "В СЕТИ"],
        ["upToDate"] = ["You're on the live version", "Estás en la versión en vivo", "Tu es sur la version en ligne", "Du bist auf der Live-Version", "У тебя живая версия"],
        ["patchNotes"] = ["PATCH NOTES", "NOTAS DEL PARCHE", "NOTES DE PATCH", "PATCHNOTES", "ЗАМЕТКИ ПАТЧА"],
        ["website"] = ["Check out our website:", "Visita nuestra web:", "Voir notre site :", "Unsere Website:", "Наш сайт:"],
        ["statusReady"] = ["READY", "LISTO", "PRÊT", "BEREIT", "ГОТОВО"],
        ["statusAction"] = ["ACTION NEEDED", "HACE FALTA ACCIÓN", "ACTION REQUISE", "HANDLUNG NÖTIG", "НУЖНО ДЕЙСТВИЕ"],
        ["statusBlocked"] = ["BLOCKED", "BLOQUEADO", "BLOQUÉ", "BLOCKIERT", "ЗАБЛОКИРОВАНО"],
        ["statusWorking"] = ["WORKING", "EN CURSO", "EN COURS", "LÄUFT", "ИДЁТ"],
        ["working"] = ["Working", "Trabajando", "Patientez", "Moment", "Секунду"],
        ["workingDetail"] = ["Hang on a moment.", "Un momento.", "Un instant.", "Einen Moment.", "Секунду."],
        ["selectSteam"] = ["Select your Steam folder", "Elige tu carpeta de Steam", "Choisis ton dossier Steam", "Wähle deinen Steam-Ordner", "Укажи папку Steam"],
        ["selectSteamDetail"] = [
            "We'll find Bannerlord and unpack CI Modpack into its Modules folder, or into the Steam Workshop folder if you already have it there.",
            "Buscaremos Bannerlord y descomprimiremos CI Modpack en su carpeta Modules, o en la de Steam Workshop si ya lo tienes ahí.",
            "Nous trouverons Bannerlord et décompresserons CI Modpack dans son dossier Modules, ou dans le Workshop Steam s'il y est déjà.",
            "Wir finden Bannerlord und entpacken CI Modpack in den Modules-Ordner, oder in den Steam-Workshop-Ordner, falls es dort schon liegt.",
            "Мы найдём Bannerlord и распакуем CI Modpack в папку Modules или в папку Steam Workshop, если модпак уже там."],
        ["feedMissing"] = ["Modpack feed is missing", "Falta la fuente del modpack", "Le flux du modpack est absent", "Der Modpack-Feed fehlt", "Нет адреса модпака"],
        ["feedMissingDetail"] = ["This build has no modpack address.", "Esta versión no tiene dirección del modpack.", "Cette version n'a pas d'adresse de modpack.", "Dieser Build hat keine Modpack-Adresse.", "В этой сборке нет адреса модпака."],
        ["checking"] = ["Checking the live modpack", "Comprobando el modpack en vivo", "Vérification du modpack en ligne", "Live-Modpack wird geprüft", "Проверяем живой модпак"],
        ["checkingDetail"] = ["Comparing this PC with the current version.", "Comparando este PC con la versión actual.", "Comparaison de ce PC avec la version actuelle.", "Dieser PC wird mit der aktuellen Version verglichen.", "Сравниваем этот ПК с текущей версией."],
        ["offline"] = ["Can't reach the modpack feed", "No se alcanza la fuente del modpack", "Impossible de joindre le flux", "Der Modpack-Feed ist nicht erreichbar", "Не удаётся связаться с модпаком"],
        ["offlineFallback"] = ["Check the connection and try again.", "Revisa la conexión e inténtalo de nuevo.", "Vérifie la connexion et réessaie.", "Prüfe die Verbindung und versuche es erneut.", "Проверь соединение и попробуй снова."],
        ["filesChangedOffline"] = ["Modpack files changed", "Los archivos del modpack cambiaron", "Les fichiers du modpack ont changé", "Modpack-Dateien haben sich geändert", "Файлы модпака изменились"],
        ["filesChangedOfflineDetail"] = [
            "The feed didn't answer, and the local files don't match the last install.",
            "La fuente no respondió y los archivos locales no coinciden con la última instalación.",
            "Le flux n'a pas répondu et les fichiers locaux ne correspondent pas à la dernière installation.",
            "Der Feed hat nicht geantwortet und die lokalen Dateien passen nicht zur letzten Installation.",
            "Сервер не ответил, а локальные файлы не совпадают с последней установкой."],
        ["readyOffline"] = ["Ready to play", "Listo para jugar", "Prêt à jouer", "Bereit zum Spielen", "Можно играть"],
        ["readyOfflineDetail"] = [
            "The feed didn't answer. This PC is still on {0}.",
            "La fuente no respondió. Este PC sigue en {0}.",
            "Le flux n'a pas répondu. Ce PC est encore en {0}.",
            "Der Feed hat nicht geantwortet. Dieser PC ist noch auf {0}.",
            "Сервер не ответил. На этом ПК всё ещё {0}."],
        ["notInstalled"] = ["Modpack not installed", "Modpack no instalado", "Modpack non installé", "Modpack nicht installiert", "Модпак не установлен"],
        ["notInstalledDetail"] = [
            "Download {0} into the Modules folder, or into the Steam Workshop folder if CI Modpack is already there.",
            "Descarga {0} en la carpeta Modules, o en la de Steam Workshop si CI Modpack ya está ahí.",
            "Télécharge {0} dans le dossier Modules, ou dans le Workshop Steam si CI Modpack y est déjà.",
            "Lade {0} in den Modules-Ordner, oder in den Steam-Workshop-Ordner, falls CI Modpack dort schon liegt.",
            "Скачай {0} в папку Modules или в папку Steam Workshop, если CI Modpack уже там."],
        ["updateRequired"] = ["Update required", "Actualización necesaria", "Mise à jour requise", "Update erforderlich", "Нужно обновление"],
        ["updateRequiredDetail"] = [
            "You have {0}. Live is {1}. Update before you join.",
            "Tienes {0}. En vivo está {1}. Actualiza antes de unirte.",
            "Tu as {0}. En ligne : {1}. Mets à jour avant de rejoindre.",
            "Du hast {0}. Live ist {1}. Aktualisiere, bevor du beitrittst.",
            "У тебя {0}. Сейчас в сети {1}. Обновись перед входом."],
        ["filesChanged"] = ["Files don't match", "Los archivos no coinciden", "Les fichiers ne correspondent pas", "Dateien stimmen nicht überein", "Файлы не совпадают"],
        ["filesChangedDetail"] = [
            "Something in the modpack folder changed. Repair it before you join.",
            "Algo cambió en la carpeta del modpack. Repáralo antes de unirte.",
            "Quelque chose a changé dans le dossier du modpack. Répare-le avant de rejoindre.",
            "Im Modpack-Ordner hat sich etwas geändert. Repariere es, bevor du beitrittst.",
            "В папке модпака что-то изменилось. Почини перед входом."],
        ["starting"] = ["Bannerlord is starting", "Bannerlord se está abriendo", "Bannerlord démarre", "Bannerlord startet", "Bannerlord запускается"],
        ["startingDetail"] = [
            "Join from the multiplayer list. Close the game before updating the modpack.",
            "Únete desde la lista multijugador. Cierra el juego antes de actualizar el modpack.",
            "Rejoins depuis la liste multijoueur. Ferme le jeu avant de mettre à jour le modpack.",
            "Tritt über die Mehrspielerliste bei. Schließe das Spiel, bevor du das Modpack aktualisierst.",
            "Заходи из списка мультиплеера. Закрой игру перед обновлением модпака."],
        ["gameOpen"] = ["Bannerlord is open", "Bannerlord está abierto", "Bannerlord est ouvert", "Bannerlord ist geöffnet", "Bannerlord открыт"],
        ["gameOpenDetail"] = ["Close it before launching again.", "Ciérralo antes de volver a iniciar.", "Ferme-le avant de relancer.", "Schließe es, bevor du erneut startest.", "Закрой его перед новым запуском."],
        ["ready"] = ["Ready to play", "Listo para jugar", "Prêt à jouer", "Bereit zum Spielen", "Можно играть"],
        ["readyDetail"] = [
            "Your modpack matches {0}. You can join.",
            "Tu modpack coincide con {0}. Puedes unirte.",
            "Ton modpack correspond à {0}. Tu peux rejoindre.",
            "Dein Modpack entspricht {0}. Du kannst beitreten.",
            "Твой модпак совпадает с {0}. Можно заходить."],
        ["play"] = ["PLAY", "JUGAR", "JOUER", "SPIELEN", "ИГРАТЬ"],
        ["download"] = ["DOWNLOAD", "DESCARGAR", "TÉLÉCHARGER", "HERUNTERLADEN", "СКАЧАТЬ"],
        ["update"] = ["UPDATE", "ACTUALIZAR", "METTRE À JOUR", "AKTUALISIEREN", "ОБНОВИТЬ"],
        ["repair"] = ["REPAIR", "REPARAR", "RÉPARER", "REPARIEREN", "ПОЧИНИТЬ"],
        ["selectSteamBtn"] = ["SELECT STEAM", "ELEGIR STEAM", "CHOISIR STEAM", "STEAM WÄHLEN", "ВЫБРАТЬ STEAM"],
        ["retry"] = ["RETRY", "REINTENTAR", "RÉESSAYER", "ERNEUT", "ПОВТОРИТЬ"],
        ["pleaseWait"] = ["PLEASE WAIT", "ESPERA", "PATIENTE", "BITTE WARTEN", "ПОДОЖДИ"],
        ["cancel"] = ["CANCEL", "CANCELAR", "ANNULER", "ABBRECHEN", "ОТМЕНА"],
        ["save"] = ["SAVE", "GUARDAR", "ENREGISTRER", "SPEICHERN", "СОХРАНИТЬ"],
        ["downloading"] = ["Downloading", "Descargando", "Téléchargement", "Wird geladen", "Скачивание"],
        ["checkingFiles"] = ["Checking your files", "Comprobando tus archivos", "Vérification de tes fichiers", "Dateien werden geprüft", "Проверяем файлы"],
        ["cleaning"] = ["Cleaning up", "Limpiando", "Nettoyage", "Aufräumen", "Очистка"],
        ["unpackingWorkshop"] = ["Unpacking into the Workshop folder", "Descomprimiendo en la carpeta de Workshop", "Décompression dans le dossier Workshop", "Entpacken in den Workshop-Ordner", "Распаковка в папку Workshop"],
        ["unpackingModules"] = ["Unpacking into Modules", "Descomprimiendo en Modules", "Décompression dans Modules", "Entpacken nach Modules", "Распаковка в Modules"],
        ["copying"] = ["Copying into the game folder", "Copiando a la carpeta del juego", "Copie dans le dossier du jeu", "Kopieren in den Spielordner", "Копирование в папку игры"],
        ["removingOld"] = ["Removing old files", "Quitando archivos viejos", "Suppression des anciens fichiers", "Alte Dateien werden entfernt", "Удаляем старые файлы"],
        ["closingGame"] = ["Closing Bannerlord", "Cerrando Bannerlord", "Fermeture de Bannerlord", "Bannerlord wird geschlossen", "Закрываем Bannerlord"],
        ["closingGameDetail"] = [
            "Steam still has the game open. Closing it, then updating the modpack.",
            "Steam todavía tiene el juego abierto. Lo cerramos y luego actualizamos el modpack.",
            "Steam a encore le jeu ouvert. Fermeture, puis mise à jour du modpack.",
            "Steam hat das Spiel noch offen. Es wird geschlossen, danach wird das Modpack aktualisiert.",
            "Steam ещё держит игру открытой. Закрываем её и обновляем модпак."],
        ["startingGame"] = ["Starting Bannerlord", "Iniciando Bannerlord", "Lancement de Bannerlord", "Bannerlord wird gestartet", "Запуск Bannerlord"],
        ["startingPlayDetail"] = ["Opening multiplayer with the CI modpack.", "Abriendo el multijugador con el modpack de CI.", "Ouverture du multijoueur avec le modpack CI.", "Mehrspieler wird mit dem CI-Modpack geöffnet.", "Открываем мультиплеер с модпаком CI."],
        ["startingJoinDetail"] = ["Opening multiplayer and joining {0}.", "Abriendo el multijugador y uniéndote a {0}.", "Ouverture du multijoueur et connexion à {0}.", "Mehrspieler wird geöffnet und {0} beigetreten.", "Открываем мультиплеер и заходим на {0}."],
        ["playLocked"] = ["Play stays locked until this install matches the feed.", "Jugar sigue bloqueado hasta que esta instalación coincida con la fuente.", "Jouer reste bloqué tant que cette installation ne correspond pas au flux.", "Spielen bleibt gesperrt, bis diese Installation zum Feed passt.", "Играть нельзя, пока установка не совпадёт с сервером."],
        ["joinLocked"] = ["Join stays locked until this install matches the feed.", "Unirse sigue bloqueado hasta que esta instalación coincida con la fuente.", "Rejoindre reste bloqué tant que cette installation ne correspond pas au flux.", "Beitreten bleibt gesperrt, bis diese Installation zum Feed passt.", "Войти нельзя, пока установка не совпадёт с сервером."],
        ["startedPlayDetail"] = ["Join from the multiplayer list. This modpack matches {0}.", "Únete desde la lista multijugador. Este modpack coincide con {0}.", "Rejoins depuis la liste multijoueur. Ce modpack correspond à {0}.", "Tritt über die Mehrspielerliste bei. Dieses Modpack entspricht {0}.", "Заходи из списка мультиплеера. Этот модпак совпадает с {0}."],
        ["startedJoinDetail"] = ["Bannerlord is joining {0}. This modpack matches {1}.", "Bannerlord se está uniendo a {0}. Este modpack coincide con {1}.", "Bannerlord rejoint {0}. Ce modpack correspond à {1}.", "Bannerlord tritt {0} bei. Dieses Modpack entspricht {1}.", "Bannerlord заходит на {0}. Этот модпак совпадает с {1}."],
        ["gameClosed"] = ["Bannerlord closed straight away. Open Steam, then press Play again.", "Bannerlord se cerró al momento. Abre Steam y pulsa Jugar otra vez.", "Bannerlord s'est fermé tout de suite. Ouvre Steam, puis appuie de nouveau sur Jouer.", "Bannerlord wurde sofort geschlossen. Öffne Steam und drücke erneut Spielen.", "Bannerlord сразу закрылся. Открой Steam и нажми Играть ещё раз."],
        ["workshopStill"] = ["The Workshop folder still shows {0}. Exit Steam completely, then update again.", "La carpeta de Workshop todavía muestra {0}. Cierra Steam por completo y actualiza otra vez.", "Le dossier Workshop affiche encore {0}. Quitte Steam complètement, puis mets à jour de nouveau.", "Der Workshop-Ordner zeigt noch {0}. Beende Steam vollständig und aktualisiere erneut.", "В папке Workshop всё ещё {0}. Полностью закрой Steam и обнови снова."],
        ["noModpack"] = ["no modpack", "ningún modpack", "aucun modpack", "kein Modpack", "нет модпака"],
        ["cancelled"] = ["Cancelled.", "Cancelado.", "Annulé.", "Abgebrochen.", "Отменено."],
        ["feedTimeout"] = ["The feed took too long to answer.", "La fuente tardó demasiado en responder.", "Le flux a mis trop de temps à répondre.", "Der Feed hat zu lange nicht geantwortet.", "Сервер слишком долго не отвечал."],
        ["downloadFailed"] = ["Couldn't download the modpack. {0}", "No se pudo descargar el modpack. {0}", "Impossible de télécharger le modpack. {0}", "Das Modpack konnte nicht geladen werden. {0}", "Не удалось скачать модпак. {0}"],
        ["folderLocked"] = ["Bannerlord still has the Workshop folder open. Close the game in Steam, then update again.", "Bannerlord todavía tiene abierta la carpeta de Workshop. Cierra el juego en Steam y actualiza otra vez.", "Bannerlord a encore le dossier Workshop ouvert. Ferme le jeu dans Steam, puis mets à jour de nouveau.", "Bannerlord hat den Workshop-Ordner noch offen. Schließe das Spiel in Steam und aktualisiere erneut.", "Bannerlord всё ещё держит папку Workshop. Закрой игру в Steam и обнови снова."],
        ["timeLeft"] = [" · {0} left", " · quedan {0}", " · reste {0}", " · noch {0}", " · осталось {0}"],
        ["notSelected"] = ["Not selected", "No seleccionado", "Non sélectionné", "Nicht ausgewählt", "Не выбрано"],
        ["notInstalledShort"] = ["Not installed", "No instalado", "Non installé", "Nicht installiert", "Не установлен"],
        ["selectSteamFirst"] = ["Select Steam first", "Elige Steam primero", "Choisis d'abord Steam", "Zuerst Steam wählen", "Сначала выбери Steam"],
        ["foundTick"] = ["Bannerlord found. Tick any other mods to launch with CI Modpack.", "Bannerlord encontrado. Marca otros mods para iniciarlos con CI Modpack.", "Bannerlord trouvé. Coche d'autres mods pour les lancer avec CI Modpack.", "Bannerlord gefunden. Hake weitere Mods an, um sie mit CI Modpack zu starten.", "Bannerlord найден. Отметь другие моды, чтобы запустить их вместе с CI Modpack."],
        ["foundWorkshopPending"] = ["Bannerlord found. CI Modpack will unpack into the Steam Workshop folder.", "Bannerlord encontrado. CI Modpack se descomprimirá en la carpeta de Steam Workshop.", "Bannerlord trouvé. CI Modpack sera décompressé dans le dossier Workshop Steam.", "Bannerlord gefunden. CI Modpack wird in den Steam-Workshop-Ordner entpackt.", "Bannerlord найден. CI Modpack распакуется в папку Steam Workshop."],
        ["foundNotInstalled"] = ["Bannerlord found. CI Modpack is not installed yet.", "Bannerlord encontrado. CI Modpack todavía no está instalado.", "Bannerlord trouvé. CI Modpack n'est pas encore installé.", "Bannerlord gefunden. CI Modpack ist noch nicht installiert.", "Bannerlord найден. CI Modpack ещё не установлен."],
        ["keepTop"] = ["Keep at the top", "Fijar arriba", "Garder en haut", "Oben halten", "Держать сверху"],
        ["removeTop"] = ["Remove from the top", "Quitar de arriba", "Retirer du haut", "Von oben entfernen", "Убрать сверху"],
        ["serversTitle"] = ["Servers", "Servidores", "Serveurs", "Server", "Серверы"],
        ["refresh"] = ["REFRESH", "ACTUALIZAR", "ACTUALISER", "AKTUALISIEREN", "ОБНОВИТЬ"],
        ["addServer"] = ["ADD A SERVER", "AÑADIR UN SERVIDOR", "AJOUTER UN SERVEUR", "SERVER HINZUFÜGEN", "ДОБАВИТЬ СЕРВЕР"],
        ["namePh"] = ["Name", "Nombre", "Nom", "Name", "Имя"],
        ["addressPh"] = ["Address", "Dirección", "Adresse", "Adresse", "Адрес"],
        ["portPh"] = ["Port", "Puerto", "Port", "Port", "Порт"],
        ["passwordPh"] = ["Password, if it has one", "Contraseña, si tiene", "Mot de passe, s'il y en a un", "Passwort, falls vorhanden", "Пароль, если есть"],
        ["add"] = ["ADD", "AÑADIR", "AJOUTER", "HINZUFÜGEN", "ДОБАВИТЬ"],
        ["join"] = ["JOIN", "UNIRSE", "REJOINDRE", "BEITRETEN", "ВОЙТИ"],
        ["selectServer"] = ["Select a server.", "Elige un servidor.", "Choisis un serveur.", "Wähle einen Server.", "Выбери сервер."],
        ["passwordLabel"] = ["PASSWORD", "CONTRASEÑA", "MOT DE PASSE", "PASSWORT", "ПАРОЛЬ"],
        ["passwordHint"] = ["Leave empty if the server is open", "Déjalo vacío si el servidor es abierto", "Laisse vide si le serveur est ouvert", "Leer lassen, wenn der Server offen ist", "Оставь пустым, если сервер открыт"],
        ["remove"] = ["REMOVE", "QUITAR", "RETIRER", "ENTFERNEN", "УДАЛИТЬ"],
        ["joinSelect"] = ["Select a server. Bannerlord opens straight into it.", "Elige un servidor. Bannerlord se abre directamente en él.", "Choisis un serveur. Bannerlord s'ouvre directement dessus.", "Wähle einen Server. Bannerlord öffnet sich direkt dort.", "Выбери сервер. Bannerlord откроется сразу на нём."],
        ["joinOffline"] = ["{0} is offline. It has to be running before Bannerlord can join it.", "{0} está desconectado. Tiene que estar en marcha para que Bannerlord pueda unirse.", "{0} est hors ligne. Il doit tourner avant que Bannerlord puisse le rejoindre.", "{0} ist offline. Er muss laufen, bevor Bannerlord beitreten kann.", "{0} не в сети. Он должен работать, чтобы Bannerlord мог зайти."],
        ["joinGoes"] = ["Bannerlord opens in multiplayer and joins {0} through the lobby.", "Bannerlord abre el multijugador y se une a {0} por el lobby.", "Bannerlord ouvre le multijoueur et rejoint {0} via le lobby.", "Bannerlord öffnet den Mehrspieler und tritt {0} über die Lobby bei.", "Bannerlord открывает мультиплеер и заходит на {0} через лобби."],
        ["serversPending"] = ["The server list loads when you open Servers.", "La lista de servidores se carga al abrir Servidores.", "La liste des serveurs se charge quand tu ouvres Serveurs.", "Die Serverliste lädt, wenn du Server öffnest.", "Список серверов загрузится, когда откроешь Серверы."],
        ["serversFail"] = ["Couldn't read the CI server list.", "No se pudo leer la lista de servidores de CI.", "Impossible de lire la liste des serveurs CI.", "Die CI-Serverliste konnte nicht gelesen werden.", "Не удалось прочитать список серверов CI."],
        ["serversFailDetail"] = ["Couldn't read the CI server list. {0}", "No se pudo leer la lista de servidores de CI. {0}", "Impossible de lire la liste des serveurs CI. {0}", "Die CI-Serverliste konnte nicht gelesen werden. {0}", "Не удалось прочитать список серверов CI. {0}"],
        ["serversEmpty"] = ["The CI site has no servers yet. Add an address below.", "La web de CI todavía no tiene servidores. Añade una dirección abajo.", "Le site CI n'a pas encore de serveurs. Ajoute une adresse ci-dessous.", "Die CI-Seite hat noch keine Server. Füge unten eine Adresse hinzu.", "На сайте CI пока нет серверов. Добавь адрес ниже."],
        ["serversOnline"] = ["{0} online of {1} CI servers.", "{0} en línea de {1} servidores de CI.", "{0} en ligne sur {1} serveurs CI.", "{0} online von {1} CI-Servern.", "{0} в сети из {1} серверов CI."],
        ["serversAdded"] = ["{0} added on this PC.", "{0} añadidos en este PC.", "{0} ajoutés sur ce PC.", "{0} auf diesem PC hinzugefügt.", "{0} добавлено на этом ПК."],
        ["serverExists"] = ["That server is already in the list.", "Ese servidor ya está en la lista.", "Ce serveur est déjà dans la liste.", "Dieser Server ist schon in der Liste.", "Этот сервер уже в списке."],
        ["serverAddFail"] = ["That server could not be added.", "No se pudo añadir ese servidor.", "Ce serveur n'a pas pu être ajouté.", "Dieser Server konnte nicht hinzugefügt werden.", "Не удалось добавить этот сервер."],
        ["added"] = ["ADDED", "AÑADIDO", "AJOUTÉ", "HINZU", "ДОБАВЛЕН"],
        ["offlineBadge"] = ["OFFLINE", "DESCONECTADO", "HORS LIGNE", "OFFLINE", "НЕ В СЕТИ"],
        ["feedBadge"] = ["FEED", "FUENTE", "FLUX", "FEED", "ЛЕНТА"],
        ["onlineWord"] = ["Online", "En línea", "En ligne", "Online", "В сети"],
        ["offlineWord"] = ["Offline", "Desconectado", "Hors ligne", "Offline", "Не в сети"],
        ["playing"] = ["{0} playing", "{0} jugando", "{0} en jeu", "{0} spielen", "{0} играют"],
        ["settingsTitle"] = ["Settings", "Ajustes", "Réglages", "Einstellungen", "Настройки"],
        ["settingsLead"] = ["Game folder, downloads, and the update feed.", "Carpeta del juego, descargas y la fuente de actualizaciones.", "Dossier du jeu, téléchargements et flux de mise à jour.", "Spielordner, Downloads und der Update-Feed.", "Папка игры, загрузки и адрес обновлений."],
        ["feedUrl"] = ["FEED", "FUENTE", "FLUX", "FEED", "АДРЕС"],
        ["feedHelp"] = ["HTTPS address of manifest.json. Nothing is downloaded until this is set.", "Dirección HTTPS de manifest.json. No se descarga nada hasta que esté puesta.", "Adresse HTTPS de manifest.json. Rien n'est téléchargé tant qu'elle n'est pas définie.", "HTTPS-Adresse von manifest.json. Es wird nichts geladen, solange sie leer ist.", "HTTPS-адрес manifest.json. Пока он пуст, ничего не скачивается."],
        ["steamFolder"] = ["STEAM FOLDER", "CARPETA DE STEAM", "DOSSIER STEAM", "STEAM-ORDNER", "ПАПКА STEAM"],
        ["browse"] = ["BROWSE", "EXAMINAR", "PARCOURIR", "DURCHSUCHEN", "ОБЗОР"],
        ["steamHelp"] = [
            "The folder that contains steam.exe. Bannerlord can sit on another drive; the launcher follows Steam's library list and unpacks into its Modules folder.",
            "La carpeta que contiene steam.exe. Bannerlord puede estar en otro disco; el lanzador sigue las bibliotecas de Steam y descomprime en su carpeta Modules.",
            "Le dossier qui contient steam.exe. Bannerlord peut être sur un autre disque ; le lanceur suit les bibliothèques Steam et décompresse dans son dossier Modules.",
            "Der Ordner mit steam.exe. Bannerlord kann auf einem anderen Laufwerk liegen; der Launcher folgt Steams Bibliotheken und entpackt in den Modules-Ordner.",
            "Папка, где лежит steam.exe. Bannerlord может быть на другом диске; лаунчер смотрит библиотеки Steam и распаковывает в папку Modules."],
        ["steamHelpLinux"] = [
            "The folder that contains steam.sh. It is usually Home/.steam/steam or Home/.local/share/Steam. Bannerlord can sit on another drive; the launcher follows Steam's library list.",
            "La carpeta que contiene steam.sh. Suele ser Home/.steam/steam o Home/.local/share/Steam. Bannerlord puede estar en otro disco; el lanzador sigue las bibliotecas de Steam.",
            "Le dossier qui contient steam.sh. En général Home/.steam/steam ou Home/.local/share/Steam. Bannerlord peut être sur un autre disque ; le lanceur suit les bibliothèques Steam.",
            "Der Ordner mit steam.sh. Meist Home/.steam/steam oder Home/.local/share/Steam. Bannerlord kann auf einem anderen Laufwerk liegen; der Launcher folgt Steams Bibliotheken.",
            "Папка со steam.sh. Обычно это Home/.steam/steam или Home/.local/share/Steam. Bannerlord может быть на другом диске; лаунчер смотрит библиотеки Steam."],
        ["steamHelpMac"] = [
            "On a Mac this is usually Home/Library/Application Support/Steam. Bannerlord has no Mac version: if you play through CrossOver, choose the Steam folder inside that bottle, the one with steam.exe.",
            "En un Mac suele ser Home/Library/Application Support/Steam. Bannerlord no tiene versión para Mac: si juegas con CrossOver, elige la carpeta de Steam dentro de esa botella, la que tiene steam.exe.",
            "Sur un Mac, c'est en général Home/Library/Application Support/Steam. Bannerlord n'a pas de version Mac : si tu joues avec CrossOver, choisis le dossier Steam dans cette bouteille, celui qui contient steam.exe.",
            "Auf einem Mac ist das meist Home/Library/Application Support/Steam. Bannerlord hat keine Mac-Version: wenn du über CrossOver spielst, wähle den Steam-Ordner in dieser Bottle, den mit steam.exe.",
            "На Mac это обычно Home/Library/Application Support/Steam. У Bannerlord нет версии для Mac: если играешь через CrossOver, выбери папку Steam внутри этой бутылки, ту, где steam.exe."],
        ["downloads"] = ["DOWNLOADS", "DESCARGAS", "TÉLÉCHARGEMENTS", "DOWNLOADS", "ЗАГРУЗКИ"],
        ["parallel"] = ["{0} parallel downloads", "{0} descargas en paralelo", "{0} téléchargements en parallèle", "{0} parallele Downloads", "{0} параллельных загрузок"],
        ["requireCheck"] = ["Require a live version check before Play", "Exigir una comprobación de la versión en vivo antes de Jugar", "Exiger une vérification de la version en ligne avant Jouer", "Live-Versionsprüfung vor dem Spielen verlangen", "Проверять живую версию перед игрой"],
        ["requireHelp"] = [
            "Leave this on. Turn it off only when the feed is down and you already have the right pack.",
            "Déjalo activado. Apágalo solo si la fuente está caída y ya tienes el pack correcto.",
            "Laisse ceci activé. Coupe-le seulement si le flux est en panne et que tu as déjà le bon pack.",
            "Lass das an. Schalte es nur aus, wenn der Feed down ist und du schon das richtige Pack hast.",
            "Оставь включённым. Выключай только если сервер лежит и нужный пак уже стоит."],
        ["admin"] = ["ADMIN", "ADMIN", "ADMIN", "ADMIN", "АДМИН"],
        ["adminPasswordPh"] = ["Admin password", "Contraseña de admin", "Mot de passe admin", "Admin-Passwort", "Пароль админа"],
        ["logIn"] = ["LOG IN", "ENTRAR", "CONNEXION", "ANMELDEN", "ВОЙТИ"],
        ["badPassword"] = ["That password is not an admin password.", "Esa contraseña no es de admin.", "Ce mot de passe n'est pas un mot de passe admin.", "Das ist kein Admin-Passwort.", "Это не пароль админа."],
        ["r2Key"] = ["R2 ACCESS KEY", "CLAVE DE ACCESO R2", "CLÉ D'ACCÈS R2", "R2-ZUGRIFFSSCHLÜSSEL", "КЛЮЧ ДОСТУПА R2"],
        ["accessKeyPh"] = ["Access key id", "Id de la clave", "Id de la clé", "Zugriffsschlüssel-ID", "Идентификатор ключа"],
        ["r2Secret"] = ["R2 SECRET", "SECRETO R2", "SECRET R2", "R2-GEHEIMNIS", "СЕКРЕТ R2"],
        ["secretKeyPh"] = ["Secret access key", "Clave secreta", "Clé secrète", "Geheimer Schlüssel", "Секретный ключ"],
        ["saveKey"] = ["SAVE KEY", "GUARDAR CLAVE", "ENREGISTRER LA CLÉ", "SCHLÜSSEL SPEICHERN", "СОХРАНИТЬ КЛЮЧ"],
        ["upload"] = ["UPLOAD MODPACK", "SUBIR MODPACK", "ENVOYER LE MODPACK", "MODPACK HOCHLADEN", "ВЫГРУЗИТЬ МОДПАК"],
        ["r2Paste"] = [
            "Paste the R2 access key once. It is saved for this Windows user and is not put in the launcher players download.",
            "Pega la clave de acceso R2 una vez. Se guarda para este usuario de Windows y no va en el lanzador que descargan los jugadores.",
            "Colle la clé d'accès R2 une fois. Elle est enregistrée pour cet utilisateur Windows et n'est pas mise dans le lanceur que les joueurs téléchargent.",
            "Füge den R2-Zugriffsschlüssel einmal ein. Er wird für diesen Windows-Benutzer gespeichert und kommt nicht in den Launcher, den Spieler laden.",
            "Вставь ключ доступа R2 один раз. Он сохранится для этого пользователя Windows и не попадёт в лаунчер, который качают игроки."],
        ["r2Replace"] = [
            "An R2 key is already saved on this PC. Paste a new one only to replace it.",
            "Ya hay una clave R2 guardada en este PC. Pega una nueva solo para sustituirla.",
            "Une clé R2 est déjà enregistrée sur ce PC. Colle-en une nouvelle seulement pour la remplacer.",
            "Ein R2-Schlüssel ist auf diesem PC schon gespeichert. Füge nur einen neuen ein, um ihn zu ersetzen.",
            "Ключ R2 уже сохранён на этом ПК. Вставляй новый только чтобы заменить его."],
        ["r2Saved"] = ["R2 key saved on this PC.", "Clave R2 guardada en este PC.", "Clé R2 enregistrée sur ce PC.", "R2-Schlüssel auf diesem PC gespeichert.", "Ключ R2 сохранён на этом ПК."],
        ["modpackMissing"] = ["CI Modpack was not found. Save the Steam folder first.", "No se encontró CI Modpack. Guarda primero la carpeta de Steam.", "CI Modpack est introuvable. Enregistre d'abord le dossier Steam.", "CI Modpack wurde nicht gefunden. Speichere zuerst den Steam-Ordner.", "CI Modpack не найден. Сначала сохрани папку Steam."],
        ["liveFeedNow"] = ["Live feed is now {0}.", "La fuente en vivo ahora es {0}.", "Le flux en ligne est maintenant {0}.", "Der Live-Feed ist jetzt {0}.", "Живая лента теперь {0}."],
        ["uploadCancelled"] = ["Upload cancelled.", "Subida cancelada.", "Envoi annulé.", "Upload abgebrochen.", "Выгрузка отменена."],
        ["r2NeedBoth"] = ["Enter the R2 access key and secret. They stay on this PC.", "Escribe la clave de acceso y el secreto de R2. Se quedan en este PC.", "Saisis la clé d'accès et le secret R2. Ils restent sur ce PC.", "Gib den R2-Zugriffsschlüssel und das Geheimnis ein. Sie bleiben auf diesem PC.", "Введи ключ доступа и секрет R2. Они останутся на этом ПК."],
        ["errFolderMissing"] = ["That folder doesn't exist.", "Esa carpeta no existe.", "Ce dossier n'existe pas.", "Dieser Ordner existiert nicht.", "Этой папки нет."],
        ["errSteamExe"] = ["Select your Steam folder — the one that contains steam.exe.", "Elige tu carpeta de Steam, la que contiene steam.exe.", "Choisis ton dossier Steam, celui qui contient steam.exe.", "Wähle deinen Steam-Ordner — den mit steam.exe.", "Укажи папку Steam, ту, где лежит steam.exe."],
        ["errSteamUnix"] = [
            "Select your Steam folder. On Linux it contains steam.sh. On a Mac it contains Steam.app, or steam.exe if you use CrossOver.",
            "Elige tu carpeta de Steam. En Linux contiene steam.sh. En un Mac contiene Steam.app, o steam.exe si usas CrossOver.",
            "Choisis ton dossier Steam. Sous Linux il contient steam.sh. Sur un Mac il contient Steam.app, ou steam.exe si tu utilises CrossOver.",
            "Wähle deinen Steam-Ordner. Unter Linux enthält er steam.sh. Auf einem Mac enthält er Steam.app, oder steam.exe mit CrossOver.",
            "Укажи папку Steam. В Linux в ней лежит steam.sh. На Mac — Steam.app, или steam.exe, если используешь CrossOver."],
        ["errNoGame"] = [
            "Steam is there, but Bannerlord isn't installed in its libraries. Install the game on Steam, then select the folder again.",
            "Steam está ahí, pero Bannerlord no está en sus bibliotecas. Instala el juego en Steam y vuelve a elegir la carpeta.",
            "Steam est là, mais Bannerlord n'est pas installé dans ses bibliothèques. Installe le jeu sur Steam, puis choisis de nouveau le dossier.",
            "Steam ist da, aber Bannerlord ist nicht in den Bibliotheken installiert. Installiere das Spiel in Steam und wähle den Ordner erneut.",
            "Steam на месте, но Bannerlord не установлен в библиотеках. Поставь игру в Steam и выбери папку снова."],
        ["errName"] = ["Give the server a name, up to 80 characters.", "Ponle un nombre al servidor, de hasta 80 caracteres.", "Donne un nom au serveur, 80 caractères au plus.", "Gib dem Server einen Namen, höchstens 80 Zeichen.", "Дай серверу имя, не длиннее 80 символов."],
        ["errAddress"] = ["Enter the server address, like 1.2.3.4 or 1.2.3.4:7210.", "Escribe la dirección del servidor, como 1.2.3.4 o 1.2.3.4:7210.", "Saisis l'adresse du serveur, comme 1.2.3.4 ou 1.2.3.4:7210.", "Gib die Serveradresse ein, zum Beispiel 1.2.3.4 oder 1.2.3.4:7210.", "Введи адрес сервера, например 1.2.3.4 или 1.2.3.4:7210."],
        ["errPort"] = ["Enter the server port.", "Escribe el puerto del servidor.", "Saisis le port du serveur.", "Gib den Serverport ein.", "Введи порт сервера."],
        ["errPortRange"] = ["The port has to be a number from 1 to 65535.", "El puerto tiene que ser un número del 1 al 65535.", "Le port doit être un nombre de 1 à 65535.", "Der Port muss eine Zahl von 1 bis 65535 sein.", "Порт должен быть числом от 1 до 65535."],
        ["errPortMismatch"] = ["The address and the port don't match.", "La dirección y el puerto no coinciden.", "L'adresse et le port ne correspondent pas.", "Adresse und Port passen nicht zusammen.", "Адрес и порт не совпадают."],
        ["errPassword"] = ["The password is too long.", "La contraseña es demasiado larga.", "Le mot de passe est trop long.", "Das Passwort ist zu lang.", "Пароль слишком длинный."],
        ["setupTitle"] = ["Let's find Steam", "Vamos a encontrar Steam", "Trouvons Steam", "Steam finden", "Найдём Steam"],
        ["setupLead"] = [
            "This launcher was downloaded on its own. Point it at the folder where Steam is installed. We'll find Bannerlord from there and install the modpack.",
            "Este lanzador se descargó por su cuenta. Indica la carpeta donde está instalado Steam. Desde ahí encontraremos Bannerlord e instalaremos el modpack.",
            "Ce lanceur a été téléchargé tout seul. Indique le dossier où Steam est installé. Nous y trouverons Bannerlord et installerons le modpack.",
            "Dieser Launcher wurde einzeln heruntergeladen. Zeig auf den Ordner, in dem Steam installiert ist. Dort finden wir Bannerlord und installieren das Modpack.",
            "Этот лаунчер скачан отдельно. Укажи папку, где установлен Steam. Оттуда мы найдём Bannerlord и поставим модпак."],
        ["setupWorkshopk"] = ["Download CI Modpack on Steam", "Descarga CI Modpack en Steam", "Télécharge CI Modpack sur Steam", "Lade CI Modpack auf Steam", "Скачай CI Modpack в Steam"],
        ["setupWorkshop"] = [
            "Get it from Steam first. This launcher updates that copy.",
            "Consíguelo en Steam primero. Este lanzador actualiza esa copia.",
            "Prends-le d'abord sur Steam. Ce lanceur met à jour cette copie.",
            "Hol es zuerst von Steam. Dieser Launcher aktualisiert diese Kopie.",
            "Сначала возьми его в Steam. Этот лаунчер обновляет эту копию."],
        ["setupWorkshopLink"] = ["Open CI Modpack on Steam", "Abrir CI Modpack en Steam", "Ouvrir CI Modpack sur Steam", "CI Modpack auf Steam öffnen", "Открыть CI Modpack в Steam"],
        ["setupStep1k"] = ["Open the folder window", "Abre la ventana de carpetas", "Ouvre la fenêtre de dossiers", "Ordnerfenster öffnen", "Открой окно папки"],
        ["setupStep1"] = [
            "Press the gold button. A folder window opens so you can point at Steam.",
            "Pulsa el botón dorado. Se abre una ventana para que indiques Steam.",
            "Appuie sur le bouton doré. Une fenêtre s'ouvre pour indiquer Steam.",
            "Drück den goldenen Knopf. Ein Ordnerfenster öffnet sich, damit du Steam zeigen kannst.",
            "Нажми золотую кнопку. Откроется окно, чтобы указать Steam."],
        ["setupStep1Mac"] = [
            "Press the gold button. Finder opens so you can point at Steam.",
            "Pulsa el botón dorado. Se abre Finder para que indiques Steam.",
            "Appuie sur le bouton doré. Le Finder s'ouvre pour indiquer Steam.",
            "Drück den goldenen Knopf. Der Finder öffnet sich, damit du Steam zeigen kannst.",
            "Нажми золотую кнопку. Откроется Finder, чтобы указать Steam."],
        ["setupStep2k"] = ["Pick the folder with steam.exe", "Elige la carpeta con steam.exe", "Choisis le dossier avec steam.exe", "Ordner mit steam.exe wählen", "Выбери папку со steam.exe"],
        ["setupStep2kLinux"] = ["Pick the folder with steam.sh", "Elige la carpeta con steam.sh", "Choisis le dossier avec steam.sh", "Ordner mit steam.sh wählen", "Выбери папку со steam.sh"],
        ["setupStep2kMac"] = ["Pick the Steam folder", "Elige la carpeta de Steam", "Choisis le dossier Steam", "Steam-Ordner wählen", "Выбери папку Steam"],
        ["setupStep2"] = [
            "On most PCs that folder is C:\\Program Files (x86)\\Steam. In the folder window, click the address bar, paste that path, and press Enter. You should see steam.exe in the folder.",
            "En la mayoría de PCs esa carpeta es C:\\Program Files (x86)\\Steam. En la ventana, pulsa la barra de dirección, pega esa ruta y pulsa Entrar. Debes ver steam.exe en la carpeta.",
            "Sur la plupart des PC, ce dossier est C:\\Program Files (x86)\\Steam. Dans la fenêtre, clique la barre d'adresse, colle ce chemin et appuie sur Entrée. Tu dois voir steam.exe dans le dossier.",
            "Auf den meisten PCs ist das C:\\Program Files (x86)\\Steam. Klick in der Adressleiste, füge den Pfad ein und drück Enter. In dem Ordner muss steam.exe liegen.",
            "На большинстве ПК это C:\\Program Files (x86)\\Steam. В окне нажми строку адреса, вставь путь и нажми Enter. В папке должен быть steam.exe."],
        ["setupStep2Linux"] = [
            "The Steam folder contains steam.sh and a steamapps folder. It is usually Home/.steam/steam or Home/.local/share/Steam. Press Ctrl+H in the folder window if you do not see folders that start with a dot. Do not open steamapps itself.",
            "La carpeta de Steam contiene steam.sh y una carpeta steamapps. Suele ser Home/.steam/steam o Home/.local/share/Steam. Pulsa Ctrl+H en la ventana si no ves las carpetas que empiezan por un punto. No abras steamapps.",
            "Le dossier Steam contient steam.sh et un dossier steamapps. En général Home/.steam/steam ou Home/.local/share/Steam. Appuie sur Ctrl+H dans la fenêtre si tu ne vois pas les dossiers qui commencent par un point. N'ouvre pas steamapps.",
            "Der Steam-Ordner enthält steam.sh und einen Ordner steamapps. Meist Home/.steam/steam oder Home/.local/share/Steam. Drück im Fenster Strg+H, wenn du Ordner mit einem Punkt am Anfang nicht siehst. Öffne nicht steamapps selbst.",
            "В папке Steam лежат steam.sh и папка steamapps. Обычно это Home/.steam/steam или Home/.local/share/Steam. Нажми Ctrl+H, если не видно папок с точкой в начале. Саму steamapps не открывай."],
        ["setupStep2Mac"] = [
            "In Finder, hold Option and choose Go, then Library. Open Application Support, then Steam. You should see Steam.app and a steamapps folder. Bannerlord has no Mac version: if you play with CrossOver, choose the Steam folder inside that bottle instead, the one that contains steam.exe.",
            "En Finder, mantén Opción y elige Ir, luego Biblioteca. Abre Application Support y después Steam. Debes ver Steam.app y una carpeta steamapps. Bannerlord no tiene versión para Mac: si juegas con CrossOver, elige la carpeta de Steam dentro de esa botella, la que contiene steam.exe.",
            "Dans le Finder, maintiens Option et choisis Aller, puis Bibliothèque. Ouvre Application Support, puis Steam. Tu dois voir Steam.app et un dossier steamapps. Bannerlord n'a pas de version Mac : si tu joues avec CrossOver, choisis plutôt le dossier Steam dans cette bouteille, celui qui contient steam.exe.",
            "Im Finder die Wahltaste halten und Gehe zu, dann Bibliothek wählen. Application Support öffnen, dann Steam. Dort liegen Steam.app und ein Ordner steamapps. Bannerlord hat keine Mac-Version: wenn du mit CrossOver spielst, wähle stattdessen den Steam-Ordner in dieser Bottle, den mit steam.exe.",
            "В Finder удерживай Option и выбери Переход, затем Библиотека. Открой Application Support, потом Steam. Там должны быть Steam.app и папка steamapps. У Bannerlord нет версии для Mac: если играешь через CrossOver, выбери папку Steam внутри этой бутылки, ту, где steam.exe."],
        ["setupStep3k"] = ["Don't pick the game", "No elijas el juego", "Ne choisis pas le jeu", "Nicht das Spiel wählen", "Не выбирай игру"],
        ["setupStep3"] = [
            "Don't open Mount & Blade II Bannerlord, and don't stop on the steamapps folder. Bannerlord can sit on another drive. We follow Steam's library list.",
            "No abras Mount & Blade II Bannerlord y no te quedes en la carpeta steamapps. Bannerlord puede estar en otro disco. Seguimos la lista de bibliotecas de Steam.",
            "N'ouvre pas Mount & Blade II Bannerlord et ne t'arrête pas sur le dossier steamapps. Bannerlord peut être sur un autre disque. Nous suivons les bibliothèques Steam.",
            "Öffne nicht Mount & Blade II Bannerlord und bleib nicht im Ordner steamapps. Bannerlord kann auf einem anderen Laufwerk liegen. Wir folgen Steams Bibliotheken.",
            "Не открывай Mount & Blade II Bannerlord и не останавливайся на папке steamapps. Bannerlord может быть на другом диске. Мы смотрим библиотеки Steam."],
        ["setupFound"] = [
            "Steam looks like it is already installed here. Use this folder, or choose a different one.",
            "Parece que Steam ya está instalado aquí. Usa esta carpeta o elige otra.",
            "Steam semble déjà installé ici. Utilise ce dossier, ou choisis-en un autre.",
            "Steam scheint schon hier installiert zu sein. Nimm diesen Ordner oder wähl einen anderen.",
            "Похоже, Steam уже установлен здесь. Используй эту папку или выбери другую."],
        ["setupUse"] = ["USE THIS FOLDER", "USAR ESTA CARPETA", "UTILISER CE DOSSIER", "DIESEN ORDNER", "ЭТА ПАПКА"],
        ["setupChooseOther"] = ["CHOOSE A DIFFERENT FOLDER", "ELEGIR OTRA CARPETA", "CHOISIR UN AUTRE DOSSIER", "ANDEREN ORDNER", "ДРУГАЯ ПАПКА"],
        ["setupChoose"] = ["CHOOSE STEAM FOLDER", "ELEGIR CARPETA DE STEAM", "CHOISIR LE DOSSIER STEAM", "STEAM-ORDNER WÄHLEN", "ВЫБРАТЬ ПАПКУ STEAM"]
    };
}
