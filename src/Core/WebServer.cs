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
        if (!WebApp.Environment.IsDevelopment())
        {
            WebApp.UseExceptionHandler("/Error");
        }
        WebApp.UseStaticFiles();
        WebApp.UseRouting();
        WebApp.UseAuthorization();
        WebApp.MapRazorPages();
        // Launch actual web host process
        WebApp.Run();
    }
}
