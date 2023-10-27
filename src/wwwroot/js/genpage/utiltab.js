

/** Triggers and process the clip tokenization utility. */
function utilClipTokenize() {
    let elem = getRequiredElementById('clip_tokenization_test_textarea');
    let resultLine = getRequiredElementById('clip_tokenization_result_line');
    function process() {
        elem.dataset.is_running_proc = true;
        genericRequest('TokenizeInDetail', { text: elem.value }, data => {
            console.log(`server says ${JSON.stringify(data)}`)
            let html = '';
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
                let weightActual = roundTo(token.weight, 0.01);
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
