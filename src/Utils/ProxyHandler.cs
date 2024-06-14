using FreneticUtilities.FreneticExtensions;
using StableSwarmUI.Core;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;

namespace StableSwarmUI.Utils;

/// <summary>Helper class to handle public proxy forwarding.</summary>
public class PublicProxyHandler
{
    /// <summary>Path to the proxy executable.</summary>
    public string Path;

    /// <summary>Proxy name, eg 'ngrok'.</summary>
    public string Name;

    /// <summary>Launch arguments.</summary>
    public string[] Args;

    /// <summary>Region input, if specified.</summary>
    public string Region;

    /// <summary>Basic auth input, if specified.</summary>
    public string BasicAuth;

    /// <summary>The running proxy process.</summary>
    public Process Process;

    /// <summary>The publicly accessible proxy URL generated (once known).</summary>
    public string PublicURL;

    /// <summary>Starts the proxy.</summary>
    public void Start()
    {
        ProcessStartInfo start = new() { FileName = Path, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        if (Args is not null)
        {
            foreach (string arg in Args)
            {
                start.ArgumentList.Add(arg);
            }
        }
        else if (Name == "Ngrok")
        {
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
        }
        else if (Name == "Cloudflare")
        {
            start.ArgumentList.Add("tunnel");
            start.ArgumentList.Add("--url");
            start.ArgumentList.Add(WebServer.HostURL);
            if (Region != null)
            {
                start.ArgumentList.Add($"--region={Region}");
            }
        }
        Process = new() { StartInfo = start };
        Process.Start();
        Logs.Debug($"{Name} launched as process #{Process.Id}.");
        foreach ((string type, StreamReader sr) in new[] { ("out", Process.StandardOutput), ("err", Process.StandardError) })
        {
            new Thread(() =>
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    Logs.Debug($"{Name} says: {line}");
                    if (Name == "Ngrok")
                    {
                        string[] parts = line.SplitFast(' ');
                        // t=time lvl=info msg="started tunnel" obj=tunnels name=command_line addr=(internal_address) url=(this-is-what-we-want)
                        if (parts.Length >= 8)
                        {
                            if (parts[2] == "msg=\"started" && parts[3] == "tunnel\"" && parts[7].StartsWith("url="))
                            {
                                PublicURL = parts[7].After("url=");
                                Logs.Info($"{Name} ready! Generated URL: {PublicURL}");
                            }
                        }
                    }
                    else if (Name == "Cloudflare")
                    {
                        // time-iso INF |  https://some-generated-name-here-trycloudflare.com      |
                        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 3 && parts[1] == "INF" && parts[2] == "|" && parts[3].StartsWith("https://") && parts[3].EndsWith(".trycloudflare.com"))
                        {
                            PublicURL = parts[3];
                            Logs.Info($"{Name} ready! Generated URL: {PublicURL}");
                        }
                    }
                }
                Logs.Info($"{Name} process exited.");
            })
            { Name = $"{Name}Monitor_{type}" }.Start();
        }
    }

    /// <summary>Stops and closes proxy cleanly.</summary>
    public void Stop()
    {
        try
        {
            if (Process is null || Process.HasExited)
            {
                return;
            }
            Logs.Info($"Shutting down {Name} process #{Process.Id}...");
            Utilities.KillProcess(Process, 15);
        }
        catch (Exception e)
        {
            if (e is DllNotFoundException && PublicURL is null) // If we kill it too quickly sys_kill will complain, so just silence irrelevant errors.
            {
                Logs.Debug($"{Name} shutdown failed.");
                return;
            }
            Logs.Error($"Error stopping {Name}: {e}");
        }
    }
}
