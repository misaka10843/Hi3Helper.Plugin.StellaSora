using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.StellaSora.Management.Api;
using Hi3Helper.Plugin.StellaSora.Utils;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.StellaSora.Management;

internal partial class StellaSoraGameInstaller
{
    private sealed class Install
    {
        private readonly StellaSoraGameInstaller _owner;

        private const int MaxConcurrentDownloads = 8;
        private long _lastUpdateTime;

        public Install(StellaSoraGameInstaller owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public async Task RunAsync(GameInstallerKind kind, InstallProgressDelegate? progressDelegate,
            InstallProgressStateDelegate? progressStateDelegate, CancellationToken token)
        {
            await _owner.InitAsync(token).ConfigureAwait(false);

            if (_owner.GameManager is not StellaSoraGameManager manager || manager.GameManifest == null || manager.GameResourceDownloadUrls.Count == 0)
                throw new InvalidOperationException("Game manifest or CDN URLs are missing.");

            _owner.GameManager.GetGamePath(out var installPath);
            if (string.IsNullOrEmpty(installPath)) throw new InvalidOperationException("Install path is missing.");

            var filesToDownload = manager.GameManifest.Files;
            var sourceDir = manager.GameManifest.Source;

            InstallProgress progress = default;
            progress.TotalCountToDownload = filesToDownload.Count;
            progress.TotalBytesToDownload = filesToDownload.Sum(f => long.Parse(f.Size));
            progress.TotalStateToComplete = 1; 

            void ReportThrottled()
            {
                long now = Stopwatch.GetTimestamp();
                long last = Interlocked.Read(ref _lastUpdateTime);
                if (now - last > Stopwatch.Frequency / 10) 
                {
                    if (Interlocked.CompareExchange(ref _lastUpdateTime, now, last) == last)
                    {
                        progressDelegate?.Invoke(in progress);
                    }
                }
            }

            progressDelegate?.Invoke(in progress);
            progressStateDelegate?.Invoke(InstallProgressState.Download);

            using var semaphore = new SemaphoreSlim(MaxConcurrentDownloads);
            var downloadTasks = new List<Task>();

            foreach (var fileInfo in filesToDownload)
            {
                downloadTasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(token);
                    try
                    {
                        var localPath = Path.Combine(installPath, fileInfo.Path.TrimStart('/'));
                        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

                        long expectedSize = long.Parse(fileInfo.Size);
                        long filePreviousBytes = 0; 

                        await DownloadSingleFileWithFallbackAsync(
                            manager.GameResourceDownloadUrls,
                            sourceDir,
                            fileInfo,
                            localPath,
                            expectedSize,
                            token,
                            fileCurrentBytes =>
                            {
                                long delta = fileCurrentBytes - filePreviousBytes;
                                filePreviousBytes = fileCurrentBytes;
                                Interlocked.Add(ref progress.DownloadedBytes, delta);
                                ReportThrottled();
                            });

                        Interlocked.Increment(ref progress.DownloadedCount);
                        ReportThrottled();
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, token));
            }

            await Task.WhenAll(downloadTasks);

            progress.StateCount = 1;
            progressDelegate?.Invoke(in progress);
            progressStateDelegate?.Invoke(InstallProgressState.Completed);
        }

        private async Task DownloadSingleFileWithFallbackAsync(
            List<string> baseUrls,
            string sourceDir,
            StellaSoraManifestFile fileInfo,
            string localPath,
            long expectedSize,
            CancellationToken token,
            Action<long> onProgress)
        {
            ulong expectedHash = ulong.Parse(fileInfo.Hash);
            long existingLength = File.Exists(localPath) ? new FileInfo(localPath).Length : 0;

            // 检查CRC
            if (existingLength == expectedSize)
            {
                ulong localHash = StellaSoraCrc64.Compute(localPath);
                if (localHash == expectedHash)
                {
                    onProgress(expectedSize);
                    return;
                }
                
                SharedStatic.InstanceLogger.LogWarning($"[StellaSoraInstaller] Hash mismatch for existing {fileInfo.Path}. Expected: {expectedHash}, Got: {localHash}. Redownloading...");
                File.Delete(localPath); // 文件损坏或版本陈旧，直接删除重下
            }
            else if (existingLength > 0)
            {
                File.Delete(localPath); // 大小不对的文件删除
            }

            Exception? lastEx = null;

            foreach (var baseUrl in baseUrls)
            {
                try
                {
                    string url = $"{baseUrl}{sourceDir}{fileInfo.Path}";
                    string tempPath = localPath + ".tmp";

                    await DoDownloadStreamAsync(url, tempPath, localPath, expectedSize, token, onProgress);
                    
                    // 下载完成后验证
                    ulong downloadedHash = StellaSoraCrc64.Compute(localPath);
                    if (downloadedHash != expectedHash)
                    {
                        File.Delete(localPath);
                        onProgress(0);
                        throw new Exception($"Hash validation failed after download! Expected: {expectedHash}, Got: {downloadedHash}");
                    }
                    
                    return;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                }
            }

            SharedStatic.InstanceLogger.LogError($"[StellaSoraInstaller] Failed to download {fileInfo.Path} from all CDNs. Last Error: {lastEx?.Message}");
            throw lastEx ?? new Exception($"Failed to download {fileInfo.Path}");
        }

        private async Task DoDownloadStreamAsync(string url, string tempPath, string finalPath, long expectedSize,
            CancellationToken token, Action<long> onProgress)
        {
            long existingLength = 0;
            if (File.Exists(tempPath))
            {
                existingLength = new FileInfo(tempPath).Length;
                if (existingLength > expectedSize)
                {
                    File.Delete(tempPath);
                    existingLength = 0;
                }
                else if (existingLength == expectedSize)
                {
                    File.Move(tempPath, finalPath, true);
                    onProgress(existingLength);
                    return;
                }
            }

            long currentDownloaded = existingLength;
            if (currentDownloaded > 0) 
            {
                onProgress(currentDownloaded);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (existingLength > 0)
                request.Headers.Range = new RangeHeaderValue(existingLength, null);

            using var response = await _owner._downloadHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

            if (existingLength > 0 && response.StatusCode != HttpStatusCode.PartialContent)
            {
                existingLength = 0;
                currentDownloaded = 0;
                onProgress(0); 
                
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            await using var fs = new FileStream(tempPath, existingLength > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = ArrayPool<byte>.Shared.Rent(131072); 
            try
            {
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, read, token);
                    currentDownloaded += read;
                    onProgress(currentDownloaded);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tempPath, finalPath);
        }
    }
}