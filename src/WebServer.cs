namespace StableUI;

/// <summary>Core handler for the web-server (mid-layer & front-end).</summary>
public static class WebServer
{
    public static WebApplication WebApp;

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
