
let models = {};
let cur_model = null;
let curModelWidth = 0, curModelHeight = 0;
let curModelArch = '';
let curModelCompatClass = '';
let curWildcardMenuWildcard = null;
let curModelMenuModel = null;
let curModelMenuBrowser = null;
let loraWeightPref = {};
let allWildcards = [];

function test_wildcard_again() {
    let card = curWildcardMenuWildcard;
    if (card == null) {
        console.log("Wildcard do test: no wildcard");
        return;
    }
    testWildcard(card);
}

function testWildcard(card) {
    if (card == null) {
        return;
    }
    curWildcardMenuWildcard = card;
    getRequiredElementById('test_wildcard_name').innerText = card.name;
    let choice = Math.floor(Math.random() * card.options.length);
    let val = card.options[choice];
    getRequiredElementById('test_wildcard_result').value = val;
    let button = getRequiredElementById('test_wildcard_again_button');
    if (val.includes('<')) {
        button.disabled = true;
        genericRequest('TestPromptFill', {'prompt': val}, data => {
            button.disabled = false;
            getRequiredElementById('test_wildcard_result').value = data.result;
            $('#test_wildcard_modal').modal('show');
        });
    }
    else {
        button.disabled = false;
        $('#test_wildcard_modal').modal('show');
    }
}

function close_test_wildcard() {
    $('#test_wildcard_modal').modal('hide');
}

function create_new_wildcard_button() {
    let card = {
        name: '',
        raw: ''
    };
    editWildcard(card);
}

function editWildcard(card) {
    if (card == null) {
        return;
    }
    curWildcardMenuWildcard = card;
    let imageInput = getRequiredElementById('edit_wildcard_image');
    imageInput.innerHTML = '';
    let enableImage = getRequiredElementById('edit_wildcard_enable_image');
    enableImage.checked = false;
    enableImage.disabled = true;
    let curImg = document.getElementById('current_image_img');
    if (curImg) {
        let newImg = curImg.cloneNode(true);
        newImg.id = 'edit_wildcard_image_img';
        newImg.style.maxWidth = '100%';
        newImg.style.maxHeight = '';
        newImg.removeAttribute('width');
        newImg.removeAttribute('height');
        imageInput.appendChild(newImg);
        if (!card.image || card.image == 'imgs/model_placeholder.jpg') {
            enableImage.checked = true;
        }
        enableImage.disabled = false;
    }
    getRequiredElementById('edit_wildcard_name').value = card.name;
    getRequiredElementById('edit_wildcard_contents').value = card.raw;
    $('#edit_wildcard_modal').modal('show');
}

function save_edit_wildcard() {
    let card = curWildcardMenuWildcard;
    if (card == null) {
        console.log("Wildcard do save: no wildcard");
        return;
    }
    let data = {
        'card': getRequiredElementById('edit_wildcard_name').value,
        'options': getRequiredElementById('edit_wildcard_contents').value.trim() + '\n',
        'preview_image': '',
        'preview_image_metadata': currentMetadataVal
    };
    function complete() {
        genericRequest('EditWildcard', data, data => {
            wildcardsBrowser.browser.refresh();
        });
        $('#edit_wildcard_modal').modal('hide');
    }
    if (getRequiredElementById('edit_wildcard_enable_image').checked) {
        imageToData(getRequiredElementById('edit_wildcard_image').getElementsByTagName('img')[0].src, (dataURL) => {
            data['preview_image'] = dataURL;
            complete();
        }, true);
    }
    else {
        complete();
    }
}

function close_edit_wildcard() {
    $('#edit_wildcard_modal').modal('hide');
}

