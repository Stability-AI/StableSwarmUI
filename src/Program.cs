using FreneticUtilities.FreneticToolkit;

namespace StableUI;

public class Program
{
    /// <summary>Primary execution entry point.</summary>
    public static void Main(string[] args)
    {
        // Fix for MS's broken localization
        SpecialTools.Internationalize();
        WebServer.Launch();
    }
}
