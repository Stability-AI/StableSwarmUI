let gen_param_types = null;

let session_id = null;

let batches = 0;

let lastImageDir = '';

let lastModelDir = '';

let input_overrides = {};

let num_current_gens = 0, num_models_loading = 0, num_live_gens = 0, num_backends_waiting = 0;

const time_started = Date.now();

function clickImageInBatch(div) {
    setCurrentImage(div.getElementsByTagName('img')[0].src);
}

function selectImageInHistory(div) {
    let batchId = div.dataset.batch_id;
    document.getElementById('current_image_batch').innerHTML = '';
    for (let img of document.getElementById('image_history').querySelectorAll(`[data-batch_id="${batchId}"]`)) {
        let batch_div = appendImage('current_image_batch', img.getElementsByTagName('img')[0].src, batchId, '(TODO)');
        batch_div.addEventListener('click', () => clickImageInBatch(batch_div));
    }
    setCurrentImage(div.getElementsByTagName('img')[0].src);
}

function upscale_current_image() {
    let currentImage = document.getElementById('current_image');
    let actualImage = currentImage.getElementsByTagName('img')[0];
    toDataURL(actualImage.src, (url => {
        input_overrides['initimage'] = url;
        input_overrides['width'] = actualImage.width * 2;
        input_overrides['height'] = actualImage.height * 2;
        doGenerate();
    }));
}

function star_current_image() {
    alert('Stars are TODO');
}

function copy_current_image_params() {
    alert('TODO');
}

function setCurrentImage(src) {
    let curImg = document.getElementById('current_image');
    curImg.innerHTML = '';
    let img = document.createElement('img');
    img.src = src;
    curImg.appendChild(img);
    let buttons = createDiv(null, 'current-image-buttons');
    buttons.innerHTML = `<button class="cur-img-button" onclick="javascript:upscale_current_image()">Upscale 2x</button>
    <button class="cur-img-button" onclick="javascript:star_current_image()">Star</button>
    <button class="cur-img-button" onclick="javascript:copy_current_image_params()">Copy Parameters</button>`;
    curImg.appendChild(buttons);
    let data = createDiv(null, 'current-image-data');
    data.innerText = "TODO: Metadata";
    curImg.appendChild(data);
}

function appendImage(container, imageSrc, batchId, textPreview) {
    if (typeof container == 'string') {
        container = document.getElementById(container);
    }
    let div = createDiv(null, `image-block image-batch-${batchId == "folder" ? "folder" : (batchId % 2)}`);
    div.dataset.batch_id = batchId;
    let img = document.createElement('img');
    img.addEventListener('load', () => {
        let ratio = img.width / img.height;
        if (batchId != "folder") {
            div.style.width = `${(ratio * 8) + 2}rem`;
        }
    });
    img.src = imageSrc;
    div.appendChild(img);
    let textBlock = createDiv(null, 'image-preview-text');
    textBlock.innerText = textPreview;
    div.appendChild(textBlock);
    container.appendChild(div);
    return div;
}

function gotImageResult(image) {
    updateGenCount();
    let src = image;
    let batch_div = appendImage('current_image_batch', src, batches, '(TODO)');
    batch_div.addEventListener('click', () => clickImageInBatch(batch_div));
    let history_div = appendImage('image_history', src, batches, '(TODO)');
    history_div.addEventListener('click', () => selectImageInHistory(history_div));
    setCurrentImage(src);
}

function getGenInput() {
    let input = {};
    for (let type of gen_param_types) {
        if (type.toggleable && !document.getElementById(`input_${type.id}_toggle`).checked) {
            continue;
        }
        let elem = document.getElementById('input_' + type.id);
        if (type.type == "boolean") {
            input[type.id] = elem.checked;
        }
        else if (type.type == "image") {
            if (elem.dataset.filedata) {
                input[type.id] = elem.dataset.filedata;
            }
        }
        else {
            input[type.id] = elem.value;
        }
    }
    input["presets"] = currentPresets.map(p => p.title);
    for (let key in input_overrides) {
        input[key] = input_overrides[key];
    }
    return input;
}

