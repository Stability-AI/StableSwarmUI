# Using More GPUs in StableSwarmUI

There are two primary ways to use more GPUs:
- More GPUs in your machine
- More machines with GPUs

## More GPUs In Your Machine

To use more GPUs in your machine, simply add more self-start backends (interface -> `Server` -> `Backends`), and increment the `GPU_ID` setting for each added backend.

## More Machines With GPUs

- If you have more machines in your home:
    - Pick one machine as your "main" machine, and install SwarmUI on that. You can (but don't have to) use a local backend on that machine if it has a GPU.
    - Boot up backends on the other machines.
        - For ComfyUI, run an instance of ComfyUI with `--listen`.
    - Make sure to allow the program through the firewall (on Windows it should just prompt and ask)
    - On your main machine, try to open the remote backend via LAN address.
        - On Windows, on the secondary machine, open a command prompt and type `ipconfig` to find your LAN address, it should look something like `192.168.0.10`
        - Make sure you can open it in a web browser on the main machine before continuing (to separate network diagnostic issues from SwarmUI-specific issues)
    - In the SwarmUI interface (`Server` -> `Backends`), add an "API By URL" backend, such as `ComfyUI API By URL`
        - Set the address to the same LAN address you used in your web browser
    - Generate!
- If you are using Google Colab or rented servers:
    - Same as in-home, but use the public address of the server if possible, or the share address if not (eg a trycloudflare or ngrok URL)
- If you have family or friends willing to share GPU power:
    - Same as rented servers, your friends will need to create some form of public share URL.
        - Note that it is technically possible to port-forward servers, but this should be avoided unless forwarding a server that was heavily security-audited, which most options are not at this time.
        - Private networks (VPNs) or other network sharing techniques (like hamachi) will also work.

## Selecting A Main Backend

Backends get used in the order they're listed. That means, the first backend in the list gets the first generation you queue. The second backend only gets used if you queue at least 2 generations at the same time. The third only if you queue 3, etc.

Because of this, you'll want to select your best/fastest backend as the first one, and put the slowest ones at the end.
