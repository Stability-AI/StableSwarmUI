using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.DataHolders;
using System;
using System.Net.WebSockets;
using System.Reflection;
using static StableSwarmUI.DataHolders.DataHolderHelper;

namespace StableSwarmUI.WebAPI;

/// <summary>Uses reflection to automatically build an API call route handler.</summary>
public class APICallReflectBuilder
{
    public static Dictionary<Type, Func<JToken, (bool, object)>> TypeCoercerMap = new()
    {
        [typeof(string)] = (JToken input) => (true, input.ToString()),
        [typeof(int)] = (JToken input) => (int.TryParse(input.ToString(), out int output), output),
        [typeof(long)] = (JToken input) => (long.TryParse(input.ToString(), out long output), output),
        [typeof(float)] = (JToken input) => (float.TryParse(input.ToString(), out float output), output),
        [typeof(double)] = (JToken input) => (double.TryParse(input.ToString(), out double output), output),
        [typeof(bool)] = (JToken input) => (bool.TryParse(input.ToString(), out bool output), output),
        [typeof(byte)] = (JToken input) => (byte.TryParse(input.ToString(), out byte output), output),
        [typeof(char)] = (JToken input) => (char.TryParse(input.ToString(), out char output), output),
        [typeof(string[])] = (JToken input) => (true, input.ToList().Select(j => j.ToString()).ToArray())
    };

    public static APICall BuildFor(object obj, MethodInfo method, bool isUserUpdate)
    {
        if (method.ReturnType != typeof(Task<JObject>))
        {
            throw new Exception($"Invalid API return type '{method.ReturnType.Name}' for method '{method.DeclaringType.Name}.{method.Name}'");
        }
        APICaller caller = new(obj, method, []);
        bool isWebSocket = false;
        foreach (ParameterInfo param in method.GetParameters())
        {
            if (param.ParameterType == typeof(HttpContext))
            {
                caller.InputMappers.Add((context, _, _, _) => (null, context));
            }
            else if (param.ParameterType == typeof(Session))
            {
                caller.InputMappers.Add((_, session, _, _) => (null, session));
            }
            else if (param.ParameterType == typeof(JObject))
            {
                caller.InputMappers.Add((_, _, _, input) => (null, input));
            }
            else if (param.ParameterType == typeof(WebSocket))
            {
                caller.InputMappers.Add((_, _, socket, _) => (null, socket));
                isWebSocket = true;
            }
            else if (TypeCoercerMap.TryGetValue(param.ParameterType, out Func<JToken, (bool, object)> coercer))
            {
                caller.InputMappers.Add((_, _, _, input) =>
                {
                    if (!input.TryGetValue(param.Name, out JToken value))
                    {
                        if (param.HasDefaultValue)
                        {
                            return (null, param.DefaultValue);
                        }
                        return ($"Missing required parameter '{param.Name}'", null);
                    }
                    (bool success, object output) = coercer(value);
                    if (!success)
                    {
                        return ($"Invalid value '{value}' for parameter '{param.Name}', must be type '{param.ParameterType.Name}'", null);
                    }
                    return (null, output);
                });
            }
            else if (typeof(IDataHolder).IsAssignableFrom(param.ParameterType))
            {
                List<Func<JObject, IDataHolder, string>> subAppliers = [];
                foreach (FieldData field in IDataHolder.GetHelper(param.ParameterType).Fields)
                {
                    if (!TypeCoercerMap.TryGetValue(field.Type, out Func<JToken, (bool, object)> fieldCoercer))
                    {
                        throw new Exception($"Invalid API parameter type '{field.Type.Name}' for field '{field.Name}' in object '{param.ParameterType.Name}' of param '{param.Name}' of method '{method.DeclaringType.Name}.{method.Name}'");
                    }
                    subAppliers.Add((input, outObj) =>
                    {
                        if (!input.TryGetValue(field.Name, out JToken value))
                        {
                            if (field.Required)
                            {
                                return $"Missing required parameter '{field.Name}'";
                            }
                            return null;
                        }
                        (bool success, object output) = fieldCoercer(value);
                        if (!success)
                        {
                            return $"Invalid value '{value}' for parameter '{field.Name}', must be type '{field.Type.Name}'";
                        }
                        field.Field.SetValue(outObj, output);
                        return null;
                    });
                }
                string keyCheck = param.Name;
                caller.InputMappers.Add((_, _, _, input) =>
                {
                    if (input.TryGetValue(keyCheck, out JToken keyVal) && keyVal is JObject subInp)
                    {
                        input = subInp;
                    }
                    IDataHolder holder = Activator.CreateInstance(param.ParameterType) as IDataHolder;
                    foreach (Func<JObject, IDataHolder, string> getter in subAppliers)
                    {
                        string err = getter(input, holder);
                        if (err != null)
                        {
                            return (err, null);
                        }
                    }
                    return (null, holder);
                });
            }
            else
            {
                throw new Exception($"Invalid API parameter type '{param.ParameterType.Name}' for param '{param.Name}' of method '{method.DeclaringType.Name}.{method.Name}'");
            }
        }
        return new APICall(method.Name, method, caller.Call, isWebSocket, isUserUpdate);
    }

    public record class APICaller(object Obj, MethodInfo Method, List<Func<HttpContext, Session, WebSocket, JObject, (string, object)>> InputMappers)
    {
        public Task<JObject> Call(HttpContext context, Session session, WebSocket socket, JObject input)
        {
            object[] arr = new object[InputMappers.Count];
            for (int i = 0; i < InputMappers.Count; i++)
            {
                (string error, object value) = InputMappers[i](context, session, socket, input);
                if (error is not null)
                {
                    return Task.FromResult(new JObject() { ["error"] = error });
                }
                arr[i] = value;
            }
            return Method.Invoke(Obj, arr) as Task<JObject>;
        }
    }
}
