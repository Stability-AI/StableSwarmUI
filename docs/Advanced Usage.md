# Advanced Usage

(TODO: More examples of advanced usage with explanation)

# Accessing StableSwarmUI From Other Devices

- To access StableSwarmUI from another device over LAN:
    - Simply open StableSwarmUI to the `Server` -> `Server Configuration` tab, find `Host` (default value is `localhost`) and change the value to `0.0.0.0`, then save and restart
        - Note you may also need to allow StableSwarmUI through your firewall.
- To access StableSwarmUI over open internet without port forwarding:
    - You can either launch use Cloudflared or Ngrok
        - For **Cloudflared:** Install Cloudflared according to [their readme](https://github.com/cloudflare/cloudflared#cloudflare-tunnel-client), and launch StableSwarmUI with `--cloudflared-path [...]`
            - For Debian Linux servers, look at how the [Colab Notebook](/colab/colab-notebook.ipynb) installs and uses cloudflared.
        - For **ngrok:**  Install ngrok according to [their documentation](https://ngrok.com/) and login to your ngrok account, and launch StableSwarmUI with `--ngrok-path [...]`