function updateCurrentStatusDirect(data) {
    if (data) {
        num_current_gens = data.waiting_gens;
        num_models_loading = data.loading_models;
        num_live_gens = data.live_gens;
        num_backends_waiting = data.waiting_backends;
    }
    let elem = document.getElementById('num_jobs_span');
    function autoBlock(num, text) {
        if (num == 0) {
            return '';
        }
        return `<span class="interrupt-line-part">${num} ${text.replaceAll('%', autoS(num))},</span> `;
    }
    elem.innerHTML = `${autoBlock(num_current_gens, 'current generation%')}${autoBlock(num_live_gens, 'running')}${autoBlock(num_backends_waiting, 'queued')}${autoBlock(num_models_loading, 'waiting on model load')} ...`;
}

let doesHaveGenCountUpdateQueued = false;

function updateGenCount() {
    updateCurrentStatusDirect(null);
    if (doesHaveGenCountUpdateQueued) {
        return;
    }
    doesHaveGenCountUpdateQueued = true;
    setTimeout(() => {
        genericRequest('GetCurrentStatus', {}, data => {
            doesHaveGenCountUpdateQueued = false;
            updateCurrentStatusDirect(data.status);
        });
    }, 500);
}

function makeWSRequestT2I(url, in_data, callback) {
    makeWSRequest(url, in_data, data => {
        if (data.status) {
            updateCurrentStatusDirect(data.status);
        }
        else {
            callback(data);
        }
    });
}

function doInterrupt() {
    genericRequest('InterruptAll', {}, data => {
        updateGenCount();
    });
}

function doGenerate() {
    if (session_id == null) {
        if (Date.now() - time_started > 1000 * 60) {
            showError("Cannot generate, session not started. Did the server crash?");
        }
        else {
            showError("Cannot generate, session not started. Please wait a moment for the page to load.");
        }
        return;
    }
    num_current_gens += parseInt(document.getElementById('input_images').value);
    setCurrentModel(() => {
        if (document.getElementById('current_model').innerText == '') {
            showError("Cannot generate, no model selected.");
            return;
        }
        document.getElementById('current_image_batch').innerHTML = '';
        batches++;
        makeWSRequestT2I('GenerateText2ImageWS', getGenInput(), data => {
            gotImageResult(data.image);
        });
    });
}

class FileListCallHelper {
    // Attempt to prevent callback recursion.
    // In practice this seems to not work.
    // Either JavaScript itself or Firefox seems to really love tracking the stack and refusing to let go.
    // TODO: Guarantee it actually works so we can't stack overflow from file browsing ffs.
    constructor(path, loadCaller) {
        this.path = path;
        this.loadCaller = loadCaller;
    }
    call() {
        this.loadCaller(this.path);
    }
};

function loadFileList(api, path, container, loadCaller, fileCallback, endCallback, sortFunc) {
    genericRequest(api, {'path': path}, data => {
        let prefix;
        if (path == '') {
            prefix = '';
        }
        else {
            prefix = path + '/';
            let above = path.split('/').slice(0, -1).join('/');
            let div = appendImage(container, '/imgs/folder_up.png', 'folder', `../`);
            let helper = new FileListCallHelper(above, loadCaller);
            div.addEventListener('click', helper.call.bind(helper));
        }
        for (let folder of data.folders.sort()) {
            let div = appendImage(container, '/imgs/folder.png', 'folder', `${folder}/`);
            let helper = new FileListCallHelper(`${prefix}${folder}`, loadCaller);
            div.addEventListener('click', helper.call.bind(helper));
        }
        container.appendChild(document.createElement('br'));
        for (let file of sortFunc(data.files)) {
            fileCallback(prefix, file);
        }
        if (endCallback) {
            endCallback();
        }
    });
}

