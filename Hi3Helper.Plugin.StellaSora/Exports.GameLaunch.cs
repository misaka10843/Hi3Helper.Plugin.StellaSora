using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.StellaSora.Management;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.StellaSora;

public partial class Exports
{
    protected override (bool IsSupported, Task<bool> Task) LaunchGameFromGameManagerCoreAsync(
        GameManagerExtension.RunGameFromGameManagerContext context, string? startArgument, bool isRunBoosted,
        ProcessPriorityClass processPriority, CancellationToken token)
    {
        InstanceLogger.LogInformation("[StellaSora::LaunchGame] Initiating game launch sequence...");
        return (true, Impl());

        async Task<bool> Impl()
        {
            if (!TryGetStartingProcessFromContext(context, startArgument, out var process))
            {
                InstanceLogger.LogError("[StellaSora::LaunchGame] Failed to get starting process context.");
                return false;
            }

            using (process)
            {
                process.Start();
                InstanceLogger.LogInformation($"[StellaSora::LaunchGame] Game process started with PID: {process.Id}");

                try
                {
                    process.PriorityBoostEnabled = isRunBoosted;
                    process.PriorityClass = processPriority;
                    InstanceLogger.LogInformation(
                        $"[StellaSora::LaunchGame] Process priority set to: {processPriority}");
                }
                catch (Exception e)
                {
                    InstanceLogger.LogError(e, "[StellaSora::LaunchGame] Failed to set process priority.");
                }

                _ = ReadGameLog(context, token);

                await process.WaitForExitAsync(token);
                InstanceLogger.LogInformation("[StellaSora::LaunchGame] Game process exited naturally.");

                return true;
            }
        }
    }

    protected override bool IsGameRunningCore(GameManagerExtension.RunGameFromGameManagerContext context,
        out bool isGameRunning, out DateTime gameStartTime)
    {
        isGameRunning = false;
        gameStartTime = default;

        if (!TryGetGameExecutablePath(context, out var gameExecutablePath)) return true;

        using var process = FindExecutableProcess(gameExecutablePath);
        if (process != null)
        {
            isGameRunning = true;
            gameStartTime = process.StartTime;
        }

        return true;
    }

    protected override (bool IsSupported, Task<bool> Task) WaitRunningGameCoreAsync(
        GameManagerExtension.RunGameFromGameManagerContext context, CancellationToken token)
    {
        return (true, Impl());

        async Task<bool> Impl()
        {
            if (!TryGetGameExecutablePath(context, out var gameExecutablePath)) return true;

            using var process = FindExecutableProcess(gameExecutablePath);
            if (process != null)
            {
                InstanceLogger.LogInformation("[StellaSora::WaitGame] Awaiting game exit...");
                await process.WaitForExitAsync(token);
            }

            return true;
        }
    }

    protected override bool KillRunningGameCore(GameManagerExtension.RunGameFromGameManagerContext context,
        out bool wasGameRunning, out DateTime gameStartTime)
    {
        wasGameRunning = false;
        gameStartTime = default;

        if (!TryGetGameExecutablePath(context, out var gameExecutablePath)) return true;

        using var process = FindExecutableProcess(gameExecutablePath);
        if (process == null) return true;

        wasGameRunning = true;
        gameStartTime = process.StartTime;
        process.Kill();
        InstanceLogger.LogInformation(
            $"[StellaSora::KillGame] Game process (PID: {process.Id}) has been forcefully killed.");
        return true;
    }

    private static Process? FindExecutableProcess(string? executablePath)
    {
        if (executablePath == null) return null;

        var executableDirPath = Path.GetDirectoryName(executablePath.AsSpan());
        var executableName = Path.GetFileNameWithoutExtension(executablePath);

        var processes = Process.GetProcessesByName(executableName);
        Process? returnProcess = null;

        foreach (var process in processes)
            if (process.MainModule?.FileName.StartsWith(executableDirPath, StringComparison.OrdinalIgnoreCase) ?? false)
            {
                returnProcess = process;
                break;
            }

        foreach (var process in processes.Where(x => x != returnProcess)) process.Dispose();

        return returnProcess;
    }

    private static bool TryGetGameExecutablePath(GameManagerExtension.RunGameFromGameManagerContext context,
        [NotNullWhen(true)] out string? gameExecutablePath)
    {
        gameExecutablePath = null;
        if (context is not
            {
                GameManager: StellaSoraGameManager stellaSoraManager, PresetConfig: PluginPresetConfigBase presetConfig
            }) return false;

        stellaSoraManager.GetGamePath(out var gamePath);
        presetConfig.comGet_GameExecutableName(out var executablePath);

        gamePath?.NormalizePathInplace();
        executablePath.NormalizePathInplace();

        if (string.IsNullOrEmpty(gamePath)) return false;

        gameExecutablePath = Path.Combine(gamePath, executablePath);
        return File.Exists(gameExecutablePath);
    }

    private static bool TryGetStartingProcessFromContext(GameManagerExtension.RunGameFromGameManagerContext context,
        string? startArgument, [NotNullWhen(true)] out Process? process)
    {
        process = null;
        if (!TryGetGameExecutablePath(context, out var startingExecutablePath)) return false;

        var startInfo = string.IsNullOrEmpty(startArgument)
            ? new ProcessStartInfo(startingExecutablePath)
            : new ProcessStartInfo(startingExecutablePath, startArgument);

        startInfo.WorkingDirectory = Path.GetDirectoryName(startingExecutablePath);
        startInfo.UseShellExecute = false;

        process = new Process
        {
            StartInfo = startInfo
        };
        return true;
    }

    private static async Task ReadGameLog(GameManagerExtension.RunGameFromGameManagerContext context,
        CancellationToken token)
    {
        if (context is not { PresetConfig: PluginPresetConfigBase presetConfig }) return;

        presetConfig.comGet_GameAppDataPath(out var gameAppDataPath);
        presetConfig.comGet_GameLogFileName(out var gameLogFileName);

        if (string.IsNullOrEmpty(gameAppDataPath) || string.IsNullOrEmpty(gameLogFileName))
            return;

        var gameLogPath = Path.Combine(gameAppDataPath, gameLogFileName);

        var retry = 5;
        while (!File.Exists(gameLogPath) && retry >= 0)
        {
            InstanceLogger.LogInformation(
                $"[StellaSora::ReadGameLog] Waiting for log file to be generated... Retry count: {retry}");
            await Task.Delay(1000, token);
            --retry;
        }

        if (retry <= 0)
        {
            InstanceLogger.LogWarning("[StellaSora::ReadGameLog] Log file was not found. Skipping log read.");
            return;
        }

        InstanceLogger.LogInformation($"[StellaSora::ReadGameLog] Successfully found log file at: {gameLogPath}");
        var printCallback = context.PrintGameLogCallback;

        try
        {
            await using var fileStream =
                File.Open(gameLogPath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fileStream);

            fileStream.Position = 0;
            while (!token.IsCancellationRequested)
            {
                while (await reader.ReadLineAsync(token) is { } line) PassStringLineToCallback(printCallback, line);
                await Task.Delay(250, token);
            }
        }
        catch (Exception ex)
        {
            InstanceLogger.LogWarning($"[StellaSora::ReadGameLog] Stopped reading log: {ex.Message}");
        }

        return;

        static unsafe void PassStringLineToCallback(GameManagerExtension.PrintGameLog? invoke, string line)
        {
            var lineP = line.GetPinnableStringPointer();
            var lineLen = line.Length;
            invoke?.Invoke(lineP, lineLen, 0);
        }
    }
}