function editModel(model, browser) {
    if (model == null) {
        return;
    }
    curModelMenuModel = model;
    curModelMenuBrowser = browser;
    let imageInput = getRequiredElementById('edit_model_image');
    imageInput.innerHTML = '';
    let enableImage = getRequiredElementById('edit_model_enable_image');
    enableImage.checked = false;
    enableImage.disabled = true;
    let curImg = document.getElementById('current_image_img');
    if (curImg) {
        let newImg = curImg.cloneNode(true);
        newImg.id = 'edit_model_image_img';
        newImg.style.maxWidth = '100%';
        newImg.style.maxHeight = '';
        newImg.removeAttribute('width');
        newImg.removeAttribute('height');
        imageInput.appendChild(newImg);
        if (!model.preview_image || model.preview_image == 'imgs/model_placeholder.jpg') {
            enableImage.checked = true;
        }
        enableImage.disabled = false;
    }
    let technical = `Created: ${formatDateTime(new Date(model.time_created))}\nModified: ${formatDateTime(new Date(model.time_modified))}`;
    if (model.hash) {
        technical += `\nHash: ${model.hash}`;
    }
    getRequiredElementById('edit_model_technical_data').innerText = technical;
    getRequiredElementById('edit_model_name').value = model.title || model.name;
    getRequiredElementById('edit_model_type').value = model.architecture || '';
    getRequiredElementById('edit_model_prediction_type').value = model.prediction_type || '';
    getRequiredElementById('edit_model_resolution').value = `${model.standard_width}x${model.standard_height}`;
    for (let val of ['description', 'author', 'usage_hint', 'date', 'license', 'trigger_phrase', 'tags']) {
        getRequiredElementById(`edit_model_${val}`).value = model[val] || '';
    }
    getRequiredElementById('edit_model_is_negative').checked = model.is_negative_embedding || false;
    getRequiredElementById('edit_model_is_negative_div').style.display = model.architecture && model.architecture.endsWith('/textual-inversion') ? 'block' : 'none';
    $('#edit_model_modal').modal('show');
}

function edit_model_load_civitai() {
    let url = getRequiredElementById('edit_model_civitai_url').value;
    let info = getRequiredElementById('edit_model_civitai_info');
    if (!url) {
        info.innerText = 'No URL provided.';
        return;
    }
    let [id, versId] = modelDownloader.parseCivitaiUrl(url);
    if (!id) {
        info.innerText = 'Invalid URL.';
        return;
    }
    info.innerText = 'Loading...';
    modelDownloader.getCivitaiMetadata(id, versId, (rawData, rawVersion, metadata, modelType, url, img) => {
        if (!rawData) {
            info.innerText = 'Failed to load metadata.';
            return;
        }
        getRequiredElementById('edit_model_name').value = metadata['modelspec.title'];
        getRequiredElementById('edit_model_author').value = metadata['modelspec.author'];
        getRequiredElementById('edit_model_description').value = metadata['modelspec.description'];
        getRequiredElementById('edit_model_date').value = metadata['modelspec.date'];
        if (metadata['modelspec.trigger_phrase']) {
            getRequiredElementById('edit_model_trigger_phrase').value = metadata['modelspec.trigger_phrase'];
        }
        if (metadata['modelspec.tags']) {
            getRequiredElementById('edit_model_tags').value = metadata['modelspec.tags'];
        }
        if (img) {
            let imageInput = getRequiredElementById('edit_model_image');
            imageInput.innerHTML = '';
            let newImg = document.createElement('img');
            newImg.src = img;
            newImg.id = 'edit_model_image_img';
            newImg.style.maxWidth = '100%';
            newImg.style.maxHeight = '';
            imageInput.appendChild(newImg);
            let enableImage = getRequiredElementById('edit_model_enable_image');
            enableImage.checked = true;
            enableImage.disabled = false;
        }
        info.innerText = 'Loaded.';
    });
}

function save_edit_model() {
    let model = curModelMenuModel;
    if (model == null) {
        console.log("Model do save: no model");
        return;
    }
    let resolution = getRequiredElementById('edit_model_resolution').value.split('x');
    let data = {
        'model': model.name,
        'title': getRequiredElementById('edit_model_name').value,
        'standard_width': parseInt(resolution[0]),
        'standard_height': parseInt(resolution[1]),
        'preview_image': '',
        'preview_image_metadata': currentMetadataVal
    };
    for (let val of ['author', 'type', 'description', 'usage_hint', 'date', 'license', 'trigger_phrase', 'tags', 'prediction_type']) {
        data[val] = getRequiredElementById(`edit_model_${val}`).value;
    }
    data['is_negative_embedding'] = (model.architecture || '').endsWith('/textual-inversion') ? getRequiredElementById('edit_model_is_negative').checked : false;
    data.subtype = curModelMenuBrowser.subType;
    function complete() {
        genericRequest('EditModelMetadata', data, data => {
            curModelMenuBrowser.browser.update();
        });
        $('#edit_model_modal').modal('hide');
    }
    if (getRequiredElementById('edit_model_enable_image').checked) {
        imageToData(getRequiredElementById('edit_model_image').getElementsByTagName('img')[0].src, (dataURL) => {
            data['preview_image'] = dataURL;
            complete();
        }, true);
    }
    else {
        complete();
    }
}

