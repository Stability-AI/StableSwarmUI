using FreneticUtilities.FreneticDataSyntax;
using StableUI.DataHolders;
using StableUI.Text2Image;
using StableUI.Utils;

namespace StableUI.Backends;

/// <summary>Represents a basic abstracted Text2Image backend provider. This is the internal low-level part, prefer <see cref="AbstractT2IBackend{T}"/> for normal usage.</summary>
public abstract class AbstractT2IBackend
{
    /// <summary>Load this backend and get it ready for usage. Do not return until ready. Throw an exception if not possible.</summary>
    public abstract Task Init();

    /// <summary>Shut down this backend and clear any memory/resources/etc. Do not return until fully cleared.</summary>
    public abstract Task Shutdown();

    /// <summary>Generate an image.</summary>
    public abstract Task<Image[]> Generate(T2IParams user_input);

    /// <summary>Whether this backend has been configured validly.</summary>
    public volatile BackendStatus Status = BackendStatus.DISABLED;

    /// <summary>Whether this backend is alive and ready.</summary>
    public bool IsAlive()
    {
        return Status == BackendStatus.RUNNING;
    }

    /// <summary>Currently loaded model, or null if none.</summary>
    public string CurrentModelName;

    /// <summary>Internal usage, settings accessor.</summary>
    public abstract AutoConfiguration InternalSettingsAccess { get; set; }

    /// <summary>Backend type data for the internal handler.</summary>
    public BackendHandler.BackendType HandlerTypeData;

    /// <summary>The backing <see cref="BackendHandler"/> instance.</summary>
    public BackendHandler Handler;

    /// <summary>Tell the backend to load a specific model. Return true if loaded, false if failed.</summary>
    public abstract Task<bool> LoadModel(T2IModel model);
}

public enum BackendStatus
{
    DISABLED,
    ERRORED,
    LOADING,
    RUNNING
}

/// <summary>Represents a basic abstracted Text2Image backend provider.</summary>
public abstract class AbstractT2IBackend<T>: AbstractT2IBackend where T: AutoConfiguration
{
    /// <summary>The backend's settings.</summary>
    public T Settings;

    /// <summary>Internal usage, settings accessor.</summary>
    public override AutoConfiguration InternalSettingsAccess { get => Settings; set => Settings = (T)value; }
}
