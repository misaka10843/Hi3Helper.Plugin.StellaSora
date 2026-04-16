using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.StellaSora.Management;

[GeneratedComClass]
internal partial class StellaSoraGameInstaller : GameInstallerBase
{
    private readonly HttpClient _downloadHttpClient;

    internal StellaSoraGameInstaller(IGameManager? gameManager) : base(gameManager)
    {
        _downloadHttpClient = new PluginHttpClientBuilder()
            .SetAllowedDecompression(DecompressionMethods.None)
            .AllowCookies()
            .AllowRedirections()
            .AllowUntrustedCert()
            .Create();
        
        _downloadHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) StellaSoraLauncher/1.3.0");
        _downloadHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
    }

    protected override async Task<int> InitAsync(CancellationToken token)
    {
        if (GameManager is not StellaSoraGameManager manager)
            throw new InvalidOperationException("GameManager is not StellaSoraGameManager");

        return await manager.InitAsyncInner(true, token).ConfigureAwait(false);
    }

    protected override async Task<long> GetGameSizeAsyncInner(GameInstallerKind gameInstallerKind, CancellationToken token)
    {
        await InitAsync(token).ConfigureAwait(false);
        if (GameManager is not StellaSoraGameManager manager || manager.GameManifest == null) 
            return 0L;

        long totalSize = 0;
        foreach (var file in manager.GameManifest.Files)
        {
            if (long.TryParse(file.Size, out long size))
                totalSize += size;
        }
        return totalSize;
    }

    protected override async Task<long> GetGameDownloadedSizeAsyncInner(GameInstallerKind gameInstallerKind, CancellationToken token)
    {
        await InitAsync(token).ConfigureAwait(false);

        if (GameManager is not StellaSoraGameManager manager || manager.GameManifest == null) 
            return 0L;

        GameManager.GetGamePath(out var installPath);
        if (string.IsNullOrEmpty(installPath)) return 0L;

        long downloadedSize = 0;
        foreach (var file in manager.GameManifest.Files)
        {
            var filePath = Path.Combine(installPath, file.Path.TrimStart('/'));
            if (File.Exists(filePath))
            {
                downloadedSize += new FileInfo(filePath).Length;
            }
        }

        return downloadedSize;
    }

    protected override Task StartInstallAsyncInner(InstallProgressDelegate? progressDelegate, InstallProgressStateDelegate? progressStateDelegate, CancellationToken token)
        => StartInstallCoreAsync(GameInstallerKind.Install, progressDelegate, progressStateDelegate, token);

    protected override Task StartUpdateAsyncInner(InstallProgressDelegate? progressDelegate, InstallProgressStateDelegate? progressStateDelegate, CancellationToken token)
        => StartInstallCoreAsync(GameInstallerKind.Update, progressDelegate, progressStateDelegate, token);

    protected override Task StartPreloadAsyncInner(InstallProgressDelegate? progressDelegate, InstallProgressStateDelegate? progressStateDelegate, CancellationToken token)
        => StartInstallCoreAsync(GameInstallerKind.Preload, progressDelegate, progressStateDelegate, token);
    
    private Task StartInstallCoreAsync(GameInstallerKind kind, InstallProgressDelegate? progressDelegate, InstallProgressStateDelegate? progressStateDelegate, CancellationToken token)
    {
        var installer = new Install(this);
        return installer.RunAsync(kind, progressDelegate, progressStateDelegate, token);
    }

    protected override Task UninstallAsyncInner(CancellationToken token)
    {
        GameManager.IsGameInstalled(out var isInstalled);
        if (!isInstalled) return Task.CompletedTask;

        GameManager.GetGamePath(out var installPath);
        if (string.IsNullOrEmpty(installPath)) return Task.CompletedTask;

        try { if (Directory.Exists(installPath)) Directory.Delete(installPath, true); }
        catch (Exception ex) { SharedStatic.InstanceLogger.LogError($"[StellaSora] Uninstall failed: {ex.Message}"); }

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _downloadHttpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}