function close_edit_model() {
    $('#edit_model_modal').modal('hide');
}

function cleanModelName(name) {
    let index = name.lastIndexOf('/');
    if (index != -1) {
        name = name.substring(index + 1);
    }
    index = name.lastIndexOf('.');
    if (index != -1) {
        name = name.substring(0, index);
    }
    return name;
}

function isModelArchCorrect(model) {
    if (model.compat_class && curModelCompatClass) {
        let slash = model.architecture.indexOf('/');
        if (slash != -1) { // Base models are excluded
            return model.compat_class == curModelCompatClass;
        }
    }
    return true;
}

function cleanModelName(name) {
    return name.endsWith('.safetensors') ? name.substring(0, name.length - '.safetensors'.length) : name;
}

function sortModelLocal(a, b, files) {
    let aCorrect = isModelArchCorrect(a);
    let bCorrect = isModelArchCorrect(b);
    if (aCorrect && !bCorrect) {
        return -1;
    }
    if (!aCorrect && bCorrect) {
        return 1;
    }
    let aName = a.name.toLowerCase();
    let bName = b.name.toLowerCase();
    if (aName.endsWith('.safetensors') && !bName.endsWith('.safetensors')) {
        return -1;
    }
    if (!aName.endsWith('.safetensors') && bName.endsWith('.safetensors')) {
        return 1;
    }
    if (aName.endsWith('.engine') && !bName.endsWith('.engine')) {
        return -1;
    }
    if (!aName.endsWith('.engine') && bName.endsWith('.engine')) {
        return 1;
    }
    let aIndex = files.indexOf(a);
    let bIndex = files.indexOf(b);
    return aIndex - bIndex;
}

class ModelBrowserWrapper {
    constructor(subType, container, id, selectOne, extraHeader = '') {
        this.subType = subType;
        this.selectOne = selectOne;
        let format = subType == 'Wildcards' ? 'Small Cards' : 'Cards';
        extraHeader += `<label for="models_${subType}_sort_by">Sort:</label> <select id="models_${subType}_sort_by"><option>Name</option><option>Title</option><option>DateCreated</option><option>DateModified</option></select> <input type="checkbox" id="models_${subType}_sort_reverse"> <label for="models_${subType}_sort_reverse">Reverse</label>`;
        this.browser = new GenPageBrowserClass(container, this.listModelFolderAndFiles.bind(this), id, format, this.describeModel.bind(this), this.selectModel.bind(this), extraHeader);
    }

    listModelFolderAndFiles(path, isRefresh, callback, depth) {
        let sortBy = localStorage.getItem(`models_${this.subType}_sort_by`) ?? 'Name';
        let reverse = localStorage.getItem(`models_${this.subType}_sort_reverse`) == 'true';
        let sortElem = document.getElementById(`models_${this.subType}_sort_by`);
        let sortReverseElem = document.getElementById(`models_${this.subType}_sort_reverse`);
        let fix = null;
        if (sortElem) {
            sortBy = sortElem.value;
            reverse = sortReverseElem.checked;
        }
        else { // first call happens before headers are added built atm
            fix = () => {
                let sortElem = document.getElementById(`models_${this.subType}_sort_by`);
                let sortReverseElem = document.getElementById(`models_${this.subType}_sort_reverse`);
                sortElem.value = sortBy;
                sortReverseElem.checked = reverse;
                sortElem.addEventListener('change', () => {
                    localStorage.setItem(`models_${this.subType}_sort_by`, sortElem.value);
                    this.browser.update();
                });
                sortReverseElem.addEventListener('change', () => {
                    localStorage.setItem(`models_${this.subType}_sort_reverse`, sortReverseElem.checked);
                    this.browser.update();
                });
            }
        }
        let prefix = path == '' ? '' : (path.endsWith('/') ? path : `${path}/`);
        genericRequest('ListModels', {'path': path, 'depth': depth, 'subtype': this.subType, 'sortBy': sortBy, 'sortReverse': reverse}, data => {
            let files = data.files.sort((a,b) => sortModelLocal(a, b, data.files)).map(f => { return { 'name': `${prefix}${f.name}`, 'data': f }; });
            if (this.subType == 'VAE') {
                let autoFile = {
                    'name': `Automatic`,
                    'data': {
                        'name': 'Automatic',
                        'title': 'Automatic',
                        'author': '(Internal)',
                        'architecture': 'VAE',
                        'class': 'VAE',
                        'description': 'Use the VAE sepcified in your User Settings, or use the VAE built-in to your Stable Diffusion model',
                        'preview_image': '/imgs/automatic.jpg',
                        'is_supported_model_format': true,
                        'local': true,
                        standard_width: 0,
                        standard_height: 0
                    }
                };
                let noneFile = {
                    'name': `None`,
                    'data': {
                        'name': 'None',
                        'title': 'None',
                        'author': '(Internal)',
                        'architecture': 'VAE',
                        'class': 'VAE',
                        'description': 'Use the VAE built-in to your Stable Diffusion model',
                        'preview_image': '/imgs/none.jpg',
                        'is_supported_model_format': true,
                        'local': true,
                        standard_width: 0,
                        standard_height: 0
                    }
                };
                files = [autoFile, noneFile].concat(files);
            }
            callback(data.folders.sort((a, b) => a.localeCompare(b)), files);
            if (fix) {
                fix();
            }
        });
    }

