

/** Triggers and process the clip tokenization utility. */
function utilClipTokenize() {
    let elem = getRequiredElementById('clip_tokenization_test_textarea');
    let resultLine = getRequiredElementById('clip_tokenization_result_line');
    function process() {
        elem.dataset.is_running_proc = true;
        genericRequest('TokenizeInDetail', { text: elem.value }, data => {
            let html = `<span style="width: 3rem; display: inline-block;">${data.tokens.length} tokens: </span>`;
            for (let token of data.tokens) {
                let text = token.text;
                let tweak = '';
                let postText = '';
                let title = '';
                if (text.endsWith('</w>')) {
                    text = text.substring(0, text.length - '</w>'.length);
                    tweak += ' clip-tokenization-word-tweak';
                    postText += '<span class="clip-tokenization-wordbreak">&lt;/w&gt;</span>';
                    title = "This token is the end of a word, meaning a word-break appears after (such as a space or punctuation).";
                }
                else {
                    title = "This token is a word-piece (as opposed to a word-end), meaning there is no word-break after it, it directly connects to the next token.";
                }
                let weightActual = roundToStr(token.weight, 2);
                let weightInfo = weightActual == 1 ? '' : `<span class="clip-tokenization-weight" title="Token weight = ${weightActual}">${weightActual}</span>`;
                html += `<span class="clip-tokenization-block${tweak}" title="${title}">${escapeHtml(text)}${postText}<br>${token.id}${weightInfo}</span>`;
            }
            resultLine.innerHTML = html;
            delete elem.dataset.is_running_proc;
            if (elem.dataset.needs_reprocess) {
                delete elem.dataset.needs_reprocess;
                process();
            }
        });
    }
    if (elem.dataset.is_running_proc) {
        elem.dataset.needs_reprocess = true;
    }
    else {
        process();
    }
}

function showPromptTokenizen(box) {
    let src = getRequiredElementById(box);
    let target = getRequiredElementById('clip_tokenization_test_textarea');
    target.value = src.value || src.innerText;
    getRequiredElementById('utilitiestabbutton').click();
    getRequiredElementById('cliptokentabbutton').click();
    triggerChangeFor(target);
}

/** Preloads conversion data. */
function pickle2safetensor_load(mapping = null) {
    if (mapping == null) {
        mapping = coreModelMap;
    }
    for (let type of ['Stable-Diffusion', 'LoRA', 'VAE', 'Embedding', 'ControlNet']) {
        let modelSet = mapping[type];
        let count = modelSet.filter(x => !x.startsWith("backup") && !x.endsWith('.safetensors')).length;
        let counter = getRequiredElementById(`pickle2safetensor_${type.toLowerCase()}_count`);
        counter.innerText = count;
        let button = getRequiredElementById(`pickle2safetensor_${type.toLowerCase()}_button`);
        button.disabled = count == 0;
    }
}

/** Triggers the actual conversion process. */
function pickle2safetensor_run(type) {
    let fp16 = getRequiredElementById(`pickle2safetensor_fp16`).checked;
    let button = getRequiredElementById(`pickle2safetensor_${type.toLowerCase()}_button`);
    button.disabled = true;
    let notif = getRequiredElementById('pickle2safetensor_text_area');
    notif.innerText = "Running, please wait ... monitor debug console for details...";
    genericRequest('Pickle2SafeTensor', { type: type, fp16: fp16 }, data => {
        notif.innerText = "Done!";
        genericRequest('TriggerRefresh', {}, data => {
            genericRequest('ListT2IParams', {}, data => {
                pickle2safetensor_load(data.models);
            });
        });
    });
}

function util_massMetadataClear() {
    let button = getRequiredElementById('util_massmetadataclear_button');
    button.disabled = true;
    genericRequest('WipeMetadata', {}, data => {
        genericRequest('TriggerRefresh', {}, data => {
            button.disabled = false;
            for (let browser of allModelBrowsers) {
                browser.browser.refresh();
            }
        });
    });
}

class LoraExtractorUtil {
    constructor() {
        this.tabHeader = getRequiredElementById('loraextractortabbutton');
        this.baseInput = getRequiredElementById('lora_extractor_base_model');
        this.otherInput = getRequiredElementById('lora_extractor_other_model');
        this.rankInput = getRequiredElementById('lora_extractor_rank');
        this.nameInput = getRequiredElementById('lora_extractor_name');
        this.textArea = getRequiredElementById('lora_extractor_text_area');
        this.progressBar = getRequiredElementById('lora_extractor_special_progressbar');
        this.tabHeader.addEventListener('click', () => this.refillInputModels());
    }

    refillInputModels() {
        let html = '';
        for (let model of coreModelMap['Stable-Diffusion']) {
            html += `<option>${model}</option>`;
        }
        let baseSelected = this.baseInput.value;
        let otherSelected = this.otherInput.value;
        this.baseInput.innerHTML = html;
        this.otherInput.innerHTML = html;
        this.baseInput.value = baseSelected;
        this.otherInput.value = otherSelected;
    }

    run() {
        let baseModel = this.baseInput.value;
        let otherModel = this.otherInput.value;
        let rank = this.rankInput.value;
        let outName = this.nameInput.value.replaceAll('\\', '/');
        while (outName.includes('//')) {
            outName = outName.replaceAll('//', '/');
        }
        if (outName.startsWith('/')) {
            outName = outName.substring(1);
        }
        if (outName.endsWith('.safetensors')) {
            outName = outName.substring(0, outName.length - '.safetensors'.length);
        }
        if (outName.endsWith('.ckpt')) {
            outName = outName.substring(0, outName.length - '.ckpt'.length);
        }
        if (!baseModel || !otherModel || !outName) {
            this.textArea.innerText = "Missing required values, cannot extract.";
            return;
        }
        if (coreModelMap['LoRA'].includes(outName)) {
            if (!confirm("That output name is already taken, are you sure you want to overwrite it?")) {
                return;
            }
        }
        this.textArea.innerText = "Running, please wait...";
        let overall = this.progressBar.querySelector('.image-preview-progress-overall');
        let current = this.progressBar.querySelector('.image-preview-progress-current');
        makeWSRequest('DoLoraExtractionWS', { baseModel: baseModel, otherModel: otherModel, rank: rank, outName: outName }, data => {
            if (data.overall_percent) {
                overall.style.width = `${data.overall_percent * 100}%`;
                current.style.width = `${data.current_percent * 100}%`;
            }
            else if (data.success) {
                this.textArea.innerText = "Done!";
                refreshParameterValues(true);
                overall.style.width = `0%`;
                current.style.width = `0%`;
            }
        }, 0, e => {
            this.textArea.innerText = `Error: ${e}`;
            overall.style.width = `0%`;
            current.style.width = `0%`;
        });
    }
}

loraExtractor = new LoraExtractorUtil();
