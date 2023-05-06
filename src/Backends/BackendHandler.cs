using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;

namespace StableUI.Backends;

/// <summary>Central manager for available backends.</summary>
public class BackendHandler
{
    public List<T2iBackendData> T2IBackends = new();

    public AutoResetEvent BackendsAvailableSignal = new(false);

    public LockObject CentralLock = new();

    public class T2iBackendData
    {
        public AbstractT2IBackend Backend;

        public volatile bool IsInUse = false;

        public LockObject AccessLock = new();

        public BackendHandler Handler;
    }

    public void AddBackend(AbstractT2IBackend backend)
    {
        T2IBackends.Add(new T2iBackendData() { Backend = backend, Handler = this });
    }

    public BackendHandler()
    {
        AddBackend(new AutoWebUIAPIBackend("http://localhost:7860"));
    }

    /// <summary>(Blocking) gets the next available Text2Image backend.</summary>
    /// <returns>A 'using'-compatible wrapper for a backend.</returns>
    /// <param name="maxWait">Maximum duration to wait for. If time runs out, throws <see cref="TimeoutException"/>.</param>
    /// <exception cref="TimeoutException">Thrown if <paramref name="maxWait"/> is reached.</exception>
    /// <exception cref="InvalidOperationException">Thrown if no backends are available.</exception>
    public T2IBackendAccess GetNextT2IBackend(TimeSpan maxWait)
    {
        long startTime = Environment.TickCount64;
        while (true)
        {
            if (new TimeSpan(Environment.TickCount64 - startTime) > maxWait)
            {
                throw new TimeoutException();
            }
            if (T2IBackends.IsEmpty())
            {
                throw new InvalidOperationException("No backends available!");
            }
            lock (CentralLock)
            {
                foreach (T2iBackendData backend in T2IBackends)
                {
                    if (!backend.IsInUse)
                    {
                        backend.IsInUse = true;
                        return new T2IBackendAccess(backend);
                    }
                }
            }
            BackendsAvailableSignal.WaitOne(maxWait);
        }
    }
}

public record class T2IBackendAccess(BackendHandler.T2iBackendData Data) : IDisposable
{
    public AbstractT2IBackend Backend => Data.Backend;

    private bool IsDisposed = false;

    public void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            Data.IsInUse = false;
            Data.Handler.BackendsAvailableSignal.Set();
            GC.SuppressFinalize(this);
        }
    }
}
