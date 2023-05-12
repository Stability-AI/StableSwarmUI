using FreneticUtilities.FreneticExtensions;
using StableUI.Utils;
using StableUI.WebAPI;
using System.ComponentModel.Design;
using System.Web;

namespace StableUI.Core;

/// <summary>Core handler for the web-server (mid-layer & front-end).</summary>
public static class WebServer
{
    /// <summary>Primary core ASP.NET <see cref="WebApplication"/> reference.</summary>
    public static WebApplication WebApp;

    /// <summary>The internal web host url this webserver is using.</summary>
    public static string HostURL;

    /// <summary>Minimum ASP.NET Log Level.</summary>
    public static LogLevel LogLevel;

    /// <summary>Called by <see cref="Program"/>, generally should not be touched externally.</summary>
    public static void Launch()
    {
        // ASP.NET web init
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions() { WebRootPath = "src/wwwroot" });
        builder.Services.AddRazorPages();
        if (!builder.Environment.IsDevelopment())
        {
            builder.Logging.SetMinimumLevel(LogLevel);
        }
        WebApp = builder.Build();
        if (WebApp.Environment.IsDevelopment())
        {
            Utilities.VaryID += ".DEV" + Random.Shared.Next(99999);
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
        WebApp.Map("/API/{*Call}", API.HandleAsyncRequest);
        WebApp.Map("/Output/{*Path}", ViewOutput);
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
        // Launch actual web host process
        Logs.Init($"Starting webserver on {HostURL}");
        WebApp.Run();
    }

    /// <summary>Test the validity of a user-given file path. Returns (path, consoleError, userError).</summary>
    public static (string, string, string) CheckOutputFilePath(string path, string userId)
    {
        path = Utilities.FilePathForbidden.TrimToNonMatches(path);
        while (path.Contains(".."))
        {
            path = path.Replace("..", "");
        }
        path = $"{Environment.CurrentDirectory}/{Program.ServerSettings.OutputPath}/{userId}/{path}";
        string expectedPath = $"{Environment.CurrentDirectory}{Path.DirectorySeparatorChar}{Program.ServerSettings.OutputPath}{Path.DirectorySeparatorChar}{userId}";
        if (!Directory.GetParent(path).FullName.StartsWith(expectedPath))
        {
            return (null, $"Refusing dangerous access, got path '{path}' which resolves to '{Directory.GetParent(path)}' which does not obey expected root '{expectedPath}'",
                "Unacceptable path. If you are the server owner, check program console log.");
        }
        return (path, null, null);
    }

    public static async Task ViewOutput(HttpContext context)
    {
        string path = context.Request.Path.ToString().After("/Output/");
        path = HttpUtility.UrlDecode(path);
        string userId = "local"; // TODO: From login cookie
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