function loadHistory(path) {
    let container = document.getElementById('image_history');
    lastImageDir = path;
    container.innerHTML = '';
    loadFileList('ListImages', path, container, loadHistory, (prefix, img) => {
        let div = appendImage('image_history', `Output/${prefix}${img.src}`, img.batch_id, img.src);
        div.addEventListener('click', () => selectImageInHistory(div));
    }, null, (list) => list.sort((a, b) => b.src.toLowerCase().localeCompare(a.src.toLowerCase())).sort((a, b) => b.batch_id - a.batch_id));
}

let models = {};
let cur_model = null;

let curModelMenuModel = null;

function modelMenuDoLoadNow() {
    if (curModelMenuModel == null) {
        return;
    }
    document.getElementById('input_model').value = curModelMenuModel.name;
    document.getElementById('current_model').innerText = curModelMenuModel.name;
    makeWSRequestT2I('SelectModelWS', {'model': curModelMenuModel.name}, data => {
        loadModelList(lastModelDir);
    });
}

function modelMenuDoEdit() {
    let model = curModelMenuModel;
    if (model == null) {
        console.log("Model do edit: no model");
        return;
    }
    let imageInput = document.getElementById('edit_model_image');
    imageInput.innerHTML = '';
    let enableImage = document.getElementById('edit_model_enable_image');
    enableImage.checked = false;
    enableImage.disabled = true;
    let curImg = document.getElementById('current_image').getElementsByTagName('img')[0];
    if (curImg) {
        let newImg = curImg.cloneNode(true);
        imageInput.appendChild(newImg);
        enableImage.checked = true;
        enableImage.disabled = false;
    }
    document.getElementById('edit_model_name').value = model.title == null ? model.name : model.title;
    document.getElementById('edit_model_author').value = model.author == null ? '' : model.author;
    document.getElementById('edit_model_type').value = model.class == null ? '' : model.class;
    document.getElementById('edit_model_resolution').value = `${model.standard_width}x${model.standard_height}`;
    document.getElementById('edit_model_description').value = model.description == null ? '' : model.description;
    $('#edit_model_modal').modal('show');
}

function save_edit_model() {
    let model = curModelMenuModel;
    if (model == null) {
        console.log("Model do save: no model");
        return;
    }
    let resolution = document.getElementById('edit_model_resolution').value.split('x');
    let data = {
        'model': model.name,
        'title': document.getElementById('edit_model_name').value,
        'author': document.getElementById('edit_model_author').value,
        'type': document.getElementById('edit_model_type').value,
        'description': document.getElementById('edit_model_description').value,
        'standard_width': parseInt(resolution[0]),
        'standard_height': parseInt(resolution[1]),
        'preview_image': ''
    };
    if (document.getElementById('edit_model_enable_image').checked) {
        let img = document.getElementById('edit_model_image').getElementsByTagName('img')[0].src;
        let index = img.indexOf('/Output/');
        if (index != -1) {
            img = img.substring(index);
        }
        data['preview_image'] = img;
    }
    genericRequest('EditModelMetadata', data, data => {
        loadModelList(lastModelDir);
    });
    $('#edit_model_modal').modal('hide');
}

function close_edit_model() {
    $('#edit_model_modal').modal('hide');
}

function appendModel(container, prefix, model) {
    models[`${prefix}${model.name}`] = model;
    let batch = document.getElementById('current_model').innerText == model.name ? 'model-selected' : (model.loaded ? 'model-loaded' : `image-batch-${Object.keys(models).length % 2}`);
    let div = createDiv(null, `model-block model-block-hoverable ${batch}`);
    let img = document.createElement('img');
    img.src = model.preview_image;
    div.appendChild(img);
    let textBlock = createDiv(null, 'model-descblock');
    if (model.is_safetensors) {
        let getLine = (label, val) => `<b>${label}:</b> ${val == null ? "(Unset)" : escapeHtml(val)}<br>`;
        textBlock.innerHTML = `${escapeHtml(model.name)}<br>${getLine("Title", model.title)}${getLine("Author", model.author)}${getLine("Type", model.class)}${getLine("Resolution", `${model.standard_width}x${model.standard_height}`)}${getLine("Description", model.description)}`;
    }
    else {
        textBlock.innerHTML = `${escapeHtml(model.name)}<br>(Metadata only available for 'safetensors' models.)<br><b>WARNING:</b> 'ckpt' pickle files can contain malicious code! Use with caution.<br>`;
    }
    div.appendChild(textBlock);
    container.appendChild(div);
    let menu = createDiv(null, 'model-block-menu-button');
    menu.innerText = '⬤⬤⬤';
    menu.addEventListener('click', () => {
        curModelMenuModel = model;
        doPopover('modelmenu');
    });
    div.appendChild(menu);
    img.addEventListener('click', () => {
        document.getElementById('input_model').value = model.name;
        document.getElementById('current_model').innerText = model.name;
        loadModelList(lastModelDir);
    });
}

