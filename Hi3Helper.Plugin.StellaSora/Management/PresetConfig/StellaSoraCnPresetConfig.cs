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
public partial class StellaSoraCnPresetConfig : PluginPresetConfigBase
{
    private const string ExEcutableName = "xtlr.exe";
    private const string CnApiBaseUrl = "https://launcher-api.yostar.net/api/launcher";
    private const string CnAuthSalt = "872550AD59A235662C5B7D5F88CEBE4B";
    private const string CnAuthGameId = "StellaSora_CN";
    private const string CnLauncherVersion = "1.3.0";

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
    [field: AllowNull] [field: MaybeNull] public override string ProfileName => field ??= "StellaSoraCn";

    [field: AllowNull]
    [field: MaybeNull]
    public override string ZoneDescription => field ??= "《星塔旅人》是一款由悠星开发的动作角色扮演游戏。";

    [field: AllowNull] [field: MaybeNull] public override string ZoneName => field ??= "Mainland China";
    [field: AllowNull] [field: MaybeNull] public override string ZoneFullName => field ??= "星塔旅人 (中国大陆)";
    [field: AllowNull] [field: MaybeNull] public override string ZoneLogoUrl => field ??= "";
    [field: AllowNull] [field: MaybeNull] public override string ZonePosterUrl => field ??= "";

    [field: AllowNull]
    [field: MaybeNull]
    public override string ZoneHomePageUrl => field ??= "https://stellasora.yostar.cn/";

    public override GameReleaseChannel ReleaseChannel => GameReleaseChannel.Public;

    [field: AllowNull] [field: MaybeNull] public override string GameMainLanguage => field ??= "zh-CN";

    [field: AllowNull]
    [field: MaybeNull]
    public override string LauncherGameDirectoryName => field ??= "Stella Sora Game";

    [field: AllowNull] [field: MaybeNull] public override List<string> SupportedLanguages => field ??= ["Chinese"];

    public override ILauncherApiMedia? LauncherApiMedia
    {
        get => field ??= new StellaSoraLauncherApiMedia(CnApiBaseUrl, CnAuthSalt, CnAuthGameId, CnLauncherVersion);
        set;
    }

    public override ILauncherApiNews? LauncherApiNews
    {
        get => field ??= new StellaSoraLauncherApiNews(CnApiBaseUrl, CnAuthSalt, CnAuthGameId, CnLauncherVersion);
        set;
    }

    public override IGameManager? GameManager
    {
        get => field ??= new StellaSoraGameManager(ExEcutableName);
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