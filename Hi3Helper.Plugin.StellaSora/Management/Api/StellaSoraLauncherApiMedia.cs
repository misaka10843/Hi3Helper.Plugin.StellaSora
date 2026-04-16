using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Utility;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.StellaSora.Management.Api;

[GeneratedComClass]
public partial class StellaSoraLauncherApiMedia : LauncherApiMediaBase
{
    private const string ApiBaseUrl = "https://launcher-api.yostar.net/api/launcher";

    private string? _backgroundUrl;

    [field: AllowNull] [field: MaybeNull] protected override HttpClient ApiResponseHttpClient { get; set; } = new();

    [field: AllowNull]
    [field: MaybeNull]
    protected HttpClient ApiDownloadHttpClient
    {
        get => field ??= new PluginHttpClientBuilder()
            .SetAllowedDecompression(DecompressionMethods.None)
            .AllowCookies()
            .AllowRedirections()
            .AllowUntrustedCert()
            .Create();
        set;
    }

    protected override string ApiResponseBaseUrl => string.Empty;

    protected override async Task<int> InitAsync(CancellationToken token)
    {
        try
        {
            string url = $"{ApiBaseUrl}/base/config";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Authorization", StellaSoraApiHelper.GetAuthHeaderString());

            using var response = await ApiResponseHttpClient.SendAsync(request, token);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync(
                StellaSoraApiContext.Default.StellaSoraBaseResponseStellaSoraBaseConfigData, token);

            if (result?.Code == 200 && result.Data != null)
            {
                _backgroundUrl = result.Data.LauncherBackgroundImg;
                SharedStatic.InstanceLogger.LogInformation($"[StellaSoraMedia] Background URL: {_backgroundUrl}");
            }
            return 0;
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogError($"[StellaSoraMedia] Failed to init media: {ex}");
            return -1;
        }
    }

    public override void GetBackgroundEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        if (string.IsNullOrEmpty(_backgroundUrl))
        {
            handle = nint.Zero;
            count = 0;
            isDisposable = false;
            isAllocated = false;
            return;
        }

        var memory = PluginDisposableMemory<LauncherPathEntry>.Alloc();
        ref var entry = ref memory[0];

        entry.Write(_backgroundUrl, Span<byte>.Empty);

        handle = memory.AsSafePointer();
        count = 1;
        isDisposable = true;
        isAllocated = true;
    }

    public override void GetBackgroundFlag(out LauncherBackgroundFlag result)
    {
        result = LauncherBackgroundFlag.TypeIsImage;
    }

    public override void GetLogoFlag(out LauncherBackgroundFlag result)
    {
        result = LauncherBackgroundFlag.None;
    }

    public override void GetLogoOverlayEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        handle = nint.Zero;
        count = 0;
        isDisposable = false;
        isAllocated = false;
    }

    public override void GetBackgroundSpriteFps(out float fps)
    {
        fps = 60f;
    }

    protected override async Task DownloadAssetAsyncInner(HttpClient? client, string fileUrl, Stream outputStream,
        PluginDisposableMemory<byte> fileChecksum, PluginFiles.FileReadProgressDelegate? downloadProgress,
        CancellationToken token)
    {
        try
        {
            await base.DownloadAssetAsyncInner(ApiDownloadHttpClient, fileUrl, outputStream, fileChecksum,
                downloadProgress, token);
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogError($"[StellaSoraMedia] Background download FAILED: {fileUrl}\nException: {ex}");
        }
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        ApiResponseHttpClient?.Dispose();
        ApiDownloadHttpClient?.Dispose();
        base.Dispose();
    }
}