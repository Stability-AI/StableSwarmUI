let gen_param_types = null, rawGenParamTypesFromServer = null;

let batches = 0;

let lastImageDir = '';

let lastModelDir = '';

let num_current_gens = 0, num_models_loading = 0, num_live_gens = 0, num_backends_waiting = 0;

let shouldApplyDefault = false;

let sessionReadyCallbacks = [];

let allModels = [];

let coreModelMap = {};

const time_started = Date.now();

let statusBarElem = getRequiredElementById('top_status_bar');

function clickImageInBatch(div) {
    setCurrentImage(div.getElementsByTagName('img')[0].src, div.dataset.metadata);
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
    let data;
    try {
        data = JSON.parse(metadata).sui_image_params;
    }
    catch (e) {
        console.log(`Error parsing metadata '${metadata}': ${e}`);
        return `Broken metadata: ${escapeHtml(metadata)}`;
    }
    let result = '';
    function appendObject(obj) {
        if (obj) {
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
        }
    };
    appendObject(data);
    return result;
}

function expandCurrentImage(src, metadata) {
    getRequiredElementById('image_fullview_modal').innerHTML = `<div class="modal-dialog" style="display:none">(click outside image to close)</div><div class="imageview_modal_inner_div"><img class="imageview_popup_modal_img" src="${src}"><br><div class="imageview_popup_modal_undertext">${formatMetadata(metadata)}</div>`;
    $('#image_fullview_modal').modal('toggle');
}

window.addEventListener('keydown', function(kbevent) {
    if ($('#image_fullview_modal').is(':visible')) {
        if (kbevent.key == 'Escape') {
            $('#image_fullview_modal').modal('toggle');
            kbevent.preventDefault();
            kbevent.stopPropagation();
            return false;
        }
    }
});

function setCurrentImage(src, metadata = '', batchId = '') {
    let curImg = getRequiredElementById('current_image');
    curImg.innerHTML = '';
    let img = document.createElement('img');
    img.id = 'current_image_img';
    img.src = src;
    img.dataset.batch_id = batchId;
    img.onclick = () => expandCurrentImage(src, metadata);
    curImg.appendChild(img);
    currentMetadataVal = metadata;
    let buttons = createDiv(null, 'current-image-buttons');
    quickAppendButton(buttons, 'Upscale 2x', () => {
        toDataURL(img.src, (url => {
            let input_overrides = {
                'initimage': url,
                'width': img.naturalWidth * 2,
                'height': img.naturalHeight * 2
            };
            doGenerate(input_overrides);
        }));
    });
    quickAppendButton(buttons, 'Star', () => {
        alert('Stars are TODO');
    });
    quickAppendButton(buttons, 'Reuse Parameters', copy_current_image_params);
    quickAppendButton(buttons, 'View In History', () => {
        let folder = src;
        if (folder.startsWith('/')) {
            folder = folder.substring(1);
        }
        if (folder.startsWith('Output/')) {
            folder = folder.substring('Output/'.length);
        }
        let lastSlash = folder.lastIndexOf('/');
        if (lastSlash != -1) {
            folder = folder.substring(0, lastSlash);
        }
        getRequiredElementById('imagehistorytabclickable').click();
        imageHistoryBrowser.navigate(folder);
    });
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

function gotImageResult(image, metadata, batchId) {
    updateGenCount();
    let src = image;
    let fname = src && src.includes('/') ? src.substring(src.lastIndexOf('/') + 1) : src;
    let batch_div = appendImage('current_image_batch', src, batches, fname, metadata, 'batch');
    batch_div.addEventListener('click', () => clickImageInBatch(batch_div));
    setCurrentImage(src, metadata, batchId);
    return batch_div;
}

function updateCurrentStatusDirect(data) {
    if (data) {
        num_current_gens = data.waiting_gens;
        num_models_loading = data.loading_models;
        num_live_gens = data.live_gens;
        num_backends_waiting = data.waiting_backends;
    }
    let total = num_current_gens + num_models_loading + num_live_gens + num_backends_waiting;
    getRequiredElementById('interrupt_button').classList.toggle('interrupt-button-none', total == 0);
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

function doGenerate(input_overrides = {}) {
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
        if (getRequiredElementById('current_model').value == '') {
            showError("Cannot generate, no model selected.");
            return;
        }
        getRequiredElementById('current_image_batch').innerHTML = '';
        batches++;
        let images = {};
        makeWSRequestT2I('GenerateText2ImageWS', getGenInput(input_overrides), data => {
            if (data.image) {
                if (!(data.batch_index in images)) {
                    let batch_div = gotImageResult(data.image, data.metadata, data.batch_index);
                    images[data.batch_index] = {div: batch_div, image: null, metadata: null, overall_percent: 0, current_percent: 0};
                }
                else {
                    let imgHolder = images[data.batch_index];
                    let curImgElem = document.getElementById('current_image_img');
                    if (curImgElem && curImgElem.dataset.batch_id == data.batch_index) {
                        setCurrentImage(data.image, data.metadata, data.batch_index)
                    }
                    imgHolder.div.querySelector('img').src = data.image;
                    imgHolder.image = data.image;
                }
                images[data.batch_index].image = data.image;
                images[data.batch_index].metadata = data.metadata;
            }
            if (data.gen_progress) {
                // TODO: Render progress bars
                if (!(data.gen_progress.batch_index in images)) {
                    let batch_div = gotImageResult(data.gen_progress.preview || 'imgs/model_placeholder.jpg', `{"preview": "${data.gen_progress.current_percent}"}`, data.gen_progress.batch_index);
                    images[data.gen_progress.batch_index] = {div: batch_div, image: null, metadata: null, overall_percent: 0, current_percent: 0};
                }
                let imgHolder = images[data.gen_progress.batch_index];
                imgHolder.overall_percent = data.gen_progress.overall_percent;
                imgHolder.current_percent = data.gen_progress.current_percent;
                let curImgElem = document.getElementById('current_image_img');
                if (data.gen_progress.preview && (!imgHolder.image || data.gen_progress.preview != imgHolder.image)) {
                    if (curImgElem && curImgElem.dataset.batch_id == data.gen_progress.batch_index) {
                        curImgElem.src = data.gen_progress.preview;
                    }
                    imgHolder.div.querySelector('img').src = data.gen_progress.preview;
                    imgHolder.image = data.gen_progress.preview;
                }
            }
            if (data.discard_indices) {
                let needsNew = false;
                for (let index of data.discard_indices) {
                    let img = images[index];
                    img.div.remove();
                    let curImgElem = document.getElementById('current_image_img');
                    if (curImgElem && curImgElem.src == img.image) {
                        needsNew = true;
                        delete images[index];
                    }
                }
                if (needsNew) {
                    let imgs = Object.values(images);
                    if (imgs.length > 0) {
                        setCurrentImage(imgs[0].image, imgs[0].metadata);
                    }
                }
            }
        });
    });
}

