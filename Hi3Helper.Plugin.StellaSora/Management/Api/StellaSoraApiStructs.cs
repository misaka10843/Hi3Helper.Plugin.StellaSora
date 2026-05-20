using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Hi3Helper.Plugin.StellaSora.Management.Api;

public class StellaSoraBaseResponse<T>
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("msg")] public required string Msg { get; set; }
    [JsonPropertyName("data")] public required T Data { get; set; }
}

public class StellaSoraGameConfigData
{
    [JsonPropertyName("game_lowest_version")]
    public required string GameLowestVersion { get; set; }

    [JsonPropertyName("game_latest_version")]
    public required string GameLatestVersion { get; set; }

    [JsonPropertyName("game_latest_file_path")]
    public required string GameLatestFilePath { get; set; }

    [JsonPropertyName("game_start_exe_name")]
    public required string GameStartExeName { get; set; }

    [JsonPropertyName("file_url")] public required string FileUrl { get; set; }

    [JsonPropertyName("decompression_size")]
    public required string DecompressionSize { get; set; }
}

public class StellaSoraCdnData
{
    [JsonPropertyName("primary_cdn")] public required string PrimaryCdn { get; set; }
    [JsonPropertyName("back_up_cdn")] public required string BackUpCdn { get; set; }
}

public class StellaSoraAuthHead
{
    [JsonPropertyName("game_tag")] public required string GameTag { get; set; }
    [JsonPropertyName("time")] public long Time { get; set; }
    [JsonPropertyName("version")] public required string Version { get; set; }
}

public class StellaSoraAuthHeader
{
    [JsonPropertyName("head")] public required StellaSoraAuthHead Head { get; set; }
    [JsonPropertyName("sign")] public required string Sign { get; set; }
}

public class StellaSoraBaseConfigData
{
    [JsonPropertyName("launcher_background_img")]
    public required string LauncherBackgroundImg { get; set; }

    [JsonPropertyName("user_agreement")] public required string UserAgreement { get; set; }
    [JsonPropertyName("privacy_policy")] public required string PrivacyPolicy { get; set; }
}

public class StellaSoraResourceData
{
    [JsonPropertyName("news_list")] public required StellaSoraNewsListResponse NewsList { get; set; }

    [JsonPropertyName("operations_banner_list")]
    public required List<StellaSoraBannerInfo> OperationsBannerList { get; set; }
}

public class StellaSoraNewsListResponse
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("data")] public required StellaSoraNewsData Data { get; set; }
}

public class StellaSoraNewsData
{
    [JsonPropertyName("news")] public required List<StellaSoraNewsCategory> News { get; set; }
}

public class StellaSoraNewsCategory
{
    [JsonPropertyName("rows")] public required List<StellaSoraNewsItem> Rows { get; set; }
    [JsonPropertyName("typeLabel")] public required string TypeLabel { get; set; }
}

public class StellaSoraNewsItem
{
    [JsonPropertyName("title")] public required string Title { get; set; }
    [JsonPropertyName("link")] public required string Link { get; set; }
    [JsonPropertyName("publishTime")] public long PublishTime { get; set; }
    [JsonPropertyName("thumbnail")] public required string Thumbnail { get; set; }
    [JsonPropertyName("typeLabel")] public required string TypeLabel { get; set; }
}

public class StellaSoraBannerInfo
{
    [JsonPropertyName("banner_img")] public required string BannerImg { get; set; }
    [JsonPropertyName("jump_url")] public required string JumpUrl { get; set; }
}

public class StellaSoraConfigJsonData
{
    /// <summary>
    ///     游戏文件清单的url
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }
}

public class StellaSoraManifest
{
    /// <summary>
    ///     游戏文件清单
    /// </summary>
    [JsonPropertyName("source")]
    public required string Source { get; set; }

    [JsonPropertyName("file")] public required List<StellaSoraManifestFile> Files { get; set; }
}

public class StellaSoraManifestFile
{
    /// <summary>
    ///     游戏文件清党的具体内容
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; set; }

    [JsonPropertyName("hash")] public required string Hash { get; set; }
    [JsonPropertyName("size")] public required string Size { get; set; }
}

/// <summary>
///     本地存储的下载清单文件 (manifest.json)，用于更新时的差异计算
/// </summary>
public class StellaSoraLocalManifest
{
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("basis")] public string? Basis { get; set; }
    [JsonPropertyName("file")] public List<StellaSoraManifestFile>? Files { get; set; }
}