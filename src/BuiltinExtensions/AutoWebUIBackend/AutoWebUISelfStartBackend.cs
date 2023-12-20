using FreneticUtilities.FreneticDataSyntax;
using FreneticUtilities.FreneticExtensions;
using StableSwarmUI.Core;
using StableSwarmUI.DataHolders;
using StableSwarmUI.Utils;
using System.Diagnostics;
using StableSwarmUI.Backends;
using System.IO;

namespace StableSwarmUI.Builtin_AutoWebUIExtension;

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
        settings.StartScript = settings.StartScript.Trim(' ', '"', '\'', '\n', '\r', '\t');
        if (settings.StartScript.AfterLast('/').BeforeLast('.') == "webui-user" && File.Exists(settings.StartScript))
        {
            if (settings.StartScript.EndsWith(".sh")) // On Linux, webui-user.sh is not a valid launcher at all
            {
                Logs.Error($"Refusing init of AutoWebUI with 'webui-user.sh' target script. Please use the 'webui.sh' script instead.");
                Status = BackendStatus.ERRORED;
                return;
            }
            string scrContent = File.ReadAllText(settings.StartScript);
            if (!scrContent.Contains("%*") && !scrContent.Contains("%~")) // on Windows, it's only valid if you forward swarm's CLI args
            {
                Logs.Error($"Refusing init of AutoWebUI with 'webui-user.bat' target script. Please use the 'webui.bat' script instead. (If webui-user.bat usage is intentional, please forward CLI args, eg 'COMMANDLINE_ARGS=%*'.");
                Status = BackendStatus.ERRORED;
                return;
            }
        }
        await NetworkBackendUtils.DoSelfStart(settings.StartScript, this, $"AutoWebUI-{BackendData.ID}", $"backend-{BackendData.ID}", settings.GPU_ID, settings.ExtraArgs + " --api --port={PORT}", InitInternal, (p, r) => { Port = p; RunningProcess = r; });
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
            Utilities.KillProcess(RunningProcess, 10);
            Logs.Info("Auto WebUI process shut down.");
        }
        catch (Exception ex)
        {
            Logs.Error($"Error stopping Auto WebUI process: {ex}");
        }
    }
}
