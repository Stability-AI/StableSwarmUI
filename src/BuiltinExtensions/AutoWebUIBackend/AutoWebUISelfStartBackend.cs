using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using StableUI.Core;
using StableUI.DataHolders;
using StableUI.Utils;
using System.Diagnostics;
using StableUI.Backends;

namespace StableUI.Builtin_AutoWebUIExtension;

public class AutoWebUISelfStartBackend : AutoWebUIAPIAbstractBackend
{
    public class AutoWebUISelfStartSettings : AutoConfiguration
    {
        [ConfigComment("The location of the 'webui.sh' or 'webui.bat' file.")]
        public string StartScript = "";

        [ConfigComment("Any arguments to include in the launch script.")]
        public string ExtraArgs = "";

        [ConfigComment("Which GPU to use, if multiple are available.")]
        public int GPU_ID = 0; // TODO: Determine GPU count and provide correct max
    }

    public Process RunningProcess;

    public int Port;

    public override string Address => $"http://localhost:{Port}";

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public override async Task Init()
    {
        AutoWebUISelfStartSettings settings = SettingsRaw as AutoWebUISelfStartSettings;
        await NetworkBackendUtils.DoSelfStart(settings.StartScript, this, "AutoWebUI", settings.GPU_ID, settings.ExtraArgs + " --api --port={PORT}", InitInternal, (p, r) => { Port = p; RunningProcess = r; });
    }

    public override async Task Shutdown()
    {
        await base.Shutdown();
        try
        {
            if (RunningProcess is null || RunningProcess.HasExited)
            {
                return;
            }
            Logs.Info($"Shutting down self-start Auto WebUI (port={Port}) process #{RunningProcess.Id}...");
            RunningProcess.Kill();
        }
        catch (Exception ex)
        {
            Logs.Error($"Error stopping Auto WebUI process: {ex}");
        }
    }
}