    describeModel(model) {
        let promptBox = getRequiredElementById('alt_prompt_textbox');
        let description = '';
        let buttons = [];
        if (this.subType == 'Stable-Diffusion' && model.data.local) {
            let buttonLoad = () => {
                directSetModel(model.data);
                makeWSRequestT2I('SelectModelWS', {'model': model.data.name}, data => {
                    this.browser.navigate(lastModelDir);
                });
            };
            let buttonRefiner = () => {
                let refinerInput = document.getElementById('input_refinermodel');
                if (!refinerInput) {
                    return;
                }
                forceSetDropdownValue(refinerInput, model.data.name);
                let toggler = document.getElementById('input_group_content_refiner_toggle');
                if (toggler && !toggler.checked) {
                    toggler.click();
                    toggleGroupOpen(toggler, true);
                }
            }
            buttons = [
                { label: 'Load Now', onclick: buttonLoad },
                { label: 'Set as Refiner', onclick: buttonRefiner }
            ];
        }
        else if (this.subType == 'Embedding') {
            buttons = [
                { label: 'Add To Prompt', onclick: () => embedAddToPrompt(model.data, 'alt_prompt_textbox') },
                { label: 'Add To Negative', onclick: () => embedAddToPrompt(model.data, 'alt_negativeprompt_textbox') },
                { label: 'Remove All Usages', onclick: () => { embedClearFromPrompt(model.data, 'alt_prompt_textbox'); embedClearFromPrompt(model.data, 'alt_negativeprompt_textbox'); } }
            ];
        }
        let name = cleanModelName(model.data.name);
        if (this.subType == 'Wildcards') {
            buttons = [
                { label: 'Edit Wildcard', onclick: () => editWildcard(model.data) },
                { label: 'Test Wildcard', onclick: () => testWildcard(model.data) },
                { label: 'Delete Wildcard', onclick: () => {
                    if (confirm("Are you sure want to delete that wildcard?")) {
                        genericRequest('DeleteWildcard', { card: model.data.name }, data => {
                            wildcardsBrowser.browser.refresh();
                        });
                    }
                } }
            ];
            let raw = model.data.raw;
            if (raw.length > 512) {
                raw = raw.substring(0, 512) + '...';
            }
            description = `<span class="wildcard_title">${escapeHtml(name)}</span><br>${escapeHtml(raw)}`;
            let match = matchWildcard(promptBox.value, model.data.name);
            let isSelected = match && match.length > 0;
            let className = isSelected ? 'model-selected' : '';
            let searchable = `${model.data.name}, ${description}`;
            return { name, description, buttons, className, searchable, 'image': model.data.image };
        }
        let isCorrect = this.subType == 'Stable-Diffusion' || isModelArchCorrect(model.data);
        let interject = '';
        if (!isCorrect && this.subType != 'Stable-Diffusion') {
            interject = `<b>(Incompatible with current model!)</b><br>`;
        }
        if (model.data.is_supported_model_format) {
            let getLine = (label, val) => `<b>${label}:</b> <span>${val == null ? "(Unset)" : safeHtmlOnly(val)}</span><br>`;
            let getOptLine = (label, val) => val ? getLine(label, val) : '';
            if (this.subType == 'LoRA' || this.subType == 'Stable-Diffusion') {
                interject += `${getLine("Resolution", `${model.data.standard_width}x${model.data.standard_height}`)}`;
            }
            if (!model.data.local) {
                interject += `<b>(This model is only available on some backends.)</b><br>`;
            }
            description = `<span class="model_filename">${escapeHtml(name)}</span><br>${getLine("Title", model.data.title)}${getOptLine("Author", model.data.author)}${getLine("Type", model.data.class)}${interject}${getOptLine('Trigger Phrase', model.data.trigger_phrase)}${getOptLine('Usage Hint', model.data.usage_hint)}${getLine("Description", model.data.description)}`;
            if (model.data.local) {
                buttons.push({ label: 'Edit Metadata', onclick: () => editModel(model.data, this) });
            }
            if (model.data.local && this.subType == 'Stable-Diffusion' && !model.data.name.endsWith('.engine')) {
                buttons.push({ label: 'Create TensorRT Engine', onclick: () => showTrtMenu(model.data) });
            }
        }
        else {
            let ext = model.data.name.substring(model.data.name.lastIndexOf('.') + 1);
            description = `${escapeHtml(name)}.${ext}<br>(Metadata only available for 'safetensors' models.)<br><b>WARNING:</b> 'ckpt' pickle files can contain malicious code! Use with caution.<br>`;
        }
        let selector = 'current_model';
        switch (this.subType) {
            case 'Stable-Diffusion': selector = 'current_model'; break;
            case 'VAE': selector = 'input_vae'; break;
            case 'LoRA': selector = 'input_loras'; break;
            case 'ControlNet': selector = 'input_controlnetmodel'; break;
        }
        let isSelected;
        let selectorElem = document.getElementById(selector);
        let clean = cleanModelName(model.data.name);
        if (!selectorElem) {
            isSelected = false;
        }
        else if (this.subType == 'VAE' && !document.getElementById('input_vae_toggle').checked) {
            isSelected = model.data.name == 'Automatic';
        }
        else if (this.subType == 'LoRA') {
            isSelected = [...selectorElem.selectedOptions].map(option => option.value).filter(value => value == clean).length > 0;
        }
        else if (this.subType == 'Embedding') {
            isSelected = promptBox.value.includes(`<embed:${clean}>`);
            let negativePrompt = document.getElementById('input_negativeprompt');
            if (negativePrompt) {
                isSelected = isSelected || negativePrompt.value.includes(`<embed:${clean}>`);
            }
        }
        else {
            isSelected = selectorElem.value == clean;
        }
        let className = isSelected ? 'model-selected' : (model.data.loaded ? 'model-loaded' : (!isCorrect ? 'model-unavailable' : ''));
        if (!model.data.local) {
            className += ' model-remote';
        }
        let searchable = `${model.data.name}, ${description}, ${model.data.license}, ${model.data.architecture||'no-arch'}, ${model.data.usage_hint}, ${model.data.trigger_phrase}, ${model.data.merged_from}, ${model.data.tags}`;
        return { name, description, buttons, 'image': model.data.preview_image, className, searchable };
    }

