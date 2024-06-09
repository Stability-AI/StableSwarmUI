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
        - The easiest and best option is to run StableSwarmUI on the other machine, set its host setting to `0.0.0.0`, and then add it as a `Swarm-API-Backend`.
        - (Not recommended:) or, you can run an instance ComfyUI or Auto WebUI with `--listen`
    - Make sure to allow the program through the firewall (on Windows it should just prompt and ask)
    - On your main machine, try to open the remote backend via LAN address.
        - On Windows, on the secondary machine, open a command prompt and type `ipconfig` to find your LAN address, it should look something like `192.168.0.10`
        - Make sure you can open it in a web browser on the main machine before continuing (to separate network diagnostic issues from SwarmUI-specific issues)
    - In the SwarmUI interface (`Server` -> `Backends`), add an "API By URL" backend, such as `Swarm-API-Backend` or `ComfyUI API By URL`
        - Set the address to the same LAN address you used in your web browser
        - Note that using the `Swarm-API-Backend` is highly recommended, if you use `ComfyUI API By URL` please make sure you know what you're doing and properly load in the [Swarm custom node set](https://github.com/Stability-AI/StableSwarmUI/tree/master/src/BuiltinExtensions/ComfyUIBackend/ExtraNodes) and all.
    - Generate!
- If you are using Google Colab, Runpod, or other rented servers:
    - Same as in-home, but use the public address of the server if possible, or the share address if not (eg a trycloudflare or ngrok URL)
- If you have family or friends willing to share GPU power:
    - Same as rented servers, your friends will need to create some form of public share URL.
        - Note that it is technically possible to port-forward servers, but this should be avoided unless forwarding a server that was heavily security-audited, which most options are not at this time.
        - Private networks (VPNs) or other network sharing techniques (like hamachi) will also work.

## Selecting A Main Backend

Backends get used in the order they're listed. That means, the first backend in the list gets the first generation you queue. The second backend only gets used if you queue at least 2 generations at the same time. The third only if you queue 3, etc.

Because of this, you'll want to select your best/fastest backend as the first one, and put the slowest ones at the end.

## Step-By-Step Setup Guide For Multiple Machines

Confused about how to set up multiple machines in a swarm network configuration? Here's a more detailed step-by-step guide you can follow along.

(TODO: Video format of the same for people that prefer videos)

#### Step 1: Planning

Before we begin, let's plan how we're going to set things up.

- Which machine is your "Main machine"? This is the one you will be using directly. Choose a machine that is most likely to be running when you want to use it, and that you can most easily access the files/etc on.
    - For example, if you have a personal desktop and a few other machines in the living room, probably select your personal desktop as the main machine.
