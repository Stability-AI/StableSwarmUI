using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Core;
using StableSwarmUI.Utils;
using System.IO;
using System.Net.WebSockets;
using System.Reflection;

namespace StableSwarmUI.WebAPI;

/// <summary>Entry point for processing calls to the web API.</summary>
public class API
{
    /// <summary>Internal mapping of API handlers, key is API path name, value is an .</summary>
    public static Dictionary<string, APICall> APIHandlers = [];

    /// <summary>Register a new API call handler.</summary>
    public static void RegisterAPICall(APICall call)
    {
        APIHandlers.Add(call.Name.ToLowerFast(), call);
    }

    /// <summary>Register a new API call handler.</summary>
    public static void RegisterAPICall(Delegate method, bool isUserUpdate = false)
    {
        RegisterAPICall(APICallReflectBuilder.BuildFor(method.Target, method.Method, isUserUpdate));
    }

    /// <summary>Web access call route, triggered from <see cref="WebServer"/>.</summary>
    public static async Task HandleAsyncRequest(HttpContext context)
    {
        // TODO: Validate that 'async' is truly async here. If needed, spin up our own threads.
        Session session = null;
        void Error(string message)
        {
            string forUser = session is null ? "" : $" for user '{session.User.UserID}'";
            Logs.Error($"[WebAPI] Error handling API request '{context.Request.Path}'{forUser}: {message}");
        }
        WebSocket socket = null;
        try
        {
            JObject input;
            if (context.WebSockets.IsWebSocketRequest)
            {
                socket = await context.WebSockets.AcceptWebSocketAsync();
                input = await socket.ReceiveJson(TimeSpan.FromMinutes(1), 100 * 1024 * 1024); // TODO: Configurable limits
            }
            else if (context.Request.Method == "POST")
            {
                if (!context.Request.HasJsonContentType())
                {
                    Error($"Request has wrong content-type: {context.Request.ContentType}");
                    context.Response.Redirect("/Error/BasicAPI");
                    return;
                }
                if (context.Request.ContentLength <= 0 || context.Request.ContentLength >= 100 * 1024 * 1024) // TODO: Configurable limits
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
            string path = context.Request.Path.ToString().ToLowerFast().After("/api/");
            if (path != "getnewsession")
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
                    await context.YieldJsonOutput(socket, 401, Utilities.ErrorObj("Invalid session ID. You may need to refresh the page.", "invalid_session_id"));
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
            if (session is not null)
            {
                session.User.TickIsPresent();
                if (handler.IsUserUpdate)
                {
                    session.UpdateLastUsedTime();
                }
            }
            JObject output = await handler.Call(context, session, socket, input);
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
            await context.YieldJsonOutput(socket, 200, output);
        }
        catch (Exception ex)
        {
            if (ex is WebSocketException wserr && wserr.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                Error($"Remote WebSocket disconnected unexpectedly (ConnectionClosedPrematurely). Did your browser crash while generating?");
                return;
            }
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

    /// <summary>Placeholder default WebSocket timeout.</summary>
    public static TimeSpan WebsocketTimeout = TimeSpan.FromMinutes(2); // TODO: Configurable timeout

    /// <summary>Helper to run simple websocket-multiresult action API calls.</summary>
    public static async Task RunWebsocketHandlerCallWS<T>(Func<Session, T, Action<JObject>, bool, Task> handler, Session session, T val, WebSocket socket)
    {
        ConcurrentQueue<JObject> outputs = new();
        AsyncAutoResetEvent signal = new(false);
        void takeOutput(JObject obj)
        {
            if (obj is not null)
            {
                outputs.Enqueue(obj);
            }
            signal.Set();
        }
        Task t = handler(session, val, takeOutput, true);
        Task _ = t.ContinueWith((t) => signal.Set());
        while (!t.IsCompleted || outputs.Any())
        {
            while (outputs.TryDequeue(out JObject output))
            {
                await socket.SendJson(output, WebsocketTimeout);
            }
            await signal.WaitAsync(TimeSpan.FromSeconds(2));
        }
        if (t.IsFaulted)
        {
            Logs.Error($"Error in websocket handler: {t.Exception}");
        }
    }

    /// <summary>Helper to run simple websocket-multiresult action API calls without a websocket.</summary>
    public static async Task<List<JObject>> RunWebsocketHandlerCallDirect<T>(Func<Session, T, Action<JObject>, bool, Task> handler, Session session, T val)
    {
        ConcurrentQueue<JObject> outputs = new();
        void takeOutput(JObject obj)
        {
            if (obj is not null)
            {
                outputs.Enqueue(obj);
            }
        }
        await handler(session, val, takeOutput, false);
        return [.. outputs];
    }

    /// <summary>Helper to generate API documentation.</summary>
    public static async Task GenerateAPIDocs()
    {
        Logs.Info("Generating API docs...");
        string path = $"{Program.ServerSettings.Paths.DataPath}/DebugNewAPIDocs";
        Directory.CreateDirectory(path);
        Dictionary<string, (StringBuilder, StringBuilder)> docs = [];
        foreach (APICall call in APIHandlers.Values.OrderBy(v => v.Name))
        {
            Type type = call.Original.DeclaringType;
            (StringBuilder docText, StringBuilder toc) = docs.GetOrCreate(type.Name, () => (new(), new()));
            if (docText.Length == 0)
            {
                docText.Append($"# StableSwarmUI API Documentation - {type.Name}\n\n> This is a subset of the API docs, see [/docs/API.md](/docs/API.md) for general info.\n\n");
                docText.Append(type.GetCustomAttribute<APIClassAttribute>()?.Description ?? "(CLASS DESCRIPTION NOT SET)");
                docText.Append("\n\n#### Table of Contents:\n\n!!!TABLE_OF_CONTENTS!!!\n");
            }
            if (call.IsWebSocket)
            {
                docText.Append($"## WebSocket Route /API/{call.Name}\n\n");
                toc.Append($"- WebSocket Route [{call.Name}](#websocket-route-api{call.Name.ToLowerFast()})\n");
            }
            else
            {
                docText.Append($"## HTTP Route /API/{call.Name}\n\n");
                toc.Append($"- HTTP Route [{call.Name}](#http-route-api{call.Name.ToLowerFast()})\n");
            }
            APIDescriptionAttribute methodDesc = call.Original.GetCustomAttribute<APIDescriptionAttribute>();
            docText.Append($"#### Description\n\n{methodDesc?.Description ?? "(ROUTE DESCRIPTION NOT SET)"}\n\n#### Parameters\n\n");
            ParameterInfo[] paramInf = [.. call.Original.GetParameters().Where(m => m.ParameterType != typeof(Session) && m.ParameterType != typeof(WebSocket))];
            if (paramInf.Length == 0)
            {
                docText.Append("**None.**\n\n");
            }
            else
            {
                docText.Append("| Name | Type | Description | Default |\n| --- | --- | --- | --- |\n");
                foreach (ParameterInfo param in paramInf)
                {
                    string description = param.GetCustomAttribute<APIParameterAttribute>()?.Description ?? "(PARAMETER DESCRIPTION NOT SET)";
                    string defaultVal;
                    if (!param.HasDefaultValue) { defaultVal = "**(REQUIRED)**"; }
                    else if (param.DefaultValue is string valStr && valStr == "") { defaultVal = "(Empty String)"; }
                    else if (param.DefaultValue is null) { defaultVal = "(null)"; }
                    else { defaultVal = $"`{param.DefaultValue}`"; }
                    docText.Append($"| {param.Name} | {param.ParameterType.Name} | {description} | {defaultVal} |\n");
                }
                docText.Append('\n');
            }
            docText.Append($"#### Return Format\n\n```js\n{methodDesc?.ReturnInfo ?? "(RETURN INFO NOT SET)"}\n```\n\n");
        }
        Logs.Info("Writing API docs...");
        foreach ((string clazz, (StringBuilder content, StringBuilder toc)) in docs)
        {
            await File.WriteAllTextAsync($"{path}/{clazz}.md", content.ToString().Replace("!!!TABLE_OF_CONTENTS!!!", toc.ToString()));
        }
        Logs.Info($"API docs generated and stored to '{path}'.");
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class APIClassAttribute(string description) : Attribute
    {
        public string Description = description;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class APIDescriptionAttribute(string description, string returnInfo) : Attribute
    {
        public string Description = description;

        public string ReturnInfo = returnInfo;
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public class APIParameterAttribute(string description) : Attribute
    {
        public string Description = description;
    }
}