    selectModel(model) {
        this.selectOne(model);
        setTimeout(() => {
            this.browser.rerender();
        }, 50);
    }
}

let sdModelBrowser = new ModelBrowserWrapper('Stable-Diffusion', 'model_list', 'modelbrowser', (model) => { directSetModel(model.data); });
let sdVAEBrowser = new ModelBrowserWrapper('VAE', 'vae_list', 'sdvaebrowser', (vae) => { directSetVae(vae.data); });
let sdLoraBrowser = new ModelBrowserWrapper('LoRA', 'lora_list', 'sdlorabrowser', (lora) => { toggleSelectLora(cleanModelName(lora.data.name)); });
let sdEmbedBrowser = new ModelBrowserWrapper('Embedding', 'embedding_list', 'sdembedbrowser', (embed) => { selectEmbedding(embed.data); });
let sdControlnetBrowser = new ModelBrowserWrapper('ControlNet', 'controlnet_list', 'sdcontrolnetbrowser', (controlnet) => { setControlNet(controlnet.data); });
let wildcardsBrowser = new ModelBrowserWrapper('Wildcards', 'wildcard_list', 'wildcardsbrowser', (wildcard) => { selectWildcard(wildcard.data); }, `<button id="wildcards_list_create_new_button" class="refresh-button" onclick="create_new_wildcard_button()">Create New Wildcard</button>`);

