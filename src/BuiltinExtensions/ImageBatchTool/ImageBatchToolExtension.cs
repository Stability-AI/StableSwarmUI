using FreneticUtilities.FreneticExtensions;
using FreneticUtilities.FreneticToolkit;
using Newtonsoft.Json.Linq;
using StableSwarmUI.Accounts;
using StableSwarmUI.Core;
using StableSwarmUI.Text2Image;
using StableSwarmUI.Utils;
using StableSwarmUI.WebAPI;
using System;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace StableSwarmUI.Builtin_ImageBatchToolExtension;

/// <summary>Extension that adds a tool to generate batches of image-inputs.</summary>
public class ImageBatchToolExtension : Extension
{
    public override void OnPreInit()
    {
        ScriptFiles.Add("Assets/image_batcher.js");
    }

    public override void OnInit()
    {
        API.RegisterAPICall(ImageBatchRun);
    }

    /// <summary>API route to generate images with WebSocket updates.</summary>
    public static async Task<JObject> ImageBatchRun(WebSocket socket, Session session, JObject rawInput, string input_folder, string output_folder, bool init_image, bool revision, bool controlnet)
    {
        // TODO: Strict path validation / user permission confirmation.
        if (input_folder.Length < 5 || output_folder.Length < 5)
        {
            await socket.SendJson(new JObject() { ["error"] = "Input or output folder looks invalid, please fill it in carefully." }, API.WebsocketTimeout);
            return null;
        }
        input_folder = Path.GetFullPath(input_folder);
        output_folder = Path.GetFullPath(output_folder);
        if (!Directory.Exists(input_folder))
        {
            await socket.SendJson(new JObject() { ["error"] = "Input folder does not exist" }, API.WebsocketTimeout);
            return null;
        }
        if (input_folder == output_folder)
        {
            await socket.SendJson(new JObject() { ["error"] = "Input and output folder cannot be the same" }, API.WebsocketTimeout);
            return null;
        }
        string[] imageFiles = Directory.EnumerateFiles(input_folder).Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg")).ToArray();
        if (imageFiles.Length == 0)
        {
            await socket.SendJson(new JObject() { ["error"] = "Input folder does not contain any images" }, API.WebsocketTimeout);
            return null;
        }
        if (!init_image && !revision && !controlnet)
        {
            await socket.SendJson(new JObject() { ["error"] = "Image batch needs to supply the images to at least one parameter." }, API.WebsocketTimeout);
            return null;
        }
        Directory.CreateDirectory(output_folder);
        await API.RunWebsocketHandlerCallWS(GenBatchRun_Internal, session, (rawInput, input_folder, output_folder, init_image, revision, controlnet, imageFiles), socket);
        Logs.Info("Image Batcher completed successfully");
        await socket.SendJson(new JObject() { ["success"] = "complete" }, API.WebsocketTimeout);
        return null;
    }

    public static async Task GenBatchRun_Internal(Session session, (JObject, string, string, bool, bool, bool, string[]) input, Action<JObject> output, bool isWS)
    {
        (JObject rawInput, string input_folder, string output_folder, bool init_image, bool revision, bool controlnet, string[] imageFiles) = input;
        using Session.GenClaim claim = session.Claim(gens: imageFiles.Length);
        async Task sendStatus()
        {
            output(BasicAPIFeatures.GetCurrentStatusRaw(session));
        }
        await sendStatus();
        void setError(string message)
        {
            Logs.Debug($"Refused to run image-batch-gen for {session.User.UserID}: {message}");
            output(new JObject() { ["error"] = message });
            claim.LocalClaimInterrupt.Cancel();
        }
        T2IParamInput baseParams;
        try
        {
            baseParams = T2IAPI.RequestToParams(session, rawInput["baseParams"] as JObject);
        }
        catch (InvalidDataException ex)
        {
            output(new JObject() { ["error"] = ex.Message });
            return;
        }
        List<T2IEngine.ImageInBatch> imageSet = new();
        List<Task> tasks = new();
        void removeDoneTasks()
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                if (tasks[i].IsCompleted)
                {
                    if (tasks[i].IsFaulted)
                    {
                        Logs.Error($"Image generation failed: {tasks[i].Exception}");
                    }
                    tasks.RemoveAt(i--);
                }
            }
        }
        int max_degrees = session.User.Restrictions.CalcMaxT2ISimultaneous;
        int batchId = 0;
        foreach (string file in imageFiles)
        {
            string fname = file.Replace('\\', '/').AfterLast('/');
            int imageIndex = batchId++;
            removeDoneTasks();
            while (tasks.Count > max_degrees)
            {
                await Task.WhenAny(tasks);
                removeDoneTasks();
            }
            if (claim.ShouldCancel)
            {
                break;
            }
            Image image = new(File.ReadAllBytes(file), Image.ImageType.IMAGE, file.AfterLast('.'));
            T2IParamInput param = baseParams.Clone();
            if (init_image)
            {
                param.Set(T2IParamTypes.InitImage, image);
            }
            if (revision)
            {
                List<Image> imgs = param.Get(T2IParamTypes.PromptImages, new()).Append(image).ToList();
                param.Set(T2IParamTypes.PromptImages, imgs);
            }
            if (controlnet)
            {
                param.Set(T2IParamTypes.ControlNetImage, image);
            }
            tasks.Add(T2IEngine.CreateImageTask(param, $"{imageIndex}", claim, output, setError, isWS, Program.ServerSettings.Backends.PerRequestTimeoutMinutes,
                (image, metadata) =>
                {
                    (string preExt, string ext) = fname.BeforeAndAfterLast('.');
                    string properExt = image.Extension;
                    if (ext == "png" && properExt != "png")
                    {
                        ext = "png";
                    }
                    else if (ext == "jpg" && properExt != "jpg" && properExt != "jpeg")
                    {
                        ext = "jpg";
                    }
                    File.WriteAllBytes($"{output_folder}/{preExt}.{ext}", image.ImageData);
                    output(new JObject() { ["image"] = session.GetImageB64(image), ["batch_index"] = $"{imageIndex}", ["metadata"] = string.IsNullOrWhiteSpace(metadata) ? null : metadata });
                }));
        }
        while (tasks.Any())
        {
            await Task.WhenAny(tasks);
            removeDoneTasks();
        }
        claim.Dispose();
        await sendStatus();
    }
}
