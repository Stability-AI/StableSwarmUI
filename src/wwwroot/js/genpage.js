let gen_param_types = null;

let session_id = null;

let batches = 0;

let lastImageDir = '';

let lastModelDir = '';

let input_overrides = {};

let num_current_gens = 0, num_models_loading = 0, num_live_gens = 0, num_backends_waiting = 0;

let shouldApplyDefault = false;

let sessionReadyCallbacks = [];

const time_started = Date.now();

let statusBarElem = document.getElementById('top_status_bar');

function clickImageInBatch(div) {
    setCurrentImage(div.getElementsByTagName('img')[0].src, div.dataset.metadata);
}

function selectImageInHistory(div) {
    let batchId = div.dataset.batch_id;
    document.getElementById('current_image_batch').innerHTML = '';
    for (let img of document.getElementById('image_history').querySelectorAll(`[data-batch_id="${batchId}"]`)) {
        let batch_div = appendImage('current_image_batch', img.getElementsByTagName('img')[0].src, batchId, img.dataset.preview_text, img.dataset.metadata);
        batch_div.addEventListener('click', () => clickImageInBatch(batch_div));
    }
    setCurrentImage(div.getElementsByTagName('img')[0].src, div.dataset.metadata);
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

let currentMetadataVal = null;

function copy_current_image_params() {
    if (!currentMetadataVal) {
        alert("No parameters to copy!");
        return;
    }
    let metadata = JSON.parse(currentMetadataVal).stableui_image_params;
    for (let param of gen_param_types) {
        let elem = document.getElementById(`input_${param.id}`);
        if (metadata[param.id]) {
            if (param.type == "boolean") {
                elem.checked = metadata[param.id] == "true";
            }
            else {
                elem.value = metadata[param.id];
            }
            if (param.toggleable) {
                let toggle = document.getElementById(`input_${param.id}_toggle`);
                toggle.checked = true;
                doToggleEnable(elem.id);
            }
        }
        else if (param.toggleable) {
            let toggle = document.getElementById(`input_${param.id}_toggle`);
            toggle.checked = false;
            doToggleEnable(elem.id);
        }
    }
}

function formatMetadata(metadata) {
    if (!metadata) {
        return '';
    }
    let data = JSON.parse(metadata).stableui_image_params;
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
    let curImg = document.getElementById('current_image');
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

function appendImage(container, imageSrc, batchId, textPreview, metadata = '') {
    if (typeof container == 'string') {
        container = document.getElementById(container);
    }
    let div = createDiv(null, `image-block image-batch-${batchId == "folder" ? "folder" : (batchId % 2)}`);
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
    let textBlock = createDiv(null, 'image-preview-text');
    textBlock.innerText = textPreview;
    div.appendChild(textBlock);
    container.appendChild(div);
    return div;
}

function gotImageResult(image, metadata) {
    updateGenCount();
    let src = image;
    let fname = src.includes('/') ? src.substring(src.lastIndexOf('/') + 1) : src;
    let batch_div = appendImage('current_image_batch', src, batches, fname, metadata);
    batch_div.addEventListener('click', () => clickImageInBatch(batch_div));
    let history_div = appendImage('image_history', src, batches, fname, metadata);
    history_div.addEventListener('click', () => selectImageInHistory(history_div));
    setCurrentImage(src, metadata);
    return [batch_div, history_div];
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
        let images = {};
        makeWSRequestT2I('GenerateText2ImageWS', getGenInput(), data => {
            if (data.image) {
                let [batch_div, history_div] = gotImageResult(data.image, data.metadata);
                images[data.index] = [batch_div, history_div, data.image, data.metadata];
            }
            if (data.discard_indices) {
                console.log(`Discarding ${data.discard_indices} images`);
                let needsNew = false;
                for (let index of data.discard_indices) {
                    let [batch_div, history_div, image, metadata] = images[index];
                    batch_div.remove();
                    history_div.remove();
                    let curImgElem = document.getElementById('current_image_img');
                    if (curImgElem.src == image) {
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

function loadFileList(api, upButton, path, container, loadCaller, fileCallback, endCallback, sortFunc) {
    genericRequest(api, {'path': path}, data => {
        let prefix;
        upButton = document.getElementById(upButton);
        if (path == '') {
            prefix = '';
            upButton.disabled = true;
        }
        else {
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
    let container = document.getElementById('image_history');
    lastImageDir = path;
    container.innerHTML = '';
    loadFileList('ListImages', 'image_history_up_button', path, container, loadHistory, (prefix, img) => {
        let fullSrc = `Output/${prefix}${img.src}`;
        let batchId = 0;
        if (img.metadata) {
            batchId = parseInt(JSON.parse(img.metadata).stableui_image_params.batch_id);
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
        let x = Math.min(mouseX, window.innerWidth - pop.offsetWidth - 10);
        pop.style.left = `${x}px`;
        pop.style.top = `${mouseY}px`;
        popHide.push(id);
    }
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

let pageBarTop = -1;
let pageBarTop2 = -1;
let pageBarMid = -1;

let setPageBarsFunc;

function resetPageSizer() {
    deleteCookie('pageBarTop');
    deleteCookie('pageBarTop2');
    deleteCookie('pageBarMid');
    pageBarTop = -1;
    pageBarTop2 = -1;
    pageBarMid = -1;
    setPageBarsFunc();
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
    function setPageBars() {
        setCookie('pageBarTop', pageBarTop, 365);
        setCookie('pageBarTop2', pageBarTop2, 365);
        setCookie('pageBarMid', pageBarMid, 365);
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
            topSplit.style.height = `${pageBarMid}vh`;
            topSplit2.style.height = `${pageBarMid}vh`;
            inputSidebar.style.height = `${pageBarMid}vh`;
            mainInputsAreaWrapper.style.height = `calc(${pageBarMid}vh - 7rem)`;
            mainImageArea.style.height = `${pageBarMid}vh`;
            currentImage.style.height = `${pageBarMid}vh`;
            currentImageBatch.style.height = `calc(${pageBarMid}vh - 2rem)`;
            topBar.style.height = `${pageBarMid}vh`;
            let invOff = 100 - pageBarMid;
            bottomBarContent.style.height = `calc(${invOff}vh - 2rem)`;
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
    let cookieC = getCookie('pageBarMid');
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
            let topY = currentImageBatch.getBoundingClientRect().top;
            let offY = (e.pageY - topY - 2) / window.innerHeight * 100;
            offY = Math.min(Math.max(offY, 5), 95);
            pageBarMid = offY;
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
        document.getElementById('preset_list').innerHTML = '';
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
