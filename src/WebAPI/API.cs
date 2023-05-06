using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableUI.Core;
using StableUI.Utils;
using System.Reflection;
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
        RegisterAPICall(new APICall(method.Method.Name, APICallReflectBuilder.BuildFor(method.Target, method.Method)));
    }

    /// <summary>Web access call route, triggered from <see cref="WebServer"/>.</summary>
    public static async Task HandleAsyncRequest(HttpContext context)
    {
        void Error(string message)
        {
            Logs.Error($"[WebAPI] Error handling API request '{context.Request.Path}': {message}");
        }
        try
        {
            if (context.Request.Method != "POST")
            {
                Error($"Invalid request method: {context.Request.Method}");
                context.Response.Redirect("/Error/NoGetAPI");
                return;
            }
            if (!context.Request.HasJsonContentType())
            {
                Error($"Request has wrong content-type: {context.Request.ContentType}");
                context.Response.Redirect("/Error/BasicAPI");
                return;
            }
            if (context.Request.ContentLength <= 0 || context.Request.ContentLength >= 10 * 1024 * 1024) // TODO: put max length as setting
            {
                Error($"Request has invalid content length: {context.Request.ContentLength}");
                context.Response.Redirect("/Error/BasicAPI");
                return;
            }
            byte[] rawData = new byte[(int)context.Request.ContentLength];
            await context.Request.Body.ReadExactlyAsync(rawData, 0, rawData.Length);
            JObject input = JObject.Parse(Encoding.UTF8.GetString(rawData));
            if (input is null)
            {
                Error($"Request input parsed to null");
                context.Response.Redirect("/Error/BasicAPI");
                return;
            }
            string path = context.Request.Path.ToString().After("/API/");
            if (!APIHandlers.TryGetValue(path, out APICall handler))
            {
                Error("Unknown API route");
                context.Response.Redirect("/Error/404");
                return;
            }
            // TODO: Authorization check
            JObject output = await handler.Call(context, input);
            if (output is null)
            {
                Error("API handler returned null");
                context.Response.Redirect("/Error/BasicAPI");
                return;
            }
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync(output.ToString(Formatting.None));
            await context.Response.CompleteAsync();
        }
        catch (Exception ex)
        {
            Error($"Internal exception: {ex}");
            context.Response.Redirect("/Error/Internal");
        }
    }
}