let allModelBrowsers = [sdModelBrowser, sdVAEBrowser, sdLoraBrowser, sdEmbedBrowser, sdControlnetBrowser, wildcardsBrowser];

function matchWildcard(prompt, wildcard) {
    let matcher = new RegExp(`<(wildcard(?:\\[\\d+(?:-\\d+)?\\])?):${regexEscape(wildcard)}>`, 'g');
    return prompt.match(matcher);
}

function selectWildcard(model) {
    let promptBox = getRequiredElementById('alt_prompt_textbox');
    let trimmed = promptBox.value.trim();
    let match = matchWildcard(trimmed, model.name);
    if (match && match.length > 0) {
        let last = match[match.length - 1];
        if (trimmed.endsWith(last.trim())) {
            promptBox.value = trimmed.substring(0, trimmed.length - last.length).trim();
            triggerChangeFor(promptBox);
            return;
        }
    }
    promptBox.value = `${trimmed} <wildcard:${model.name}>`.trim();
    triggerChangeFor(promptBox);
}

function embedClearFromPrompt(model, element) {
    let box = getRequiredElementById(element);
    let chunk = `<embed:${cleanModelName(model.name)}>`;
    box.value = box.value.replace(` ${chunk}`, '').replace(chunk, '').trim();
    triggerChangeFor(box);
    sdEmbedBrowser.browser.rerender();
}

function embedAddToPrompt(model, element) {
    let box = getRequiredElementById(element);
    box.value += ` <embed:${cleanModelName(model.name)}>`;
    triggerChangeFor(box);
    sdEmbedBrowser.browser.rerender();
}

function selectEmbedding(model) {
    let promptBox = getRequiredElementById(model.is_negative_embedding ? 'alt_negativeprompt_textbox' : 'alt_prompt_textbox');
    let chunk = `<embed:${cleanModelName(model.name)}>`;
    if (promptBox.value.endsWith(chunk)) {
        promptBox.value = promptBox.value.substring(0, promptBox.value.length - chunk.length).trim();
    }
    else {
        promptBox.value += ` ${chunk}`;
    }
    triggerChangeFor(promptBox);
}

let lastPromptForEmbedMonitor = {};

function monitorPromptChangeForEmbed(promptText, type) {
    let last = lastPromptForEmbedMonitor[type];
    if (!last) {
        last = "";
    }
    if (promptText == last) {
        return;
    }
    let countEndsNew = promptText.split(`>`).length - 1;
    let countEndsOld = last.split(`>`).length - 1;
    lastPromptForEmbedMonitor[type] = promptText;
    let countNew = promptText.split(`<embed:`).length - 1;
    let countOld = last.split(`<embed:`).length - 1;
    if (countNew != countOld || (countNew > 0 && countEndsNew != countEndsOld)) {
        sdEmbedBrowser.browser.rerender();
    }
    let countNewWc = promptText.split(`<wildcard`).length - 1;
    let countOldWc = last.split(`<wildcard`).length - 1;
    if (countNewWc != countOldWc || (countNewWc > 0 && countEndsNew != countEndsOld)) {
        wildcardsBrowser.browser.rerender();
    }
}

function setControlNet(model) {
    let input = document.getElementById('input_controlnetmodel');
    if (!input) {
        return;
    }
    forceSetDropdownValue(input, cleanModelName(model.name));
    let group = document.getElementById('input_group_content_controlnet_toggle');
    if (group) {
        group.checked = true;
    }
}

function initialModelListLoad() {
    for (let browser of allModelBrowsers) {
        browser.browser.navigate('');
    }
}

function reapplyLoraWeights() {
    let valSet = [...getRequiredElementById('input_loras').selectedOptions].map(option => option.value);
    let weightVal = getRequiredElementById('input_loraweights').value;
    if (!weightVal) {
        return;
    }
    let weights = weightVal.split(',');
    if (weights.length != valSet.length) {
        console.log(`Ignoring invalid LoRA weights value. Have ${valSet.length} LoRAs (${JSON.stringify(valSet)}), but ${weights.length} weights (${weightVal})`);
        return;
    }
    let viewable = [...getRequiredElementById('current_lora_list_view').children];
    for (let i = 0; i < valSet.length; i++) {
        loraWeightPref[valSet[i]] = weights[i];
        let entry = viewable.filter(elem => elem.dataset.lora_name == valSet[i]);
        if (entry.length == 1) {
            entry[0].querySelector('.lora-weight-input').value = weights[i];
        }
    }
}

