let gen_param_types = null, rawGenParamTypesFromServer = null;

let session_id = null;

let batches = 0;

let lastImageDir = '';

let lastModelDir = '';

let input_overrides = {};

let num_current_gens = 0, num_models_loading = 0, num_live_gens = 0, num_backends_waiting = 0;

let shouldApplyDefault = false;

let sessionReadyCallbacks = [];

let allModels = [];

const time_started = Date.now();

let statusBarElem = getRequiredElementById('top_status_bar');

function clickImageInBatch(div) {
    setCurrentImage(div.getElementsByTagName('img')[0].src, div.dataset.metadata);
}

function selectImageInHistory(div) {
    let batchId = div.dataset.batch_id;
    getRequiredElementById('current_image_batch').innerHTML = '';
    for (let img of getRequiredElementById('image_history').querySelectorAll(`[data-batch_id="${batchId}"]`)) {
        let batch_div = appendImage('current_image_batch', img.getElementsByTagName('img')[0].src, batchId, img.dataset.preview_text, img.dataset.metadata);
        batch_div.addEventListener('click', () => clickImageInBatch(batch_div));
    }
    setCurrentImage(div.getElementsByTagName('img')[0].src, div.dataset.metadata);
}

function upscale_current_image() {
    let actualImage = getRequiredElementById('current_image_img');
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

let currentMetadataVal = null;

function copy_current_image_params() {
    if (!currentMetadataVal) {
        alert('No parameters to copy!');
        return;
    }
    let metadata = JSON.parse(currentMetadataVal).sui_image_params;
    for (let param of gen_param_types) {
        let elem = document.getElementById(`input_${param.id}`);
        if (elem && metadata[param.id]) {
            if (param.type == "boolean") {
                elem.checked = metadata[param.id] == "true";
            }
            else {
                elem.value = metadata[param.id];
            }
            if (param.toggleable && param.visible) {
                let toggle = getRequiredElementById(`input_${param.id}_toggle`);
                toggle.checked = true;
                doToggleEnable(elem.id);
            }
        }
        else if (elem && param.toggleable && param.visible) {
            let toggle = getRequiredElementById(`input_${param.id}_toggle`);
            toggle.checked = false;
            doToggleEnable(elem.id);
        }
    }
}

function formatMetadata(metadata) {
    if (!metadata) {
        return '';
    }
    let data = JSON.parse(metadata).sui_image_params;
    let result = '';
    function appendObject(obj) {
        for (let key of Object.keys(obj)) {
            let val = obj[key];
            if (val) {
                if (typeof val == 'object') {
                    result += `<span class="param_view_block"><span class="param_view_name">${escapeHtml(key)}</span>: `;
                    appendObject(val);
                    result += `</span>, `;
                }
                else {
                    result += `<span class="param_view_block"><span class="param_view_name">${escapeHtml(key)}</span>: <span class="param_view">${escapeHtml(`${val}`)}</span></span>, `;
                }
            }
        }
    };
    appendObject(data);
    return result;
}

function setCurrentImage(src, metadata = '') {
    let curImg = getRequiredElementById('current_image');
    curImg.innerHTML = '';
    let img = document.createElement('img');
    img.id = 'current_image_img';
    img.src = src;
    curImg.appendChild(img);
    currentMetadataVal = metadata;
    let buttons = createDiv(null, 'current-image-buttons');
    buttons.innerHTML = `<button class="basic-button" onclick="javascript:upscale_current_image()">Upscale 2x</button>
    <button class="basic-button" onclick="javascript:star_current_image()">Star</button>
    <button class="basic-button" onclick="javascript:copy_current_image_params()">Copy Parameters</button>`;
    curImg.appendChild(buttons);
    let data = createDiv(null, 'current-image-data');
    data.innerHTML = formatMetadata(metadata);
    curImg.appendChild(data);
}

function appendImage(container, imageSrc, batchId, textPreview, metadata = '', type = 'legacy') {
    if (typeof container == 'string') {
        container = getRequiredElementById(container);
    }
    let div = createDiv(null, `image-block image-block-${type} image-batch-${batchId == "folder" ? "folder" : (batchId % 2)}`);
    div.dataset.batch_id = batchId;
    div.dataset.preview_text = textPreview;
    div.dataset.metadata = metadata;
    let img = document.createElement('img');
    img.addEventListener('load', () => {
        let ratio = img.width / img.height;
        if (batchId != "folder") {
            div.style.width = `${(ratio * 8) + 2}rem`;
        }
    });
    img.src = imageSrc;
    div.appendChild(img);
    if (type == 'legacy') {
        let textBlock = createDiv(null, 'image-preview-text');
        textBlock.innerText = textPreview;
        div.appendChild(textBlock);
    }
    container.appendChild(div);
    return div;
}

function gotImageResult(image, metadata) {
    updateGenCount();
    let src = image;
    let fname = src.includes('/') ? src.substring(src.lastIndexOf('/') + 1) : src;
    let batch_div = appendImage('current_image_batch', src, batches, fname, metadata, 'batch');
    batch_div.addEventListener('click', () => clickImageInBatch(batch_div));
    setCurrentImage(src, metadata);
    return batch_div;
}

function updateCurrentStatusDirect(data) {
    if (data) {
        num_current_gens = data.waiting_gens;
        num_models_loading = data.loading_models;
        num_live_gens = data.live_gens;
        num_backends_waiting = data.waiting_backends;
    }
    let elem = getRequiredElementById('num_jobs_span');
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
    num_current_gens += parseInt(getRequiredElementById('input_images').value);
    setCurrentModel(() => {
        if (getRequiredElementById('current_model').innerText == '') {
            showError("Cannot generate, no model selected.");
            return;
        }
        getRequiredElementById('current_image_batch').innerHTML = '';
        batches++;
        let images = {};
        makeWSRequestT2I('GenerateText2ImageWS', getGenInput(), data => {
            if (data.image) {
                let batch_div = gotImageResult(data.image, data.metadata);
                images[data.index] = [batch_div, data.image, data.metadata];
            }
            if (data.discard_indices) {
                let needsNew = false;
                for (let index of data.discard_indices) {
                    let [batch_div, image, metadata] = images[index];
                    batch_div.remove();
                    let curImgElem = document.getElementById('current_image_img');
                    if (curImgElem && curImgElem.src == image) {
                        needsNew = true;
                        delete images[index];
                    }
                }
                if (needsNew) {
                    let img = Object.values(images);
                    if (img.length > 0) {
                        setCurrentImage(img[0][2], img[0][3]);
                    }
                }
            }
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

function loadFileList(api, upButton, pather, path, container, loadCaller, fileCallback, endCallback, sortFunc) {
    genericRequest(api, {'path': path}, data => {
        let pathGen = getRequiredElementById(pather);
        pathGen.innerText = '';
        let prefix;
        upButton = getRequiredElementById(upButton);
        if (path == '') {
            prefix = '';
            upButton.disabled = true;
        }
        else {
            let partial = '';
            for (let part of ("../" + path).split('/')) {
                partial += part + '/';
                let span = document.createElement('span');
                span.className = 'path-list-part';
                span.innerText = part;
                let route = partial.substring(3, partial.length - 1);
                if (route == '/') {
                    route = '';
                }
                let helper = new FileListCallHelper(route, loadCaller);
                span.onclick = helper.call.bind(helper);
                pathGen.appendChild(span);
                pathGen.appendChild(document.createTextNode('/'));
            }
            prefix = path + '/';
            upButton.disabled = false;
            let above = path.split('/').slice(0, -1).join('/');
            let helper = new FileListCallHelper(above, loadCaller);
            upButton.onclick = helper.call.bind(helper);
        }
        for (let folder of data.folders.sort()) {
            let div = appendImage(container, '/imgs/folder.png', 'folder', `${folder}/`);
            let helper = new FileListCallHelper(`${prefix}${folder}`, loadCaller);
            div.addEventListener('click', helper.call.bind(helper));
        }
        if (data.folders.length > 0) {
            container.appendChild(document.createElement('br'));
        }
        for (let file of sortFunc(data.files)) {
            fileCallback(prefix, file);
        }
        if (endCallback) {
            endCallback();
        }
    });
}

function loadHistory(path) {
    let container = getRequiredElementById('image_history');
    lastImageDir = path;
    container.innerHTML = '';
    loadFileList('ListImages', 'image_history_up_button', 'image_history_path', path, container, loadHistory, (prefix, img) => {
        let fullSrc = `Output/${prefix}${img.src}`;
        let batchId = 0;
        if (img.metadata) {
            batchId = parseInt(JSON.parse(img.metadata).sui_image_params.batch_id);
        }
        else if (img.src.endsWith('.html')) {
            batchId = 'folder';
        }
        let div = appendImage('image_history', fullSrc, batchId, img.src, img.metadata);
        if (img.src.endsWith('.html')) {
            div.addEventListener('click', () => window.open(fullSrc, '_blank'));
        }
        else {
            div.addEventListener('click', () => selectImageInHistory(div));
        }
    }, null, (list) => list.sort((a, b) => a.src.toLowerCase().localeCompare(b.src.toLowerCase())));
}

function getCurrentStatus() {
    if (versionIsWrong) {
        return ['error', 'The server has updated since you opened the page, please refresh.'];
    }
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
        let pop = getRequiredElementById(`popover_${id}`);
        pop.style.display = 'none';
        pop.dataset.visible = "false";
    }
    popHide = [];
}, true);

function doPopover(id) {
    let pop = getRequiredElementById(`popover_${id}`);
    if (pop.dataset.visible == "true") {
        pop.style.display = 'none';
        pop.dataset.visible = "false";
        popHide.splice(popHide.indexOf(id), 1);
    }
    else {
        pop.style.display = 'block';
        pop.dataset.visible = "true";
        let x = Math.min(mouseX, window.innerWidth - pop.offsetWidth - 10);
        pop.style.left = `${x}px`;
        pop.style.top = `${mouseY}px`;
        popHide.push(id);
    }
}

let toolSelector = getRequiredElementById('tool_selector');
let toolContainer = getRequiredElementById('tool_container');

function genToolsList() {
    let generateButton = getRequiredElementById('generate_button');
    let generateButtonRawText = generateButton.innerText;
    let generateButtonRawOnClick = generateButton.onclick;
    toolSelector.value = '';
    // TODO: Dynamic-from-server option list generation
    toolSelector.addEventListener('change', () => {
        for (let opened of toolContainer.getElementsByClassName('tool-open')) {
            opened.classList.remove('tool-open');
        }
        generateButton.innerText = generateButtonRawText;
        generateButton.onclick = generateButtonRawOnClick;
        let tool = toolSelector.value;
        if (tool == '') {
            return;
        }
        let div = getRequiredElementById(`tool_${tool}`);
        div.classList.add('tool-open');
        let override = toolOverrides[tool];
        if (override) {
            generateButton.innerText = override.text;
            generateButton.onclick = override.run;
        }
    });
}

let toolOverrides = {};

function registerNewTool(id, name, genOverride = null, runOverride = null) {
    let option = document.createElement('option');
    option.value = id;
    option.innerText = name;
    toolSelector.appendChild(option);
    let div = createDiv(`tool_${id}`, 'tool');
    toolContainer.appendChild(div);
    if (genOverride) {
        toolOverrides[id] = { 'text': genOverride, 'run': runOverride };
    }
    return div;
}

let pageBarTop = -1;
let pageBarTop2 = -1;
let pageBarMid = -1;

let setPageBarsFunc;

function resetPageSizer() {
    deleteCookie('pageBarTop');
    deleteCookie('pageBarTop2');
    deleteCookie('pageBarMidPx');
    pageBarTop = -1;
    pageBarTop2 = -1;
    pageBarMid = -1;
    setPageBarsFunc();
}

function pageSizer() {
    let topSplit = getRequiredElementById('t2i-top-split-bar');
    let topSplit2 = getRequiredElementById('t2i-top-2nd-split-bar');
    let midSplit = getRequiredElementById('t2i-mid-split-bar');
    let topBar = getRequiredElementById('t2i_top_bar');
    let bottomBarContent = getRequiredElementById('t2i_bottom_bar_content');
    let inputSidebar = getRequiredElementById('input_sidebar');
    let mainInputsAreaWrapper = getRequiredElementById('main_inputs_area_wrapper');
    let mainImageArea = getRequiredElementById('main_image_area');
    let currentImage = getRequiredElementById('current_image');
    let currentImageBatch = getRequiredElementById('current_image_batch');
    let topDrag = false;
    let topDrag2 = false;
    let midDrag = false;
    function setPageBars() {
        setCookie('pageBarTop', pageBarTop, 365);
        setCookie('pageBarTop2', pageBarTop2, 365);
        setCookie('pageBarMidPx', pageBarMid, 365);
        if (pageBarTop != -1) {
            inputSidebar.style.width = `${pageBarTop}px`;
            mainInputsAreaWrapper.style.width = `${pageBarTop}px`;
            mainImageArea.style.width = `calc(100vw - ${pageBarTop}px)`;
            currentImageBatch.style.width = `calc(100vw - ${pageBarTop}px - min(max(40vw, 28rem), 49vh))`;
        }
        else {
            inputSidebar.style.width = '';
            mainInputsAreaWrapper.style.width = '';
            mainImageArea.style.width = '';
            currentImageBatch.style.width = '';
        }
        if (pageBarTop2 != -1) {
            let adaptedX = pageBarTop2 - inputSidebar.getBoundingClientRect().width - 17;
            adaptedX = Math.min(Math.max(adaptedX, 100), window.innerWidth - 100);
            currentImage.style.width = `${adaptedX}px`;
            currentImageBatch.style.width = `calc(100vw - ${pageBarTop2}px)`;
        }
        else {
            currentImage.style.width = '';
            currentImageBatch.style.width = '';
        }
        if (pageBarMid != -1) {
            let fixed = `${pageBarMid}px`;
            topSplit.style.height = `calc(100vh - ${fixed})`;
            topSplit2.style.height = `calc(100vh - ${fixed})`;
            inputSidebar.style.height = `calc(100vh - ${fixed})`;
            mainInputsAreaWrapper.style.height = `calc(100vh - ${fixed} - 7rem)`;
            mainImageArea.style.height = `calc(100vh - ${fixed})`;
            currentImage.style.height = `calc(100vh - ${fixed})`;
            currentImageBatch.style.height = `calc(100vh - ${fixed} - 2rem)`;
            topBar.style.height = `calc(100vh - ${fixed})`;
            bottomBarContent.style.height = `calc(${fixed} - 2rem)`;
        }
        else {
            topSplit.style.height = '';
            topSplit2.style.height = '';
            inputSidebar.style.height = '';
            mainInputsAreaWrapper.style.height = '';
            mainImageArea.style.height = '';
            currentImage.style.height = '';
            currentImageBatch.style.height = '';
            topBar.style.height = '';
            bottomBarContent.style.height = '';
        }
    }
    setPageBarsFunc = setPageBars;
    let cookieA = getCookie('pageBarTop');
    if (cookieA) {
        pageBarTop = parseInt(cookieA);
    }
    let cookieB = getCookie('pageBarTop2');
    if (cookieB) {
        pageBarTop2 = parseInt(cookieB);
    }
    let cookieC = getCookie('pageBarMidPx');
    if (cookieC) {
        pageBarMid = parseInt(cookieC);
    }
    setPageBars();
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
            pageBarTop = offX;
            setPageBars();
        }
        if (topDrag2) {
            pageBarTop2 = offX;
            setPageBars();
        }
        if (midDrag) {
            let refY = Math.min(Math.max(e.pageY, 85), window.innerHeight - 85);
            pageBarMid = window.innerHeight - refY + topBar.getBoundingClientRect().top + 15;
            setPageBars();
        }
    });
    document.addEventListener('mouseup', (e) => {
        topDrag = false;
        topDrag2 = false;
        midDrag = false;
    });
}

function show_t2i_quicktools() {
    doPopover('quicktools');
}

function loadUserData() {
    genericRequest('GetMyUserData', {}, data => {
        allPresets = [];
        getRequiredElementById('preset_list').innerHTML = '';
        for (let preset of data.presets.sort((a, b) => a.title.toLowerCase() == "default" ? -1 : (b.title.toLowerCase() == "default" ? 1 : 0))) {
            addPreset(preset);
        }
        if (shouldApplyDefault) {
            shouldApplyDefault = false;
            let defaultPreset = getPresetByTitle('default');
            if (defaultPreset) {
                applyOnePreset(defaultPreset);
            }
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
            allModels = data.models;
            rawGenParamTypesFromServer = data.list.sort(paramSorter);
            gen_param_types = rawGenParamTypesFromServer;
            genInputs();
            genToolsList();
            reviseStatusBar();
            toggle_advanced();
            setCurrentModel();
            loadUserData();
            getRequiredElementById('image_history_refresh_button').addEventListener('click', () => loadHistory(lastImageDir));
            getRequiredElementById('model_list_refresh_button').addEventListener('click', () => loadModelList(lastModelDir, true));
            for (let callback of sessionReadyCallbacks) {
                callback();
            }
        });
    });
    setInterval(genpageLoop, 1000);
}

setTimeout(genpageLoad, 1);
