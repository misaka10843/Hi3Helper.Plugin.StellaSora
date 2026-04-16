using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Hi3Helper.Plugin.StellaSora.Management.Api;

public class StellaSoraBaseResponse<T>
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("msg")] public string Msg { get; set; }
    [JsonPropertyName("data")] public T Data { get; set; }
}

public class StellaSoraGameConfigData
{
    [JsonPropertyName("game_lowest_version")] public string GameLowestVersion { get; set; }
    [JsonPropertyName("game_latest_version")] public string GameLatestVersion { get; set; }
    [JsonPropertyName("game_latest_file_path")] public string GameLatestFilePath { get; set; }
    [JsonPropertyName("game_start_exe_name")] public string GameStartExeName { get; set; }
    [JsonPropertyName("file_url")] public string FileUrl { get; set; }
    [JsonPropertyName("decompression_size")] public string DecompressionSize { get; set; }
}

public class StellaSoraCdnData
{
    [JsonPropertyName("primary_cdn")] public string PrimaryCdn { get; set; }
    [JsonPropertyName("back_up_cdn")] public string BackUpCdn { get; set; }
}

public class StellaSoraAuthHead
{
    [JsonPropertyName("game_tag")] public string GameTag { get; set; }
    [JsonPropertyName("time")] public long Time { get; set; }
    [JsonPropertyName("version")] public string Version { get; set; }
}

public class StellaSoraAuthHeader
{
    [JsonPropertyName("head")] public StellaSoraAuthHead Head { get; set; }
    [JsonPropertyName("sign")] public string Sign { get; set; }
}

public class StellaSoraBaseConfigData
{
    [JsonPropertyName("launcher_background_img")] public string LauncherBackgroundImg { get; set; }
    [JsonPropertyName("user_agreement")] public string UserAgreement { get; set; }
    [JsonPropertyName("privacy_policy")] public string PrivacyPolicy { get; set; }
}

public class StellaSoraResourceData
{
    [JsonPropertyName("news_list")] public StellaSoraNewsListResponse NewsList { get; set; }
    [JsonPropertyName("operations_banner_list")] public List<StellaSoraBannerInfo> OperationsBannerList { get; set; }
}

public class StellaSoraNewsListResponse
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("data")] public StellaSoraNewsData Data { get; set; }
}

public class StellaSoraNewsData
{
    [JsonPropertyName("news")] public List<StellaSoraNewsCategory> News { get; set; }
}

public class StellaSoraNewsCategory
{
    [JsonPropertyName("rows")] public List<StellaSoraNewsItem> Rows { get; set; }
    [JsonPropertyName("typeLabel")] public string TypeLabel { get; set; }
}

public class StellaSoraNewsItem
{
    [JsonPropertyName("title")] public string Title { get; set; }
    [JsonPropertyName("link")] public string Link { get; set; }
    [JsonPropertyName("publishTime")] public long PublishTime { get; set; }
    [JsonPropertyName("thumbnail")] public string Thumbnail { get; set; }
    [JsonPropertyName("typeLabel")] public string TypeLabel { get; set; }
}

public class StellaSoraBannerInfo
{
    [JsonPropertyName("banner_img")] public string BannerImg { get; set; }
    [JsonPropertyName("jump_url")] public string JumpUrl { get; set; }
}

public class StellaSoraConfigJsonData
{
    /// <summary>
    /// 游戏文件清单的url
    /// </summary>
    [JsonPropertyName("url")] public string Url { get; set; }
}

public class StellaSoraManifest
{
    /// <summary>
    /// 游戏文件清单
    /// </summary>
    [JsonPropertyName("source")] public string Source { get; set; }
    [JsonPropertyName("file")] public List<StellaSoraManifestFile> Files { get; set; }
}

public class StellaSoraManifestFile
{
    /// <summary>
    /// 游戏文件清党的具体内容
    /// </summary>
    [JsonPropertyName("path")] public string Path { get; set; }
    [JsonPropertyName("hash")] public string Hash { get; set; }
    [JsonPropertyName("size")] public string Size { get; set; }
}