function sortModelName(a, b) {
    let aName = a.name.toLowerCase();
    let bName = b.name.toLowerCase();
    if (aName.endsWith('.safetensors') && !bName.endsWith('.safetensors')) {
        return -1;
    }
    if (!aName.endsWith('.safetensors') && bName.endsWith('.safetensors')) {
        return 1;
    }
    return aName.localeCompare(bName);
}

function loadModelList(path, isRefresh = false) {
    let container = document.getElementById('model_list');
    lastModelDir = path;
    container.innerHTML = '';
    models = {};
    call = () => loadFileList('ListModels', path, container, loadModelList, (prefix, model) => {
        appendModel(container, prefix, model);
    }, () => {
        let current_model = document.getElementById('current_model');
        if (current_model.innerText == '') {
            let model = Object.values(models).find(m => m.loaded);
            if (model) {
                document.getElementById('input_model').value = model.name;
                current_model.innerText = model.name;
            }
        }
    }, (list) => list.sort(sortModelName));
    if (isRefresh) {
        genericRequest('TriggerRefresh', {}, data => {
            for (let param of data.list) {
                let origParam = gen_param_types.find(p => p.id == param.id);
                if (origParam) {
                    origParam.values = param.values;
                    if (origParam.type == "dropdown") {
                        let dropdown = document.getElementById(`input_${param.id}`);
                        let val = dropdown.value;
                        let html = '';
                        for (let value of param.values) {
                            let selected = value == val ? ' selected="true"' : '';
                            html += `<option value="${escapeHtml(value)}"${selected}>${escapeHtml(value)}</option>`;
                        }
                        dropdown.innerHTML = html;
                    }
                }
            }
            call();
        });
    }
    else {
        call();
    }
}

function toggle_advanced() {
    let advancedArea = document.getElementById('main_inputs_area_advanced');
    let toggler = document.getElementById('advanced_options_checkbox');
    advancedArea.style.display = toggler.checked ? 'block' : 'none';
}
function toggle_advanced_checkbox_manual() {
    let toggler = document.getElementById('advanced_options_checkbox');
    toggler.checked = !toggler.checked;
    toggle_advanced();
}

let statusBarElem = document.getElementById('top_status_bar');

function getCurrentStatus() {
    if (!hasLoadedBackends) {
        return ['warn', 'Loading...'];
    }
    if (Object.values(backends_loaded).length == 0) {
        return ['warn', 'No backends present. You must configure backends on the Settings page before you can continue.'];
    }
    let loading = countBackendsByStatus('waiting') + countBackendsByStatus('loading');
    if (countBackendsByStatus('running') == 0) {
        if (loading > 0) {
            return ['warn', 'Backends are still loading on the server...'];
        }
        if (countBackendsByStatus('errored') > 0) {
            return ['error', 'Some backends have errored on the server. Check the server logs for details.'];
        }
        if (countBackendsByStatus('disabled') > 0) {
            return ['warn', 'Some backends are disabled. Please configure them to continue.'];
        }
        return ['error', 'Something is wrong with your backends. Please check the Backends Settings page or the server logs.'];
    }
    if (loading > 0) {
        return ['soft', 'Some backends are ready, but others are still loading...'];
    }
    return ['', ''];
}

