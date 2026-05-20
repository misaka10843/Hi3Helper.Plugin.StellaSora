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
public partial class StellaSoraJpPresetConfig : PluginPresetConfigBase
{
    private const string ExEcutableName = "StellaSora.exe";
    private const string JpApiBaseUrl = "https://api-launcher-jp.yo-star.com/api/launcher";
    private const string JpAuthSalt = "DE7108E9B2842FD460F4777702727869";
    private const string JpAuthGameId = "StellaSora_JP";
    private const string JpLauncherVersion = "1.3.0";

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
    [field: AllowNull] [field: MaybeNull] public override string ProfileName => field ??= "StellaSoraJp";

    [field: AllowNull]
    [field: MaybeNull]
    public override string ZoneDescription => field ??= "Yostarが贈る、旅と日常が交差するファンタジーRPG『ステラソラ』の公式サイトです。";

    [field: AllowNull] [field: MaybeNull] public override string ZoneName => field ??= "Japan";
    [field: AllowNull] [field: MaybeNull] public override string ZoneFullName => field ??= "StellaSora (Japan)";
    [field: AllowNull] [field: MaybeNull] public override string ZoneLogoUrl => field ??= "";
    [field: AllowNull] [field: MaybeNull] public override string ZonePosterUrl => field ??= "";

    [field: AllowNull]
    [field: MaybeNull]
    public override string ZoneHomePageUrl => field ??= "https://stellasora.jp/";

    public override GameReleaseChannel ReleaseChannel => GameReleaseChannel.Public;

    [field: AllowNull] [field: MaybeNull] public override string GameMainLanguage => field ??= "ja-JP";

    [field: AllowNull] [field: MaybeNull] public override string LauncherGameDirectoryName => field ??= "StellaSora_JP";

    [field: AllowNull]
    [field: MaybeNull]
    public override List<string> SupportedLanguages => field ??= ["Japanese"];

    public override ILauncherApiMedia? LauncherApiMedia
    {
        get => field ??= new StellaSoraLauncherApiMedia(JpApiBaseUrl, JpAuthSalt, JpAuthGameId, JpLauncherVersion);
        set;
    }

    public override ILauncherApiNews? LauncherApiNews
    {
        get => field ??= new StellaSoraLauncherApiNews(JpApiBaseUrl, JpAuthSalt, JpAuthGameId, JpLauncherVersion);
        set;
    }

    public override IGameManager? GameManager
    {
        get => field ??= new StellaSoraGameManager(
            ExEcutableName,
            JpApiBaseUrl,
            JpAuthSalt,
            JpAuthGameId);
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