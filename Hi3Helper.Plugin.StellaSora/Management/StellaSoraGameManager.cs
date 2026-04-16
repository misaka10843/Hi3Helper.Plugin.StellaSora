using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.StellaSora.Management.Api;
using Hi3Helper.Plugin.StellaSora.Utils;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.StellaSora.Management;

[GeneratedComClass]
internal partial class StellaSoraGameManager : GameManagerBase
{
    private const string ApiBaseUrl = "https://launcher-api.yostar.net/api/launcher";
    private readonly string _gameExecutableNameByPreset;

    private StellaSoraGameConfigData? _latestGameConfig;
    private StellaSoraCdnData? _cdnData;

    internal StellaSoraGameManager(string gameExecutableNameByPreset)
    {
        _gameExecutableNameByPreset = gameExecutableNameByPreset;
    }

    private string GameDataFolderName => Path.GetFileNameWithoutExtension(_gameExecutableNameByPreset) + "_Data";

    internal StellaSoraManifest? GameManifest { get; private set; }
    internal List<string> GameResourceDownloadUrls { get; private set; } = new();

    private bool IsInitialized { get; set; }

    protected override HttpClient ApiResponseHttpClient { get; set; } = new();

    protected override bool IsInstalled
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentGameInstallPath)) return false;
            var exePath = Path.Combine(CurrentGameInstallPath, _gameExecutableNameByPreset);
            bool exists = File.Exists(exePath);
            if (!exists)
                SharedStatic.InstanceLogger.LogWarning(
                    $"[StellaSoraGameManager] IsInstalled check failed. EXE not found at: {exePath}");
            return exists;
        }
    }

    protected override bool HasUpdate => IsInstalled &&
                                         !string.IsNullOrEmpty(ApiGameVersion.VersionString) &&
                                         ApiGameVersion.VersionString.Trim() != CurrentGameVersion.VersionString.Trim();

    protected override bool HasPreload => false;
    protected override GameVersion ApiGameVersion { get; set; }

    protected override void SetGamePathInner(string gamePath)
    {
        SharedStatic.InstanceLogger.LogInformation($"[StellaSoraGameManager] SetGamePathInner: '{gamePath}'");
        CurrentGameInstallPath = gamePath;

        _latestGameConfig = null;
        _cdnData = null;
        GameManifest = null;

        if (!string.IsNullOrEmpty(gamePath))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await InitAsyncInner(true);
                }
                catch (Exception ex)
                {
                    SharedStatic.InstanceLogger.LogError($"[StellaSoraGameManager] Re-init failed: {ex}");
                }
            });
        }
    }

    internal async Task<int> InitAsyncInner(bool forceInit = false, CancellationToken token = default)
    {
        if (!forceInit && IsInitialized) return 0;

        ReadLocalGameVersion();

        try
        {
            await FetchCdnDataAsync(token);
            await FetchGameConfigAndManifestAsync(token);

            if (_latestGameConfig != null && _cdnData != null)
            {
                ApiGameVersion = new GameVersion(_latestGameConfig.GameLatestVersion.Trim());

                GameResourceDownloadUrls.Clear();
                if (!string.IsNullOrEmpty(_cdnData.PrimaryCdn))
                    GameResourceDownloadUrls.Add(_cdnData.PrimaryCdn.TrimEnd('/'));
                if (!string.IsNullOrEmpty(_cdnData.BackUpCdn))
                    GameResourceDownloadUrls.Add(_cdnData.BackUpCdn.TrimEnd('/'));

                SharedStatic.InstanceLogger.LogInformation(
                    $"[StellaSoraGameManager] API Version: {ApiGameVersion.VersionString}");
            }
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogWarning(
                $"[StellaSoraGameManager] API fetch failed, using local state: {ex.Message}");

            if (string.IsNullOrEmpty(ApiGameVersion.VersionString))
                ApiGameVersion = CurrentGameVersion;
        }

        IsInitialized = true;
        return 0;
    }

    private void ReadLocalGameVersion()
    {
        if (!IsInstalled) return;

        try
        {
            var maniPath = Path.Combine(CurrentGameInstallPath!, GameDataFolderName, "StreamingAssets",
                "InstallResource", "install_resource_manifest.mani");

            if (File.Exists(maniPath))
            {
                var lines = File.ReadAllLines(maniPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("$GAME_VER:", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("$CLIENT_VER:", StringComparison.OrdinalIgnoreCase))
                    {
                        var verStr = line.Split(':', 2)[1].Trim();
                        CurrentGameVersion = new GameVersion(verStr);
                        SharedStatic.InstanceLogger.LogInformation(
                            $"[StellaSoraGameManager] Local Version found from .mani: {verStr}");
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogWarning($"[StellaSoraGameManager] Local version read failed: {ex}");
        }
    }

    private async Task FetchCdnDataAsync(CancellationToken token)
    {
        string url = $"{ApiBaseUrl}/advanced/game/download/cdn";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", StellaSoraApiHelper.GetAuthHeaderString());

        using var response = await ApiResponseHttpClient.SendAsync(request, token);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync(
            StellaSoraApiContext.Default.StellaSoraBaseResponseStellaSoraCdnData, token);

        if (result?.Code == 200 && result.Data != null)
        {
            _cdnData = result.Data;
            SharedStatic.InstanceLogger.LogInformation($"[StellaSoraGameManager] CDN Fetched: {_cdnData.PrimaryCdn}");
        }
        else
        {
            throw new Exception($"Fetch CDN failed, Code: {result?.Code}, Msg: {result?.Msg}");
        }
    }

    private async Task FetchGameConfigAndManifestAsync(CancellationToken token)
    {
        string url = $"{ApiBaseUrl}/game/config";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization", StellaSoraApiHelper.GetAuthHeaderString());

        using var response = await ApiResponseHttpClient.SendAsync(request, token);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync(
            StellaSoraApiContext.Default.StellaSoraBaseResponseStellaSoraGameConfigData, token);

        if (result?.Code == 200 && result.Data != null)
        {
            _latestGameConfig = result.Data;
            SharedStatic.InstanceLogger.LogInformation(
                $"[StellaSoraGameManager] Game Config Fetched. Latest Ver: {_latestGameConfig.GameLatestVersion}");

            // 获取游戏文件清单URL
            string exchangeUrl =
                $"{ApiBaseUrl}/game/config/json?version={_latestGameConfig.GameLatestVersion.Trim()}&file_path={_latestGameConfig.GameLatestFilePath}";
            var exchangeReq = new HttpRequestMessage(HttpMethod.Get, exchangeUrl);
            exchangeReq.Headers.TryAddWithoutValidation("Authorization", StellaSoraApiHelper.GetAuthHeaderString());

            using var exchangeRes = await ApiResponseHttpClient.SendAsync(exchangeReq, token);
            if (exchangeRes.IsSuccessStatusCode)
            {
                var exchangeResult = await exchangeRes.Content.ReadFromJsonAsync(
                    StellaSoraApiContext.Default.StellaSoraBaseResponseStellaSoraConfigJsonData, token);

                if (exchangeResult?.Code == 200 && exchangeResult.Data != null &&
                    !string.IsNullOrEmpty(exchangeResult.Data.Url))
                {
                    SharedStatic.InstanceLogger.LogInformation(
                        $"[StellaSoraGameManager] Manifest JSON URL: {exchangeResult.Data.Url}");

                    GameManifest = await ApiResponseHttpClient.GetFromJsonAsync(exchangeResult.Data.Url,
                        StellaSoraApiContext.Default.StellaSoraManifest, token);
                    SharedStatic.InstanceLogger.LogInformation(
                        $"[StellaSoraGameManager] Manifest loaded. Total files: {GameManifest?.Files?.Count}");
                }
            }
        }
        else
        {
            throw new Exception($"Fetch Game Config failed, Code: {result?.Code}, Msg: {result?.Msg}");
        }
    }

    protected override Task<int> InitAsync(CancellationToken token) => InitAsyncInner(true, token);

    protected override void SetCurrentGameVersionInner(in GameVersion gameVersion)
    {
        CurrentGameVersion = gameVersion;
    }

    protected override Task<string?> FindExistingInstallPathAsyncInner(CancellationToken token)
    {
        return Task.FromResult<string?>(null);
    }

    public override void LoadConfig()
    {
    }

    public override void SaveConfig()
    {
    }

    public override void Dispose()
    {
        base.Dispose();
        ApiResponseHttpClient?.Dispose();
    }
}