function updateLoraWeights(doChange = true) {
    let valSet = [...getRequiredElementById('input_loras').selectedOptions].map(option => option.value);
    let inputWeights = getRequiredElementById('input_loraweights');
    inputWeights.value = valSet.map(lora => loraWeightPref[lora] || 1).join(',');
    if (doChange) {
        inputWeights.dispatchEvent(new Event('change'));
    }
    getRequiredElementById('input_loraweights_toggle').checked = valSet.length > 0;
    doToggleEnable('input_loraweights');
}

function updateLoraList() {
    let view = getRequiredElementById('current_lora_list_view');
    let loraElem = document.getElementById('input_loras');
    if (!loraElem) {
        return;
    }
    let currentLoras = [...loraElem.selectedOptions].map(option => option.value);
    view.innerHTML = '';
    for (let lora of currentLoras) {
        let div = createDiv(null, 'preset-in-list');
        div.dataset.lora_name = lora;
        div.innerText = cleanModelName(lora);
        let weightInput = document.createElement('input');
        weightInput.className = 'lora-weight-input';
        weightInput.type = 'number';
        let weightsParam = gen_param_types.find(p => p.id == 'loraweights');
        weightInput.min = weightsParam ? weightsParam.min : -10;
        weightInput.max = weightsParam ? weightsParam.max : 10;
        weightInput.step = weightsParam ? weightsParam.step : 0.1;
        weightInput.value = loraWeightPref[lora] || 1;
        weightInput.addEventListener('change', () => {
            loraWeightPref[lora] = weightInput.value;
            updateLoraWeights();
        });
        weightInput.addEventListener('input', () => {
            loraWeightPref[lora] = weightInput.value;
            updateLoraWeights(false);
        });
        let removeButton = createDiv(null, 'preset-remove-button');
        removeButton.innerHTML = '&times;';
        removeButton.title = "Remove this LoRA";
        removeButton.addEventListener('click', () => {
            toggleSelectLora(lora);
            updateLoraList();
            sdLoraBrowser.browser.rerender();
        });
        div.appendChild(weightInput);
        div.appendChild(removeButton);
        view.appendChild(div);
    }
    getRequiredElementById('current_loras_wrapper').style.display = currentLoras.length > 0 ? 'inline-block' : 'none';
    getRequiredElementById('lora_info_slot').innerText = ` (${currentLoras.length})`;
}

function toggleSelectLora(lora) {
    let loraInput = document.getElementById('input_loras');
    if (!loraInput) {
        showError("Cannot set LoRAs currently. Are you using a custom workflow? LoRAs only work in the default mode.");
        return;
    }
    let selected = [...loraInput.selectedOptions].map(option => option.value);
    if (selected.includes(lora)) {
        selected = selected.filter(l => l != lora);
    }
    else {
        selected.push(lora);
    }
    $(loraInput).val(selected);
    $(loraInput).trigger('change');
    loraInput.dispatchEvent(new Event('change'));
    getRequiredElementById('input_loras_toggle').checked = selected.length > 0;
    doToggleEnable('input_loras');
    updateLoraWeights();
    updateLoraList();
}

function directSetVae(vae) {
    let toggler = getRequiredElementById('input_vae_toggle');
    if (!vae) {
        toggler.checked = false;
        doToggleEnable('input_vae');
        return;
    }
    forceSetDropdownValue('input_vae', cleanModelName(vae.name));
    toggler.checked = true;
    doToggleEnable('input_vae');
}

