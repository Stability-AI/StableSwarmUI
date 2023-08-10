using FreneticUtilities.FreneticDataSyntax;
using StableSwarmUI.DataHolders;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;

namespace StableSwarmUI.Backends;

/// <summary>Represents a basic abstracted Text2Image backend provider.</summary>
public abstract class AbstractT2IBackend
{
    /// <summary>Load this backend and get it ready for usage. Do not return until ready. Throw an exception if not possible.</summary>
    public abstract Task Init();

    /// <summary>Shut down this backend and clear any memory/resources/etc. Do not return until fully cleared.</summary>
    public abstract Task Shutdown();

    /// <summary>Generate an image.</summary>
    public abstract Task<Image[]> Generate(T2IParamInput user_input);

    /// <summary>Runs a generating with live feedback (progress updates, previews, etc.)</summary>
    /// <param name="user_input">The user input data to generate.</param>
    /// <param name="batchId">Local batch-ID for this generation.</param>
    /// <param name="takeOutput">Takes an output object: Image for final images, JObject for anything else.</param>
    public virtual async Task GenerateLive(T2IParamInput user_input, string batchId, Action<object> takeOutput)
    {
        foreach (Image img in await Generate(user_input))
        {
            takeOutput(img);
        }
    }

    /// <summary>Whether this backend has been configured validly.</summary>
    public volatile BackendStatus Status = BackendStatus.WAITING;

    /// <summary>Whether this backend is alive and ready.</summary>
    public bool IsAlive()
    {
        return Status == BackendStatus.RUNNING;
    }

    /// <summary>Currently loaded model, or null if none.</summary>
    public volatile string CurrentModelName;

    /// <summary>Backend type data for the internal handler.</summary>
    public BackendHandler.BackendType HandlerTypeData;

    /// <summary>The backing <see cref="BackendHandler"/> instance.</summary>
    public BackendHandler Handler;

    /// <summary>Tell the backend to load a specific model. Return true if loaded, false if failed.</summary>
    public abstract Task<bool> LoadModel(T2IModel model);

    /// <summary>A set of feature-IDs this backend supports.</summary>
    public abstract IEnumerable<string> SupportedFeatures { get; }

    /// <summary>The backend's settings.</summary>
    public AutoConfiguration SettingsRaw;

    /// <summary>Handler-internal data for this backend.</summary>
    public BackendHandler.T2IBackendData BackendData;

    /// <summary>Real backends are user-managed and save to file. Non-real backends are invisible to the user and file.</summary>
    public bool IsReal = true;
}

public enum BackendStatus
{
    DISABLED,
    ERRORED,
    WAITING,
    LOADING,
    RUNNING
}
