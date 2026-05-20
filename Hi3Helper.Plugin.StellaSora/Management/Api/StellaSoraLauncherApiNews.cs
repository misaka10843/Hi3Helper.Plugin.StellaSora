using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Management.Api;
using Hi3Helper.Plugin.Core.Utility;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.StellaSora.Management.Api;

[GeneratedComClass]
public partial class StellaSoraLauncherApiNews : LauncherApiNewsBase
{
    private readonly string _apiBaseUrl;
    private readonly string _authSalt;
    private readonly string _authGameId;

    private StellaSoraResourceData? _resourceData;

    private readonly string _authLauncherVersion;

    public StellaSoraLauncherApiNews(
        string apiBaseUrl,
        string authSalt,
        string authGameId,
        string authLauncherVersion)
    {
        _apiBaseUrl            = apiBaseUrl;
        _authSalt              = authSalt;
        _authGameId            = authGameId;
        _authLauncherVersion   = authLauncherVersion;
    }

    [field: AllowNull] [field: MaybeNull] protected override HttpClient ApiResponseHttpClient { get; set; } = new();

    [field: AllowNull]
    [field: MaybeNull]
    protected HttpClient ApiDownloadHttpClient
    {
        get => field ??= new PluginHttpClientBuilder()
            .SetAllowedDecompression(DecompressionMethods.GZip)
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
            string url = $"{_apiBaseUrl}/operations/resource";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Authorization", StellaSoraApiHelper.GetAuthHeaderString(_authSalt, _authGameId, _authLauncherVersion));

            using var response = await ApiResponseHttpClient.SendAsync(request, token);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync(
                StellaSoraApiContext.Default.StellaSoraBaseResponseStellaSoraResourceData, token);

            if (result?.Code == 200 && result.Data != null)
            {
                _resourceData = result.Data;
            }

            return 0;
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogError($"[StellaSoraNews] Failed to init news: {ex}");
            return -1;
        }
    }

    public override void GetNewsEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        if (_resourceData?.NewsList?.Data?.News == null)
        {
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
            return;
        }

        var flatList = new List<FlatNewsItem>();
        foreach (var category in _resourceData.NewsList.Data.News)
        {
            if (category.Rows == null) continue;
            var tName = category.TypeLabel ?? "Info";

            foreach (var item in category.Rows)
            {
                flatList.Add(new FlatNewsItem { Item = item, TypeName = tName });
            }
        }

        if (flatList.Count == 0)
        {
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
            return;
        }

        count = flatList.Count;

        var memory = PluginDisposableMemory<LauncherNewsEntry>.Alloc(count);
        handle = memory.AsSafePointer();
        isDisposable = true;
        isAllocated = true;

        for (var i = 0; i < count; i++)
        {
            var flatItem = flatList[i];
            var item = flatItem.Item;

            var type = LauncherNewsEntryType.Info;
            var typeNameLower = flatItem.TypeName?.ToLowerInvariant() ?? "";

            if (typeNameLower.Contains("公告") || typeNameLower.Contains("notice"))
                type = LauncherNewsEntryType.Notice;
            else if (typeNameLower.Contains("活动") || typeNameLower.Contains("event"))
                type = LauncherNewsEntryType.Event;

            var dateStr = DateTimeOffset.FromUnixTimeMilliseconds(item.PublishTime).ToLocalTime()
                .ToString("yyyy-MM-dd");
            var content = item.Title ?? "";
            var jumpUrl = item.Link ?? "";

            ref var entry = ref memory[i];
            entry.Write(content, null, jumpUrl, dateStr, type);
        }
    }

    public override void GetCarouselEntries(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        var banners = _resourceData?.OperationsBannerList;
        if (banners == null || banners.Count == 0)
        {
            InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
            return;
        }

        count = banners.Count;

        var memory = PluginDisposableMemory<LauncherCarouselEntry>.Alloc(count);
        handle = memory.AsSafePointer();
        isDisposable = true;
        isAllocated = true;

        for (var i = 0; i < count; i++)
        {
            var banner = banners[i];
            var imgUrl = banner.BannerImg ?? "";
            var jumpUrl = banner.JumpUrl ?? "";

            ref var entry = ref memory[i];
            entry.Write(null, imgUrl, jumpUrl);
        }
    }

    public override void GetSocialMediaEntries(out nint handle, out int count, out bool isDisposable,
        out bool isAllocated)
    {
        InitializeEmpty(out handle, out count, out isDisposable, out isAllocated);
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
            SharedStatic.InstanceLogger.LogError($"[StellaSoraNews] Download FAILED: {fileUrl}\nException: {ex}");
        }
    }

    private static void InitializeEmpty(out nint handle, out int count, out bool isDisposable, out bool isAllocated)
    {
        handle = nint.Zero;
        count = 0;
        isDisposable = false;
        isAllocated = false;
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        ApiResponseHttpClient?.Dispose();
        ApiDownloadHttpClient?.Dispose();
        base.Dispose();
    }

    private struct FlatNewsItem
    {
        public StellaSoraNewsItem Item;
        public string TypeName;
    }
}