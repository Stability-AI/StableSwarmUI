using FreneticUtilities.FreneticExtensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using StableUI.Backends;
using StableUI.Core;
using StableUI.Text2Image;
using StableUI.Utils;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace StableUI.Builtin_ComfyUIBackend;

/// <summary>Main class for the ComfyUI Backend extension.</summary>
public class ComfyUIBackendExtension : Extension
{
    public static string Folder;

    public static Dictionary<string, string> Workflows;

    /// <summary>Set of all feature-ids supported by ComfyUI backends.</summary>
    public static HashSet<string> FeaturesSupported = new() { "comfyui" };

    public override void OnPreInit()
    {
        Folder = FilePath;
        Refresh();
        Program.ModelRefreshEvent += Refresh;
        ScriptFiles.Add("Assets/comfy_workflow_editor_helper.js");
    }

    public void Refresh()
    {
        Workflows = new();
        foreach (string workflow in Directory.EnumerateFiles($"{Folder}/Workflows", "*.json", new EnumerationOptions() { RecurseSubdirectories = true }).Order())
        {
            string fileName = workflow.Replace('\\', '/').After("/Workflows/");
            if (fileName.EndsWith(".json"))
            {
                Workflows.Add(fileName.BeforeLast('.'), File.ReadAllText(workflow));
            }
        }
    }

    public override void OnInit()
    {
        T2IParamGroup comfyGroup = new("ComfyUI", Toggles: false, Open: false);
        T2IParamTypes.Register(new("[ComfyUI] Workflow", "What workflow to use in ComfyUI (files in 'Workflows' folder within the ComfyUI extension)",
            T2IParamDataType.DROPDOWN, "basic", (s, p) => p.OtherParams["comfyui_workflow"] = s, Toggleable: true, FeatureFlag: "comfyui", Group: comfyGroup,
            GetValues: (_) => Workflows.Keys.ToList()
            ));
        T2IParamTypes.Register(new("[ComfyUI] Sampler", "Sampler type (for ComfyUI)",
            T2IParamDataType.DROPDOWN, "euler", (s, p) => p.OtherParams["comfyui_sampler"] = s, Toggleable: true, FeatureFlag: "comfyui", Group: comfyGroup,
            GetValues: (_) => new() { "euler", "euler_ancestral", "heun", "dpm_2", "dpm_2_ancestral", "lms", "dpm_fast", "dpm_adaptive", "dpmpp_2s_ancestral", "dpmpp_sde", "dpmpp_2m", "dpmpp_2m_sde", "ddim", "uni_pc", "uni_pc_bh2" }
            ));
        T2IParamTypes.Register(new("[ComfyUI] Scheduler", "Scheduler type (for ComfyUI)",
            T2IParamDataType.DROPDOWN, "normal", (s, p) => p.OtherParams["comfyui_scheduler"] = s, Toggleable: true, FeatureFlag: "comfyui", Group: comfyGroup,
            GetValues: (_) => new() { "normal", "karras", "exponential", "simple", "ddim_uniform" }
            ));
        Program.Backends.RegisterBackendType<ComfyUIAPIBackend>("comfyui_api", "ComfyUI API By URL", "A backend powered by a pre-existing installation of ComfyUI, referenced via API base URL.");
        Program.Backends.RegisterBackendType<ComfyUISelfStartBackend>("comfyui_selfstart", "ComfyUI Self-Starting", "A backend powered by a pre-existing installation of the ComfyUI, automatically launched and managed by this UI server.");
    }

    public override void OnPreLaunch()
    {
        WebServer.WebApp.Map("/ComfyBackendDirect/{*Path}", ComfyBackendDirectHandler);
    }

    /// <summary>Web route for viewing output images. This just works as a simple proxy.</summary>
    public async Task ComfyBackendDirectHandler(HttpContext context)
    {
        ComfyUIAPIAbstractBackend backend = Program.Backends.T2IBackends.Values
            .Select(b => b.Backend as ComfyUIAPIAbstractBackend)
            .Where(b => b.Status == BackendStatus.RUNNING)
            .FirstOrDefault(b => b is not null);
        if (backend is null)
        {
            context.Response.ContentType = "text/plain";
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("No ComfyUI backend available, loading failed.");
            await context.Response.CompleteAsync();
            return;
        }
        string path = context.Request.Path.Value;
        path = path.After("/ComfyBackendDirect");
        if (path.StartsWith("/"))
        {
            path = path[1..];
        }
        if (!string.IsNullOrWhiteSpace(context.Request.QueryString.Value))
        {
            path = $"{path}{context.Request.QueryString.Value}";
        }
        if (context.WebSockets.IsWebSocketRequest)
        {
            WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
            ClientWebSocket outSocket = new();
            string scheme = backend.Address.BeforeAndAfter("://", out string addr);
            scheme = scheme == "http" ? "ws" : "wss";
            await outSocket.ConnectAsync(new Uri($"{scheme}://{addr}/{path}"), Program.GlobalProgramCancel);
            Task a = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        JObject input = await socket.ReceiveJson(TimeSpan.FromMinutes(2), 10 * 1024 * 1024); // TODO: Configurable limits
                        await outSocket.SendJson(input, TimeSpan.FromMinutes(1));
                        if (socket.CloseStatus.HasValue)
                        {
                            await outSocket.CloseAsync(socket.CloseStatus.Value, socket.CloseStatusDescription, Program.GlobalProgramCancel);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logs.Debug($"ComfyUI redirection failed: {ex}");
                }
            });
            Task b = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        JObject output = await outSocket.ReceiveJson(TimeSpan.FromMinutes(2), 10 * 1024 * 1024);
                        await socket.SendJson(output, TimeSpan.FromMinutes(1));
                        if (socket.CloseStatus.HasValue)
                        {
                            await socket.CloseAsync(socket.CloseStatus.Value, socket.CloseStatusDescription, Program.GlobalProgramCancel);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logs.Debug($"ComfyUI redirection failed: {ex}");
                }
            });
            await Task.WhenAll(a, b);
            return;
        }
        HttpResponseMessage response;
        if (context.Request.Method == "POST")
        {
            response = await backend.HttpClient.PostAsync($"{backend.Address}/{path}", new StreamContent(context.Request.Body));
        }
        else
        {
            response = await backend.HttpClient.SendAsync(new(new(context.Request.Method), $"{backend.Address}/{path}"));
        }
        int code = (int)response.StatusCode;
        if (code != 200)
        {
            Logs.Debug($"ComfyUI redirection gave non-200 code: '{code}' for URL: {context.Request.Method} '{path}'");
        }
        context.Response.StatusCode = code;
        context.Response.ContentType = response.Content.Headers.ContentType.ToString();
        await response.Content.CopyToAsync(context.Response.Body);
        await context.Response.CompleteAsync();
    }
}