- All other machines that are not your "Main machine" will be referred to from here on as simply "Other Machines".
- Consider your network setup.
    - If you have experience with network configuration, you may want to go into your router settings to set up a static internal IP for all relevant machines.
    - If you don't have network configuration experience... you may want to google how to do that and do it. This isn't strictly required, and you can skip it for now, it will just save you from troubleshooting later if your LAN IPs change (see [Troubleshooting section below](#troubleshooting)).
- If you're using remote machines (ie not in the same building, such as remote servers), consider how you'll connect to them.
    - The specifics here are very dependent on what service you're using for the remote servers.
    - For example, if using temporary hosting (eg Colab) you can just use cloudflare-generated temporary address instead of the LAN addresses that will be described below.
    - If using a purchased remote server, you'll want to get a direct IP to connect to it, and set it up with a firewall that only allows your "Main Machine" to connect to each "Other Machine".

#### Step 2: Install On The Main Machine

- Naturally, you're going to have to install StableSwarmUI on your main machine. The information on how to do this is in [the README, here](https://github.com/Stability-AI/StableSwarmUI#installing-on-windows).
    - Just follow the instructions, you can install this however is appropriate for your usage, for the 'main machine' there is nothing special/different yet. We will make changes later after it's installed.
- I recommend at this stage you also configure any models/etc. you desire, and test Swarm on the machine - generate locally at will.
    - If you have multiple GPUs in the machine, set them up per [the instructions above](#more-gpus-in-your-machine).

![img](/.github/images/stableswarmui.jpg)

#### Step 3: Install on Other Machines

- Repeat the following steps on every "Other Machine", that is EVERY MACHINE you're using EXCEPT the "Main Machine":
    - Install StableSwarmUI on the Other Machine, again per [the README, here](https://github.com/Stability-AI/StableSwarmUI#installing-on-windows), with one specific change:
        - During the Installer UI that appears on first boot, you will get an option asking `Who is this StableSwarmUI installation going to be used by?` - make sure to Select the `Just Yourself, with LAN access` option. This is essential to allow the Main Machine to connect remotely in to your Other Machine.
        - ![img](/docs/images/lan-access.png)
    - After it is installed, also configure any models/etc as desired.
        - Importantly, make sure any models you have on the Main Machine, you also copy to this Other Machine. These models must have the exact same filename and folder path.
            - For example, if you have `OfficialStableDiffusion/sd_xl_base_1.0.safetensors` on the Main Machine, you should have exactly the same `OfficialStableDiffusion/sd_xl_base_1.0.safetensors` on the Other Machine.
            - For ease if you have several models to use, you might consider copying all your models onto a USB Data Drive and copy/pasting exactly over to each machine. Alternately, you might use an FTP program to directly send files over.
        - If the machine has multiple GPUs, again set them up per [the instructions above](#more-gpus-in-your-machine).
        - At this stage I recommend again testing the Swarm on the machine locally to make sure everything is working.
    - Go to the tab `Server` -> `Server Info`, and look at the block labeled `Local Network`. Make note of the address it gives, this will be needed later.
        - It should look something like `http://192.168.50.17:7801`
        - ![img](/docs/images/local-network.png)
        - (Note: if you're using a remotely-hosted server, do not use the local address here, use your server's remote address)
        - If this spot says `This server is only accessible from this computer.`, you forgot to enable LAN access during the install process. That's okay - go to `Server Configuration`, find `Network` -> `Host`, and set the value to `0.0.0.0`, then click `Save` at the bottom and restart Swarm.
        - If this spot does not have a valid local network address, you may need to check your router settings to correctly identify your local network. (If you get to this issue and are lost as to how to figure out router settings, unfortunately you're going a bit out of range of Swarm and into the complex world of networking technology, so you may need to do some google searching or support forum posting to figure it out.)
    - Make sure Swarm is allowed through your machine's firewall.
        - On Windows, this will be a popup asking if you want to allow Swarm to "access your network" or similar - click allow. If you have other firewall software, allow Swarm or allow incoming port `7801` per that software's own instructions.
        - On Linux, there is no firewall by default, but if you have `ufw`/`iptables`/other firewall software, configure it per that firewall's instructions to allow your server port (default `7801`) to be accessible on LAN for incoming TCP connections.

#### Step 4: Connect Them All

- Return to your Main Machine, and repeat the following steps for each "Other Machine" you have:
    - First, from the Main Machine, open a web browser and connect to the Other Machine.
        - This is where you use that local IP you noted down earlier. Just paste (or retype) it your browser address bar (eg `http://192.168.50.17:7801`). If all goes well, you should see the other machine's generate page appear.
        - ![img](/docs/images/remote-machine-open.png)
            - If it does not appear, something went wrong. Make sure (1) Swarm is running on that Other Machine, (2) your local address is correct, and (3) Swarm is not blocked by the firewall on the other machine.
        - Once you're sure it's working, close that tab, and open the Main Machine's local Swarm UI.
        - In your Main Machine's local Swarm UI (normally `localhost:7801`), go to the tab `Server` -> `Backends`
            - In this tab, at the top, there are buttons to add new backends - click `Swarm-API-Backend` and when it asks if you're sure click `Ok`
            - ![img](/docs/images/add-swarm-backend.png)
            - This will cause a new backend box to appear with editable settings.
                - Feel free to replace the default name `Swarm-API-Backend` with a name that more clearly identifies which machine it is to you, for example `Living Room PC`
                - In the box labeled `Address`, enter in the Other Machine's address as copied above (eg `http://192.168.50.17:7801`)
                - ![img](/docs/images/config-swarm-backend.png)
                - Optionally check `AllowIdle` if that Other Machine will not always be turned on. This allows it to fail to connect without showing you any error. Leave it unchecked if you expect the machine will always be turned on.
                - Then press `Save`, and wait for its borders to turn from Orange to Vibrant Green with the label `Running backend:`. Green indicates it is working properly.
                    - If it turned Red with the label `errored backend:`, something went wrong. Double-check your configuration, or see [Troubleshooting below](#troubleshooting). You can click the Pencil icon to edit the settings.
                    - If it turned Gray (or soft green) with the label `idle backend:`, that means you enabled `AllowIdle` and it did not connect, which during setup essentially means it errored.
                    - If it remains Orange with the label `Disabled backend:` that means your configuration is invalid (eg your `Address` is not formatted like a real address)

#### Step 5: Verify It's All Working

- On your Main Machine, open the Generate tab.
- Leave your settings mostly default for now (you can click `Quick Tools` at the top right then `Reset Params to Default`)
- Select a model that is on all machines, such as `OfficialStableDiffusion/sd_xl_base_1.0.safetensors` if you used the installer suggested model
- On the left near the top, find `Core Parameters` and inside of that find `Images`, normally at `1`, and change the value to a large number such as `10`
    - Pick a number that is at least twice as many as the number of GPUs you have in total. For example, if you have 3 machines connected with 2 GPUs each, that is 6 total GPUs, so your number here should be greater than 12.
- In the Prompt box in the center of the UI, type a basic prompt, such as `a cat`
- To the right of the prompt box, click the `Generate` button.
- Wait and allow it to run and generate all images. For right now don't worry about how it's doing, we just want to "Warm Up" the network (ie cause Swarm to queue out the generation to all backends and load models on all).
    - If you have a lot of machines, it may be a good idea to wait until this done and then hit Generate again and let that finish too. (sometimes with large networks a queue will be finished out by the first few machines to load before the others start loading). Repeat until there is no longer a `Waiting on model load...` message at the bottom when you hit generate.
- Then, hit `Generate` again, and this time watch the status text in the bottom-center.
    - It should say something like `10 current generations, 4 running, 6 queued, (est. 3m 50s)...`
    - ![img](/docs/images/queue-running.png)
        - The `10 current generations` indicates how many images in total are being generated (in this case `10`)
        - The `4 running` indicates how many are currently assigned to a backend GPU (in this case `4` images are assigned). Note that generally by default 2 images get assigned to any GPU at a time, so `4 running` indicates that **2** backends are working.
        - The `6 queued` indicates how many images are not yet assigned and are waiting in a queue behind others (in this case `6`). You can generally expect queued+running to equal the total (4 + 6 = 10).
        - The `est 3m 50s` is an estimate of how long it will take to generate all the images waiting. Note that this is a loose estimate and is likely to be wrong.
    - Pay close attention to the `running` count. Make sure it is double the number of GPUs you expect.
        - If it is just `2`, your Other Machines are not likely not running properly.
        - If it is above 2 but less than double your backends, you may have some backends not working.
        - In either case of failure, check [Troubleshooting below](#troubleshooting).

#### Troubleshooting

- **1: My Swarm-API-Backends Can't Connect** or **My network was working before, but NOW my backends can't connect**
    - Make sure your other machines are actually turned on.
    - Make sure Swarm is actually running on that remote machine.
    - Check if the "Local Network" address has changed. If it has, you will need to edit the address listed in "Backends" on the Main Machine to the new address.
    - Check if you can open Swarm's UI on that Other Machine directly. If you can't, Swarm is not loading properly.
    - Check if you can connect to the local address in a browser from the Main Machine. If you can't, it's a networking issue, your address may be wrong or a firewall may have been enabled.
- **2: My Backends have turned Red as an Error, but it's not a Network issue**
    - First, close and restart Swarm. Sometimes restarting fixes it magically, and if not it's helpful to clear the logs that way.
    - Go to the `Server` tab then `Logs`, and set `View Type` to `Debug`. With any luck, there will be a clear error message explaining why.
- **3: My Backends are Green and Running, but aren't used for generations**
    - Go to the `Server` tab then `Logs` and set `View Type` to `Verbose`, and in the `Filter` box type the word `filter` again.
        - If the logs box is empty, trigger a generation then go back to the Logs page.
        - You should see a message like `Filter out backend 1 as the request requires lora mylora.safetensors, but the backend does not have that lora` which explains why the backend was rejected for the generation job.


### I want Comfy Workflows on Multiple GPUs

- The Comfy tab works differently than the main Generate tab.
    - I recommend for multi-GPU usage you simply click `Use This Workflow in Generate Tab` and then queue up generations on the main tab that way.
- If you must use the comfy tab directly and need multiple GPUs:
    - At the top left click `MultiGPU` then `Use All`.
    - This will spread multiple queued requests to multiple backends.
    - The outputs will replace one another rapidly, so you will likely need a way to browser image history separately in this case (eg an image history extension)
- If you must queue one 1 request to several backends:
    - At the top left click `MultiGPU` then `Use All`.
    - make multiple output nodes (eg `Preview Image`)
    - For each output node, right click it and go to `Colors` and select a unique color
    - Each unique color will be assigned to a different GPU.
    - Note that everything leading to the node will also attach to that GPU.
    - This means for example, if you have `LoadCheckpoint` -> `KSampler` -> then 5 output nodes, you will generate the same image 5 times and get 5 outputs, redundantly.
        - However if you have 1 `LoadCheckpoint` -> then 5 KSamplers each with their own output, the LoadCheckpoint will duplicate on all machines but the KSamplers will be separated, and each GPU will only run 1 KSampler. This is ideal.
