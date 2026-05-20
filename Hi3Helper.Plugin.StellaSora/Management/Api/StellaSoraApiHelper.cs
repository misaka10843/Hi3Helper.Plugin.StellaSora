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
    /// <summary>
    /// 生成 Yostar 启动器 API 所需的 Authorization Header 字符串
    /// </summary>
    /// <param name="salt">区服专用 Salt</param>
    /// <param name="gameId">区服专用 GameId</param>
    /// <param name="launcherVersion">启动器版本号（各区服在 preset config 中维护）</param>
    /// <param name="postData">如果是 GET 请求，传入空字符串即可</param>
    public static string GetAuthHeaderString(
        string salt,
        string gameId,
        string launcherVersion,
        string postData = "")
    {
        try
        {
            long time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // 严格构建 head 对象
            var head = new StellaSoraAuthHead
            {
                GameTag = gameId,
                Time = time,
                Version = launcherVersion
            };

            string headJson = JsonSerializer.Serialize(head, StellaSoraApiContext.Default.StellaSoraAuthHead);

            string stringToSign = headJson + postData + salt;

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