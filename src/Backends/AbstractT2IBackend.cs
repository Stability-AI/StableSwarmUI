using StableUI.Utils;

namespace StableUI.Backends;

/// <summary>Represents a basic abstracted Text2Image backend provider.</summary>
public abstract class AbstractT2IBackend
{
    /// <summary>Load this backend and get it ready for usage. Do not return until ready. Throw an exception if not possible.</summary>
    public abstract void Init();

    /// <summary>Shut down this backend and clear any memory/resources/etc. Do not return until fully cleared.</summary>
    public abstract void Shutdown();

    /// <summary>Generate an image.</summary>
    public abstract Task<Image[]> Generate(string prompt, string negativePrompt, long seed, int steps, int width, int height, double cfgScale);
}
