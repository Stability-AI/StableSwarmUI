# Advanced Usage

(TODO: More examples of advanced usage with explanation)

# Accessing StableSwarmUI From Other Devices

- To access StableSwarmUI from another device over LAN:
    - Simply open StableSwarmUI to the `Server` -> `Server Configuration` tab, find `Host` (default value is `localhost`) and change the value to `0.0.0.0`, then save and restart
        - Note you may also need to allow StableSwarmUI through your firewall.
- To access StableSwarmUI over open internet without port forwarding:
    - You can either launch use Cloudflared or Ngrok
        - For **Cloudflared:** Install Cloudflared according to [their readme](https://github.com/cloudflare/cloudflared?tab=readme-ov-file#installing-cloudflared) (note: ignore the stuff about accounts/domains/whatever, only the `cloudflared` software install is relevant), and launch StableSwarmUI with `--cloudflared-path [...]` or set the path in Server Configuration `CloudflaredPath` option and restart
            - For Debian Linux servers, look at how the [Colab Notebook](/colab/colab-notebook.ipynb) installs and uses cloudflared.
        - For **ngrok:**  Install ngrok according to [their documentation](https://ngrok.com/) and login to your ngrok account, and launch StableSwarmUI with `--ngrok-path [...]`

## Custom Workflows (ComfyUI)

So, all those parameters aren't enough, you want MORE control? Don't worry, we got you covered, with the power of raw ComfyUI node graphs!

- Note that this requires you use a ComfyUI backend.
- At the top, click the `Comfy Workflow Editor` tab
- Use [the full power of ComfyUI](https://comfyanonymous.github.io/ComfyUI_examples/) at will to build a workflow that suites your crazy needs.
- You can generate images within comfy while you're going.
- If you have weird parameters, I highly recommend creating `Primitive` nodes and setting their title to something clear, and routing them to the inputs, so you can recognize them easily later.
- Once you're done, make sure you have a single `Save Image` node at the end, then click the `Use This Workflow` button

![img](/docs/images/usecomfy.png)

- Your parameter listing is now updated to parameters that are in your workflow. Recognized ones use their default parameter view, other ones get listed on their own with Node IDs or titles.
- You can now generate images as normal, but it will automatically use your workflow. This applies to all generation features, including the Grid Generator tool - which has its axes list automatically updated to the workflow parameter list!
- If you update the workflow in the comfy tab, you have to click `Use This Workflow` again to load your changes.
- If you want to go back to normal and remove the comfy workflow, make sure your parameters list is scrolled up, as there's a `Disable Custom Comfy Workflow` button you can click there.