function reviseStatusBar() {
    let status = getCurrentStatus();
    statusBarElem.innerText = status[1];
    statusBarElem.className = `top-status-bar status-bar-${status[0]}`;
}

function genpageLoop() {
    backendLoopUpdate();
    reviseStatusBar();
}

let mouseX, mouseY;
let popHide = [];

document.addEventListener('click', (e) => {
    mouseX = e.pageX;
    mouseY = e.pageY;
    for (let id of popHide) {
        let pop = document.getElementById(`popover_${id}`);
        pop.style.display = 'none';
        pop.dataset.visible = "false";
    }
    popHide = [];
}, true);

function doPopover(id) {
    let pop = document.getElementById(`popover_${id}`);
    if (pop.dataset.visible == "true") {
        pop.style.display = 'none';
        pop.dataset.visible = "false";
        popHide.splice(popHide.indexOf(id), 1);
    }
    else {
        pop.style.display = 'block';
        pop.dataset.visible = "true";
        pop.style.left = `${mouseX}px`;
        pop.style.top = `${mouseY}px`;
        popHide.push(id);
    }
}

function getHtmlForParam(param, prefix) {
    // Actual HTML popovers are too new at time this code was written (experimental status, not supported on most browsers)
    let example = param.examples ? `<br><br>Examples: <code>${param.examples.map(escapeHtml).join("</code>,&emsp;<code>")}</code>` : '';
    let pop = `<div class="sui-popover" id="popover_${prefix}${param.id}"><b>${escapeHtml(param.name)}</b> (${param.type}):<br>&emsp;${escapeHtml(param.description)}${example}</div>`;
    switch (param.type) {
        case 'text':
            return makeTextInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, 2, param.description, param.toggleable) + pop;
        case 'decimal':
        case 'integer':
            let min = param.min;
            let max = param.max;
            if (min == 0 && max == 0) {
                min = -9999999;
                max = 9999999;
            }
            switch (param.number_view_type) {
                case 'small':
                    return makeNumberInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, param.min, param.max, param.step, true, param.toggleable) + pop;
                case 'big':
                    return makeNumberInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, param.min, param.max, param.step, false, param.toggleable) + pop;
                case 'slider':
                    let val = makeSliderInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, param.min, param.max, param.step, false, param.toggleable) + pop;
                    return val;
                case 'pot_slider':
                    return makeSliderInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, param.min, param.max, param.step, true, param.toggleable) + pop;
            }
        case 'boolean':
            return makeCheckboxInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, param.toggleable) + pop;
        case 'dropdown':
            return makeDropdownInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.values, param.default, param.toggleable) + pop;
        case 'image':
            return makeImageInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.toggleable) + pop;
    }
    console.log(`Cannot generate input for param ${param.id} of type ${param.type} - unknown type`);
    return null;
}

function toggleGroupOpen(elem) {
    // ⮟⮞
    let group = elem.parentElement.getElementsByClassName('input-group-content')[0];
    if (group.style.display == 'none') {
        group.style.display = 'block';
        elem.innerText = '⮟' + elem.innerText.replaceAll('⮞', '');
    }
    else {
        group.style.display = 'none';
        elem.innerText = '⮞' + elem.innerText.replaceAll('⮟', '');
    }
}

