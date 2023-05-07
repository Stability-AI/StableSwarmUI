using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableUI.Accounts;
using StableUI.Core;
using StableUI.Utils;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace StableUI.WebAPI;

/// <summary>Entry point for processing calls to the web API.</summary>
public class API
{
    /// <summary>Internal mapping of API handlers, key is API path name, value is an .</summary>
    public static Dictionary<string, APICall> APIHandlers = new();

    /// <summary>Register a new API call handler.</summary>
    public static void RegisterAPICall(APICall call)
    {
        APIHandlers.Add(call.Name, call);
    }

    /// <summary>Register a new API call handler.</summary>
    public static void RegisterAPICall(Delegate method)
    {
        RegisterAPICall(APICallReflectBuilder.BuildFor(method.Target, method.Method));
    }

    /// <summary>Web access call route, triggered from <see cref="WebServer"/>.</summary>
    public static async Task HandleAsyncRequest(HttpContext context)
    {
        void Error(string message)
        {
            Logs.Error($"[WebAPI] Error handling API request '{context.Request.Path}': {message}");
        }
        WebSocket socket = null;
        async Task YieldJson(int status, JObject obj)
        {
            if (socket != null)
            {
                await socket.SendJson(obj, TimeSpan.FromMinutes(1));
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, Utilities.TimedCancel(TimeSpan.FromMinutes(1)));
                return;
            }
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = status;
            await context.Response.WriteAsync(obj.ToString(Formatting.None));
            await context.Response.CompleteAsync();
        }
        JObject ErrorObj(string message, string error_id)
        {
            return new JObject() { ["error"] = message, ["error_id"] = error_id };
        }
        try
        {
            JObject input;
            if (context.WebSockets.IsWebSocketRequest)
            {
                socket = await context.WebSockets.AcceptWebSocketAsync();
                input = await socket.ReceiveJson(TimeSpan.FromMinutes(1), 10 * 1024 * 1024); // TODO: Configurable limits
            }
            else if (context.Request.Method == "POST")
            {
                if (!context.Request.HasJsonContentType())
                {
                    Error($"Request has wrong content-type: {context.Request.ContentType}");
                    context.Response.Redirect("/Error/BasicAPI");
                    return;
                }
                if (context.Request.ContentLength <= 0 || context.Request.ContentLength >= 10 * 1024 * 1024) // TODO: Configurable limits
                {
                    Error($"Request has invalid content length: {context.Request.ContentLength}");
                    context.Response.Redirect("/Error/BasicAPI");
                    return;
                }
                byte[] rawData = new byte[(int)context.Request.ContentLength];
                await context.Request.Body.ReadExactlyAsync(rawData, 0, rawData.Length);
                input = JObject.Parse(Encoding.UTF8.GetString(rawData));
            }
            else
            {
                Error($"Invalid request method: {context.Request.Method}");
                context.Response.Redirect("/Error/NoGetAPI");
                return;
            }
            if (input is null)
            {
                Error("Request input parsed to null");
                context.Response.Redirect("/Error/BasicAPI");
                return;
            }
            string path = context.Request.Path.ToString().After("/API/");
            Session session = null;
            if (path != "GetNewSession")
            {
                if (!input.TryGetValue("session_id", out JToken session_id))
                {
                    Error("Request input lacks required session id");
                    context.Response.Redirect("/Error/BasicAPI");
                    return;
                }
                if (!Program.Sessions.Sessions.TryGetValue(session_id.ToString(), out session))
                {
                    Error("Request input has unknown session id");
                    await YieldJson(401, ErrorObj("Invalid session ID. You may need to refresh the page.", "invalid_session_id"));
                    return;
                }
            }
            if (!APIHandlers.TryGetValue(path, out APICall handler))
            {
                Error("Unknown API route");
                context.Response.Redirect("/Error/404");
                return;
            }
            // TODO: Authorization check
            if (handler.IsWebSocket && socket is null)
            {
                Error("API route is a websocket but request is not");
                context.Response.Redirect("/Error/BasicAPI");
                return;
            }
            if (!handler.IsWebSocket && socket is not null)
            {
                Error("API route is not a websocket but request is");
                context.Response.Redirect("/Error/BasicAPI");
                return;
            }
            JObject output = await handler.Call(context, socket, input);
            if (socket is not null)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, Utilities.TimedCancel(TimeSpan.FromMinutes(1)));
                return;
            }
            if (output is null)
            {
                Error("API handler returned null");
                context.Response.Redirect("/Error/BasicAPI");
                return;
            }
            await YieldJson(200, output);
        }
        catch (Exception ex)
        {
            Error($"Internal exception: {ex}");
            if (socket is null)
            {
                context.Response.Redirect("/Error/Internal");
                return;
            }
            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.InternalServerError, null, Utilities.TimedCancel(TimeSpan.FromMinutes(1)));
                return;
            }
            catch (Exception ex2)
            {
                Error($"Internal exception while handling prior exception closure: {ex2}");
            }
        }
    }
}
