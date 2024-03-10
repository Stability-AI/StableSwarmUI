using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Backends;
using StableSwarmUI.Core;
using System.Net.Http;

namespace StableSwarmUI.Utils;

/// <summary>Central class for processing webhooks.</summary>
public static class WebhookManager
{
    /// <summary>All server settings related to webhooks.</summary>
    public static Settings.WebHooksData HookSettings => Program.ServerSettings.WebHooks;

    /// <summary>If true, the server is believed to currently be generating images. If false, it is idle.</summary>
    public static volatile bool IsServerGenerating = false;

    /// <summary>Web client for the hook manager to use.</summary>
    public static HttpClient Client = NetworkBackendUtils.MakeHttpClient();

    /// <summary>Lock to prevent overlapping updates to <see cref="IsServerGenerating"/> state.</summary>
    public static SemaphoreSlim Lock = new(1, 1);

    /// <summary>The timestamp of when the server initially stopped generating anything.</summary>
    public static long TimeStoppedGenerating = 0;

    /// <summary>Marks the server as currently trying to generate and completes when the state is updated and the webhook is done processing, if relevant.</summary>
    public static async Task WaitUntilCanStartGenerating()
    {
        if (IsServerGenerating)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(HookSettings.QueueStartWebhook))
        {
            Logs.Verbose("[Webhooks] Marking server as starting generations silently.");
            TimeStoppedGenerating = 0;
            IsServerGenerating = true;
            return;
        }
        await Lock.WaitAsync();
        try
        {
            if (IsServerGenerating)
            {
                return;
            }
            TimeStoppedGenerating = 0;
            Logs.Verbose("[Webhooks] Marking server as starting generations, sending Queue Start webhook.");
            HttpResponseMessage msg = await Client.PostAsync(HookSettings.QueueStartWebhook, Utilities.JSONContent([]));
            string response = await msg.Content.ReadAsStringAsync();
            Logs.Verbose($"[Webhooks] Queue Start webhook response: {msg.StatusCode}: {response}");
            IsServerGenerating = true;
            return;
        }
        catch (Exception ex)
        {
            Logs.Error($"Failed to send queue start webhook: {ex}");
        }
        finally
        {
            Lock.Release();
        }
    }

    /// <summary>Marks the server as currently done generating (ie, idle) and completes when the state is updated and the webhook is done processing, if relevant.</summary>
    public static async Task TryMarkDoneGenerating()
    {
        if (!IsServerGenerating)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(HookSettings.QueueEndWebhook))
        {
            Logs.Verbose("[Webhooks] Marking server as done generating silently.");
            TimeStoppedGenerating = 0;
            IsServerGenerating = false;
            return;
        }
        await Lock.WaitAsync();
        try
        {
            if (!IsServerGenerating)
            {
                return;
            }
            TimeStoppedGenerating = 0;
            IsServerGenerating = false;
            Logs.Verbose("[Webhooks] Marking server as done generating, sending Queue End webhook.");
            HttpResponseMessage msg = await Client.PostAsync(HookSettings.QueueEndWebhook, Utilities.JSONContent([]));
            string response = await msg.Content.ReadAsStringAsync();
            Logs.Verbose($"[Webhooks] Queue End webhook response: {msg.StatusCode}: {response}");
            return;
        }
        catch (Exception ex)
        {
            Logs.Error($"Failed to send queue end webhook: {ex}");
        }
        finally
        {
            Lock.Release();
        }
    }

    /// <summary>Does an idle tick for the server having no current generations running.</summary>
    public static async Task TickNoGenerations()
    {
        if (!IsServerGenerating)
        {
            return;
        }
        if (TimeStoppedGenerating == 0)
        {
            TimeStoppedGenerating = Environment.TickCount64;
        }
        if (Environment.TickCount64 - TimeStoppedGenerating >= HookSettings.QueueEndDelay * 1000)
        {
            TimeStoppedGenerating = 0;
            await TryMarkDoneGenerating();
        }
    }
}
