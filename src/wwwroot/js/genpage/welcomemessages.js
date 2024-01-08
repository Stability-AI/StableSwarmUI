/** Removes any current visible image or other content and forcibly redisplays any welcome message. */
function forceShowWelcomeMessage() {
    getRequiredElementById('current_image').innerHTML = '<div id="welcome_message" class="welcome_message"></div>';
    resetWelcomeMessage();
}

/** (Only if a welcome message is displayed) clears the existing welcome message and replaces it automatically. */
function resetWelcomeMessage(override = null) {
    let div = document.getElementById('welcome_message');
    if (div) {
        div.innerHTML = '';
    }
    automaticWelcomeMessage(override);
}

/** (Only if there is no pre-existing welcome message and the current_image area is empty) automatically chooses a welcome message to display and applies it. */
function automaticWelcomeMessage(override = null) {
    let div = document.getElementById('welcome_message');
    if (!div) {
        return;
    }
    if (div.innerHTML.trim() != '') {
        return;
    }
    let prefix = `Welcome to <b>${getRequiredElementById('version_display').innerText}</b>!\n`;
    let curModelElem = getRequiredElementById('current_model');
    if (!curModelElem.value) {
        if (allModels.length == 0) {
            div.innerHTML = `${prefix}You don't seem to have downloaded any models yet.\nPlease download a model, or go to the <a href="#Settings-Server" onclick="getRequiredElementById('servertabbutton').click();getRequiredElementById('serverconfigtabbutton').click();">Server Configuration Tab</a> to set your models folder.`;
            return;
        }
        div.innerHTML = `${prefix}Please select a model in the <a href="#models-tab" onclick="getRequiredElementById('modelstabheader').click()">Models</a> tab below to get started.`;
        curModelElem.addEventListener('change', () => {
            resetWelcomeMessage(0);
        });
        return;
    }
    let messages = [
        /* Generic welcome messages, order-sensitive, keep at top */
        `Type your prompt below and hit Generate!`,
        /* "Did you known" facts - interesting things you can do in swarm */
        `Did you know:\nyou can drag and drop images onto the prompt box to use them as an image-prompt.`,
        `Did you know:\nyou can create multiple variations of one image by locking in your seed, then enabling the <b>Variation Seed</b> parameter.`,
        `Did you know:\nWant to have live-previews of your generation while you edit the details?\nLock in a seed, then right click the <b>Generate</b> button and select 'Generate Previews'.\nIf you add a <b>Preset</b> and name it exactly <b>Preview</b>, that preset will automatically be used for previews (LCM or Turbo recommended in the preset for speed).`,
        `Did you know:\nWant to compare how different values of a parameter affect your generations?\nHead to the <b>Tools</b> tab below and select the <b>Grid Generator</b> tool.`,
        `Did you know:\nYou can create a <b>Preset</b> and name it exactly <b>Default</b>\nand that preset will automatically be used to load your default params when you launch Swarm.`,
        /* Recent feature updates */
        `New feature (2023-11-25): SVD Support!\nDownload <a href="https://huggingface.co/stabilityai/stable-video-diffusion-img2vid-xt/blob/main/svd_xt.safetensors">the Stable Video Diffusion model</a>, and then you can do text-2-image-2-video in the Generate tab! Just select the <b>Video</b> parameter group on the left to try it.\nOr image-2-video by selecting an <b>Init Image</b> and setting <b>Creativity</b> to <b>0</b>. <a href="https://github.com/Stability-AI/StableSwarmUI/discussions/11#discussioncomment-7668336">(Feature Announcement Link)</a>`,
        `New feature (2023-12-03): Log viewer!\nNeed to check those debug logs?\nThey're available under the <b>Server</b> tab, with log-level selection and filters. <a href="https://github.com/Stability-AI/StableSwarmUI/discussions/11#discussioncomment-7772593">(Feature Announcement Link)</a>`,
        `New feature (2023-12-06): LoRA Extractor!\nWant to reduce your model file space waste and extract some of your finetuned models into LoRAs?\nHead to the <b>Utilities</b> tab to try the LoRA extractor. <a href="https://github.com/Stability-AI/StableSwarmUI/discussions/11#discussioncomment-7772593">(Feature Announcement Link)</a>`,
        `New feature (2023-12-06): Tab Completion in prompts!\nJust type a '&lt;' symbol and watch the suggestions for prompt-syntax tools appear! <a href="https://github.com/Stability-AI/StableSwarmUI/discussions/11#discussioncomment-7775593">(Feature Announcement Link)</a>`,
        `New feature (2023-12-07): Welcome messages!\nOh, well, hi, that's this right here. <a href="https://github.com/Stability-AI/StableSwarmUI/discussions/11#discussioncomment-7791189">(Feature Announcement Link)</a>`
    ];
    let dotnetNotice = document.getElementById('dotnet_missing_message');
    if (dotnetNotice) {
        messages.push(dotnetNotice.innerHTML.trim());
    }
    if (override == null) {
        if (dotnetNotice) {
            override = messages.length - 1;
        }
        else {
            override = Math.floor(Math.random() * messages.length);
        }
    }
    override = override % messages.length;
    if (override < 0) {
        override += messages.length;
    }
    div.innerHTML = `${prefix}\n<div class="welcome-message-wrapper">${messages[override]}</div>\n\n<button class="btn btn-secondary" onclick="resetWelcomeMessage(${override - 1})">&lt;</button> <button class="btn btn-secondary" onclick="resetWelcomeMessage(${override + 1})">&gt;</button>`;
    return;
}
