using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using System.Net.WebSockets;
using System.Reflection;

namespace StableSwarmUI.WebAPI;


/// <summary>Represents an API Call route and associated core data (permissions, etc).</summary>
/// <param name="Name">The name, ie the call path, in full.</param>
/// <param name="Original">The original method that this call was generated from.</param>
/// <param name="Call">Actual call function: an async function that takes the HttpContext and the JSON input, and returns JSON output.</param>
/// <param name="IsWebSocket">Whether this call is for websockets. If false, normal HTTP API.</param>
/// <param name="IsUserUpdate">If true, this is considered a 'user update' behavior of some form. Use false for basic getters or automated actions.</param>
public record class APICall(string Name, MethodInfo Original, Func<HttpContext, Session, WebSocket, JObject, Task<JObject>> Call, bool IsWebSocket, bool IsUserUpdate)
{
    // TODO: Permissions, etc.
}