function listImageHistoryFolderAndFiles(path, isRefresh, callback, depth) {
    let prefix = path == '' ? '' : (path.endsWith('/') ? path : `${path}/`);
    genericRequest('ListImages', {'path': path, 'depth': depth}, data => {
        data.files = data.files.sort((a, b) => a.src.toLowerCase().localeCompare(b.src.toLowerCase()));
        let folders = data.folders.sort((a, b) => a.toLowerCase().localeCompare(b.toLowerCase()));
        let mapped = data.files.map(f => {
            let fullSrc = `${prefix}${f.src}`;
            return { 'name': fullSrc, 'data': { 'src': `Output/${fullSrc}`, 'name': f.src, 'metadata': f.metadata } };
        });
        callback(folders, mapped);
    });
}

function describeImage(image) {
    let buttons = [
        {
            label: 'Delete',
            onclick: () => {
                genericRequest('DeleteImage', {'path': image.data.src.substring("Output/".length)}, data => {
                    imageHistoryBrowser.refresh();
                });
            }
        }
    ]; // TODO: download button, etc.
    let description = image.data.name + "\n" + formatMetadata(image.data.metadata);
    let name = image.data.name;
    let imageSrc = image.data.src.endsWith('.html') ? 'imgs/html.jpg' : image.data.src;
    let searchable = description;
    return { name, description, buttons, 'image': imageSrc, className: '', searchable };
}

function selectImageInHistory(image) {
    if (image.data.name.endsWith('.html')) {
        window.open(image.data.src, '_blank');
    }
    else {
        setCurrentImage(image.data.src, image.data.metadata);
    }
}

let imageHistoryBrowser = new GenPageBrowserClass('image_history', listImageHistoryFolderAndFiles, 'imagehistorybrowser', 'Thumbnails', describeImage, selectImageInHistory);

