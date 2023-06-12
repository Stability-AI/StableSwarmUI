using FreneticUtilities.FreneticExtensions;
using Microsoft.AspNetCore.Html;
using StableUI.Utils;
using StableUI.WebAPI;
using System.Text;
using System.Web;

namespace StableUI.Core;

/// <summary>Core handler for the web-server (mid-layer & front-end).</summary>
public class WebServer
{
    /// <summary>Primary core ASP.NET <see cref="WebApplication"/> reference.</summary>
    public static WebApplication WebApp;

    /// <summary>The internal web host url this webserver is using.</summary>
    public static string HostURL;

    /// <summary>Minimum ASP.NET Log Level.</summary>
    public static LogLevel LogLevel;

    /// <summary>Extra file content added by extensions.</summary>
    public Dictionary<string, string> ExtensionSharedFiles = new();

    /// <summary>Extra content for the page header. Automatically set based on extensions.</summary>
    public static HtmlString PageHeaderExtra = new("");

    /// <summary>Extra content for the page footer. Automatically set based on extensions.</summary>
    public static HtmlString PageFooterExtra = new("");

    /// <summary>Initial prep, called by <see cref="Program"/>, generally should not be touched externally.</summary>
    public void Prep()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions() { WebRootPath = "src/wwwroot" });
        builder.Services.AddRazorPages();
        builder.Logging.SetMinimumLevel(LogLevel);
        WebApp = builder.Build();
        if (WebApp.Environment.IsDevelopment())
        {
            Utilities.VaryID += ".DEV" + ((DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 10L) % 1000000L);
            WebApp.UseDeveloperExceptionPage();
        }
        else
        {
            WebApp.UseExceptionHandler("/Error/Internal");
        }
        if (Program.Ngrok is not null)
        {
            WebApp.Lifetime.ApplicationStarted.Register(Program.Ngrok.Start);
        }
        WebApp.Lifetime.ApplicationStopping.Register(Program.Shutdown);
        WebApp.UseStaticFiles(new StaticFileOptions());
        WebApp.UseRouting();
        WebApp.UseWebSockets();
        WebApp.MapRazorPages();
        WebApp.MapGet("/", () => Results.Redirect("/Text2Image"));
        WebApp.Map("/API/{*Call}", API.HandleAsyncRequest);
        WebApp.MapGet("/Output/{*Path}", ViewOutput);
        WebApp.MapGet("/ExtensionFile/{*f}", ViewExtensionScript);
        WebApp.Use(async (context, next) =>
        {
            await next();
            if (context.Response.StatusCode == 404)
            {
                if (!context.Request.Path.Value.ToLowerFast().StartsWith("/error/"))
                {
                    context.Response.Redirect("/Error/404");
                    await next();
                }
            }
        });
        GatherExtensionPageAdditions();
    }

    public void GatherExtensionPageAdditions()
    {
        StringBuilder scripts = new(), stylesheets = new();
        Program.RunOnAllExtensions(e =>
        {
            foreach (string script in e.ScriptFiles)
            {
                string fname = $"/ExtensionFile/{e.ExtensionName}/{script}";
                ExtensionSharedFiles.Add(fname, File.ReadAllText($"{e.FilePath}{script}"));
                scripts.Append($"<script src=\"{fname}?vary={Utilities.VaryID}\"></script>\n");
            }
            foreach (string css in e.StyleSheetFiles)
            {
                string fname = $"/ExtensionFile/{e.ExtensionName}/{css}";
                ExtensionSharedFiles.Add(fname, File.ReadAllText($"{e.FilePath}{css}"));
                stylesheets.Append($"<link rel=\"stylesheet\" href=\"{fname}?vary={Utilities.VaryID}\" />");
            }
        });
        PageHeaderExtra = new(stylesheets.ToString());
        PageFooterExtra = new(scripts.ToString());
    }

    /// <summary>Called by <see cref="Program"/>, generally should not be touched externally.</summary>
    public void Launch()
    {
        Logs.Init($"Starting webserver on {HostURL}");
        WebApp.Run();
    }

    /// <summary>Test the validity of a user-given file path. Returns (path, consoleError, userError).</summary>
    public (string, string, string) CheckOutputFilePath(string path, string userId)
    {
        string root = $"{Environment.CurrentDirectory}/{Program.ServerSettings.OutputPath}/{userId}";
        return CheckFilePath(root, path);
    }

    /// <summary>Test the validity of a user-given file path. Returns (path, consoleError, userError).</summary>
    public static (string, string, string) CheckFilePath(string root, string path)
    {
        path = Utilities.FilePathForbidden.TrimToNonMatches(path);
        while (path.Contains(".."))
        {
            path = path.Replace("..", "");
        }
        root = root.Replace('\\', '/');
        path = $"{root}/{path}";
        if (!Directory.GetParent(path).FullName.Replace('\\', '/').StartsWith(root))
        {
            return (null, $"Refusing dangerous access, got path '{path}' which resolves to '{Directory.GetParent(path)}' which does not obey expected root '{root}'",
                "Unacceptable path. If you are the server owner, check program console log.");
        }
        return (path, null, null);
    }

    /// <summary>Web route for scripts from extensions.</summary>
    public async Task ViewExtensionScript(HttpContext context)
    {
        if (ExtensionSharedFiles.TryGetValue(context.Request.Path.Value, out string script))
        {
            context.Response.ContentType = Utilities.GuessContentType(context.Request.Path.Value);
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync(script);
        }
        else
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("404, file not found.");
        }
        await context.Response.CompleteAsync();
    }

    /// <summary>Web route for viewing output images.</summary>
    public async Task ViewOutput(HttpContext context)
    {
        string path = context.Request.Path.ToString().After("/Output/");
        path = HttpUtility.UrlDecode(path).Replace('\\', '/');
        string userId = Program.Sessions.AdminUser.UserID; // TODO: From login cookie
        (path, string consoleError, string userError) = CheckOutputFilePath(path, userId);
        if (consoleError is not null)
        {
            Logs.Error(consoleError);
            await context.YieldJsonOutput(null, 400, Utilities.ErrorObj(userError, "bad_path"));
            return;
        }
        byte[] data;
        try
        {
            data = await File.ReadAllBytesAsync(path);
        }
        catch (Exception ex)
        {
            if (ex is FileNotFoundException || ex is DirectoryNotFoundException || ex is PathTooLongException)
            {
                await context.YieldJsonOutput(null, 04, Utilities.ErrorObj("404, file not found.", "file_not_found"));
            }
            else
            {
                Logs.Error($"Failed to read output file '{path}': {ex}");
                await context.YieldJsonOutput(null, 500, Utilities.ErrorObj("Error reading file. If you are the server owner, check program console log.", "file_error"));
            }
            return;
        }
        context.Response.ContentType = Utilities.GuessContentType(path);
        context.Response.StatusCode = 200;
        await context.Response.Body.WriteAsync(data);
        await context.Response.CompleteAsync();
    }
}
