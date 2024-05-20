# StableSwarmUI API Documentation

StableSwarmUI has a full-capability network API that you can use from external programs, both to use Swarm features (eg generate images) and to manage the Swarm instance (eg modify backends).

Swarm uses its own API - you can simply open the web interface, open your browser tools, and interact with Swarm to watch what API calls are made, and then replicate those from your own code.

The majority of Swarm's API takes the form of `POST` requests sent to `(your server)/API/(route)`, containing JSON formatted inputs and receiving JSON formatted outputs.

Some API routes, designated with a `WS` suffix, take WebSocket connections. Usually these take one up front input, and give several outputs slowly over time (for example `GenerateText2ImageWS` gives progress updates as it goes and preview images).

All API routes, with the exception of `GetNewSession`, require a `session_id` input in the JSON. Naturally, call `GetNewSession` to get a session ID to use.

Any API route can potentially return an `error` or `error_id`.
- If the `error_id` is `invalid_session_id`, you must recall `/API/GetNewSession` and try again.
- Other `error_id`s are used contextually for specific calls.
- Otherwise, generally `error` is display text fit to display to end users.

### Quick Call Guide To Generate An Image

The follow example generates an image using bash curl commands. This assumes running locally, on the default port.

```bash
# First, get a usable session ID:
curl -H "Content-Type: application/json" -d "{}" -X POST http://localhost:7801/API/GetNewSession
# EXAMPLE REPLY:
#        {"session_id":"9D3534E30DA38499DE782BC38211976A58555AA6","user_id":"local","output_append_user":true,"version":"0.6.3.0.GIT-5ee406ba","server_id":"058716b5-c6f5-49ed-9ca3-be20d82e4c5f","count_running":1}
# Copy the "session_id" value above to reuse

# Now, generate the image
curl -H "Content-Type: application/json" -d '{"session_id":"9D3534E30DA38499DE782BC38211976A58555AA6","images":1,"prompt":"a cat","model":"OfficialStableDiffusion/sd_xl_base_1.0","width":1024,"height":1024}' -X POST http://localhost:7801/API/GenerateText2Image
# EXAMPLE REPLY:
#        {"images":["View/local/raw/2024-05-19/a cat-OfficialStableDiffusionsd_xl_base_10s-1872258705.png"]}
# Copy the image URL

# Now download the image to look at it
wget "http://localhost:7801/View/local/raw/2024-05-19/a cat-OfficialStableDiffusionsd_xl_base_10s-1872258705.png"

```

### Routes

Route documentation is categorized into a few sections:

- [AdminAPI](/docs/APIRoutes/AdminAPI.md) - Administrative server management APIs.
- [BackendAPI](/docs/APIRoutes/BackendAPI.md) - Backend management APIs.
- [UtilAPI](/docs/APIRoutes/UtilAPI.md) - General utility APIs.
- [ModelsAPI](/docs/APIRoutes/ModelsAPI.md) - API routes related to handling models (including loras, wildcards, etc).
- [T2IAPI](/docs/APIRoutes/T2IAPI.md) - Text2Image APIs.
- [BasicAPIFeatures](/docs/APIRoutes/BasicAPIFeatures.md) - Basic general API routes, primarily for users and session handling.
- Built-in-Extension APIs:
    - [GridGeneratorExtension](/docs/APIRoutes/GridGeneratorExtension.md)
    - [ComfyUIWebAPI](/docs/APIRoutes/ComfyUIWebAPI.md)
    - [ImageBatchToolExtension](/docs/APIRoutes/ImageBatchToolExtension.md)

### GET Routes

The following `GET` routes are also available:
- `/Text2Image` - HTML main page of the interface.
- `/Install` - HTML page for the installer interface.
- `/Error/404`, `/Error/BasicAPI`, `/Error/Internal`, `/Error/NoGetAPI` - Return HTML page displays explaining certain common errors.
- `/css/*.css` - Stylesheets and themes.
- `/js/*.js` - JavaScripts for the interface.
- `/fonts/*.woff2`, `/imgs/*.jpg`, `/imgs/*.png`, `/favicon.ico` - Various asset files for the interface.
- `/View/*.*` - Returns saved outputs. By default usually in a format like `/View/(user)/raw/(year-month-day)/(file).(ext)`, but is user-customizable.
- `/Output/*.*` - legacy output call format. Do not use.
- `/ExtensionFile/(extension)/*.*` - gets a web asset file from an extension.
- `/ComfyBackendDirect/*.*` - direct pass-through to a comfy instance, if the [ComfyUI Backend Extension](/src/BuiltinExtensions/ComfyUIBackend/README.md) is in use.

### Example Client (C#)

```cs
using System.Text;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FreneticUtilities.FreneticToolkit;
using FreneticUtilities.FreneticExtensions;

namespace MySwarmClient;

public static class SwarmAPI
{
    public static HttpClient Client = new();

    public static string Session = "";

    public static string Address => "http://127.0.0.1:7801";

    static SwarmAPI()
    {
        Client.DefaultRequestHeaders.Add("user-agent", "MySwarmClient/1.0");
    }

    public class SessionInvalidException : Exception
    {
    }

    public static async Task GetSession()
    {
        JObject sessData = await Client.PostJson($"{Address}/API/GetNewSession", new());
        Session = sessData["session_id"].ToString();
    }

    public static async Task<string> GenerateAnImage(string prompt)
    {
        return await RunWithSession(async () =>
        {
            JObject request = new()
            {
                ["images"] = 1,
                ["session_id"] = Session,
                ["donotsave"] = true,
                ["prompt"] = prompt,
                ["negativeprompt"] = "",
                ["model"] = "OfficialStableDiffusion/sd_xl_base_1.0",
                ["width"] = 1024,
                ["height"] = 1024,
                ["cfgscale"] = 7.5,
                ["steps"] = 20,
                ["seed"] = -1
            };
            JObject generated = await Client.PostJson($"{Address}/API/GenerateText2Image", request);
            if (generated.TryGetValue("error_id", out JToken errorId) && errorId.ToString() == "invalid_session_id")
            {
                throw new SessionInvalidException();
            }
            return $"{generated["images"].First()}";
        });
    }

    public static async Task<T> RunWithSession<T>(Func<Task<T>> call)
    {
        if (string.IsNullOrWhiteSpace(Session))
        {
            await GetSession();
        }
        try
        {
            return await call();
        }
        catch (SessionInvalidException)
        {
            await GetSession();
            return await call();
        }
    }

    /// <summary>Sends a JSON object post and receives a JSON object back.</summary>
    public static async Task<JObject> PostJson(this HttpClient client, string url, JObject data)
    {
        ByteArrayContent content = new(data.ToString(Formatting.None).EncodeUTF8());
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return JObject.Parse(await (await client.PostAsync(url, content)).Content.ReadAsStringAsync());
    }
}
```
