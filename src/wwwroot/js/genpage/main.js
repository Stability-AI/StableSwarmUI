let gen_param_types = null, rawGenParamTypesFromServer = null;

let lastImageDir = '';

let lastModelDir = '';

let num_current_gens = 0, num_models_loading = 0, num_live_gens = 0, num_backends_waiting = 0;

let shouldApplyDefault = false;

let sessionReadyCallbacks = [];

let allModels = [];

let coreModelMap = {};

let otherInfoSpanContent = [];

function updateOtherInfoSpan() {
    let span = getRequiredElementById('other_info_span');
    span.innerHTML = otherInfoSpanContent.join(' ');
}

const time_started = Date.now();

let statusBarElem = getRequiredElementById('top_status_bar');

/** Called when the user clicks the clear batch button. */
function clearBatch() {
    getRequiredElementById('current_image_batch').innerHTML = '';
}

/** Reference to the auto-clear-batch toggle checkbox. */
let autoClearBatchElem = getRequiredElementById('auto_clear_batch_checkbox');
autoClearBatchElem.checked = localStorage.getItem('autoClearBatch') != 'false';
/** Called when the user changes auto-clear-batch toggle to update local storage. */
function toggleAutoClearBatch() {
    localStorage.setItem('autoClearBatch', `${autoClearBatchElem.checked}`);
}

/** Reference to the auto-load-previews toggle checkbox. */
let autoLoadPreviewsElem = getRequiredElementById('auto_load_previews_checkbox');
autoLoadPreviewsElem.checked = localStorage.getItem('autoLoadPreviews') == 'true';
/** Called when the user changes auto-load-previews toggle to update local storage. */
function toggleAutoLoadPreviews() {
    localStorage.setItem('autoLoadPreviews', `${autoLoadPreviewsElem.checked}`);
}

function clickImageInBatch(div) {
    let imgElem = div.getElementsByTagName('img')[0];
    setCurrentImage(imgElem.src, div.dataset.metadata, div.dataset.batch_id || '', imgElem.dataset.previewGrow == 'true');
}

let currentMetadataVal = null;

