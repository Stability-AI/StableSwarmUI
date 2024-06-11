

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
        let count = modelSet.filter(x => !x.startsWith("backup") && !x.endsWith('.safetensors') && !x.endsWith('.engine')).length;
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
        for (let model of allModels.filter(m => !m.endsWith('.engine'))) {
            html += `<option>${cleanModelName(model)}</option>`;
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

class ModelDownloaderUtil {
    constructor() {
        this.tabHeader = getRequiredElementById('modeldownloadertabbutton');
        this.url = getRequiredElementById('model_downloader_url');
        this.urlStatusArea = getRequiredElementById('model_downloader_status');
        this.type = getRequiredElementById('model_downloader_type');
        this.name = getRequiredElementById('model_downloader_name');
        this.textArea = getRequiredElementById('model_downloader_text_area');
        this.progressBar = getRequiredElementById('model_downloader_special_progressbar');
        this.button = getRequiredElementById('model_downloader_button');
        this.metadataZone = getRequiredElementById('model_downloader_metadatazone');
        this.hfPrefix = 'https://huggingface.co/';
        this.civitPrefix = 'https://civitai.com/';
    }

    getCivitaiMetadata(id, versId, callback) {
        getJsonDirect(`${this.civitPrefix}api/v1/models/${id}`, (status, rawData) => {
            let modelType = null;
            let metadata = null;
            let rawVersion = rawData.modelVersions[0];
            let file = rawVersion.files[0];
            if (versId) {
                for (let vers of rawData.modelVersions) {
                    for (let vFile of vers.files) {
                        if (vFile.downloadUrl.endsWith(`/${versId}`)) {
                            rawVersion = vers;
                            file = vFile;
                            break;
                        }
                    }
                }
            }
            if (rawData.type == 'Checkpoint') { modelType = 'Stable-Diffusion'; }
            if (rawData.type == 'LORA') { modelType = 'LoRA'; }
            if (rawData.type == 'TextualInversion') { modelType = 'Embedding'; }
            if (rawData.type == 'ControlNet') { modelType = 'ControlNet'; }
            let applyMetadata = (img) => {
                let url = `${this.civitPrefix}models/${id}?modelVersionId=${versId}`;
                metadata = {
                    'modelspec.title': `${rawData.name} - ${rawVersion.name}`,
                    'modelspec.author': rawData.creator.username,
                    'modelspec.description': `From <a href="${url}">${url}</a>\n${rawVersion.description || ''}\n${rawData.description}\n`,
                    'modelspec.date': rawVersion.createdAt,
                };
                if (rawVersion.trainedWords) {
                    metadata['modelspec.trigger_phrase'] = rawVersion.trainedWords.join(", ");
                }
                if (rawData.tags) {
                    metadata['modelspec.tags'] = rawData.tags.join(", ");
                }
                if (img) {
                    metadata['modelspec.thumbnail'] = img;
                }
                callback(rawData, rawVersion, metadata, modelType, file.downloadUrl, img);
            }
            let imgs = rawVersion.images ? rawVersion.images.filter(img => img.type == 'image') : [];
            if (imgs.length > 0) {
                imageToData(imgs[0].url, img => applyMetadata(img));
            }
            else {
                applyMetadata('');
            }
        }, (status, data) => {
            callback(null, null, null, null, null, null);
        });
    }

    parseCivitaiUrl(url) {
        let parts = url.substring(this.civitPrefix.length).split('/', 4); // 'models', id, name + sometimes version OR 'api', 'download', 'models', versid
        if (parts.length == 2 && parts[0] == 'models' && parts[1].includes('?')) {
            let subparts = parts[1].split('?', 2);
            parts = ['models', subparts[0], `?${subparts[1]}`];
        }
        if (parts.length < 3) {
            return [null, null];
        }
        if (parts[0] == 'models') {
            let subparts = parts[2].split('?modelVersionId=', 2);
            if (subparts.length == 2) {
                return [parts[1], subparts[1]];
            }
            else {
                return [parts[1], null];
            }
        }
        return [null, null];
    }

    urlInput() {
        let url = this.url.value;
        if (url.endsWith('.pt') || url.endsWith('.pth') || url.endsWith('.ckpt') || url.endsWith('.bin')) {
            this.urlStatusArea.innerText = "URL looks to be a pickle file, cannot download. Only safetensors can be auto-downloaded. Pickle files may contain malware.";
            this.button.disabled = true;
            return;
        }
        if (url.startsWith(this.hfPrefix)) {
            let parts = url.substring(this.hfPrefix.length).split('/', 5); // org, repo, 'blob', branch, filepath
            if (parts.length < 5) {
                this.urlStatusArea.innerText = "URL appears to be a huggingface link, but not a specific file. Please use the path of a specific file inside the repo.";
                this.button.disabled = false;
                return;
            }
            if (parts[4].endsWith('?download=true')) {
                parts[4] = parts[4].substring(0, parts[4].length - '?download=true'.length);
                this.url.value = `${this.hfPrefix}${parts.join('/')}`;
            }
            if (!parts[4].endsWith('.safetensors')) {
                this.urlStatusArea.innerText = "URL appears to be a huggingface link, but not a safetensors file. Only safetensors can be auto-downloaded.";
                this.button.disabled = false;
                return;
            }
            if (parts[2] == 'blob') {
                parts[2] = 'resolve';
                this.url.value = `${this.hfPrefix}${parts.join('/')}`;
                this.urlStatusArea.innerText = "URL appears to be a huggingface link, and has been autocorrected to a download link.";
                this.button.disabled = false;
                this.name.value = parts.slice(4).join('/').replaceAll('.safetensors', '');
                this.nameInput();
                return;
            }
            if (parts[2] == 'resolve') {
                this.urlStatusArea.innerText = "URL appears to be a valid HuggingFace download link.";
                this.button.disabled = false;
                this.name.value = parts.slice(4).join('/').replaceAll('.safetensors', '');
                this.nameInput();
                return;
            }
            this.urlStatusArea.innerText = "URL appears to be a huggingface link, but seems to not be valid. Please double-check the link.";
            this.button.disabled = false;
            return;
        }
        if (url.startsWith(this.civitPrefix)) {
            let parts = url.substring(this.civitPrefix.length).split('/', 4); // 'models', id, name + sometimes version OR 'api', 'download', 'models', versid
            if (parts.length == 2 && parts[0] == 'models' && parts[1].includes('?')) {
                let subparts = parts[1].split('?', 2);
                parts = ['models', subparts[0], `?${subparts[1]}`];
            }
            let loadMetadata = (id, versId) => {
                this.getCivitaiMetadata(id, versId, (rawData, rawVersion, metadata, modelType, url, img) => {
                    if (!rawData) {
                        this.urlStatusArea.innerText = "URL appears to be a CivitAI link, but seems to not be valid. Please double-check the link.";
                        this.nameInput();
                        return;
                    }
                    this.url.value = url;
                    if (modelType) {
                        this.type.value = modelType;
                    }
                    this.urlStatusArea.innerText = "URL appears to be a CivitAI link, and has been loaded from Civitai API.";
                    this.name.value = `${rawData.name} - ${rawVersion.name}`;
                    this.nameInput();
                    this.metadataZone.innerHTML = `
                        Found civitai metadata for model ID ${escapeHtml(id)} version id ${escapeHtml(versId)}:
                        <br><b>Model title</b>: ${escapeHtml(rawData.name)}
                        <br><b>Version title</b>: ${escapeHtml(rawVersion.name)}
                        <br><b>Base model</b>: ${escapeHtml(rawVersion.baseModel)}
                        <br><b>Date</b>: ${escapeHtml(rawVersion.createdAt)}`
                        + (img ? `<br><b>Thumbnail</b>:<br> <img src="${img}" style="max-width: 100%; max-height: 100%;">` : '')
                        + `<br><b>Model description</b>: ${safeHtmlOnly(rawData.description)}`
                        + (rawVersion.description ? `<br><b>Version description</b>: ${safeHtmlOnly(rawVersion.description)}` : '')
                        + (rawVersion.trainedWords ? `<br><b>Trained words</b>: ${escapeHtml(rawVersion.trainedWords.join(", "))}` : '');
                    this.metadataZone.dataset.raw = `${JSON.stringify(metadata, null, 2)}`;
                });
            }
            if (parts.length < 3) {
                this.urlStatusArea.innerText = "URL appears to be a CivitAI link, but not a specific model. Please use the path of a specific model.";
                this.nameInput();
                return;
            }
            if (parts[0] == 'models') {
                let [id, versId] = this.parseCivitaiUrl(url);
                if (id) {
                    if (versId) {
                        this.url.value = `${this.civitPrefix}api/download/models/${versId}`;
                        this.urlStatusArea.innerText = "URL appears to be a CivitAI link, and has been autocorrected to a download link.";
                        this.nameInput();
                    }
                    loadMetadata(id, versId);
                    return;
                }
                this.urlStatusArea.innerText = "URL appears to be a CivitAI link, but is missing a version ID. Please double-check the link.";
                this.nameInput();
                return;
            }
            if (parts[0] == 'api' && parts[1] == 'download' && parts[2] == 'models') {
                this.urlStatusArea.innerText = "URL appears to be a valid CivitAI download link.";
                this.nameInput();
                loadMetadata(parts[3], null);
                return;
            }
            this.urlStatusArea.innerText = "URL appears to be a CivitAI link, but seems to not be valid. Attempting to check it...";
            this.nameInput();
            return;
        }
        else {
            this.metadataZone.innerHTML = '';
            this.metadataZone.dataset.raw = '';
        }
        if (url.trim() == '') {
            this.urlStatusArea.innerText = "(...)";
            this.button.disabled = true;
            return;
        }
        if (!url.startsWith('http://') && !url.startsWith('https://')) {
            this.urlStatusArea.innerText = "URL is not a valid link (should start with 'https://').";
            this.button.disabled = true;
            return;
        }
        this.urlStatusArea.innerText = "URL is unrecognized but looks valid.";
        this.nameInput();
        return;
    }

    nameInput() {
        this.button.disabled = false;
        if (this.name.value.trim() == '') {
            this.name.style.borderColor = 'red';
            this.button.disabled = true;
        }
        else {
            this.name.style.borderColor = '';
        }

        if (this.url.value.trim() == '') {
            this.url.style.borderColor = 'red';
            this.button.disabled = true;
        }
        else {
            this.url.style.borderColor = '';
        }
    }

    run() {
        let data = {
            'url': this.url.value,
            'type': this.type.value,
            'name': this.name.value,
            'metadata': this.metadataZone.dataset.raw || '',
        }
        this.textArea.innerText = "Downloading, please wait...";
        this.button.disabled = true;
        let overall = this.progressBar.querySelector('.image-preview-progress-overall');
        let current = this.progressBar.querySelector('.image-preview-progress-current');
        makeWSRequest('DoModelDownloadWS', data, data => {
            if (data.overall_percent) {
                overall.style.width = `${data.overall_percent * 100}%`;
                current.style.width = `${data.current_percent * 100}%`;
                this.textArea.innerText = `Downloading, please wait... ${roundToStr(data.current_percent * 100, 1)}%`;
            }
            else if (data.success) {
                this.textArea.innerText = "Done!";
                refreshParameterValues(true);
                overall.style.width = `0%`;
                current.style.width = `0%`;
            }
        }, 0, e => {
            this.textArea.innerHTML = `Error: ${escapeHtml(e)}\n<br>Are you sure the URL is correct? Note some models may require you to be logged in, which is incompatible with an auto-downloader.`;
            overall.style.width = `0%`;
            current.style.width = `0%`;
        });
    }
}

modelDownloader = new ModelDownloaderUtil();
