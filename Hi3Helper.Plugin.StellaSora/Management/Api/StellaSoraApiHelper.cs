using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Utility;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.StellaSora.Management.Api;

public static class StellaSoraApiHelper
{
    private const string Salt = "872550AD59A235662C5B7D5F88CEBE4B";
    private const string GameId = "StellaSora_CN";
    private const string LauncherVersion = "1.3.0"; // 可能要测试是否需要时刻获取到最新的启动器版本才能正常认证

    /// <summary>
    /// 生成 Yostar 启动器 API 所需的 Authorization Header 字符串
    /// </summary>
    /// <param name="postData">如果是 GET 请求，传入空字符串即可</param>
    public static string GetAuthHeaderString(string postData = "")
    {
        try
        {
            long time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // 严格构建 head 对象
            var head = new StellaSoraAuthHead
            {
                GameTag = GameId,
                Time = time,
                Version = LauncherVersion
            };

            string headJson = JsonSerializer.Serialize(head, StellaSoraApiContext.Default.StellaSoraAuthHead);

            string stringToSign = headJson + postData + Salt;

            string sign = CalculateMD5(stringToSign).ToLowerInvariant();

            var authHeader = new StellaSoraAuthHeader
            {
                Head = head,
                Sign = sign
            };

            return JsonSerializer.Serialize(authHeader, StellaSoraApiContext.Default.StellaSoraAuthHeader);
        }
        catch (Exception ex)
        {
            SharedStatic.InstanceLogger.LogError($"[StellaSoraApiHelper] Failed to generate Auth Header: {ex}");
            return string.Empty;
        }
    }

    private static string CalculateMD5(string input)
    {
        using var md5 = MD5.Create();
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = md5.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes);
    }
}