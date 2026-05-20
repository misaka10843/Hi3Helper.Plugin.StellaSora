using System;
using System.Runtime.InteropServices.Marshalling;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Management.PresetConfig;
using Hi3Helper.Plugin.Core.Update;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Plugin.StellaSora.Management.PresetConfig;
using Hi3Helper.Plugin.StellaSora.Utils;
using Microsoft.Extensions.Logging;

namespace Hi3Helper.Plugin.StellaSora;

[GeneratedComClass]
public partial class StellaSoraPlugin : PluginBase
{
    private static readonly IPluginPresetConfig[] PresetConfigInstances =
    [
        new StellaSoraCnPresetConfig(),
        new StellaSoraTwPresetConfig()
    ];

    private static DateTime _pluginCreationDate = new(2026, 04, 16, 00, 00, 0, DateTimeKind.Utc);

    public override void GetPluginName(out string result)
    {
        result = "Stella Sora Plugin";
    }

    public override void GetPluginDescription(out string result)
    {
        result = "A plugin for Stella Sora in Collapse Launcher";
    }

    public override void GetPluginAuthor(out string result)
    {
        result = "misaka10843";
    }

    public override unsafe void GetPluginCreationDate(out DateTime* result)
    {
        result = _pluginCreationDate.AsPointer();
    }

    public override void GetPresetConfigCount(out int count)
    {
        count = PresetConfigInstances.Length;
    }

    public override void GetPresetConfig(int index, out IPluginPresetConfig presetConfig)
    {
        SharedStatic.InstanceLogger.LogInformation("[StellaSora] Starting execution...");
        if (index < 0 || index >= PresetConfigInstances.Length)
        {
            SharedStatic.InstanceLogger.LogWarning($"[StellaSora] Invalid PresetConfig index requested: {index}");
            presetConfig = null!;
            return;
        }

        SharedStatic.InstanceLogger.LogInformation($"[StellaSora] Loading PresetConfig at index: {index}");
        presetConfig = PresetConfigInstances[index];
    }

    public override void GetPluginSelfUpdater(out IPluginSelfUpdate selfUpdate)
    {
        selfUpdate = new SelfUpdate();
    }

    public override void GetPluginAppIconUrl(out string result)
    {
        result = Convert.ToBase64String(StellaSoraImageData.StellaSoraAppIconData);
    }

    public override void GetNotificationPosterUrl(out string result)
    {
        result = Convert.ToBase64String(StellaSoraImageData.StellaSoraPosterData);
    }
}