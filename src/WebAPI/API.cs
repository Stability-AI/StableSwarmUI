using FreneticUtilities.FreneticExtensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StableUI.Core;
using StableUI.Utils;
using System.Text;

namespace StableUI.WebAPI;

/// <summary>Entry point for processing calls to the web API.</summary>
public class API
{
    /// <summary>Internal mapping of API handlers, key is API path name, value is an .</summary>
    public static Dictionary<string, APICall> APIHandlers = new();

    /// <summary>Represents an API Call route and associated core data (permissions, etc).</summary>
    /// <param name="Name">The name, ie the call path, in full.</param>
    /// <param name="Call">Actual call function: an async function that takes the HttpContext and the JSON input, and returns JSON output.</param>
    public record class APICall(string Name, Func<HttpContext, JObject, Task<JObject>> Call); // TODO: Permissions, etc.

    /// <summary>Register a new API call handler.</summary>
    public static void RegisterAPICall(APICall call)
    {
        APIHandlers.Add(call.Name, call);
    }

    /// <summary>Web access call route, triggered from <see cref="WebServer"/>.</summary>
    public static async Task HandleAsyncRequest(HttpContext context)
    {
        try
        {
            if (context.Request.Method != "POST")
            {
                context.Response.Redirect("/Error/NoGetAPI");
                return;
            }
            if (!context.Request.HasJsonContentType())
            {
                context.Response.Redirect("/Error/BasicAPI");
                return;
            }
            JObject input = Utilities.StreamToJSON(context.Request.Body);
            if (input is null)
            {
                context.Response.Redirect("/Error/BasicAPI");
                return;
            }
            string path = context.Request.Path.ToString().After("/API/");
            if (!APIHandlers.TryGetValue(path, out APICall handler))
            {
                context.Response.Redirect("/Error/404");
                return;
            }
            // TODO: Authorization check
            JObject output = await handler.Call(context, input);
            if (output is null)
            {
                context.Response.Redirect("/Error/BasicAPI");
                return;
            }
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync(output.ToString(Formatting.None));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Internal error handling API request '{context.Request.Path}': {ex}");
            context.Response.Redirect("/Error/Internal");
        }
    }
}
