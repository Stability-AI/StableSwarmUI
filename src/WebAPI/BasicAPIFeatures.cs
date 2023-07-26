using Newtonsoft.Json.Linq;
using StableSwarmUI.Core;
using StableSwarmUI.Utils;
using StableSwarmUI.Accounts;
using StableSwarmUI.Text2Image;
using FreneticUtilities.FreneticExtensions;
using Microsoft.AspNetCore.Http;
using FreneticUtilities.FreneticDataSyntax;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.IO;
using StableSwarmUI.Builtin_ComfyUIBackend;
using StableSwarmUI.Backends;
using System.Diagnostics;
using System.Net.Http;

namespace StableSwarmUI.WebAPI;

/// <summary>Internal helper for all the basic API routes.</summary>
public static class BasicAPIFeatures
{
    /// <summary>Called by <see cref="Program"/> to register the core API calls.</summary>
    public static void Register()
    {
        API.RegisterAPICall(GetNewSession);
        API.RegisterAPICall(InstallConfirmWS);
        API.RegisterAPICall(GetMyUserData);
        API.RegisterAPICall(AddNewPreset);
        API.RegisterAPICall(DeletePreset);
        API.RegisterAPICall(GetCurrentStatus);
        API.RegisterAPICall(InterruptAll);
        API.RegisterAPICall(GetUserSettings);
        API.RegisterAPICall(ChangeUserSettings);
        T2IAPI.Register();
        BackendAPI.Register();
        AdminAPI.Register();
    }

    /// <summary>API Route to create a new session automatically.</summary>
    public static async Task<JObject> GetNewSession(HttpContext context)
    {
        return new JObject() { ["session_id"] = Program.Sessions.CreateAdminSession(context.Connection.RemoteIpAddress?.ToString() ?? "unknown").ID, ["version"] = Utilities.VaryID };
    }

