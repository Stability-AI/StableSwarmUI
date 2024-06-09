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

    /// <summary>Shut down this backend and clear any memory/resources/etc. Do not return until fully cleared. Call <see cref="DoShutdownNow"/> to trigger this correctly.</summary>
    public abstract Task Shutdown();

    /// <summary>Event fired when this backend is about to shutdown.</summary>
    public Action OnShutdown;

    /// <summary>Shuts down this backend and clears any memory/resources/etc. Does not return until fully cleared.</summary>
    public async Task DoShutdownNow()
    {
        OnShutdown?.Invoke();
        OnShutdown = null;
        CurrentModelName = null;
        await Shutdown();
    }

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
    public BackendHandler.BackendType HandlerTypeData => BackendData.BackType;

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

    /// <summary>If true, the backend should be live. If false, the server admin wants the backend turned off.</summary>
    public volatile bool IsEnabled = true;

    /// <summary>If non-empty, is a user-facing title-override for the given backend.</summary>
    public string Title = "";

    /// <summary>If true, this backend is intending to shutdown, and should be excluded from generation.</summary>
    public volatile bool ShutDownReserve = false;

    /// <summary>The maximum number of simultaneous requests this backend should take.</summary>
    public int MaxUsages = 1;

    /// <summary>Whether this backend has the capability to load a model.</summary>
    public bool CanLoadModels = true;

    /// <summary>If above 0, something wants preferential ownership of this backend, and so general generations should not be sent to it.</summary>
    public volatile int Reservations = 0;

    /// <summary>The list of all model names this server has (key=model subtype, value=list of filenames), or null if untracked.</summary>
    public ConcurrentDictionary<string, List<string>> Models = null;

    /// <summary>Tells the backend to free its memory usage. Returns true if it happened, false if memory is still in use.
    /// Note that some backends may take extra time between when this call returns and when memory is actually freed, such as if they have jobs to wrap up or slow polling rates.
    /// Generally give at least one full second before assuming memory is properly cleared.</summary>
    /// <param name="systemRam">If true, system RAM should be cleaned. If false, only VRAM needs to be freed.</param>
    public virtual async Task<bool> FreeMemory(bool systemRam)
    {
        return false;
    }

    /// <summary>Exception can be thrown to indicate the backend cannot fulfill the request, but for temporary reasons, and another backend should be used instead.</summary>
    public class PleaseRedirectException : Exception
    {
    }
}

public enum BackendStatus
{
    DISABLED,
    ERRORED,
    WAITING,
    LOADING,
    IDLE,
    RUNNING
}
