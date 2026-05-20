using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.StellaSora.Management.Api;

namespace Hi3Helper.Plugin.StellaSora.Management.PresetConfig;

[GeneratedComClass]
public partial class StellaSoraGlobalPresetConfig : PluginPresetConfigBase
{
    private const string ExEcutableName = "StellaSora.exe";
    private const string GlobalApiBaseUrl = "https://api-launcher-en.yo-star.com/api/launcher";
    private const string GlobalAuthSalt = "DE7108E9B2842FD460F4777702727869";
    private const string GlobalAuthGameId = "StellaSora_EN";
    private const string GlobalLauncherVersion = "1.3.0";

    [field: AllowNull] [field: MaybeNull] public override string GameName => field ??= "Stella Sora";
    [field: AllowNull] [field: MaybeNull] public override string GameExecutableName => field ??= ExEcutableName;

    public override string GameAppDataPath
    {
        get
        {
            string? gamePath = null;
            GameManager?.GetGamePath(out gamePath);
            if (!string.IsNullOrEmpty(gamePath))
            {
                var dataFolderName = Path.GetFileNameWithoutExtension(GameExecutableName) + "_Data";
                return Path.Combine(gamePath, dataFolderName);
            }

            return string.Empty;
        }
    }

    [field: AllowNull] [field: MaybeNull] public override string GameLogFileName => field ??= "Player.log";
    [field: AllowNull] [field: MaybeNull] public override string GameVendorName => field ??= "Yostar";
    [field: AllowNull] [field: MaybeNull] public override string GameRegistryKeyName => field ??= "StellaSora";
    [field: AllowNull] [field: MaybeNull] public override string ProfileName => field ??= "StellaSoraGlobal";

    [field: AllowNull]
    [field: MaybeNull]
    public override string ZoneDescription => field ??= "Stella Sora is a top-down, light-action adventure game developed by YOSTAR.";

    [field: AllowNull] [field: MaybeNull] public override string ZoneName => field ??= "Global";
    [field: AllowNull] [field: MaybeNull] public override string ZoneFullName => field ??= "StellaSora (Global)";
    [field: AllowNull] [field: MaybeNull] public override string ZoneLogoUrl => field ??= "";
    [field: AllowNull] [field: MaybeNull] public override string ZonePosterUrl => field ??= "";

    [field: AllowNull]
    [field: MaybeNull]
    public override string ZoneHomePageUrl => field ??= "https://stellasora.global/";

    public override GameReleaseChannel ReleaseChannel => GameReleaseChannel.Public;

    [field: AllowNull] [field: MaybeNull] public override string GameMainLanguage => field ??= "en-US";

    [field: AllowNull] [field: MaybeNull] public override string LauncherGameDirectoryName => field ??= "StellaSora_EN";

    [field: AllowNull]
    [field: MaybeNull]
    public override List<string> SupportedLanguages => field ??= ["English"];

    public override ILauncherApiMedia? LauncherApiMedia
    {
        get => field ??= new StellaSoraLauncherApiMedia(GlobalApiBaseUrl, GlobalAuthSalt, GlobalAuthGameId, GlobalLauncherVersion);
        set;
    }

    public override ILauncherApiNews? LauncherApiNews
    {
        get => field ??= new StellaSoraLauncherApiNews(GlobalApiBaseUrl, GlobalAuthSalt, GlobalAuthGameId, GlobalLauncherVersion);
        set;
    }

    public override IGameManager? GameManager
    {
        get => field ??= new StellaSoraGameManager(
            ExEcutableName,
            GlobalApiBaseUrl,
            GlobalAuthSalt,
            GlobalAuthGameId);
        set;
    }

    public override IGameInstaller? GameInstaller
    {
        get => field ??= new StellaSoraGameInstaller(GameManager!);
        set;
    }

    protected override Task<int> InitAsync(CancellationToken token)
    {
        return Task.FromResult(0);
    }
}