function genInputs() {
    for (let areaData of [['main_inputs_area', 'new_preset_modal_inputs', (p) => p.visible && !p.advanced],
            ['main_inputs_area_advanced', 'new_preset_modal_advanced_inputs', (p) => p.visible && p.advanced],
            ['main_inputs_area_hidden', null, (p) => !p.visible]]) {
        let area = document.getElementById(areaData[0]);
        let presetArea = areaData[1] ? document.getElementById(areaData[1]) : null;
        let html = '', presetHtml = '';
        let lastGroup = null;
        let groupsClose = [];
        let groupId = 0;
        for (let param of gen_param_types.filter(areaData[2])) {
            if (param.group != lastGroup) {
                if (lastGroup) {
                    html += '</div></div>';
                    if (presetArea) {
                        presetHtml += '</div></div>';
                    }
                }
                if (param.group) {
                    groupId++;
                    if (!param.group_open) {
                        groupsClose.push(groupId);
                    }
                    html += `<div class="input-group"><span id="input_group_${groupId}" onclick="toggleGroupOpen(this)" class="input-group-header">⮟${escapeHtml(param.group)}</span><div class="input-group-content">`;
                    if (presetArea) {
                        presetHtml += `<div class="input-group"><span id="input_group_preset_${groupId}" onclick="toggleGroupOpen(this)" class="input-group-header">⮟${escapeHtml(param.group)}</span><div class="input-group-content">`;
                    }
                }
                lastGroup = param.group;
            }
            html += getHtmlForParam(param, "input_");
            if (param.visible) { // Hidden excluded from presets.
                let presetParam = JSON.parse(JSON.stringify(param));
                presetParam.toggleable = true;
                presetHtml += getHtmlForParam(presetParam, "preset_input_");
            }
        }
        area.innerHTML = html;
        enableSlidersIn(area);
        if (presetArea) {
            presetArea.innerHTML = presetHtml;
            enableSlidersIn(presetArea);
        }
        for (let group of groupsClose) {
            let elem = document.getElementById(`input_group_${group}`);
            toggleGroupOpen(elem);
            let pelem = document.getElementById(`input_group_preset_${group}`);
            if (pelem) {
                toggleGroupOpen(pelem);
            }
        }
    }
    for (let param of gen_param_types) {
        if (param.toggleable) {
            doToggleEnable(`input_${param.id}`);
        }
    }
    let inputWidth = document.getElementById('input_width');
    let inputWidthSlider = document.getElementById('input_width_rangeslider');
    let inputHeight = document.getElementById('input_height');
    let inputHeightSlider = document.getElementById('input_height_rangeslider');
    let resGroupLabel = findParentOfClass(inputWidth, 'input-group').getElementsByClassName('input-group-header')[0];
    let resTrick = () => {
        resGroupLabel.innerText = resGroupLabel.innerText[0] + `Resolution: ${describeAspectRatio(inputWidth.value, inputHeight.value)} (${inputWidth.value}x${inputHeight.value})`;
    };
    for (let target of [inputWidth, inputWidthSlider, inputHeight, inputHeightSlider]) {
        target.addEventListener('input', resTrick);
    }
    resTrick();
}

let toolSelector = document.getElementById('tool_selector');
let toolContainer = document.getElementById('tool_container');

function genToolsList() {
    toolSelector.value = '';
    // TODO: Dynamic-from-server option list generation
    toolSelector.addEventListener('change', () => {
        for (let opened of toolContainer.getElementsByClassName('tool-open')) {
            opened.classList.remove('tool-open');
        }
        let tool = toolSelector.value;
        if (tool == '') {
            return;
        }
        let div = document.getElementById(`tool_${tool}`);
        div.classList.add('tool-open');
    });
}

function registerNewTool(id, name) {
    let option = document.createElement('option');
    option.value = id;
    option.innerText = name;
    toolSelector.appendChild(option);
    let div = createDiv(`tool_${id}`, 'tool');
    toolContainer.appendChild(div);
    return div;
}

let sessionReadyCallbacks = [];