function getCurrentStatus() {
    if (versionIsWrong) {
        return ['error', 'The server has updated since you opened the page, please refresh.'];
    }
    if (!hasLoadedBackends) {
        return ['warn', 'Loading...'];
    }
    if (Object.values(backends_loaded).length == 0) {
        return ['warn', 'No backends present. You must configure backends in the Backends section of the Server tab before you can continue.'];
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
        return ['error', 'Something is wrong with your backends. Please check the Backends section of the Server tab, or the server logs.'];
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
        pop.style.width = '200px';
        pop.dataset.visible = "true";
        let x = Math.min(mouseX, window.innerWidth - pop.offsetWidth - 10);
        pop.style.left = `${x}px`;
        pop.style.top = `${mouseY}px`;
        pop.style.width = '';
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
let midForceToBottom = localStorage.getItem('barspot_midForceToBottom') == 'true';

let setPageBarsFunc;

let layoutResets = [];

function resetPageSizer() {
    for (let cookie of listCookies('barspot_')) {
        deleteCookie(cookie);
    }
    pageBarTop = -1;
    pageBarTop2 = -1;
    pageBarMid = -1;
    localStorage.removeItem('barspot_midForceToBottom');
    setPageBarsFunc();
    for (let runnable of layoutResets) {
        runnable();
    }
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
    let midSplitButton = getRequiredElementById('t2i-mid-split-quickbutton');
    let topDrag = false;
    let topDrag2 = false;
    let midDrag = false;
    function setPageBars() {
        setCookie('barspot_pageBarTop', pageBarTop, 365);
        setCookie('barspot_pageBarTop2', pageBarTop2, 365);
        setCookie('barspot_pageBarMidPx', pageBarMid, 365);
        let barTopLeft = pageBarTop == -1 ? `28rem` : `${pageBarTop}px`;
        let barTopRight = pageBarTop2 == -1 ? `21rem` : `${pageBarTop2}px`;
        inputSidebar.style.width = `${barTopLeft}`;
        mainInputsAreaWrapper.style.width = `${barTopLeft}`;
        mainImageArea.style.width = `calc(100vw - ${barTopLeft})`;
        currentImage.style.width = `calc(100vw - ${barTopLeft} - ${barTopRight} - 10px)`;
        currentImageBatch.style.width = `${barTopRight}`;
        midSplitButton.innerHTML = midForceToBottom ? '&#x290A;' : '&#x290B;';
        if (pageBarMid != -1 || midForceToBottom) {
            let fixed = midForceToBottom ? `9rem` : `${pageBarMid}px`;
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
    let cookieA = getCookie('barspot_pageBarTop');
    if (cookieA) {
        pageBarTop = parseInt(cookieA);
    }
    let cookieB = getCookie('barspot_pageBarTop2');
    if (cookieB) {
        pageBarTop2 = parseInt(cookieB);
    }
    let cookieC = getCookie('barspot_pageBarMidPx');
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
        if (e.target == midSplitButton) {
            return;
        }
        midDrag = true;
        midForceToBottom = false;
        e.preventDefault();
    }, true);
    midSplitButton.addEventListener('click', (e) => {
        midDrag = false;
        midForceToBottom = !midForceToBottom;
        localStorage.setItem('barspot_midForceToBottom', midForceToBottom);
        pageBarMid = Math.max(pageBarMid, 400);
        setPageBars();
        e.preventDefault();
    }, true);
    document.addEventListener('mousemove', (e) => {
        let offX = e.pageX;
        offX = Math.min(Math.max(offX, 100), window.innerWidth - 100);
        if (topDrag) {
            pageBarTop = offX - 5;
            setPageBars();
        }
        if (topDrag2) {
            pageBarTop2 = window.innerWidth - offX + 15;
            setPageBars();
        }
        if (midDrag) {
            let refY = Math.min(Math.max(e.pageY, 85), window.innerHeight - 85);
            midForceToBottom = refY == window.innerHeight - 85;
            pageBarMid = window.innerHeight - refY + topBar.getBoundingClientRect().top + 15;
            setPageBars();
        }
    });
    document.addEventListener('mouseup', (e) => {
        topDrag = false;
        topDrag2 = false;
        midDrag = false;
    });
    for (let tab of getRequiredElementById('bottombartabcollection').getElementsByTagName('a')) {
        tab.addEventListener('click', (e) => {
            midForceToBottom = false;
            setPageBars();
        });
    }
}

function show_t2i_quicktools() {
    doPopover('quicktools');
}

function loadUserData() {
    genericRequest('GetMyUserData', {}, data => {
        allPresets = data.presets;
        sortPresets();
        presetBrowser.update();
        if (shouldApplyDefault) {
            shouldApplyDefault = false;
            let defaultPreset = getPresetByTitle('default');
            if (defaultPreset) {
                applyOnePreset(defaultPreset);
            }
        }
    });
}

function updateAllModels(models) {
    coreModelMap = models;
    allModels = models['Stable-Diffusion'];
    let selector = getRequiredElementById('current_model');
    selector.innerHTML = '';
    let emptyOption = document.createElement('option');
    emptyOption.value = '';
    emptyOption.innerText = '';
    selector.appendChild(emptyOption);
    for (let model of allModels) {
        let option = document.createElement('option');
        option.value = model;
        option.innerText = model;
        selector.appendChild(option);
    }
}

function genpageLoad() {
    console.log('Load page...');
    pageSizer();
    reviseStatusBar();
    getSession(() => {
        console.log('First session loaded - prepping page.');
        imageHistoryBrowser.navigate('');
        initialModelListLoad();
        loadBackendTypesMenu();
        genericRequest('ListT2IParams', {}, data => {
            updateAllModels(data.models);
            rawGenParamTypesFromServer = data.list.sort(paramSorter);
            gen_param_types = rawGenParamTypesFromServer;
            genInputs();
            genToolsList();
            reviseStatusBar();
            toggle_advanced();
            setCurrentModel();
            loadUserData();
            for (let callback of sessionReadyCallbacks) {
                callback();
            }
        });
    });
    setInterval(genpageLoop, 1000);
}

setTimeout(genpageLoad, 1);
