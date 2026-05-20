using System;
using System.Buffers;
using System.Collections.Concurrent;
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

            if (_owner.GameManager is not StellaSoraGameManager manager
                || manager.GameManifest == null
                || manager.GameResourceDownloadUrls.Count == 0)
                throw new InvalidOperationException("Game manifest or CDN URLs are missing.");

            _owner.GameManager.GetGamePath(out var installPath);
            if (string.IsNullOrEmpty(installPath)) throw new InvalidOperationException("Install path is missing.");

            var newManifest = manager.GameManifest;
            var sourceDir   = newManifest.Source;
            var cdnUrls     = manager.GameResourceDownloadUrls;
            var allNewFiles = newManifest.Files;

            progressStateDelegate?.Invoke(InstallProgressState.Preparing);

            // Determine what to download / delete
            List<StellaSoraManifestFile> filesToDownload;
            List<StellaSoraManifestFile> filesToDelete = [];

            if (kind == GameInstallerKind.Update)
            {
                var localManifest  = manager.ReadLocalManifest(installPath);
                var currentFiles   = localManifest?.Files ?? [];
                (filesToDownload, filesToDelete) = ComputeManifestDiff(currentFiles, allNewFiles, installPath);

                SharedStatic.InstanceLogger.LogInformation(
                    $"[StellaSoraInstaller] Update diff — download: {filesToDownload.Count}, delete: {filesToDelete.Count}");
            }
            else
            {
                filesToDownload = ComputeStatMismatchFiles(allNewFiles, installPath);
                SharedStatic.InstanceLogger.LogInformation(
                    $"[StellaSoraInstaller] Install — {filesToDownload.Count} files need downloading");
            }

            // Download missing / changed files
            if (filesToDownload.Count > 0)
                await DownloadFilesAsync(filesToDownload, sourceDir, cdnUrls, installPath,
                    progressDelegate, progressStateDelegate, token);

            // Remove obsolete files (update only)
            if (filesToDelete.Count > 0)
            {
                progressStateDelegate?.Invoke(InstallProgressState.Removing);
                DeleteObsoleteFiles(filesToDelete, installPath);
            }

            //  Post-install CRC verification + repair
            //    (mirrors Endfield's HgGameRepairer.StartRepairAsync)
            await VerifyAndRepairAsync(allNewFiles, sourceDir, cdnUrls, installPath,
                progressDelegate, progressStateDelegate, token);

            // Persist manifest.json
            manager.WriteLocalManifest(installPath, newManifest);

            progressStateDelegate?.Invoke(InstallProgressState.Completed);
        }

        // Helpers

        /// <summary>
        /// Computes which files to download (new / hash-changed / locally missing by size)
        /// and which to delete (present in old manifest but absent from new manifest).
        /// </summary>
        private static (List<StellaSoraManifestFile> needDownload, List<StellaSoraManifestFile> needDelete)
            ComputeManifestDiff(
                List<StellaSoraManifestFile> currentFiles,
                List<StellaSoraManifestFile> newFiles,
                string installPath)
        {
            var needDownload   = new List<StellaSoraManifestFile>();
            var needDelete     = new List<StellaSoraManifestFile>();
            var newFileByPath  = newFiles.ToDictionary(f => f.Path, StringComparer.OrdinalIgnoreCase);
            var currFileByPath = currentFiles.ToDictionary(f => f.Path, StringComparer.OrdinalIgnoreCase);

            foreach (var newFile in newFiles)
            {
                bool download;
                if (!currFileByPath.TryGetValue(newFile.Path, out var currFile))
                {
                    download = true; // brand-new file
                }
                else if (currFile.Hash != newFile.Hash)
                {
                    download = true; // content changed
                }
                else
                {
                    // Same manifest hash — do a quick stat check
                    var localPath    = Path.Combine(installPath, newFile.Path.TrimStart('/'));
                    long expectedSize = long.Parse(newFile.Size);
                    download = !File.Exists(localPath) || new FileInfo(localPath).Length != expectedSize;
                }

                if (download) needDownload.Add(newFile);
            }

            foreach (var currFile in currentFiles)
            {
                if (!newFileByPath.ContainsKey(currFile.Path))
                    needDelete.Add(currFile);
            }

            return (needDownload, needDelete);
        }

        /// <summary>
        /// Returns files whose local size does not match the manifest (used for fresh installs).
        /// </summary>
        private static List<StellaSoraManifestFile> ComputeStatMismatchFiles(
            List<StellaSoraManifestFile> files, string installPath)
        {
            var result = new List<StellaSoraManifestFile>();
            foreach (var file in files)
            {
                var localPath    = Path.Combine(installPath, file.Path.TrimStart('/'));
                long expectedSize = long.Parse(file.Size);
                if (!File.Exists(localPath) || new FileInfo(localPath).Length != expectedSize)
                    result.Add(file);
            }
            return result;
        }

        private static void DeleteObsoleteFiles(List<StellaSoraManifestFile> files, string installPath)
        {
            foreach (var file in files)
            {
                var localPath = Path.Combine(installPath, file.Path.TrimStart('/'));
                try
                {
                    if (File.Exists(localPath))
                    {
                        File.Delete(localPath);
                        SharedStatic.InstanceLogger.LogInformation(
                            $"[StellaSoraInstaller] Deleted obsolete file: {file.Path}");
                    }
                }
                catch (Exception ex)
                {
                    SharedStatic.InstanceLogger.LogWarning(
                        $"[StellaSoraInstaller] Could not delete {file.Path}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Downloads a list of files concurrently with per-file CRC verification and CDN fallback.
        /// </summary>
        private async Task DownloadFilesAsync(
            List<StellaSoraManifestFile> files,
            string sourceDir,
            List<string> cdnUrls,
            string installPath,
            InstallProgressDelegate? progressDelegate,
            InstallProgressStateDelegate? progressStateDelegate,
            CancellationToken token)
        {
            InstallProgress progress = default;
            progress.TotalCountToDownload = files.Count;
            progress.TotalBytesToDownload = files.Sum(f => long.Parse(f.Size));
            progress.TotalStateToComplete = 1;

            void ReportThrottled()
            {
                long now  = Stopwatch.GetTimestamp();
                long last = Interlocked.Read(ref _lastUpdateTime);
                if (now - last > Stopwatch.Frequency / 10
                    && Interlocked.CompareExchange(ref _lastUpdateTime, now, last) == last)
                    progressDelegate?.Invoke(in progress);
            }

            progressDelegate?.Invoke(in progress);
            progressStateDelegate?.Invoke(InstallProgressState.Download);

            using var semaphore    = new SemaphoreSlim(MaxConcurrentDownloads);
            var       downloadTasks = new List<Task>();

            foreach (var fileInfo in files)
            {
                downloadTasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(token);
                    try
                    {
                        var  localPath    = Path.Combine(installPath, fileInfo.Path.TrimStart('/'));
                        long expectedSize = long.Parse(fileInfo.Size);
                        long prevBytes    = 0;

                        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

                        await DownloadSingleFileWithFallbackAsync(
                            cdnUrls, sourceDir, fileInfo, localPath, expectedSize, token,
                            currentBytes =>
                            {
                                long delta = currentBytes - prevBytes;
                                prevBytes = currentBytes;
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
        }

        /// <summary>
        /// Verifies CRC64 of every file in the manifest in parallel.
        /// Any file that fails is collected, re-downloaded, and re-verified once.
        /// </summary>
        private async Task VerifyAndRepairAsync(
            List<StellaSoraManifestFile> allFiles,
            string sourceDir,
            List<string> cdnUrls,
            string installPath,
            InstallProgressDelegate? progressDelegate,
            InstallProgressStateDelegate? progressStateDelegate,
            CancellationToken token)
        {
            progressStateDelegate?.Invoke(InstallProgressState.Verify);
            SharedStatic.InstanceLogger.LogInformation(
                $"[StellaSoraInstaller] Verifying {allFiles.Count} files...");

            InstallProgress verifyProgress = default;
            verifyProgress.TotalCountToDownload = allFiles.Count;
            verifyProgress.TotalBytesToDownload  = allFiles.Sum(f => long.Parse(f.Size));
            verifyProgress.TotalStateToComplete  = 1;

            var brokenFiles = new ConcurrentBag<StellaSoraManifestFile>();

            await Parallel.ForEachAsync(allFiles,
                new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentDownloads, CancellationToken = token },
                async (file, innerToken) =>
                {
                    var  localPath    = Path.Combine(installPath, file.Path.TrimStart('/'));
                    long expectedSize = long.Parse(file.Size);
                    ulong expectedHash = ulong.Parse(file.Hash);
                    bool isOk         = false;

                    if (File.Exists(localPath) && new FileInfo(localPath).Length == expectedSize)
                    {
                        ulong localHash = await Task.Run(() => StellaSoraCrc64.Compute(localPath), innerToken);
                        isOk = localHash == expectedHash;
                    }

                    if (!isOk)
                    {
                        brokenFiles.Add(file);
                        SharedStatic.InstanceLogger.LogWarning(
                            $"[StellaSoraInstaller] Integrity check failed: {file.Path}");
                    }

                    Interlocked.Add(ref verifyProgress.DownloadedBytes, expectedSize);
                    Interlocked.Increment(ref verifyProgress.DownloadedCount);
                    progressDelegate?.Invoke(in verifyProgress);
                });

            var brokenList = brokenFiles.ToList();
            if (brokenList.Count == 0)
            {
                SharedStatic.InstanceLogger.LogInformation("[StellaSoraInstaller] All files passed integrity check.");
                verifyProgress.StateCount = 1;
                progressDelegate?.Invoke(in verifyProgress);
                return;
            }

            SharedStatic.InstanceLogger.LogWarning(
                $"[StellaSoraInstaller] {brokenList.Count} broken file(s) found — repairing...");

            // Re-download broken files
            await DownloadFilesAsync(brokenList, sourceDir, cdnUrls, installPath,
                progressDelegate, progressStateDelegate, token);

            verifyProgress.StateCount = 1;
            progressDelegate?.Invoke(in verifyProgress);

            SharedStatic.InstanceLogger.LogInformation("[StellaSoraInstaller] Repair complete.");
        }

        //Low-level download primitives

        private async Task DownloadSingleFileWithFallbackAsync(
            List<string> baseUrls,
            string sourceDir,
            StellaSoraManifestFile fileInfo,
            string localPath,
            long expectedSize,
            CancellationToken token,
            Action<long> onProgress)
        {
            ulong expectedHash   = ulong.Parse(fileInfo.Hash);
            long  existingLength = File.Exists(localPath) ? new FileInfo(localPath).Length : 0;

            // Skip if file is already correct
            if (existingLength == expectedSize)
            {
                ulong localHash = StellaSoraCrc64.Compute(localPath);
                if (localHash == expectedHash)
                {
                    onProgress(expectedSize);
                    return;
                }
                SharedStatic.InstanceLogger.LogWarning(
                    $"[StellaSoraInstaller] Hash mismatch: {fileInfo.Path}. Expected: {expectedHash}, Got: {localHash}. Re-downloading...");
                File.Delete(localPath);
            }
            else if (existingLength > 0)
            {
                File.Delete(localPath);
            }

            Exception? lastEx = null;
            foreach (var baseUrl in baseUrls)
            {
                try
                {
                    string tempPath = localPath + ".tmp";
                    await DoDownloadStreamAsync(
                        $"{baseUrl}{sourceDir}{fileInfo.Path}", tempPath, localPath, expectedSize, token, onProgress);

                    ulong downloadedHash = StellaSoraCrc64.Compute(localPath);
                    if (downloadedHash != expectedHash)
                    {
                        File.Delete(localPath);
                        onProgress(0);
                        throw new Exception(
                            $"Post-download hash mismatch for {fileInfo.Path}. Expected: {expectedHash}, Got: {downloadedHash}");
                    }
                    return;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                }
            }

            SharedStatic.InstanceLogger.LogError(
                $"[StellaSoraInstaller] All CDNs failed for {fileInfo.Path}. Last error: {lastEx?.Message}");
            throw lastEx ?? new Exception($"Failed to download {fileInfo.Path}");
        }

        private async Task DoDownloadStreamAsync(string url, string tempPath, string finalPath,
            long expectedSize, CancellationToken token, Action<long> onProgress)
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
            if (currentDownloaded > 0) onProgress(currentDownloaded);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (existingLength > 0)
                request.Headers.Range = new RangeHeaderValue(existingLength, null);

            using var response = await _owner._downloadHttpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);

            if (existingLength > 0 && response.StatusCode != HttpStatusCode.PartialContent)
            {
                existingLength    = 0;
                currentDownloaded = 0;
                onProgress(0);
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            await using var fs     = new FileStream(tempPath,
                existingLength > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None);

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