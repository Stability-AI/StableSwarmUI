using Newtonsoft.Json.Linq;
using StableUI.Core;
using StableUI.Backends;
using StableUI.Utils;
using StableUI.Accounts;
using System.Runtime.ConstrainedExecution;

namespace StableUI.WebAPI;

/// <summary>Internal helper for all the basic API routes.</summary>
public static class BasicAPIFeatures
{
    /// <summary>Called by <see cref="Program"/> to register the core API calls.</summary>
    public static void Register()
    {
        API.RegisterAPICall(GetNewSession);
        T2IAPI.Register();
        BackendAPI.Register();
    }

    /// <summary>API Route to create a new session automatically.</summary>
    public static async Task<JObject> GetNewSession(HttpContext context)
    {
        return new JObject() { ["session_id"] = Program.Sessions.CreateAdminSession(context.Connection.RemoteIpAddress?.ToString() ?? "unknown").ID };
    }
}