function copy_current_image_params() {
    if (!currentMetadataVal) {
        alert('No parameters to copy!');
        return;
    }
    let metadata = JSON.parse(currentMetadataVal).sui_image_params;
    if ('original_prompt' in metadata) {
        metadata.prompt = metadata.original_prompt;
    }
    for (let param of gen_param_types) {
        let elem = document.getElementById(`input_${param.id}`);
        if (elem && metadata[param.id]) {
            setDirectParamValue(param, metadata[param.id]);
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

let metadataKeyFormatCleaners = [];

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
                    for (let cleaner of metadataKeyFormatCleaners) {
                        key = cleaner(key);
                    }
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
    getRequiredElementById('image_fullview_modal_content').innerHTML = `<div class="modal-dialog" style="display:none">(click outside image to close)</div><div class="imageview_modal_inner_div"><img class="imageview_popup_modal_img" src="${src}"><br><div class="imageview_popup_modal_undertext">${formatMetadata(metadata)}</div>`;
    $('#image_fullview_modal').modal('show');
}

function closeImageFullview() {
    $('#image_fullview_modal').modal('hide');
}

function shiftToNextImagePreview(next = true, expand = false) {
    let curImgElem = document.getElementById('current_image_img');
    if (!curImgElem) {
        return;
    }
    let batch_area = getRequiredElementById('current_image_batch');
    let imgs = [...batch_area.getElementsByTagName('img')];
    let index = imgs.findIndex(img => img.src == curImgElem.src);
    if (index == -1) {
        let cleanSrc = (img) => img.src.length > 100 ? img.src.substring(0, 100) + '...' : img.src;
        console.log(`Image preview shift failed as current image ${cleanSrc(curImgElem)} is not in batch area set ${imgs.map(cleanSrc)}`);
        return;
    }
    let newIndex = index + (next ? 1 : -1);
    if (newIndex < 0) {
        newIndex = imgs.length - 1;
    }
    else if (newIndex >= imgs.length) {
        newIndex = 0;
    }
    let newImg = imgs[newIndex];
    let block = findParentOfClass(newImg, 'image-block');
    setCurrentImage(newImg.src, block.dataset.metadata, block.dataset.batch_id, newImg.dataset.previewGrow == 'true');
    if (expand) {
        expandCurrentImage(newImg.src, block.dataset.metadata);
    }
}

window.addEventListener('keydown', function(kbevent) {
    let isFullView = $('#image_fullview_modal').is(':visible');
    let isCurImgFocused = document.activeElement && 
        (findParentOfClass(document.activeElement, 'current_image')
        || findParentOfClass(document.activeElement, 'current_image_batch')
        || document.activeElement.tagName == 'BODY');
    if (isFullView && kbevent.key == 'Escape') {
        $('#image_fullview_modal').modal('toggle');
    }
    else if ((kbevent.key == 'ArrowLeft' || kbevent.key == 'ArrowUp') && (isFullView || isCurImgFocused)) {
        shiftToNextImagePreview(false, isFullView);
    }
    else if ((kbevent.key == 'ArrowRight' || kbevent.key == 'ArrowDown') && (isFullView || isCurImgFocused)) {
        shiftToNextImagePreview(true, isFullView);
    }
    else {
        return;
    }
    kbevent.preventDefault();
    kbevent.stopPropagation();
    return false;
});

function alignImageDataFormat() {
    let curImg = getRequiredElementById('current_image');
    let img = document.getElementById('current_image_img');
    if (!img) {
        return;
    }
    let extrasWrapper = curImg.querySelector('.current-image-extras-wrapper');
    let scale = img.dataset.previewGrow == 'true' ? 8 : 1;
    let imgWidth = img.naturalWidth * scale;
    let imgHeight = img.naturalHeight * scale;
    let ratio = imgWidth / imgHeight;
    let height = Math.min(imgHeight, curImg.offsetHeight);
    let width = Math.min(imgWidth, height * ratio);
    let remainingWidth = curImg.offsetWidth - width - 20;
    img.style.maxWidth = `calc(min(100%, ${width}px))`;
    if (remainingWidth > 25 * 16) {
        extrasWrapper.style.width = `${remainingWidth}px`;
        extrasWrapper.style.maxWidth = `${remainingWidth}px`;
        extrasWrapper.style.display = 'inline-block';
        img.style.maxHeight = `calc(max(15rem, 100%))`;
    }
    else {
        extrasWrapper.style.width = '100%';
        extrasWrapper.style.maxWidth = `100%`;
        extrasWrapper.style.display = 'block';
        img.style.maxHeight = `calc(max(15rem, 100% - 5rem))`;
    }
}

function setCurrentImage(src, metadata = '', batchId = '', previewGrow = false) {
    let curImg = getRequiredElementById('current_image');
    curImg.innerHTML = '';
    let isVideo = src.endsWith(".mp4") || src.endsWith(".webm");
    let img;
    if (isVideo) {
        img = document.createElement('video');
        img.loop = true;
        img.autoplay = true;
        img.muted = true;
        let sourceObj = document.createElement('source');
        sourceObj.src = src;
        sourceObj.type = `video/${src.substring(src.lastIndexOf('.') + 1)}`;
        img.appendChild(sourceObj);
    }
    else {
        img = document.createElement('img');
        img.src = src;
    }
    img.className = 'current-image-img';
    img.id = 'current_image_img';
    img.dataset.batch_id = batchId;
    img.onclick = () => expandCurrentImage(src, metadata);
    currentMetadataVal = metadata;
    let extrasWrapper = createDiv(null, 'current-image-extras-wrapper');
    let buttons = createDiv(null, 'current-image-buttons');
    function naturalDim() {
        if (isVideo) {
            return [img.videoWidth, img.videoHeight];
        }
        else {
            return [img.naturalWidth, img.naturalHeight];
        }
    }
    quickAppendButton(buttons, 'Upscale 2x', () => {
        toDataURL(img.src, (url => {
            let [width, height] = naturalDim();
            let input_overrides = {
                'initimage': url,
                'width': width * 2,
                'height': height * 2
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
    extrasWrapper.appendChild(buttons);
    let data = createDiv(null, 'current-image-data');
    data.innerHTML = formatMetadata(metadata);
    extrasWrapper.appendChild(data);
    img.onload = () => {
        let [width, height] = naturalDim();
        if (previewGrow) {
            img.width = width * 8;
            img.height = height * 8;
            img.dataset.previewGrow = 'true';
        }
        alignImageDataFormat();
    }
    curImg.appendChild(img);
    curImg.appendChild(extrasWrapper);
}

function appendImage(container, imageSrc, batchId, textPreview, metadata = '', type = 'legacy', prepend = true) {
    if (typeof container == 'string') {
        container = getRequiredElementById(container);
    }
    let div = createDiv(null, `image-block image-block-${type} image-batch-${batchId == "folder" ? "folder" : (batchId % 2)}`);
    div.dataset.batch_id = batchId;
    div.dataset.preview_text = textPreview;
    div.dataset.metadata = metadata;
    let img = document.createElement('img');
    img.addEventListener('load', () => {
        let ratio = img.naturalWidth / img.naturalHeight;
        if (batchId != "folder") {
            div.style.width = `calc(${roundToStr(ratio * 10, 2)}rem + 2px)`;
        }
    });
    img.src = imageSrc;
    div.appendChild(img);
    if (type == 'legacy') {
        let textBlock = createDiv(null, 'image-preview-text');
        textBlock.innerText = textPreview;
        div.appendChild(textBlock);
    }
    if (prepend) {
        container.prepend(div);
    }
    else {
        container.appendChild(div);
    }
    return div;
}

function gotImageResult(image, metadata, batchId) {
    updateGenCount();
    let src = image;
    let fname = src && src.includes('/') ? src.substring(src.lastIndexOf('/') + 1) : src;
    let batch_div = appendImage('current_image_batch', src, batchId, fname, metadata, 'batch');
    batch_div.addEventListener('click', () => clickImageInBatch(batch_div));
    setCurrentImage(src, metadata, batchId);
    return batch_div;
}

function gotImagePreview(image, metadata, batchId) {
    updateGenCount();
    let src = image;
    let fname = src && src.includes('/') ? src.substring(src.lastIndexOf('/') + 1) : src;
    let batch_div = appendImage('current_image_batch', src, batchId, fname, metadata, 'batch', true);
    batch_div.querySelector('img').dataset.previewGrow = 'true';
    batch_div.addEventListener('click', () => clickImageInBatch(batch_div));
    if (!document.getElementById('current_image_img') || (autoLoadPreviewsElem.checked && image != 'imgs/model_placeholder.jpg')) {
        setCurrentImage(src, metadata, batchId, true);
    }
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
    getRequiredElementById('alt_interrupt_button').classList.toggle('interrupt-button-none', total == 0);
    let oldInterruptButton = document.getElementById('interrupt_button');
    if (oldInterruptButton) {
        oldInterruptButton.classList.toggle('interrupt-button-none', total == 0);
    }
    let elem = getRequiredElementById('num_jobs_span');
    function autoBlock(num, text) {
        if (num == 0) {
            return '';
        }
        return `<span class="interrupt-line-part">${num} ${text.replaceAll('%', autoS(num))},</span> `;
    }
    elem.innerHTML = total == 0 ? '' : `${autoBlock(num_current_gens, 'current generation%')}${autoBlock(num_live_gens, 'running')}${autoBlock(num_backends_waiting, 'queued')}${autoBlock(num_models_loading, 'waiting on model load')} ...`;
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

let isGeneratingForever = false;

function doInterrupt(allSessions = false) {
    genericRequest('InterruptAll', {'other_sessions': allSessions}, data => {
        updateGenCount();
    });
    if (isGeneratingForever) {
        toggleGenerateForever();
    }
}
let genForeverInterval;

function toggleGenerateForever() {
    let button = getRequiredElementById('generate_forever_button');
    isGeneratingForever = !isGeneratingForever;
    if (isGeneratingForever) {
        button.innerText = 'Stop Generating';
        genForeverInterval = setInterval(() => {
            if (num_current_gens == 0) {
                doGenerate();
            }
        }, 100);
    }
    else {
        button.innerText = 'Generate Forever';
        clearInterval(genForeverInterval);
    }
}

let batchesEver = 0;

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
        resetBatchIfNeeded();
        let images = {};
        let batch_id = batchesEver++;
        makeWSRequestT2I('GenerateText2ImageWS', getGenInput(input_overrides), data => {
            if (data.image) {
                if (!(data.batch_index in images)) {
                    let batch_div = gotImageResult(data.image, data.metadata, `${batch_id}_${data.batch_index}`);
                    images[data.batch_index] = {div: batch_div, image: data.image, metadata: data.metadata, overall_percent: 0, current_percent: 0};
                }
                else {
                    let imgHolder = images[data.batch_index];
                    setCurrentImage(data.image, data.metadata, `${batch_id}_${data.batch_index}`);
                    let imgElem = imgHolder.div.querySelector('img');
                    imgElem.src = data.image;
                    delete imgElem.dataset.previewGrow;
                    imgHolder.image = data.image;
                    imgHolder.div.dataset.metadata = data.metadata;
                    let progress_bars = imgHolder.div.querySelector('.image-preview-progress-wrapper');
                    if (progress_bars) {
                        progress_bars.remove();
                    }
                }
                images[data.batch_index].image = data.image;
                images[data.batch_index].metadata = data.metadata;
            }
            if (data.gen_progress) {
                if (!(data.gen_progress.batch_index in images)) {
                    let batch_div = gotImagePreview(data.gen_progress.preview || 'imgs/model_placeholder.jpg', `{"preview": "${data.gen_progress.current_percent}"}`, `${batch_id}_${data.gen_progress.batch_index}`);
                    images[data.gen_progress.batch_index] = {div: batch_div, image: null, metadata: null, overall_percent: 0, current_percent: 0};
                    let progress_bars_html = `<div class="image-preview-progress-inner"><div class="image-preview-progress-overall"></div><div class="image-preview-progress-current"></div></div>`;
                    let progress_bars = createDiv(null, 'image-preview-progress-wrapper', progress_bars_html);
                    batch_div.prepend(progress_bars);
                }
                let imgHolder = images[data.gen_progress.batch_index];
                let overall = imgHolder.div.querySelector('.image-preview-progress-overall');
                if (overall && data.gen_progress.overall_percent) {
                    imgHolder.overall_percent = data.gen_progress.overall_percent;
                    imgHolder.current_percent = data.gen_progress.current_percent;
                    overall.style.width = `${imgHolder.overall_percent * 100}%`;
                    imgHolder.div.querySelector('.image-preview-progress-current').style.width = `${imgHolder.current_percent * 100}%`;
                    if (data.gen_progress.preview && autoLoadPreviewsElem.checked && imgHolder.image == null) {
                        setCurrentImage(data.gen_progress.preview, `{"preview": "${data.gen_progress.current_percent}"}`, `${batch_id}_${data.gen_progress.batch_index}`, true);
                    }
                    let curImgElem = document.getElementById('current_image_img');
                    if (data.gen_progress.preview && (!imgHolder.image || data.gen_progress.preview != imgHolder.image)) {
                        if (curImgElem && curImgElem.dataset.batch_id == `${batch_id}_${data.gen_progress.batch_index}`) {
                            curImgElem.src = data.gen_progress.preview;
                            let metadata = getRequiredElementById('current_image').querySelector('.current-image-data');
                            if (metadata) {
                                metadata.remove();
                            }
                        }
                        imgHolder.div.querySelector('img').src = data.gen_progress.preview;
                        imgHolder.image = data.gen_progress.preview;
                    }
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
        data.files = data.files.sort((a, b) => b.src.toLowerCase().localeCompare(a.src.toLowerCase()));
        let folders = data.folders.sort((a, b) => b.toLowerCase().localeCompare(a.toLowerCase()));
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
            onclick: (e) => {
                genericRequest('DeleteImage', {'path': image.data.src.substring("Output/".length)}, data => {
                    e.remove();
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
    if (Object.values(backends_loaded).filter(x => x.enabled).length == 0) {
        return ['warn', 'All backends are disabled. You must enable backends in the Backends section of the Server tab before you can continue.'];
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
            return ['warn', 'Some backends are disabled. Please enable or configure them to continue.'];
        }
        if (countBackendsByStatus('idle') > 0) {
            return ['warn', 'All backends are idle. Cannot generate until at least one backend is running.'];
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

function doPopHideCleanup(target) {
    for (let x = 0; x < popHide.length; x++) {
        let id = popHide[x];
        let pop = getRequiredElementById(`popover_${id}`);
        if (pop.contains(target) && !target.classList.contains('sui_popover_model_button')) {
            continue;
        }
        pop.classList.remove('sui-popover-visible');
        pop.dataset.visible = "false";
        popHide.splice(x, 1);
    }
}

document.addEventListener('mousedown', (e) => {
    mouseX = e.pageX;
    mouseY = e.pageY;
    if (e.button == 2) { // right-click
        doPopHideCleanup(e.target);
    }
}, true);

document.addEventListener('click', (e) => {
    doPopHideCleanup(e.target);
    if (getRequiredElementById('image_fullview_modal').style.display == 'block' && !findParentOfClass(e.target, 'imageview_popup_modal_undertext')) {
        closeImageFullview();
        e.preventDefault();
        e.stopPropagation();
        return false;
    }
}, true);

/** Ensures the popover for the given ID is hidden. */
function hidePopover(id) {
    let pop = getRequiredElementById(`popover_${id}`);
    if (pop.dataset.visible == "true") {
        pop.classList.remove('sui-popover-visible');
        pop.dataset.visible = "false";
        popHide.splice(popHide.indexOf(id), 1);
    }
}

/** Shows the given popover, optionally at the specified location. */
function showPopover(id, targetX = mouseX, targetY = mouseY) {
    let pop = getRequiredElementById(`popover_${id}`);
    if (pop.dataset.visible == "true") {
        hidePopover(id); // Hide to reset before showing again.
    }
    pop.classList.add('sui-popover-visible');
    pop.style.width = '200px';
    pop.dataset.visible = "true";
    let x = Math.min(targetX, window.innerWidth - pop.offsetWidth - 10);
    let y = Math.min(targetY, window.innerHeight - pop.offsetHeight);
    pop.style.left = `${x}px`;
    pop.style.top = `${y}px`;
    pop.style.width = '';
    popHide.push(id);
}

/** Toggles the given popover, showing it or hiding it as relevant. */
function doPopover(id) {
    let pop = getRequiredElementById(`popover_${id}`);
    if (pop.dataset.visible == "true") {
        hidePopover(id);
    }
    else {
        showPopover(id);
    }
}

let toolSelector = getRequiredElementById('tool_selector');
let toolContainer = getRequiredElementById('tool_container');

function genToolsList() {
    let altGenerateButton = getRequiredElementById('alt_generate_button');
    let oldGenerateButton = document.getElementById('generate_button');
    let altGenerateButtonRawText = altGenerateButton.innerText;
    let altGenerateButtonRawOnClick = altGenerateButton.onclick;
    toolSelector.value = '';
    // TODO: Dynamic-from-server option list generation
    toolSelector.addEventListener('change', () => {
        for (let opened of toolContainer.getElementsByClassName('tool-open')) {
            opened.classList.remove('tool-open');
        }
        altGenerateButton.innerText = altGenerateButtonRawText;
        altGenerateButton.onclick = altGenerateButtonRawOnClick;
        if (oldGenerateButton) {
            oldGenerateButton.innerText = altGenerateButtonRawText;
        }
        let tool = toolSelector.value;
        if (tool == '') {
            return;
        }
        let div = getRequiredElementById(`tool_${tool}`);
        div.classList.add('tool-open');
        let override = toolOverrides[tool];
        if (override) {
            altGenerateButton.innerText = override.text;
            altGenerateButton.onclick = override.run;
            if (oldGenerateButton) {
                oldGenerateButton.innerText = override.text;
            }
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
let leftShut = localStorage.getItem('barspot_leftShut') == 'true';

let setPageBarsFunc;
let altPromptSizeHandleFunc;

let layoutResets = [];

function resetPageSizer() {
    for (let cookie of listCookies('barspot_')) {
        deleteCookie(cookie);
    }
    pageBarTop = -1;
    pageBarTop2 = -1;
    pageBarMid = -1;
    midForceToBottom = false;
    leftShut = false;
    localStorage.removeItem('barspot_midForceToBottom');
    localStorage.removeItem('barspot_leftShut');
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
    let currentImageBatch = getRequiredElementById('current_image_batch_wrapper');
    let midSplitButton = getRequiredElementById('t2i-mid-split-quickbutton');
    let topSplitButton = getRequiredElementById('t2i-top-split-quickbutton');
    let altRegion = getRequiredElementById('alt_prompt_region');
    let altText = getRequiredElementById('alt_prompt_textbox');
    let altImageRegion = getRequiredElementById('alt_prompt_extra_area');
    let topDrag = false;
    let topDrag2 = false;
    let midDrag = false;
    function setPageBars() {
        if (altRegion.style.display != 'none') {
            altText.style.height = 'auto';
            altText.style.height = `${Math.max(altText.scrollHeight, 15) + 5}px`;
            altRegion.style.top = `calc(-${altText.offsetHeight + altImageRegion.offsetHeight}px - 2rem)`;
        }
        setCookie('barspot_pageBarTop', pageBarTop, 365);
        setCookie('barspot_pageBarTop2', pageBarTop2, 365);
        setCookie('barspot_pageBarMidPx', pageBarMid, 365);
        let barTopLeft = leftShut ? `0px` : pageBarTop == -1 ? `28rem` : `${pageBarTop}px`;
        let barTopRight = pageBarTop2 == -1 ? `21rem` : `${pageBarTop2}px`;
        inputSidebar.style.width = `${barTopLeft}`;
        mainInputsAreaWrapper.style.width = `${barTopLeft}`;
        inputSidebar.style.display = leftShut ? 'none' : '';
        altRegion.style.width = `calc(100vw - ${barTopLeft} - ${barTopRight} - 10px)`;
        mainImageArea.style.width = `calc(100vw - ${barTopLeft})`;
        currentImage.style.width = `calc(100vw - ${barTopLeft} - ${barTopRight} - 10px)`;
        currentImageBatch.style.width = `${barTopRight}`;
        topSplitButton.innerHTML = leftShut ? '&#x21DB;' : '&#x21DA;';
        midSplitButton.innerHTML = midForceToBottom ? '&#x290A;' : '&#x290B;';
        let altHeight = altRegion.style.display == 'none' ? '0px' : `(${altText.offsetHeight + altImageRegion.offsetHeight}px + 2rem)`;
        if (pageBarMid != -1 || midForceToBottom) {
            let fixed = midForceToBottom ? `8rem` : `${pageBarMid}px`;
            topSplit.style.height = `calc(100vh - ${fixed})`;
            topSplit2.style.height = `calc(100vh - ${fixed})`;
            inputSidebar.style.height = `calc(100vh - ${fixed})`;
            mainInputsAreaWrapper.style.height = `calc(100vh - ${fixed})`;
            mainImageArea.style.height = `calc(100vh - ${fixed})`;
            currentImage.style.height = `calc(100vh - ${fixed} - ${altHeight})`;
            currentImageBatch.style.height = `calc(100vh - ${fixed})`;
            topBar.style.height = `calc(100vh - ${fixed})`;
            bottomBarContent.style.height = `calc(${fixed} - 2rem)`;
        }
        else {
            topSplit.style.height = '';
            topSplit2.style.height = '';
            inputSidebar.style.height = '';
            mainInputsAreaWrapper.style.height = '';
            mainImageArea.style.height = '';
            currentImage.style.height = `calc(49vh - ${altHeight})`;
            currentImageBatch.style.height = '';
            topBar.style.height = '';
            bottomBarContent.style.height = '';
        }
        alignImageDataFormat();
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
    function setMidForce(val) {
        midForceToBottom = val;
        localStorage.setItem('barspot_midForceToBottom', midForceToBottom);
    }
    function setLeftShut(val) {
        leftShut = val;
        localStorage.setItem('barspot_leftShut', leftShut);
    }
    midSplit.addEventListener('mousedown', (e) => {
        if (e.target == midSplitButton) {
            return;
        }
        midDrag = true;
        setMidForce(false);
        e.preventDefault();
    }, true);
    midSplitButton.addEventListener('click', (e) => {
        midDrag = false;
        setMidForce(!midForceToBottom);
        pageBarMid = Math.max(pageBarMid, 400);
        setPageBars();
        e.preventDefault();
    }, true);
    topSplitButton.addEventListener('click', (e) => {
        topDrag = false;
        setLeftShut(!leftShut);
        pageBarTop = Math.max(pageBarTop, 400);
        setPageBars();
        e.preventDefault();
        triggerChangeFor(altText);
    }, true);
    document.addEventListener('mousemove', (e) => {
        let offX = e.pageX;
        offX = Math.min(Math.max(offX, 100), window.innerWidth - 100);
        if (topDrag) {
            pageBarTop = Math.min(offX - 5, 51 * 16);
            setLeftShut(pageBarTop < 280);
            setPageBars();
        }
        if (topDrag2) {
            pageBarTop2 = window.innerWidth - offX + 15;
            setPageBars();
        }
        if (midDrag) {
            const MID_OFF = 85;
            let refY = Math.min(Math.max(e.pageY, MID_OFF), window.innerHeight - MID_OFF);
            setMidForce(refY >= window.innerHeight - MID_OFF);
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
            setMidForce(false);
            setPageBars();
        });
    }
    altText.addEventListener('keydown', (e) => {
        if (e.key == 'Enter' && !e.shiftKey) {
            altText.dispatchEvent(new Event('change'));
            getRequiredElementById('alt_generate_button').click();
            e.preventDefault();
            e.stopPropagation();
            return false;
        }
    });
    altText.addEventListener('input', (e) => {
        let inputPrompt = document.getElementById('input_prompt');
        if (inputPrompt) {
            inputPrompt.value = altText.value;
        }
        setCookie(`lastparam_input_prompt`, altText.value, 0.25);
        textPromptDoCount(altText);
        monitorPromptChangeForEmbed(altText.value, 'positive');
    });
    altText.addEventListener('input', () => {
        setCookie(`lastparam_input_prompt`, altText.value, 0.25);
        setPageBars();
    });
    function altPromptSizeHandle() {
        altRegion.style.top = `calc(-${altText.offsetHeight + altImageRegion.offsetHeight}px - 2rem)`;
        setPageBars();
    }
    altPromptSizeHandle();
    new ResizeObserver(altPromptSizeHandle).observe(altText);
    altPromptSizeHandleFunc = altPromptSizeHandle;
    textPromptAddKeydownHandler(altText);
    addEventListener("resize", setPageBars);
}

/** Clears out and resets the image-batch view, only if the user wants that. */
function resetBatchIfNeeded() {
    if (autoClearBatchElem.checked) {
        getRequiredElementById('current_image_batch').innerHTML = '';
    }
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
    pickle2safetensor_load();
}

/** Set some element titles via JavaScript (to allow '\n'). */
function setTitles() {
    getRequiredElementById('alt_prompt_textbox').title = "Tell the AI what you want to see, then press Enter to submit.\nConsider 'a photo of a cat', or 'cartoonish drawing of an astronaut'";
    getRequiredElementById('alt_interrupt_button').title = "Interrupt current generation(s)\nRight-click for advanced options.";
    getRequiredElementById('alt_generate_button').title = "Start generating images\nRight-click for advanced options.";
    let oldGenerateButton = document.getElementById('generate_button');
    if (oldGenerateButton) {
        oldGenerateButton.title = getRequiredElementById('alt_generate_button').title;
        getRequiredElementById('interrupt_button').title = getRequiredElementById('alt_interrupt_button').title;
    }
}
setTitles();

function hideRevisionInputs() {
    let promptImageArea = getRequiredElementById('alt_prompt_image_area');
    promptImageArea.innerHTML = '';
    let clearButton = getRequiredElementById('alt_prompt_image_clear_button');
    clearButton.style.display = 'none';
    let revisionGroup = document.getElementById('input_group_revision');
    let revisionToggler = document.getElementById('input_group_content_revision_toggle');
    if (revisionGroup) {
        revisionToggler.checked = false;
        triggerChangeFor(revisionToggler);
        toggleGroupOpen(revisionGroup, false);
        revisionGroup.style.display = 'none';
    }
    altPromptSizeHandleFunc();
}

function revisionInputHandler() {
    let dragArea = getRequiredElementById('alt_prompt_region');
    dragArea.addEventListener('dragover', (e) => {
        e.preventDefault();
        e.stopPropagation();
    });
    let clearButton = getRequiredElementById('alt_prompt_image_clear_button');
    let promptImageArea = getRequiredElementById('alt_prompt_image_area');
    clearButton.addEventListener('click', () => {
        hideRevisionInputs();
    });
    dragArea.addEventListener('drop', (e) => {
        e.preventDefault();
        e.stopPropagation();
        if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
            let file = e.dataTransfer.files[0];
            if (file.type.startsWith('image/')) {
                let reader = new FileReader();
                reader.onload = (e) => {
                    let data = e.target.result;
                    let imageObject = new Image();
                    imageObject.src = data;
                    imageObject.height = 128;
                    imageObject.className = 'alt-prompt-image';
                    imageObject.dataset.filedata = data;
                    clearButton.style.display = '';
                    let revisionGroup = document.getElementById('input_group_revision');
                    let revisionToggler = document.getElementById('input_group_content_revision_toggle');
                    if (revisionGroup) {
                        toggleGroupOpen(revisionGroup, true);
                        revisionToggler.checked = true;
                        triggerChangeFor(revisionToggler);
                        revisionGroup.style.display = '';
                    }
                    promptImageArea.appendChild(imageObject);
                    altPromptSizeHandleFunc();
                };
                reader.readAsDataURL(file);
            }
        }
    });
}
revisionInputHandler();

function upvertAutoWebuiMetadataToSwarm(lines) {
    let realData = {};
    realData['prompt'] = lines[0];
    if (lines.length == 3) {
        realData['negativeprompt'] = lines[1];
    }
    let dataParts = lines[lines.length - 1].split(',').map(x => x.split(':').map(y => y.trim()));
    for (let part of dataParts) {
        if (part.length == 2) {
            let clean = cleanParamName(part[0]);
            if (rawGenParamTypesFromServer.find(x => x.id == clean)) {
                realData[clean] = part[1];
            }
            else if (clean == "size") {
                let sizeParts = part[1].split('x').map(x => parseInt(x));
                if (sizeParts.length == 2) {
                    realData['width'] = sizeParts[0];
                    realData['height'] = sizeParts[1];
                }
            }
            else {
                realData[part[0]] = part[1];
            }
        }
    }
    return JSON.stringify({ 'sui_image_params': realData });
}

let fooocusMetadataMap = [
    ['Prompt', 'prompt'],
    ['Negative', 'negativeprompt'],
    ['cfg', 'cfgscale'],
    ['sampler_name', 'sampler'],
    ['base_model_name', 'model'],
    ['denoise', 'imageinitcreativity']
];

function remapMetadataKeys(metadata, keymap) {
    for (let pair of keymap) {
        if (pair[0] in metadata) {
            metadata[pair[1]] = metadata[pair[0]];
            delete metadata[pair[0]];
        }
    }
    for (let key in metadata) {
        if (metadata[key] == null) { // Why does Fooocus emit nulls?
            delete metadata[key];
        }
    }
    return metadata;
}

function imageInputHandler() {
    let imageArea = getRequiredElementById('current_image');
    imageArea.addEventListener('dragover', (e) => {
        e.preventDefault();
        e.stopPropagation();
    });
    imageArea.addEventListener('drop', (e) => {
        e.preventDefault();
        e.stopPropagation();
        if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
            let file = e.dataTransfer.files[0];
            if (file.type.startsWith('image/')) {
                let reader = new FileReader();
                reader.onload = (e) => {
                    let data = e.target.result;
                    exifr.parse(data).then(parsed => {
                        let metadata = null;
                        if (parsed) {
                            if (parsed.parameters) {
                                metadata = parsed.parameters;
                            }
                            else if (parsed.Parameters) {
                                metadata = parsed.Parameters;
                            }
                            else if (parsed.prompt) {
                                metadata = parsed.prompt;
                            }
                        }
                        if (metadata) {
                            metadata = metadata.trim();
                            if (metadata.startsWith('{')) {
                                let json = JSON.parse(metadata);
                                if ('sui_image_params' in json) {
                                    // It's swarm, we're good
                                }
                                else if ("Prompt" in json) {
                                    // Fooocus
                                    json = remapMetadataKeys(json, fooocusMetadataMap);
                                    metadata = JSON.stringify({ 'sui_image_params': json });
                                }
                                else {
                                    // Don't know - discard for now.
                                    metadata = null;
                                }
                            }
                            else {
                                let lines = metadata.split('\n');
                                if (lines.length > 1) {
                                    metadata = upvertAutoWebuiMetadataToSwarm(lines);
                                }
                                else {
                                    // ???
                                    metadata = null;
                                }
                            }
                        }
                        setCurrentImage(data, metadata);
                    });
                };
                reader.readAsDataURL(file);
            }
        }
    });
}
imageInputHandler();

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
            paramConfig.preInit();
            paramConfig.applyParamEdits(data.param_edits);
            paramConfig.loadUserParamConfigTab();
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
