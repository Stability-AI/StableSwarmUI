# Why Use Swarm?

Why should you use Swarm, a series of 'sales'\* pitches:
\*(Swarm is and always will be 100% free and open source)

The answer to why you should use Swarm depends on who you are...

### I am a ComfyUI User

If you're a happy comfy user, you're probably a fan of the noodles. If you're not a fan of the noodles but just tolerating, switch to the "other local SD UI user" option below.

Swarm is a wrapper around Comfy - it contains Comfy, in full, node graph and all, and then adds more. So right away point one for a Comfy user considering Swarm: you lose literally nothing. You get the exact same Comfy you already have. Here's some things you get on top:
- **Integrated workflow browser:** Have a bunch of cool workflows? Save/load/browser is a super convenient integrated interface.
- **Sharing/Teamwork:** working as a team? The ability to share a common instance with a common workflow browser list is invaluable to keeping everyone on track together.
- **Easy Install:** no nonsense for the install, just download and run, it sets itself up. (If you want to customize you can just disable the autoinstaller features and do things manually.) Swarm can even autoinstall nodes, pip dependencies, etc. if you let it.
- **Grid Generator:** One of the best features of Swarm, just configure and save workflows you're happy with and pop over to the Generate tab to generate grids comparing different workflows or parameters within the workflows.
- **Workflow AutoGenerator:**
    - **Convenience:** don't want to fight the noodles *all* the time? The Generate tab is much easier and friendly to quickly change things around. Do you hate how SDv1 uses 512 and SDXL uses 1024 and you have to fiddle the emptylatent node whenever you change them? Me too, that's why the generate tab autodetects preferred resolution for a model and updates (you can of course set resolution to `Custom` and define it manually). It gives you recommended aspect ratios that fit the model too (don't memorize `1344x768`, just click it in a dropdown list labeled as the human-understandable `16:9`). Why add and configure five differents nodes to do a hires fix when you can just check 'refiner' and drag a slider for your preferred upscale value.
    - **Education:** newer to Comfy? the workflow autogenerator with it's super easy and learnable interface is perfect to set things up in, and then you can click the Comfy tab and click "Import from Generate Tab" to see how your generation works on the inside. This is perfect to learn how Comfy components work and slot together.
- **Simple Tab:**
    - **For you:** Have your workflow perfected? Add some `SwarmInput<..>` nodes for your primary inputs, save it, and checkmark `Enable In Simple Tab` - then click the Simple tab and use a simplified interface for your workflow. Focus on what matters without jumping back and forth across the canvas.
    - **For your friends:** Want to share your workflow with a friend that's afraid of noodles? Save it to the simple tab, then provide your friend with a direct link to your simple tab page. They can use the friendly interface without having to see what horrifying cthulu monster you made to power it.
- **Easy control:** Want to change the models path? Toggle some other setting? No more digging through files to edit configs - Swarm's configuration is entirely done in a friendly UI! Every setting even has a clickable `?` button to show documentation explaining what it does. (You can edit config files if you really prefer though still). Even pulling updates is just a UI button under the 'Server' tab!
- **More immediate control:** Why does Comfy have preview method as a command line arg, and not in a node? I don't know. With Swarm you have the option to control it on the fly dynamically, either on the generate tab or with the `SwarmKSampler` node that has extra power-user options.
- **Image history, model browser, etc:** the generate tab has friendly browsers and organizers for all your important fifty-terabyte-file-dumps. Why remember what your model is named and how it works, when you browse a view with thumbnails, descriptions, usage hints, etc?
- **Wildcards:** Tons of tack on features like savable wildcards you can use in your prompts when on the Generate tab. All integrated and clean. No need to figure out the correct custom node to use for every random little thing, Swarm integrates many common features clearly out of the box.
- **Var Seed and other special features:** Did you use auto webui in the past and now you wonder why Comfy doesn't have it? Wonder no longer, Swarm adds comfy features for all the user favorite features found on other UIs and missing from Comfy. All easy to use and available by default.
- **Fancy prompt syntax:** You bet that 'favorite features from other UIs' includes the ability to do timestep prompt alternating/fromto and all those funky things that are really hard to do in comfy normally! Just type a `<` in the generate tab prompt box to see what fancy prompt syntax is available. And don't forget, you can always just import to the Comfy tab to see how it works on the inside and connect it elsewhere.
- **Smarter model generations:** Did you know that SVD prefers a Sigma Max of 1000 (non-default)? Did you know that SDXL generates better if you slightly boost the Positive Prompt rescond, and slightly lower the Negative Prompt rescond? You don't need to know this, Swarm knows it and will generate by default with model-specific-enhancements (you can of course turn this off).
- **API:** Want a powerful developer API? Swarm's got your back, it has a very very clear HTTP API and websocket API. Where comfy might want you to just through a few hoops to use as API, Swarm's API is handcrafted to be super convenient - and just to be sure it's perfect, Swarm uses its own API at all times (so just check network traffic in your browser for live examples), and has full documentation [(here)](/docs/API.md)
- **Multi-GPU:** Oh, yeah, did I mention the reason it's named "Swarm"?! You can connect multiple GPUs, multiple machines, even remote machines over network! Generations will be automatically queued between them, and you can even split a single custom comfy workflow across multiple backend GPUs if you want! (*With some limitations, see [Using More GPUs](/docs/Using%20More%20GPUs.md) for specifics)
- **assorted other features:** Want to convert your old `.ckpt` files to `.safetensors`? Want to extract a LoRA from your fat checkpoint models? Want to check how the CLIP tokenization of your prompt works out? Swarm has a bunch of tack-on handy dandy utilities like those.
- **And more!** Frankly at this point this section is getting too long and I'm probably forgetting things anyway. Just give Swarm a try, you have nothing to lose, and I'm pretty sure once you see it in action, you'll stick with it forever!

### I am an Auto WebUI or other local SD UI user

(TODO)

### I am an online SD UI user

(TODO)

### I am new to AI Image Gen

(TODO)

### I'm Lost, Where Am I? What's AI?

You should use Swarm cause it's really cool. AI is really cool, and Swarm is a great way to try it out!
