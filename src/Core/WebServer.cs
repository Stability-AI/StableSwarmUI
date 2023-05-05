using FreneticUtilities.FreneticExtensions;
using StableUI.WebAPI;

namespace StableUI.Core;

/// <summary>Core handler for the web-server (mid-layer & front-end).</summary>
public static class WebServer
{
    /// <summary>Primary core ASP.NET <see cref="WebApplication"/> reference.</summary>
    public static WebApplication WebApp;

    /// <summary>The internal web host url this webserver is using.</summary>
    public static string HostURL;

    /// <summary>Called by <see cref="Program"/>, generally should not be touched externally.</summary>
    public static void Launch()
    {
        // ASP.NET web init
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRazorPages();
        WebApp = builder.Build();
        if (WebApp.Environment.IsDevelopment())
        {
            WebApp.UseDeveloperExceptionPage();
        }
        else
        {
            WebApp.UseExceptionHandler("/Error/Internal");
        }
        WebApp.UseStaticFiles();
        WebApp.UseRouting();
        WebApp.UseAuthorization();
        WebApp.MapRazorPages();
        WebApp.Map("/API/{*Call}", API.HandleAsyncRequest);
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
        WebApp.Run();
    }
}