    public static async Task<JObject> InstallConfirmWS(Session session, WebSocket socket, string theme, string installed_for, string backend, string stability_api_key, string models)
    {
        if (Program.ServerSettings.IsInstalled)
        {
            await socket.SendJson(new JObject() { ["error"] = $"Server is already installed!" }, API.WebsocketTimeout);
            return null;
        }
        if (!session.User.Restrictions.Admin)
        {
            await socket.SendJson(new JObject() { ["error"] = $"You are not an admin of this server, install request refused." }, API.WebsocketTimeout);
            return null;
        }
        async Task output(string str) => await socket.SendJson(new JObject() { ["info"] = str }, API.WebsocketTimeout);
        await output("Installation request received, processing...");
        if (Program.Web.RegisteredThemes.ContainsKey(theme))
        {
            await output($"Setting theme to {theme}.");
            Program.ServerSettings.DefaultUser.Theme = theme;
        }
        else
        {
            await output($"Theme {theme} is not valid!");
            await socket.SendJson(new JObject() { ["error"] = $"Invalid theme input!" }, API.WebsocketTimeout);
            return null;
        }
        switch (installed_for)
        {
            case "just_self":
                await output("Configuring settings as 'just yourself' install.");
                Program.ServerSettings.Network.Host = "localhost";
                Program.ServerSettings.Network.Port = 7801;
                Program.ServerSettings.Network.PortCanChange = true;
                Program.ServerSettings.LaunchMode = "web"; // TODO: Electron?
                break;
            case "just_self_lan":
                await output("Configuring settings as 'just yourself (LAN)' install.");
                Program.ServerSettings.Network.Host = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "*" : "0.0.0.0";
                Program.ServerSettings.Network.Port = 7801;
                Program.ServerSettings.Network.PortCanChange = true;
                Program.ServerSettings.LaunchMode = "web";
                break;
            default:
                await output($"Invalid install type {installed_for}!");
                await socket.SendJson(new JObject() { ["error"] = $"Invalid install type!" }, API.WebsocketTimeout);
                return null;
        }
        HttpClient client = NetworkBackendUtils.MakeHttpClient();
        switch (backend)
        {
            case "comfyui":
                {
                    await output("Downloading ComfyUI backend... please wait...");
                    Directory.CreateDirectory("dlbackend/");
                    string path;
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        await Utilities.DownloadFile("https://github.com/comfyanonymous/ComfyUI/releases/download/latest/ComfyUI_windows_portable_nvidia_or_cpu_nightly_pytorch.7z", "dlbackend/comfyui_dl.7z");
                        await output("Downloaded! Extracting...");
                        Directory.CreateDirectory("dlbackend/tmpcomfy/");
                        await Process.Start("launchtools/7z/win/7za.exe", $"x dlbackend/comfyui_dl.7z -o\"dlbackend/tmpcomfy/\" -y").WaitForExitAsync(Program.GlobalProgramCancel);
                        Directory.Move("dlbackend/tmpcomfy/ComfyUI_windows_portable_nightly_pytorch", "dlbackend/comfy");
                        await output("Installing prereqs...");
                        await Utilities.DownloadFile("https://aka.ms/vs/16/release/vc_redist.x64.exe", "dlbackend/vc_redist.x64.exe");
                        Process.Start(new ProcessStartInfo(Path.GetFullPath("dlbackend/vc_redist.x64.exe"), "/quiet /install /passive /norestart") { UseShellExecute = true }).WaitForExit();
                        path = "dlbackend/comfy/ComfyUI/main.py";
                    }
                    else
                    {
                        await Process.Start("/bin/bash", "launchtools/comfy-install-linux.sh").WaitForExitAsync(Program.GlobalProgramCancel);
                        path = "dlbackend/ComfyUI/main.py";
                    }
                    NvidiaUtil.NvidiaInfo[] nv = NvidiaUtil.QueryNvidia();
                    int gpu = 0;
                    if (nv is not null && nv.Length > 0)
                    {
                        NvidiaUtil.NvidiaInfo mostVRAM = nv.OrderByDescending(n => n.TotalMemory.InBytes).First();
                        gpu = mostVRAM.ID;
                    }
                    await output("Enabling ComfyUI...");
                    Program.Backends.AddNewOfType(Program.Backends.BackendTypes["comfyui_selfstart"], new ComfyUISelfStartBackend.ComfyUISelfStartSettings() { StartScript = path, GPU_ID = gpu });
                    break;
                }
            case "stabilityapi":
                if (string.IsNullOrWhiteSpace(stability_api_key))
                {
                    await output($"Invalid stability API key!");
                    await socket.SendJson(new JObject() { ["error"] = $"Invalid stability API key!" }, API.WebsocketTimeout);
                    return null;
                }
                File.WriteAllText("Data/sapi_key.dat", stability_api_key);
                Program.Backends.AddNewOfType(Program.Backends.BackendTypes["stabilityapi"]);
                break;
            case "none":
                await output("Not installing any backend.");
                break;
            default:
                await output($"Invalid backend type {backend}!");
                await socket.SendJson(new JObject() { ["error"] = $"Invalid backend type!" }, API.WebsocketTimeout);
                return null;
        }
        if (models != "none")
        {
            foreach (string model in models.Split(','))
            {
                string file = model.Trim() switch
                {
                    "sd15" => "https://huggingface.co/runwayml/stable-diffusion-v1-5/resolve/main/v1-5-pruned-emaonly.safetensors",
                    "sd21" => "https://huggingface.co/stabilityai/stable-diffusion-2-1/resolve/main/v2-1_768-ema-pruned.safetensors",
                    "sdxl1" => "https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0/resolve/main/sd_xl_base_1.0.safetensors",
                    _ => null
                };
                if (file is null)
                {
                    await output($"Invalid model {model}!");
                    await socket.SendJson(new JObject() { ["error"] = $"Invalid model!" }, API.WebsocketTimeout);
                    return null;
                }
                await output($"Downloading model from '{file}'... please wait...");
                string folder = Program.ServerSettings.Paths.SDModelFullPath + "/OfficialStableDiffusion";
                Directory.CreateDirectory(folder);
                string filename = file.AfterLast('/');
                await Utilities.DownloadFile(file, $"{folder}/{filename}");
                await output("Model download complete.");
            }
        }
        Program.ServerSettings.IsInstalled = true;
        if (Program.ServerSettings.LaunchMode == "webinstall")
        {
            Program.ServerSettings.LaunchMode = "web";
        }
        Program.SaveSettingsFile();
        await output("Installed!");
        await socket.SendJson(new JObject() { ["success"] = true }, API.WebsocketTimeout);
        return null;
    }

    /// <summary>API Route to get the user's own base data.</summary>
    public static async Task<JObject> GetMyUserData(Session session)
    {
        return new JObject()
        {
            ["user_name"] = session.User.UserID,
            ["presets"] = new JArray(session.User.GetAllPresets().Select(p => p.NetData()).ToArray())
        };
    }

    /// <summary>API Route to add a new user parameters preset.</summary>
    public static async Task<JObject> AddNewPreset(Session session, string title, string description, JObject raw, string preview_image = null, bool is_edit = false)
    {
        JObject paramData = (JObject)raw["param_map"];
        if (session.User.GetPreset(title) is not null && !is_edit)
        {
            return new JObject() { ["preset_fail"] = "A preset with that title already exists." };
        }
        T2IPreset preset = new()
        {
            Author = session.User.UserID,
            Title = title,
            Description = description,
            ParamMap = paramData.Properties().Select(p => (p.Name, p.Value.ToString())).PairsToDictionary(),
            PreviewImage = string.IsNullOrWhiteSpace(preview_image) ? "imgs/model_placeholder.jpg" : preview_image
        };
        if (preset.PreviewImage != "imgs/model_placeholder.jpg" && (!preset.PreviewImage.StartsWith("/Output") || preset.PreviewImage.Contains('?')))
        {
            Logs.Info($"User {session.User.UserID} tried to set a preset preview image to forbidden path: {preset.PreviewImage}");
            return new JObject() { ["preset_fail"] = "Forbidden preview-image path." };
        }
        session.User.SavePreset(preset);
        return new JObject() { ["success"] = true };
    }

    /// <summary>API Route to delete a user preset.</summary>
    public static async Task<JObject> DeletePreset(Session session, string preset)
    {
        return new JObject() { ["success"] = session.User.DeletePreset(preset) };
    }

    /// <summary>Gets current session status. Not an API call.</summary>
    public static JObject GetCurrentStatusRaw(Session session)
    {
        lock (session.StatsLocker)
        {
            return new JObject()
            {
                ["status"] = new JObject()
                {
                    ["waiting_gens"] = session.WaitingGenerations,
                    ["loading_models"] = session.LoadingModels,
                    ["waiting_backends"] = session.WaitingBackends,
                    ["live_gens"] = session.LiveGens
                }
            };
        }
    }

    /// <summary>API Route to get current waiting generation count, model loading count, etc.</summary>
    public static async Task<JObject> GetCurrentStatus(Session session)
    {
        return GetCurrentStatusRaw(session);
    }

    /// <summary>API Route to tell all waiting generations to interrupt.</summary>
    public static async Task<JObject> InterruptAll(Session session)
    {
        session.SessInterrupt.Cancel();
        session.SessInterrupt = new();
        return new JObject() { ["success"] = true };
    }

    public static async Task<JObject> GetUserSettings(Session session)
    {
        return new JObject() { ["settings"] = AdminAPI.AutoConfigToParamData(session.User.Settings) };
    }

    public static async Task<JObject> ChangeUserSettings(Session session, JObject rawData)
    {
        JObject settings = (JObject)rawData["settings"];
        foreach ((string key, JToken val) in settings)
        {
            AutoConfiguration.Internal.SingleFieldData field = session.User.Settings.TryGetFieldInternalData(key, out _);
            if (field is null)
            {
                Logs.Error($"User '{session.User.UserID}' tried to set unknown setting '{key}' to '{val}'.");
                continue;
            }
            object obj = AdminAPI.DataToType(val, field.Field.FieldType);
            if (obj is null)
            {
                Logs.Error($"User '{session.User.UserID}' tried to set setting '{key}' of type '{field.Field.FieldType.Name}' to '{val}', but type-conversion failed.");
                continue;
            }
            session.User.Settings.TrySetFieldValue(key, obj);
        }
        session.User.Save();
        return new JObject() { ["success"] = true };
    }
}
