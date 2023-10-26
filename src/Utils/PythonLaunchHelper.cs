using FreneticUtilities.FreneticExtensions;
using StableSwarmUI.Core;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace StableSwarmUI.Utils;

/// <summary>Tiny specialty class to help launch python programs easily.</summary>
public class PythonLaunchHelper
{
    /// <summary>Clean up a <see cref="ProcessStartInfo"/> environment of python env vars that cause problems.</summary>
    public static void CleanEnvironmentOfPythonMess(ProcessStartInfo start, string prefix)
    {
        void RemoveEnvLoudly(string key)
        {
            if (start.Environment.TryGetValue(key, out string val))
            {
                start.Environment.Remove(key);
                Logs.Debug($"{prefix}Removing environment variable {key} which was {val}");
            }
        }
        RemoveEnvLoudly("PYTHONHOME");
        RemoveEnvLoudly("PYTHONPATH");
        if (start.Environment.TryGetValue("LIB", out string libVal) && libVal.Contains("python"))
        {
            start.Environment.Remove("LIB");
            Logs.Debug($"{prefix}Removing environment variable LIB due to being a python-lib val which was {libVal}");
        }
        start.Environment["PYTHONUNBUFFERED"] = "true";
    }

    /// <summary>Helper to fix up python paths in environment PATH var.</summary>
    public static string ReworkPythonPaths(string path)
    {
        string above = Path.GetFullPath($"{path}/..");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Strip python but be a little cautious about it
            string[] paths = Environment.GetEnvironmentVariable("PATH").Split(';').Where(p => !p.Contains("Python3") && !p.Contains("Programs\\Python") && !p.Contains("Python\\Python")).ToArray();
            string[] python = paths.Where(p => p.ToLowerFast().Contains("python")).ToArray();
            if (python.Any())
            {
                Logs.Debug($"Python paths left: {python.JoinString("; ")}");
            }
            return $"{path};{path}\\Scripts;{path}\\Lib;{path}\\Lib\\site-packages;{above};{paths.JoinString(";")}";
        }
        else
        {
            string libFolder = Directory.GetDirectories(path, "lib").FirstOrDefault();
            string libPath = libFolder == null ? "" : Path.GetFullPath(Utilities.CombinePathWithAbsolute(path, "lib", libFolder));
            return $"{path}:{path}/bin:{path}/lib:{libPath}:{libPath}/site-packages:{above}:{Environment.GetEnvironmentVariable("PATH")}";
        }
    }

    /// <summary>Helper to launch a generic python process.</summary>
    public static Process LaunchGeneric(string script, bool autoOutput, string[] args)
    {
        ProcessStartInfo start = new()
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Environment.CurrentDirectory
        };
        // Favor Comfy dlbackend python, as that's known to have torch/etc. packages already.
        if (File.Exists("./dlbackend/comfy/python_embeded/python.exe"))
        {
            start.FileName = "./dlbackend/comfy/python_embeded/python.exe";
            start.WorkingDirectory = Path.GetFullPath("./dlbackend/comfy/");
            start.Environment["PATH"] = ReworkPythonPaths(Path.GetFullPath("./dlbackend/comfy/python_embeded"));
            CleanEnvironmentOfPythonMess(start, "(Generic python launch) ");
        }
        else if (File.Exists("./dlbackend/ComfyUI/venv/bin/python"))
        {
            start.FileName = "./dlbackend/ComfyUI/venv/bin/python";
            CleanEnvironmentOfPythonMess(start, "(Generic python launch) ");
            start.Environment["PATH"] = ReworkPythonPaths(Path.GetFullPath("./dlbackend/ComfyUI/venv/bin"));
        }
        else
        {
            // Fall back to global python.
            start.FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python" : "python3";
        }
        Logs.Debug($"(Generic python launch) Will use python: {start.FileName}");
        start.ArgumentList.Add("-s");
        start.ArgumentList.Add(Path.GetFullPath(script));
        foreach (string arg in args)
        {
            start.ArgumentList.Add(arg);
        }
        Process runningProcess = new() { StartInfo = start };
        runningProcess.Start();
        if (autoOutput)
        {
            void MonitorOut()
            {
                string line;
                while ((line = runningProcess.StandardOutput.ReadLine()) != null)
                {
                    Logs.Debug($"(Generic python launch) stdout: {line}");
                }
            }
            void MonitorErr()
            {
                string line;
                while ((line = runningProcess.StandardError.ReadLine()) != null)
                {
                    Logs.Debug($"(Generic python launch) stderr: {line}");
                }
            }
            new Thread(MonitorOut).Start();
            new Thread(MonitorErr).Start();
        }
        return runningProcess;
    }
}