function directSetModel(model) {
    if (!model) {
        return;
    }
    if (model.name) {
        let clean = cleanModelName(model.name);
        forceSetDropdownValue('input_model', clean);
        forceSetDropdownValue('current_model', clean);
        setCookie('selected_model', `${clean},${model.standard_width},${model.standard_height},${model.architecture},${model.compat_class}`, 90);
        curModelWidth = model.standard_width;
        curModelHeight = model.standard_height;
        curModelArch = model.architecture;
        curModelCompatClass = model.compat_class;
    }
    else if (model.includes(',')) {
        let [name, width, height, arch, compatClass] = model.split(',');
        forceSetDropdownValue('input_model', name);
        forceSetDropdownValue('current_model', name);
        setCookie('selected_model', `${name},${width},${height},${arch},${compatClass}`, 90);
        curModelWidth = parseInt(width);
        curModelHeight = parseInt(height);
        curModelArch = arch;
        curModelCompatClass = compatClass;
    }
    reviseBackendFeatureSet();
    getRequiredElementById('input_model').dispatchEvent(new Event('change'));
    currentAutomaticVae = 'None';
    if (curModelArch) {
        let setting = null;
        if (curModelArch.startsWith('stable-diffusion-xl-v1')) {
            setting = document.getElementById('usersettings_vaes.defaultsdxlvae');
        }
        else if (curModelArch.startsWith('stable-diffusion-v1')) {
            setting = document.getElementById('usersettings_vaes.defaultsdv1vae');
        }
        if (setting) {
            currentAutomaticVae = setting.value;
        }
    }
    let aspect = document.getElementById('input_aspectratio');
    if (aspect) {
        aspect.dispatchEvent(new Event('change'));
    }
    for (let browser of allModelBrowsers) {
        browser.browser.update();
    }
}

function setCurrentModel(callback) {
    let currentModel = getRequiredElementById('current_model');
    if (currentModel.value == '') {
        genericRequest('ListLoadedModels', {}, data => {
            if (data.models.length > 0) {
                directSetModel(data.models[0]);
            }
            if (callback) {
                callback();
            }
        });
    }
    else {
        if (callback) {
            callback();
        }
    }
}

function showTrtMenu(model) {
    if (!currentBackendFeatureSet.includes('tensorrt')) {
        getRequiredElementById('tensorrt_mustinstall').style.display = '';
        getRequiredElementById('tensorrt_modal_ready').style.display = 'none';
    }
    else {
        getRequiredElementById('tensorrt_mustinstall').style.display = 'none';
        getRequiredElementById('tensorrt_modal_ready').style.display = '';
    }
    getRequiredElementById('tensorrt_create_result_box').innerText = '';
    let modelSelect = getRequiredElementById('tensorrt_model_select');
    modelSelect.innerHTML = '';
    for (let model of allModels.filter(m => m.endsWith('.safetensors'))) {
        let option = document.createElement('option');
        let clean = cleanModelName(model);
        option.value = clean;
        option.innerText = clean;
        modelSelect.appendChild(option);
    }
    modelSelect.value = cleanModelName(model.name);
    $('#create_tensorrt_modal').modal('show');
}

function close_trt_modal() {
    $('#create_tensorrt_modal').modal('hide');
}

function trt_modal_create() {
    let modelSelect = getRequiredElementById('tensorrt_model_select');
    let aspectSelect = getRequiredElementById('tensorrt_aspect_ratio');
    let rangeSelect = getRequiredElementById('tensorrt_aspect_range');
    let batchSize = getRequiredElementById('tensorrt_batch_size');
    let maxBatch = getRequiredElementById('tensorrt_max_batch_size');
    let createButton = getRequiredElementById('trt_create_button');
    let resultBox = getRequiredElementById('tensorrt_create_result_box');
    let data = {
        'model': modelSelect.value,
        'aspect': aspectSelect.value,
        'aspectRange': rangeSelect.value,
        'optBatch': batchSize.value,
        'maxBatch': maxBatch.value
    };
    createButton.disabled = true;
    resultBox.innerText = 'Creating TensorRT engine, please wait...';
    makeWSRequest('DoTensorRTCreateWS', data, data => {
        resultBox.innerText = data.status;
        if (data.complete) {
            setTimeout(() => {
                createButton.disabled = false;
                $('#create_tensorrt_modal').modal('hide');
            }, 2000);
        }
    }, 0, err => {
        createButton.disabled = false;
        resultBox.innerText = `Error: ${err}`;
    });
}

let noModelChangeDup = false;

function currentModelChanged() {
    if (noModelChangeDup) {
        return;
    }
    let name = getRequiredElementById('current_model').value;
    if (name == '') {
        return;
    }
    genericRequest('DescribeModel', {'modelName': name}, data => {
        noModelChangeDup = true;
        directSetModel(data.model);
        noModelChangeDup = false;
    });
}

getRequiredElementById('current_model').addEventListener('change', currentModelChanged);
