using FreneticUtilities.FreneticExtensions;
using StableSwarmUI.Core;
using System.Diagnostics;

namespace StableSwarmUI.Utils;

/// <summary>Helper class to handle ngrok forwarding.</summary>
public class NgrokHandler
{
    /// <summary>Path to ngrok executable.</summary>
    public string Path;

    /// <summary>Region input, if specified.</summary>
    public string Region;

    /// <summary>Basic auth input, if specified.</summary>
    public string BasicAuth;

    /// <summary>The running Ngrok process.</summary>
    public Process NgrokProcess;

    /// <summary>The publicly accessible Ngrok URL generated (once known).</summary>
    public string PublicURL;

    /// <summary>Starts ngrok.</summary>
    public void Start()
    {
        ProcessStartInfo start = new() { FileName = Path, UseShellExecute = false, RedirectStandardOutput = true };
        start.ArgumentList.Add("http");
        start.ArgumentList.Add(WebServer.HostURL);
        if (Region != null)
        {
            start.ArgumentList.Add("--region");
            start.ArgumentList.Add(Region);
        }
        if (BasicAuth != null)
        {
            start.ArgumentList.Add("--basic-auth");
            start.ArgumentList.Add(BasicAuth);
        }
        start.ArgumentList.Add("--log");
        start.ArgumentList.Add("stdout");
        NgrokProcess = new() { StartInfo = start };
        NgrokProcess.Start();
        Logs.Debug($"Ngrok launched as process #{NgrokProcess.Id}.");
        new Thread(() =>
        {
            string line;
            while ((line = NgrokProcess.StandardOutput.ReadLine()) != null)
            {
                Logs.Debug($"Ngrok says: {line}");
                string[] parts = line.SplitFast(' ');
                // t=time lvl=info msg="started tunnel" obj=tunnels name=command_line addr=(internal_address) url=(this-is-what-we-want)
                if (parts.Length >= 8)
                {
                    if (parts[2] == "msg=\"started" && parts[3] == "tunnel\"" && parts[7].StartsWith("url="))
                    {
                        PublicURL = parts[7].After("url=");
                        Logs.Info($"Ngrok ready! Generated URL: {PublicURL}");
                    }
                }
            }
            Logs.Info("Ngrok process exited.");
        }) { Name = "NgrokMonitor" }.Start();
    }

    /// <summary>Stops and closes ngrok cleanly.</summary>
    public void Stop()
    {
        try
        {
            if (NgrokProcess is null || NgrokProcess.HasExited)
            {
                return;
            }
            Logs.Info($"Shutting down ngrok process #{NgrokProcess.Id}...");
            Utilities.sys_kill(NgrokProcess.Id, 15); // try graceful exit (SIGTERM=15)
            NgrokProcess.WaitForExit(TimeSpan.FromSeconds(15));
            if (!NgrokProcess.HasExited)
            {
                NgrokProcess.Kill(); // If still running 15 seconds later, hardkill it (SIGKILL=9)
            }
        }
        catch (Exception e)
        {
            if (e is DllNotFoundException && PublicURL is null) // If we kill it too quickly sys_kill will complain, so just silence irrelevant errors.
            {
                Logs.Debug("Ngrok shutdown failed.");
                return;
            }
            Logs.Error($"Error stopping ngrok: {e}");
        }
    }
}
