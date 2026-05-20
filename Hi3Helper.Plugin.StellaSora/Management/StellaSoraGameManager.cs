using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices.Marshalling;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.StellaSora.Management.Api;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.StellaSora.Management;

[GeneratedComClass]
internal partial class StellaSoraGameManager : GameManagerBase
{
    internal const string LocalManifestFileName = "manifest.json";
    private readonly string _apiBaseUrl;
    private readonly string _authGameId;
    private readonly string _authLauncherVersion;
    private readonly string _authSalt;
    private readonly string _gameExecutableNameByPreset;
    private StellaSoraCdnData? _cdnData;

    internal StellaSoraGameManager(
        string gameExecutableNameByPreset,
        string apiBaseUrl = "https://launcher-api.yostar.net/api/launcher",
        string authSalt = "872550AD59A235662C5B7D5F88CEBE4B",
        string authGameId = "StellaSora_CN",
        string authLauncherVersion = "1.3.0")
    {
        _gameExecutableNameByPreset = gameExecutableNameByPreset;
        _apiBaseUrl = apiBaseUrl;
        _authSalt = authSalt;
        _authGameId = authGameId;
        _authLauncherVersion = authLauncherVersion;
    }

    private string GameDataFolderName
    {
        get
        {
            var exeName = !string.IsNullOrEmpty(LatestGameConfig?.GameStartExeName)
                ? LatestGameConfig.GameStartExeName
                : _gameExecutableNameByPreset;
            return Path.GetFileNameWithoutExtension(exeName) + "_Data";
        }
    }

    internal StellaSoraManifest? GameManifest { get; private set; }
    internal StellaSoraGameConfigData? LatestGameConfig { get; private set; }

    internal List<string> GameResourceDownloadUrls { get; } = new();

    private bool IsInitialized { get; set; }

    private bool? _isInstalledCache;

    protected override HttpClient ApiResponseHttpClient { get; set; } = new();

    protected override bool IsInstalled
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentGameInstallPath)) return false;
            if (_isInstalledCache.HasValue) return _isInstalledCache.Value;

            var exePath = Path.Combine(CurrentGameInstallPath, _gameExecutableNameByPreset);
            var exists = File.Exists(exePath);
            SharedStatic.InstanceLogger.LogDebug(
                $"[StellaSoraGameManager] IsInstalled={exists} (EXE: {exePath})");
            _isInstalledCache = exists;
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
        _isInstalledCache = null;

        LatestGameConfig = null;
        _cdnData = null;
        GameManifest = null;

        if (!string.IsNullOrEmpty(gamePath))
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

    internal async Task<int> InitAsyncInner(bool forceInit = false, CancellationToken token = default)
    {
        if (!forceInit && IsInitialized) return 0;

        ReadLocalGameVersion();
        try
        {
            await FetchCdnDataAsync(token);
            await FetchGameConfigAndManifestAsync(token);

            if (LatestGameConfig != null && _cdnData != null)
            {
                ApiGameVersion = new GameVersion(LatestGameConfig.GameLatestVersion.Trim());

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
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogWarning($"[StellaSoraGameManager] Local version read failed: {ex}");
        }
    }

    private async Task FetchCdnDataAsync(CancellationToken token)
    {
        var url = $"{_apiBaseUrl}/advanced/game/download/cdn";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization",
            StellaSoraApiHelper.GetAuthHeaderString(_authSalt, _authGameId, _authLauncherVersion));

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
        var url = $"{_apiBaseUrl}/game/config";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Authorization",
            StellaSoraApiHelper.GetAuthHeaderString(_authSalt, _authGameId, _authLauncherVersion));

        using var response = await ApiResponseHttpClient.SendAsync(request, token);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync(
            StellaSoraApiContext.Default.StellaSoraBaseResponseStellaSoraGameConfigData, token);

        if (result?.Code == 200 && result.Data != null)
        {
            LatestGameConfig = result.Data;
            SharedStatic.InstanceLogger.LogInformation(
                $"[StellaSoraGameManager] Game Config Fetched. Latest Ver: {LatestGameConfig.GameLatestVersion}");

            // 获取游戏文件清单URL
            var exchangeUrl =
                $"{_apiBaseUrl}/game/config/json?version={LatestGameConfig.GameLatestVersion.Trim()}&file_path={LatestGameConfig.GameLatestFilePath}";
            var exchangeReq = new HttpRequestMessage(HttpMethod.Get, exchangeUrl);
            exchangeReq.Headers.TryAddWithoutValidation("Authorization",
                StellaSoraApiHelper.GetAuthHeaderString(_authSalt, _authGameId, _authLauncherVersion));

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

    internal StellaSoraLocalManifest? ReadLocalManifest(string installPath)
    {
        var manifestPath = Path.Combine(installPath, LocalManifestFileName);
        if (!File.Exists(manifestPath)) return null;
        try
        {
            var json = File.ReadAllText(manifestPath);
            return JsonSerializer.Deserialize(json, StellaSoraApiContext.Default.StellaSoraLocalManifest);
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogWarning(
                $"[StellaSoraGameManager] Failed to read local manifest: {ex.Message}");
            return null;
        }
    }

    internal void WriteLocalManifest(string installPath, StellaSoraManifest newManifest)
    {
        if (LatestGameConfig == null) return;
        var localManifest = new StellaSoraLocalManifest
        {
            Version = LatestGameConfig.GameLatestVersion,
            Basis = LatestGameConfig.GameLatestFilePath,
            Files = newManifest.Files
        };
        var manifestPath = Path.Combine(installPath, LocalManifestFileName);
        try
        {
            var json = JsonSerializer.Serialize(localManifest, StellaSoraApiContext.Default.StellaSoraLocalManifest);
            File.WriteAllText(manifestPath, json);
            _isInstalledCache = null;
            SharedStatic.InstanceLogger.LogInformation("[StellaSoraGameManager] Local manifest saved.");
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogWarning(
                $"[StellaSoraGameManager] Failed to write local manifest: {ex.Message}");
        }
    }

    protected override Task<int> InitAsync(CancellationToken token)
    {
        return InitAsyncInner(true, token);
    }

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