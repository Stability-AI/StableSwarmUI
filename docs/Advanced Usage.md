# Advanced Usage

(TODO: More examples of advanced usage with explanation)

# Accessing StableSwarmUI From Other Devices

- To access StableSwarmUI from another device over LAN:
    - Simply launch from the command line with `--host 0.0.0.0` and connect to the LAN IP of the host from your other device.
        - For example, on Windows, use `./launch-windows.bat --host 0.0.0.0`
        - Note you may also need to allow StableSwarmUI through your firewall.
- To access StableSwarmUI over open internet without port forwarding:
    - You can either launch use Cloudflared or Ngrok
        - For **Cloudflared:** Install Cloudflared according to [their readme](https://github.com/cloudflare/cloudflared#cloudflare-tunnel-client), and launch StableSwarmUI with `--cloudflared-path [...]`
            - For Debian Linux servers, look at how the [Colab Notebook](/colab/colab-notebook.ipynb) installs and uses cloudflared.
        - For **ngrok:**  Install ngrok according to [their documentation](https://ngrok.com/) and login to your ngrok account, and launch StableSwarmUI with `--ngrok-path [...]`
