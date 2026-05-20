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
public partial class StellaSoraTwPresetConfig : PluginPresetConfigBase
{
    private const string ExEcutableName = "StellaSora.exe";
    private const string TwApiBaseUrl = "https://api-launcher-tw.stargazer-games.com/api/launcher";
    private const string TwAuthSalt = "DE7108E9B2842FD460F4777702727869";
    private const string TwAuthGameId = "StellaSora_TW";
    private const string TwLauncherVersion = "1.3.0";

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
    [field: AllowNull] [field: MaybeNull] public override string ProfileName => field ??= "StellaSoraTw";

    [field: AllowNull]
    [field: MaybeNull]
    public override string ZoneDescription => field ??= "《星塔旅人》是一款由悠星開發的動作角色扮演遊戲。";

    [field: AllowNull] [field: MaybeNull] public override string ZoneName => field ??= "繁体中文";
    [field: AllowNull] [field: MaybeNull] public override string ZoneFullName => field ??= "星塔旅人 (台服)";
    [field: AllowNull] [field: MaybeNull] public override string ZoneLogoUrl => field ??= "";
    [field: AllowNull] [field: MaybeNull] public override string ZonePosterUrl => field ??= "";

    [field: AllowNull]
    [field: MaybeNull]
    public override string ZoneHomePageUrl => field ??= "https://stellasora.yostar.com/";

    public override GameReleaseChannel ReleaseChannel => GameReleaseChannel.Public;

    [field: AllowNull] [field: MaybeNull] public override string GameMainLanguage => field ??= "zh-TW";

    [field: AllowNull] [field: MaybeNull] public override string LauncherGameDirectoryName => field ??= "StellaSora_TW";

    [field: AllowNull]
    [field: MaybeNull]
    public override List<string> SupportedLanguages => field ??= ["Chinese Traditional"];

    public override ILauncherApiMedia? LauncherApiMedia
    {
        get => field ??= new StellaSoraLauncherApiMedia(TwApiBaseUrl, TwAuthSalt, TwAuthGameId, TwLauncherVersion);
        set;
    }

    public override ILauncherApiNews? LauncherApiNews
    {
        get => field ??= new StellaSoraLauncherApiNews(TwApiBaseUrl, TwAuthSalt, TwAuthGameId, TwLauncherVersion);
        set;
    }

    public override IGameManager? GameManager
    {
        get => field ??= new StellaSoraGameManager(
            ExEcutableName,
            TwApiBaseUrl,
            TwAuthSalt,
            TwAuthGameId);
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