function setCurrentModel(callback) {
    let currentModel = document.getElementById('current_model');
    if (currentModel.innerText == '') {
        genericRequest('ListLoadedModels', {}, data => {
            if (data.models.length > 0) {
                currentModel.innerText = data.models[0].name;
                document.getElementById('input_model').value = data.models[0].name;
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

function pageSizer() {
    let topSplit = document.getElementById('t2i-top-split-bar');
    let topSplit2 = document.getElementById('t2i-top-2nd-split-bar');
    let midSplit = document.getElementById('t2i-mid-split-bar');
    let topBar = document.getElementById('t2i_top_bar');
    let bottomBarContent = document.getElementById('t2i_bottom_bar_content');
    let inputSidebar = document.getElementById('input_sidebar');
    let mainInputsAreaWrapper = document.getElementById('main_inputs_area_wrapper');
    let mainImageArea = document.getElementById('main_image_area');
    let currentImage = document.getElementById('current_image');
    let currentImageBatch = document.getElementById('current_image_batch');
    let topDrag = false;
    let topDrag2 = false;
    let midDrag = false;
    topSplit.addEventListener('mousedown', (e) => {
        topDrag = true;
        e.preventDefault();
    }, true);
    topSplit2.addEventListener('mousedown', (e) => {
        topDrag2 = true;
        e.preventDefault();
    }, true);
    midSplit.addEventListener('mousedown', (e) => {
        midDrag = true;
        e.preventDefault();
    }, true);
    document.addEventListener('mousemove', (e) => {
        let offX = e.pageX - 5;
        offX = Math.min(Math.max(offX, 100), window.innerWidth - 100);
        if (topDrag) {
            inputSidebar.style.width = `${offX}px`;
            mainInputsAreaWrapper.style.width = `${offX}px`;
            mainImageArea.style.width = `calc(100vw - ${offX}px)`;
            currentImageBatch.style.width = `calc(100vw - ${offX}px - min(max(40vw, 28rem), 49vh))`;
        }
        if (topDrag2) {
            let adaptedX = offX - inputSidebar.getBoundingClientRect().width - 17;
            adaptedX = Math.min(Math.max(adaptedX, 100), window.innerWidth - 100);
            currentImage.style.width = `${adaptedX}px`;
            currentImageBatch.style.width = `calc(100vw - ${offX}px)`;
        }
        if (midDrag) {
            let topY = currentImageBatch.getBoundingClientRect().top;
            let offY = (e.pageY - topY - 2) / window.innerHeight * 100;
            offY = Math.min(Math.max(offY, 5), 95);
            topSplit.style.height = `${offY}vh`;
            topSplit2.style.height = `${offY}vh`;
            inputSidebar.style.height = `${offY}vh`;
            mainInputsAreaWrapper.style.height = `calc(${offY}vh - 7rem)`;
            mainImageArea.style.height = `${offY}vh`;
            currentImage.style.height = `${offY}vh`;
            currentImageBatch.style.height = `calc(${offY}vh - 2rem)`;
            topBar.style.height = `${offY}vh`;
            let invOff = 100 - offY;
            bottomBarContent.style.height = `calc(${invOff}vh - 2rem)`;
        }
    });
    document.addEventListener('mouseup', (e) => {
        topDrag = false;
        topDrag2 = false;
        midDrag = false;
    });
}


function loadUserData() {
    genericRequest('GetMyUserData', {}, data => {
        document.getElementById('preset_list').innerHTML = '';
        for (let preset of data.presets) {
            addPreset(preset);
        }
    });
}

function genpageLoad() {
    console.log('Load page...');
    pageSizer();
    reviseStatusBar();
    getSession(() => {
        console.log('First session loaded - prepping page.');
        loadHistory('');
        loadModelList('');
        loadBackendTypesMenu();
        genericRequest('ListT2IParams', {}, data => {
            gen_param_types = data.list.sort((a, b) => a.priority - b.priority);
            genInputs(data);
            genToolsList();
            reviseStatusBar();
            toggle_advanced();
            setCurrentModel();
            loadUserData();
            document.getElementById('generate_button').addEventListener('click', doGenerate);
            document.getElementById('image_history_refresh_button').addEventListener('click', () => loadHistory(lastImageDir));
            document.getElementById('model_list_refresh_button').addEventListener('click', () => loadModelList(lastModelDir, true));
            for (let callback of sessionReadyCallbacks) {
                callback();
            }
        });
    });
    setInterval(genpageLoop, 1000);
}

setTimeout(genpageLoad, 1);
