using Newtonsoft.Json.Linq;
using StableUI.Accounts;
using StableUI.Backends;
using StableUI.Core;
using StableUI.DataHolders;
using StableUI.Utils;
using StableUI.WebAPI;
using System.Diagnostics;
using System.IO;

namespace StableUI.Text2Image
{
    /// <summary>Central core handler for text-to-image processing.</summary>
    public static class T2IEngine
    {
        /// <summary>Extension event, fired before images will be generated, just after the request is received.
        /// No backend is claimed yet.
        /// Use <see cref="InvalidOperationException"/> for a user-readable refusal message.</summary>
        public static Action<PreGenerationEventParams> PreGenerateEvent;

        public record class PreGenerationEventParams(T2IParams UserInput);

        /// <summary>Extension event, fired after images were generated, but before saving the result.
        /// Backend is already released, but the gen request is not marked completed.
        /// Ran before metadata is applied.
        /// Use <see cref="InvalidDataException"/> for a user-readable hard-refusal message.</summary>
        public static Action<PostGenerationEventParams> PostGenerateEvent;

        public record class PostGenerationEventParams(Image Image, Dictionary<string, object> ExtraMetadata, T2IParams UserInput, Action RefuseImage);

        /// <summary>Internal handler route to create an image based on a user request.</summary>
        public static async Task CreateImageTask(T2IParams user_input, Session.GenClaim claim, Action<JObject> output, Action<string> setError, bool isWS, float backendTimeoutMin, Action<Image[]> saveImages)
        {
            Stopwatch timer = Stopwatch.StartNew();
            void sendStatus()
            {
                if (isWS && user_input.SourceSession is not null)
                {
                    output(BasicAPIFeatures.GetCurrentStatusRaw(user_input.SourceSession));
                }
            }
            if (claim.ShouldCancel)
            {
                return;
            }
            T2IBackendAccess backend;
            try
            {
                PreGenerateEvent?.Invoke(new(user_input));
                claim.Extend(backendWaits: 1);
                sendStatus();
                backend = await Program.Backends.GetNextT2IBackend(TimeSpan.FromMinutes(backendTimeoutMin), user_input.Model, filter: user_input.BackendMatcher, session: user_input.SourceSession, notifyWillLoad: sendStatus, cancel: claim.InterruptToken);
            }
            catch (InvalidOperationException ex)
            {
                setError($"Invalid operation: {ex.Message}");
                return;
            }
            catch (TimeoutException)
            {
                setError("Timeout! All backends are occupied with other tasks.");
                return;
            }
            finally
            {
                claim.Complete(backendWaits: 1);
                sendStatus();
            }
            if (claim.ShouldCancel)
            {
                backend.Dispose();
                return;
            }
            try
            {
                claim.Extend(liveGens: 1);
                sendStatus();
                Image[] outputs;
                long prepTime, genTime;
                using (backend)
                {
                    if (claim.ShouldCancel)
                    {
                        return;
                    }
                    prepTime = timer.ElapsedMilliseconds;
                    outputs = await backend.Backend.Generate(user_input);
                    genTime = timer.ElapsedMilliseconds;
                }
                string genTimeReport = $"{prepTime / 1000.0:0.00} (prep) and {(genTime - prepTime) / 1000.0:0.00} (gen) seconds";
                for (int i = 0; i < outputs.Length; i++)
                {
                    Dictionary<string, object> extras = new() { ["generation_time"] = genTimeReport };
                    bool refuse = false;
                    PostGenerateEvent?.Invoke(new(outputs[i], extras, user_input, () => refuse = true));
                    outputs[i] = refuse ? null : user_input.SourceSession.ApplyMetadata(outputs[i], user_input, extras);
                }
                int nullCount = outputs.Count(i => i is null);
                outputs = outputs.Where(i => i is not null).ToArray();
                string label = outputs.Length == 1 ? "an image" : $"{outputs.Length} images" + (nullCount > 0 ? $" (and removed {nullCount})" : "");
                Logs.Info($"Generated {label} in {genTimeReport}");
                saveImages(outputs);
            }
            catch (InvalidDataException ex)
            {
                setError($"Invalid data: {ex.Message}");
                return;
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Logs.Error($"Internal error processing T2I request: {ex}");
                setError("Something went wrong while generating images.");
                return;
            }
            finally
            {
                claim.Complete(gens: 1, liveGens: 1);
                sendStatus();
            }
        }
    }
}
