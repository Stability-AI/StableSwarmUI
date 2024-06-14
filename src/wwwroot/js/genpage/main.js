let gen_param_types = null, rawGenParamTypesFromServer = null;

let lastImageDir = '';

let lastModelDir = '';

let num_current_gens = 0, num_models_loading = 0, num_live_gens = 0, num_backends_waiting = 0;

let shouldApplyDefault = false;

let sessionReadyCallbacks = [];

let allModels = [];

let coreModelMap = {};

let otherInfoSpanContent = [];

let isGeneratingForever = false, isGeneratingPreviews = false;

let lastHistoryImage = null, lastHistoryImageDiv = null;

let currentMetadataVal = null, currentImgSrc = null;

let autoCompletionsList = null;
let autoCompletionsOptimize = false;

let mainGenHandler = new GenerateHandler();

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

/** Reference to the auto-load-images toggle checkbox. */
let autoLoadImagesElem = getRequiredElementById('auto_load_images_checkbox');
autoLoadImagesElem.checked = localStorage.getItem('autoLoadImages') != 'false';
/** Called when the user changes auto-load-images toggle to update local storage. */
function toggleAutoLoadImages() {
    localStorage.setItem('autoLoadImages', `${autoLoadImagesElem.checked}`);
}

function clickImageInBatch(div) {
    let imgElem = div.getElementsByTagName('img')[0];
    setCurrentImage(div.dataset.src, div.dataset.metadata, div.dataset.batch_id ?? '', imgElem.dataset.previewGrow == 'true');
}

function copy_current_image_params() {
    if (!currentMetadataVal) {
        alert('No parameters to copy!');
        return;
    }
    let metadata = JSON.parse(currentMetadataVal).sui_image_params;
    if ('original_prompt' in metadata) {
        metadata.prompt = metadata.original_prompt;
    }
    if ('original_negativeprompt' in metadata) {
        metadata.negativeprompt = metadata.original_negativeprompt;
    }
    let exclude = getUserSetting('reuseparamexcludelist').split(',').map(s => cleanParamName(s));
    resetParamsToDefault(exclude);
    for (let param of gen_param_types) {
        let elem = document.getElementById(`input_${param.id}`);
        if (elem && metadata[param.id] && !exclude.includes(param.id)) {
            setDirectParamValue(param, metadata[param.id]);
            if (param.toggleable && param.visible) {
                let toggle = getRequiredElementById(`input_${param.id}_toggle`);
                toggle.checked = true;
                doToggleEnable(elem.id);
            }
            if (param.group && param.group.toggles) {
                let toggle = getRequiredElementById(`input_group_content_${param.group.id}_toggle`);
                if (!toggle.checked) {
                    toggle.click();
                }
            }
        }
        else if (elem && param.toggleable && param.visible) {
            let toggle = getRequiredElementById(`input_${param.id}_toggle`);
            toggle.checked = false;
            doToggleEnable(elem.id);
        }
    }
    hideUnsupportableParams();
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
                    let hash = Math.abs(hashCode(key.toLowerCase().replaceAll(' ', '').replaceAll('_', ''))) % 10;
                    if (typeof val == 'object') {
                        result += `<span class="param_view_block tag-text tag-type-${hash}"><span class="param_view_name">${escapeHtml(key)}</span>: `;
                        appendObject(val);
                        result += `</span>, `;
                    }
                    else {
                        result += `<span class="param_view_block tag-text tag-type-${hash}"><span class="param_view_name">${escapeHtml(key)}</span>: <span class="param_view tag-text-soft tag-type-${hash}">${escapeHtml(`${val}`)}</span></span>, `;
                    }
                }
            }
        }
    };
    appendObject(data);
    return result;
}

/** Central helper class to handle the 'image full view' modal. */
class ImageFullViewHelper {
    constructor() {
        this.zoomRate = 1.1;
        this.modal = getRequiredElementById('image_fullview_modal');
        this.content = getRequiredElementById('image_fullview_modal_content');
        this.modalJq = $('#image_fullview_modal');
        this.noClose = false;
        document.addEventListener('click', (e) => {
            if (e.target.tagName == 'BODY') {
                return; // it's impossible on the genpage to actually click body, so this indicates a bugged click, so ignore it
            }
            if (!this.noClose && this.modal.style.display == 'block' && !findParentOfClass(e.target, 'imageview_popup_modal_undertext')) {
                this.close();
                e.preventDefault();
                e.stopPropagation();
                return false;
            }
            this.noClose = false;
        }, true);
        this.lastMouseX = 0;
        this.lastMouseY = 0;
        this.isDragging = false;
        this.didDrag = false;
        this.content.addEventListener('wheel', this.onWheel.bind(this));
        this.content.addEventListener('mousedown', this.onMouseDown.bind(this));
        document.addEventListener('mouseup', this.onGlobalMouseUp.bind(this));
        document.addEventListener('mousemove', this.onGlobalMouseMove.bind(this));
    }

    getImg() {
        return getRequiredElementById('imageview_popup_modal_img');
    }

    getHeightPercent() {
        return parseFloat((this.getImg().style.height || '100%').replaceAll('%', ''));
    }

    getImgLeft() {
        return parseFloat((this.getImg().style.left || '0').replaceAll('px', ''));
    }

    getImgTop() {
        return parseFloat((this.getImg().style.top || '0').replaceAll('px', ''));
    }

    onMouseDown(e) {
        if (this.modal.style.display != 'block') {
            return;
        }
        this.lastMouseX = e.clientX;
        this.lastMouseY = e.clientY;
        this.isDragging = true;
        this.getImg().style.cursor = 'grabbing';
        e.preventDefault();
        e.stopPropagation();
    }

    onGlobalMouseUp(e) {
        if (!this.isDragging) {
            return;
        }
        this.getImg().style.cursor = 'grab';
        this.isDragging = false;
        this.noClose = this.didDrag;
        this.didDrag = false;
    }

    moveImg(xDiff, yDiff) {
        let img = this.getImg();
        let newLeft = this.getImgLeft() + xDiff;
        let newTop = this.getImgTop() + yDiff;
        let overWidth = img.parentElement.offsetWidth / 2;
        let overHeight = img.parentElement.offsetHeight / 2;
        newLeft = Math.min(overWidth, Math.max(newLeft, img.parentElement.offsetWidth - img.width - overWidth));
        newTop = Math.min(overHeight, Math.max(newTop, img.parentElement.offsetHeight - img.height - overHeight));
        img.style.left = `${newLeft}px`;
        img.style.top = `${newTop}px`;
    }

    onGlobalMouseMove(e) {
        if (!this.isDragging) {
            return;
        }
        this.detachImg();
        let xDiff = e.clientX - this.lastMouseX;
        let yDiff = e.clientY - this.lastMouseY;
        this.lastMouseX = e.clientX;
        this.lastMouseY = e.clientY;
        this.moveImg(xDiff, yDiff);
        if (Math.abs(xDiff) > 1 || Math.abs(yDiff) > 1) {
            this.didDrag = true;
        }
    }

    detachImg() {
        let wrap = getRequiredElementById('imageview_modal_imagewrap');
        if (wrap.style.textAlign == 'center') {
            let img = this.getImg();
            wrap.style.textAlign = 'left';
            let imgAspectRatio = img.naturalWidth / img.naturalHeight;
            let wrapAspectRatio = wrap.offsetWidth / wrap.offsetHeight;
            let targetWidth = wrap.offsetHeight * imgAspectRatio;
            if (targetWidth > wrap.offsetWidth) {
                img.style.top = `${(wrap.offsetHeight - (wrap.offsetWidth / imgAspectRatio)) / 2}px`;
                img.style.height = `${(wrapAspectRatio / imgAspectRatio) * 100}%`;
                img.style.left = '0px';
            }
            else {
                img.style.top = '0px';
                img.style.left = `${(wrap.offsetWidth - targetWidth) / 2}px`;
                img.style.height = `100%`;
            }
            img.style.objectFit = '';
            img.style.maxWidth = '';
        }
    }

    copyState() {
        let img = this.getImg();
        if (img.style.objectFit) {
            return {};
        }
        return {
            left: this.getImgLeft(),
            top: this.getImgTop(),
            height: this.getHeightPercent()
        };
    }

    pasteState(state) {
        if (!state || !state.left) {
            return;
        }
        let img = this.getImg();
        this.detachImg();
        img.style.left = `${state.left}px`;
        img.style.top = `${state.top}px`;
        img.style.height = `${state.height}%`;
    }

    onWheel(e) {
        this.detachImg();
        let img = this.getImg();
        let origHeight = this.getHeightPercent();
        let zoom = Math.pow(this.zoomRate, -e.deltaY / 100);
        let maxHeight = Math.sqrt(img.naturalWidth * img.naturalHeight) * 2;
        let newHeight = Math.max(10, Math.min(origHeight * zoom, maxHeight));
        if (newHeight > maxHeight / 5) {
            img.style.imageRendering = 'pixelated';
        }
        else {
            img.style.imageRendering = '';
        }
        img.style.cursor = 'grab';
        let [imgLeft, imgTop] = [this.getImgLeft(), this.getImgTop()];
        let [mouseX, mouseY] = [e.clientX - img.offsetLeft, e.clientY - img.offsetTop];
        let [origX, origY] = [mouseX / origHeight - imgLeft, mouseY / origHeight - imgTop];
        let [newX, newY] = [mouseX / newHeight - imgLeft, mouseY / newHeight - imgTop];
        this.moveImg((newX - origX) * newHeight, (newY - origY) * newHeight);
        img.style.height = `${newHeight}%`;
    }

    showImage(src, metadata) {
        this.content.innerHTML = `
        <div class="modal-dialog" style="display:none">(click outside image to close)</div>
        <div class="imageview_modal_inner_div">
            <div class="imageview_modal_imagewrap" id="imageview_modal_imagewrap" style="text-align:center;">
                <img class="imageview_popup_modal_img" id="imageview_popup_modal_img" style="cursor:grab;max-width:100%;object-fit:contain;" src="${src}">
            </div>
            <div class="imageview_popup_modal_undertext">
            ${formatMetadata(metadata)}
            </div>
        </div>`;
        this.modalJq.modal('show');
    }

    close() {
        this.isDragging = false;
        this.didDrag = false;
        this.modalJq.modal('hide');
    }

    isOpen() {
        return this.modalJq.is(':visible');
    }
}

let imageFullView = new ImageFullViewHelper();

function shiftToNextImagePreview(next = true, expand = false) {
    let curImgElem = document.getElementById('current_image_img');
    if (!curImgElem) {
        return;
    }
    let expandedState = imageFullView.isOpen() ? imageFullView.copyState() : {};
    if (curImgElem.dataset.batch_id == 'history') {
        let divs = [...lastHistoryImageDiv.parentElement.children].filter(div => div.classList.contains('image-block'));
        let index = divs.findIndex(div => div == lastHistoryImageDiv);
        if (index == -1) {
            console.log(`Image preview shift failed as current image ${lastHistoryImage} is not in history area`);
            return;
        }
        let newIndex = index + (next ? 1 : -1);
        if (newIndex < 0) {
            newIndex = divs.length - 1;
        }
        else if (newIndex >= divs.length) {
            newIndex = 0;
        }
        divs[newIndex].querySelector('img').click();
        if (expand) {
            divs[newIndex].querySelector('img').click();
            imageFullView.showImage(currentImgSrc, currentMetadataVal);
            imageFullView.pasteState(expandedState);
        }
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
    setCurrentImage(block.dataset.src, block.dataset.metadata, block.dataset.batch_id, newImg.dataset.previewGrow == 'true');
    if (expand) {
        imageFullView.showImage(block.dataset.src, block.dataset.metadata);
        imageFullView.pasteState(expandedState);
    }
}

window.addEventListener('keydown', function(kbevent) {
    let isFullView = imageFullView.isOpen();
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
    else if (kbevent.key === "Enter" && kbevent.ctrlKey && isVisible(getRequiredElementById('main_image_area'))) {
        getRequiredElementById('alt_generate_button').click();
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
    if (remainingWidth > 30 * 16) {
        curImg.classList.remove('current_image_small');
        extrasWrapper.style.width = `${remainingWidth}px`;
        extrasWrapper.style.maxWidth = `${remainingWidth}px`;
        extrasWrapper.style.display = 'inline-block';
        img.style.maxHeight = `calc(max(15rem, 100%))`;
    }
    else {
        curImg.classList.add('current_image_small');
        extrasWrapper.style.width = '100%';
        extrasWrapper.style.maxWidth = `100%`;
        extrasWrapper.style.display = 'block';
        img.style.maxHeight = `calc(max(15rem, 100% - 5.1rem))`;
    }
}

function toggleStar(path, rawSrc) {
    genericRequest('ToggleImageStarred', {'path': path}, data => {
        let curImgImg = document.getElementById('current_image_img');
        if (curImgImg && curImgImg.dataset.src == rawSrc) {
            let button = getRequiredElementById('current_image').querySelector('.star-button');
            if (data.new_state) {
                button.classList.add('button-starred-image');
                button.innerText = 'Starred';
            }
            else {
                button.classList.remove('button-starred-image');
                button.innerText = 'Star';
            }
        }
        let batchDiv = getRequiredElementById('current_image_batch').querySelector(`.image-block[data-src="${rawSrc}"]`);
        if (batchDiv) {
            batchDiv.dataset.metadata = JSON.stringify({ ...(JSON.parse(batchDiv.dataset.metadata ?? '{}') ?? {}), is_starred: data.new_state });
            batchDiv.classList.toggle('image-block-starred', data.new_state);
        }
        let historyDiv = getRequiredElementById('imagehistorybrowser-content').querySelector(`.image-block[data-src="${rawSrc}"]`);
        if (historyDiv) {
            historyDiv.dataset.metadata = JSON.stringify({ ...(JSON.parse(historyDiv.dataset.metadata ?? '{}') ?? {}), is_starred: data.new_state });
            historyDiv.classList.toggle('image-block-starred', data.new_state);
        }
    });
}

function setCurrentImage(src, metadata = '', batchId = '', previewGrow = false, smoothAdd = false) {
    currentImgSrc = src;
    currentMetadataVal = metadata;
    if (smoothAdd) {
        let image = new Image();
        image.src = src;
        image.onload = () => {
            setCurrentImage(src, metadata, batchId, previewGrow);
        };
        return;
    }
    let curImg = getRequiredElementById('current_image');
    let isVideo = src.endsWith(".mp4") || src.endsWith(".webm") || src.endsWith(".mov");
    let img;
    let isReuse = false;
    let srcTarget;
    if (isVideo) {
        curImg.innerHTML = '';
        img = document.createElement('video');
        img.loop = true;
        img.autoplay = true;
        img.muted = true;
        let sourceObj = document.createElement('source');
        srcTarget = sourceObj;
        sourceObj.type = `video/${src.substring(src.lastIndexOf('.') + 1)}`;
        img.appendChild(sourceObj);
    }
    else {
        img = document.getElementById('current_image_img');
        if (!img || img.tagName != 'IMG') {
            curImg.innerHTML = '';
            img = document.createElement('img');
        }
        else {
            isReuse = true;
            delete img.dataset.previewGrow;
            img.removeAttribute('width');
            img.removeAttribute('height');
        }
        srcTarget = img;
    }
    function naturalDim() {
        if (isVideo) {
            return [img.videoWidth, img.videoHeight];
        }
        else {
            return [img.naturalWidth, img.naturalHeight];
        }
    }
    img.onload = () => {
        let [width, height] = naturalDim();
        if (previewGrow || getUserSetting('centerimagealwaysgrow')) {
            img.width = width * 8;
            img.height = height * 8;
            img.dataset.previewGrow = 'true';
        }
        alignImageDataFormat();
    }
    srcTarget.src = src;
    img.className = 'current-image-img';
    img.id = 'current_image_img';
    img.dataset.src = src;
    img.dataset.batch_id = batchId;
    img.onclick = () => imageFullView.showImage(src, metadata);
    let extrasWrapper = isReuse ? document.getElementById('current-image-extras-wrapper') : createDiv('current-image-extras-wrapper', 'current-image-extras-wrapper');
    extrasWrapper.innerHTML = '';
    let buttons = createDiv(null, 'current-image-buttons');
    let imagePathClean = src;
    if (imagePathClean.startsWith("http://") || imagePathClean.startsWith("https://")) {
        imagePathClean = imagePathClean.substring(imagePathClean.indexOf('/', imagePathClean.indexOf('/') + 2));
    }
    if (imagePathClean.startsWith('/')) {
        imagePathClean = imagePathClean.substring(1);
    }
    if (imagePathClean.startsWith('Output/')) {
        imagePathClean = imagePathClean.substring('Output/'.length);
    }
    if (imagePathClean.startsWith('View/')) {
        imagePathClean = imagePathClean.substring('View/'.length);
        let firstSlash = imagePathClean.indexOf('/');
        if (firstSlash != -1) {
            imagePathClean = imagePathClean.substring(firstSlash + 1);
        }
    }
    quickAppendButton(buttons, 'Use As Init', () => {
        let initImageParam = document.getElementById('input_initimage');
        if (initImageParam) {
            let tmpImg = new Image();
            tmpImg.crossOrigin = 'Anonymous';
            tmpImg.onload = () => {
                let canvas = document.createElement('canvas');
                canvas.width = tmpImg.naturalWidth;
                canvas.height = tmpImg.naturalHeight;
                let ctx = canvas.getContext('2d');
                ctx.drawImage(tmpImg, 0, 0);
                canvas.toBlob(blob => {
                    let file = new File([blob], imagePathClean, { type: img.src.substring(img.src.lastIndexOf('.') + 1) });
                    let container = new DataTransfer(); 
                    container.items.add(file);
                    initImageParam.files = container.files;
                    triggerChangeFor(initImageParam);
                    toggleGroupOpen(initImageParam, true);
                    let toggler = getRequiredElementById('input_group_content_initimage_toggle');
                    toggler.checked = true;
                    triggerChangeFor(toggler);
                });
            };
            tmpImg.src = img.src;
        }
    }, '', 'Sets this image as the Init Image parameter input');
    quickAppendButton(buttons, 'Edit Image', () => {
        let initImageGroupToggle = getRequiredElementById('input_group_content_initimage_toggle');
        if (initImageGroupToggle) {
            initImageGroupToggle.checked = true;
            triggerChangeFor(initImageGroupToggle);
        }
        imageEditor.setBaseImage(img);
        imageEditor.activate();
    }, '', 'Opens an Image Editor for this image');
    quickAppendButton(buttons, 'Upscale 2x', () => {
        toDataURL(img.src, (url => {
            let [width, height] = naturalDim();
            let input_overrides = {
                'initimage': url,
                'width': width * 2,
                'height': height * 2
            };
            mainGenHandler.doGenerate(input_overrides, { 'initimagecreativity': 0.6 });
        }));
    }, '', 'Runs an instant generation with this image as the input and scale doubled');
    let metaParsed = JSON.parse(metadata) ?? { is_starred: false };
    quickAppendButton(buttons, metaParsed.is_starred ? 'Starred' : 'Star', (e, button) => {
        toggleStar(imagePathClean, src);
    }, (metaParsed.is_starred ? ' star-button button-starred-image' : ' star-button'), 'Toggles this image as starred - starred images get moved to a separate folder and highlighted');
    quickAppendButton(buttons, 'Reuse Parameters', copy_current_image_params, '', 'Copies the parameters used to generate this image to the current generation settings');
    quickAppendButton(buttons, 'More &#x2B9F;', (e, button) => {
        let subButtons = [];
        for (let added of buttonsForImage(imagePathClean, src)) {
            if (added.href) {
                subButtons.push({ key: added.label, href: added.href, is_download: added.is_download });
            }
            else {
                subButtons.push({ key: added.label, action: added.onclick });
            }
        }
        subButtons.push({ key: 'View In History', action: () => {
            let folder = imagePathClean;
            let lastSlash = folder.lastIndexOf('/');
            if (lastSlash != -1) {
                folder = folder.substring(0, lastSlash);
            }
            getRequiredElementById('imagehistorytabclickable').click();
            imageHistoryBrowser.navigate(folder);
        } });
        let rect = button.getBoundingClientRect();
        new AdvancedPopover('image_more_popover', subButtons, false, rect.x, rect.y + button.offsetHeight + 6, document.body, null);

    });
    extrasWrapper.appendChild(buttons);
    let data = createDiv(null, 'current-image-data');
    data.innerHTML = formatMetadata(metadata);
    extrasWrapper.appendChild(data);
    if (!isReuse) {
        curImg.appendChild(img);
        curImg.appendChild(extrasWrapper);
    }
}

function appendImage(container, imageSrc, batchId, textPreview, metadata = '', type = 'legacy', prepend = true) {
    if (typeof container == 'string') {
        container = getRequiredElementById(container);
    }
    let div = createDiv(null, `image-block image-block-${type} image-batch-${batchId == "folder" ? "folder" : (batchId % 2)}`);
    div.dataset.batch_id = batchId;
    div.dataset.preview_text = textPreview;
    div.dataset.src = imageSrc;
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
    if (!document.getElementById('current_image_img') || autoLoadImagesElem.checked) {
        setCurrentImage(src, metadata, batchId, false, true);
        if (getUserSetting('AutoSwapImagesIncludesFullView') && imageFullView.isOpen()) {
            imageFullView.showImage(src, metadata);
        }
    }
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
    if (isGeneratingPreviews && num_current_gens <= getRequiredElementById('usersettings_maxsimulpreviews').value) {
        total = 0;
    }
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
    let timeEstimate = '';
    if (total > 0 && mainGenHandler.totalGensThisRun > 0) {
        let avgGenTime = mainGenHandler.totalGenRunTime / mainGenHandler.totalGensThisRun;
        let estTime = avgGenTime * total;
        timeEstimate = ` (est. ${durationStringify(estTime)})`;
    }
    elem.innerHTML = total == 0 ? (isGeneratingPreviews ? 'Generating live previews...' : '') : `${autoBlock(num_current_gens, 'current generation%')}${autoBlock(num_live_gens, 'running')}${autoBlock(num_backends_waiting, 'queued')}${autoBlock(num_models_loading, 'waiting on model load')} ${timeEstimate}...`;
}

let doesHaveGenCountUpdateQueued = false;

function updateGenCount() {
    updateCurrentStatusDirect(null);
    if (doesHaveGenCountUpdateQueued) {
        return;
    }
    doesHaveGenCountUpdateQueued = true;
    setTimeout(() => {
        reviseStatusBar();
    }, 500);
}

function makeWSRequestT2I(url, in_data, callback, errorHandle = null) {
    makeWSRequest(url, in_data, data => {
        if (data.status) {
            updateCurrentStatusDirect(data.status);
        }
        else {
            callback(data);
        }
    }, 0, errorHandle);
}

function doInterrupt(allSessions = false) {
    genericRequest('InterruptAll', {'other_sessions': allSessions}, data => {
        updateGenCount();
    });
    if (isGeneratingForever) {
        toggleGenerateForever();
    }
}
let genForeverInterval, genPreviewsInterval;

let lastGenForeverParams = null;

function doGenForeverOnce() {
    if (num_current_gens > 0) {
        return;
    }
    let allParams = getGenInput();
    if (!('seed' in allParams) || allParams['seed'] != -1) {
        if (lastGenForeverParams && JSON.stringify(lastGenForeverParams) == JSON.stringify(allParams)) {
            return;
        }
        lastGenForeverParams = allParams;
    }
    mainGenHandler.doGenerate();
}

function toggleGenerateForever() {
    let button = getRequiredElementById('generate_forever_button');
    isGeneratingForever = !isGeneratingForever;
    if (isGeneratingForever) {
        button.innerText = 'Stop Generating';
        let delaySeconds = parseFloat(getUserSetting('generateforeverdelay', '0.1'));
        let delayMs = Math.max(parseInt(delaySeconds * 1000), 1);
        genForeverInterval = setInterval(() => {
            doGenForeverOnce();
        }, delayMs);
    }
    else {
        button.innerText = 'Generate Forever';
        clearInterval(genForeverInterval);
    }
}

let lastPreviewParams = null;

function genOnePreview() {
    let allParams = getGenInput();
    if (lastPreviewParams && JSON.stringify(lastPreviewParams) == JSON.stringify(allParams)) {
        return;
    }
    lastPreviewParams = allParams;
    let previewPreset = allPresets.find(p => p.title == 'Preview');
    let input_overrides = {};
    if (previewPreset) {
        for (let key of Object.keys(previewPreset.param_map)) {
            let param = gen_param_types.filter(p => p.id == key)[0];
            if (param) {
                let val = previewPreset.param_map[key];
                let elem = document.getElementById(`input_${param.id}`);
                if (elem) {
                    let rawVal = getInputVal(elem);
                    if (typeof val == "string" && val.includes("{value}")) {
                        val = val.replace("{value}", elem.value);
                    }
                    else if (key == 'loras' && rawVal) {
                        val = rawVal + "," + val;
                    }
                    else if (key == 'loraweights' && rawVal) {
                        val = rawVal + "," + val;
                    }
                    input_overrides[key] = val;
                }
            }
        }
    }
    input_overrides['_preview'] = true;
    input_overrides['donotsave'] = true;
    input_overrides['images'] = 1;
    for (let param of gen_param_types) {
        if (param.do_not_preview) {
            input_overrides[param.id] = null;
        }
    }
    mainGenHandler.doGenerate(input_overrides);
}

function needsNewPreview() {
    if (!isGeneratingPreviews) {
        return;
    }
    let max = getRequiredElementById('usersettings_maxsimulpreviews').value;
    if (num_current_gens < max) {
        genOnePreview();
    }
}

getRequiredElementById('alt_prompt_textbox').addEventListener('input', () => needsNewPreview());

function toggleGeneratePreviews(override_preview_req = false) {
    if (!isGeneratingPreviews) {
        let previewPreset = allPresets.find(p => p.title == 'Preview');
        if (!previewPreset && !override_preview_req) {
            let autoButtonArea = getRequiredElementById('gen_previews_autobutton');
            let lcm = coreModelMap['LoRA'].find(m => m.toLowerCase().includes('sdxl_lcm'));
            if (lcm) {
                autoButtonArea.innerHTML = `<hr>You have a LoRA named "${escapeHtml(lcm)}" available - would you like to autogenerate a Preview preset? <button class="btn btn-primary">Generate Preview Preset</button>`;
                autoButtonArea.querySelector('button').addEventListener('click', () => {
                    let toSend = {
                        'is_edit': false,
                        'title': 'Preview',
                        'description': '(Auto-generated) LCM Preview Preset, used when "Generate Previews" is clicked',
                        'param_map': {
                            'loras': lcm,
                            'loraweights': '1',
                            'steps': 4,
                            'cfgscale': 1,
                            'sampler': 'lcm',
                            'scheduler': 'normal'
                        }
                    };
                    genericRequest('AddNewPreset', toSend, data => {
                        if (Object.keys(data).includes("preset_fail")) {
                            gen_previews_autobutton.innerText = data.preset_fail;
                            return;
                        }
                        loadUserData(() => {
                            $('#gen_previews_missing_preset_modal').modal('hide');
                            toggleGeneratePreviews();
                        });
                    });
                });
            }
            $('#gen_previews_missing_preset_modal').modal('show');
            return;
        }
    }
    let button = getRequiredElementById('generate_previews_button');
    isGeneratingPreviews = !isGeneratingPreviews;
    if (isGeneratingPreviews) {
        let seed = document.getElementById('input_seed');
        if (seed && seed.value == -1) {
            seed.value = 1;
        }
        button.innerText = 'Stop Generating Previews';
        genPreviewsInterval = setInterval(() => {
            if (num_current_gens == 0) {
                genOnePreview();
            }
        }, 100);
    }
    else {
        button.innerText = 'Generate Previews';
        clearInterval(genPreviewsInterval);
    }
}

function listImageHistoryFolderAndFiles(path, isRefresh, callback, depth) {
    let sortBy = localStorage.getItem('image_history_sort_by') ?? 'Name';
    let reverse = localStorage.getItem('image_history_sort_reverse') == 'true';
    let sortElem = document.getElementById('image_history_sort_by');
    let sortReverseElem = document.getElementById('image_history_sort_reverse');
    let fix = null;
    if (sortElem) {
        sortBy = sortElem.value;
        reverse = sortReverseElem.checked;
    }
    else { // first call happens before headers are added built atm
        fix = () => {
            let sortElem = document.getElementById('image_history_sort_by');
            let sortReverseElem = document.getElementById('image_history_sort_reverse');
            sortElem.value = sortBy;
            sortReverseElem.checked = reverse;
            sortElem.addEventListener('change', () => {
                localStorage.setItem('image_history_sort_by', sortElem.value);
                imageHistoryBrowser.update();
            });
            sortReverseElem.addEventListener('change', () => {
                localStorage.setItem('image_history_sort_reverse', sortReverseElem.checked);
                imageHistoryBrowser.update();
            });
        }
    }
    let prefix = path == '' ? '' : (path.endsWith('/') ? path : `${path}/`);
    genericRequest('ListImages', {'path': path, 'depth': depth, 'sortBy': sortBy, 'sortReverse': reverse}, data => {
        let folders = data.folders.sort((a, b) => b.toLowerCase().localeCompare(a.toLowerCase()));
        let mapped = data.files.map(f => {
            let fullSrc = `${prefix}${f.src}`;
            return { 'name': fullSrc, 'data': { 'src': `${getImageOutPrefix()}/${fullSrc}`, 'fullsrc': fullSrc, 'name': f.src, 'metadata': f.metadata } };
        });
        callback(folders, mapped);
        if (fix) {
            fix();
        }
    });
}

function buttonsForImage(fullsrc, src) {
    return [
        {
            label: 'Star',
            onclick: (e) => {
                toggleStar(fullsrc, src);
            }
        },
        {
            label: 'Open In Folder',
            onclick: (e) => {
                genericRequest('OpenImageFolder', {'path': fullsrc}, data => {});
            }
        },
        {
            label: 'Download',
            href: src,
            is_download: true
        },
        {
            label: 'Delete',
            onclick: (e) => {
                genericRequest('DeleteImage', {'path': fullsrc}, data => {
                    if (e) {
                        e.remove();
                    }
                    else {
                        let historySection = getRequiredElementById('imagehistorybrowser-content');
                        let div = historySection.querySelector(`.image-block[data-src="${src}"]`);
                        if (div) {
                            div.remove();
                        }
                    }
                    let currentImage = document.getElementById('current_image_img');
                    if (currentImage && currentImage.dataset.src == src) {
                        forceShowWelcomeMessage();
                    }
                });
            }
        }
    ];;
}

function describeImage(image) {
    let buttons = buttonsForImage(image.data.fullsrc, image.data.src);
    let parsedMeta = { is_starred: false };
    if (image.data.metadata) {
        try {
            parsedMeta = JSON.parse(image.data.metadata);
        }
        catch (e) {
            console.log(`Failed to parse image metadata: ${e}`);
        }
    }
    let description = image.data.name + "\n" + formatMetadata(image.data.metadata);
    let name = image.data.name;
    let imageSrc = image.data.src.endsWith('.html') ? 'imgs/html.jpg' : `${image.data.src}?preview=true`;
    let searchable = description;
    return { name, description, buttons, 'image': imageSrc, className: parsedMeta.is_starred ? 'image-block-starred' : '', searchable };
}

function selectImageInHistory(image, div) {
    let curImg = document.getElementById('current_image_img');
    if (curImg && curImg.dataset.src == image.data.src) {
        curImg.click();
        return;
    }
    lastHistoryImage = image.data.src;
    lastHistoryImageDiv = div;
    if (image.data.name.endsWith('.html')) {
        window.open(image.data.src, '_blank');
    }
    else {
        if (!div.dataset.metadata) {
            div.dataset.metadata = image.data.metadata;
            div.dataset.src = image.data.src;
        }
        setCurrentImage(image.data.src, div.dataset.metadata, 'history');
    }
}

let imageHistoryBrowser = new GenPageBrowserClass('image_history', listImageHistoryFolderAndFiles, 'imagehistorybrowser', 'Thumbnails', describeImage, selectImageInHistory,
    `<label for="image_history_sort_by">Sort:</label> <select id="image_history_sort_by"><option>Name</option><option>Date</option></select> <input type="checkbox" id="image_history_sort_reverse"> <label for="image_history_sort_reverse">Reverse</label>`);

let hasAppliedFirstRun = false;
let backendsWereLoadingEver = false;
let reviseStatusInterval = null;
let currentBackendFeatureSet = [];
let rawBackendFeatureSet = [];
let lastStatusRequestPending = 0;
function reviseStatusBar() {
    if (lastStatusRequestPending + 20 * 1000 > Date.now()) {
        return;
    }
    if (session_id == null) {
        statusBarElem.innerText = 'Loading...';
        statusBarElem.className = `top-status-bar status-bar-warn`;
        return;
    }
    lastStatusRequestPending = Date.now();
    genericRequest('GetCurrentStatus', {}, data => {
        lastStatusRequestPending = 0;
        if (JSON.stringify(data.supported_features) != JSON.stringify(currentBackendFeatureSet)) {
            rawBackendFeatureSet = data.supported_features;
            currentBackendFeatureSet = data.supported_features;
            reviseBackendFeatureSet();
            hideUnsupportableParams();
        }
        doesHaveGenCountUpdateQueued = false;
        updateCurrentStatusDirect(data.status);
        let status;
        if (versionIsWrong) {
            status = { 'class': 'error', 'message': 'The server has updated since you opened the page, please refresh.' };
        }
        else {
            status = data.backend_status;
            if (data.backend_status.any_loading) {
                backendsWereLoadingEver = true;
            }
            else {
                if (!hasAppliedFirstRun) {
                    hasAppliedFirstRun = true;
                    refreshParameterValues(backendsWereLoadingEver || window.alwaysRefreshOnLoad);
                }
            }
            if (reviseStatusInterval != null) {
                if (status.class != '') {
                    clearInterval(reviseStatusInterval);
                    reviseStatusInterval = setInterval(reviseStatusBar, 2 * 1000);
                }
                else {
                    clearInterval(reviseStatusInterval);
                    reviseStatusInterval = setInterval(reviseStatusBar, 60 * 1000);
                }
            }
        }
        statusBarElem.innerText = translate(status.message);
        statusBarElem.className = `top-status-bar status-bar-${status.class}`;
    });
}

function reviseBackendFeatureSet() {
    currentBackendFeatureSet = Array.from(currentBackendFeatureSet);
    let addMe = [], removeMe = [];
    if (curModelCompatClass == 'stable-diffusion-v3-medium') {
        addMe.push('sd3');
    }
    else {
        removeMe.push('sd3');
    }
    let anyChanged = false;
    for (let add of addMe) {
        if (!currentBackendFeatureSet.includes(add)) {
            currentBackendFeatureSet.push(add);
            anyChanged = true;
        }
    }
    for (let remove of removeMe) {
        let index = currentBackendFeatureSet.indexOf(remove);
        if (index != -1) {
            currentBackendFeatureSet.splice(index, 1);
            anyChanged = true;
        }
    }
    if (anyChanged) {
        hideUnsupportableParams();
    }
}

function serverResourceLoop() {
    if (isVisible(getRequiredElementById('Server-Info'))) {
        genericRequest('GetServerResourceInfo', {}, data => {
            let target = getRequiredElementById('resource_usage_area');
            let priorWidth = 0;
            if (target.style.minWidth) {
                priorWidth = parseFloat(target.style.minWidth.replaceAll('px', ''));
            }
            target.style.minWidth = `${Math.max(priorWidth, target.offsetWidth)}px`;
            if (data.gpus) {
                let html = '<table class="simple-table"><tr><th>Resource</th><th>ID</th><th>Temp</th><th>Usage</th><th>Mem Usage</th><th>Used Mem</th><th>Free Mem</th><th>Total Mem</th></tr>';
                html += `<tr><td>CPU</td><td>...</td><td>...</td><td>${Math.round(data.cpu.usage * 100)}% (${data.cpu.cores} cores)</td><td>${Math.round(data.system_ram.used / data.system_ram.total * 100)}%</td><td>${fileSizeStringify(data.system_ram.used)}</td><td>${fileSizeStringify(data.system_ram.free)}</td><td>${fileSizeStringify(data.system_ram.total)}</td></tr>`;
                for (let gpu of Object.values(data.gpus)) {
                    html += `<tr><td>${gpu.name}</td><td>${gpu.id}</td><td>${gpu.temperature}&deg;C</td><td>${gpu.utilization_gpu}% Core, ${gpu.utilization_memory}% Mem</td><td>${Math.round(gpu.used_memory / gpu.total_memory * 100)}%</td><td>${fileSizeStringify(gpu.used_memory)}</td><td>${fileSizeStringify(gpu.free_memory)}</td><td>${fileSizeStringify(gpu.total_memory)}</td></tr>`;
                }
                html += '</table>';
                target.innerHTML = html;
            }
        });
        genericRequest('ListConnectedUsers', {}, data => {
            let target = getRequiredElementById('connected_users_list');
            let priorWidth = 0;
            if (target.style.minWidth) {
                priorWidth = parseFloat(target.style.minWidth.replaceAll('px', ''));
            }
            target.style.minWidth = `${Math.max(priorWidth, target.offsetWidth)}px`;
            let html = '<table class="simple-table"><tr><th>Name</th><th>Last Active</th><th>Active Sessions</th></tr>';
            for (let user of data.users) {
                html += `<tr><td>${user.id}</td><td>${user.last_active}</td><td>${user.active_sessions.map(sess => `${sess.count}x from ${sess.address}`).join(', ')}</td></tr>`;
            }
            html += '</table>';
            target.innerHTML = html;
        });
    }
    if (isVisible(backendsListView)) {
        backendLoopUpdate();
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
let imageEditorSizeBarVal = -1;
let midForceToBottom = localStorage.getItem('barspot_midForceToBottom') == 'true';
let leftShut = localStorage.getItem('barspot_leftShut') == 'true';

let setPageBarsFunc;
let altPromptSizeHandleFunc;

let layoutResets = [];

function resetPageSizer() {
    for (let localStore of Object.keys(localStorage).filter(k => k.startsWith('barspot_'))) {
        localStorage.removeItem(localStore);
    }
    pageBarTop = -1;
    pageBarTop2 = -1;
    pageBarMid = -1;
    imageEditorSizeBarVal = -1;
    midForceToBottom = false;
    leftShut = false;
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
    let currentImageBatchCore = getRequiredElementById('current_image_batch');
    let midSplitButton = getRequiredElementById('t2i-mid-split-quickbutton');
    let topSplitButton = getRequiredElementById('t2i-top-split-quickbutton');
    let altRegion = getRequiredElementById('alt_prompt_region');
    let altText = getRequiredElementById('alt_prompt_textbox');
    let altNegText = getRequiredElementById('alt_negativeprompt_textbox');
    let altImageRegion = getRequiredElementById('alt_prompt_extra_area');
    let editorSizebar = getRequiredElementById('image_editor_sizebar');
    let topDrag = false;
    let topDrag2 = false;
    let midDrag = false;
    let imageEditorSizeBarDrag = false;
    let isSmallWindow = window.innerWidth < 768 || window.innerHeight < 768;
    function setPageBars() {
        if (altRegion.style.display != 'none') {
            altText.style.height = 'auto';
            altText.style.height = `${Math.max(altText.scrollHeight, 15) + 5}px`;
            altNegText.style.height = 'auto';
            altNegText.style.height = `${Math.max(altNegText.scrollHeight, 15) + 5}px`;
            altRegion.style.top = `calc(-${altText.offsetHeight + altNegText.offsetHeight + altImageRegion.offsetHeight}px - 2rem)`;
        }
        setCookie('barspot_pageBarTop', pageBarTop, 365);
        setCookie('barspot_pageBarTop2', pageBarTop2, 365);
        setCookie('barspot_pageBarMidPx', pageBarMid, 365);
        setCookie('barspot_imageEditorSizeBar', imageEditorSizeBarVal, 365);
        let barTopLeft = leftShut ? `0px` : pageBarTop == -1 ? (isSmallWindow ? `14rem` : `28rem`) : `${pageBarTop}px`;
        let barTopRight = pageBarTop2 == -1 ? (isSmallWindow ? `4rem` : `21rem`) : `${pageBarTop2}px`;
        let curImgWidth = `100vw - ${barTopLeft} - ${barTopRight} - 10px`;
        // TODO: this 'eval()' hack to read the size in advance is a bit cursed.
        let fontRem = parseFloat(getComputedStyle(document.documentElement).fontSize);
        let curImgWidthNum = eval(curImgWidth.replace(/vw/g, `* ${window.innerWidth * 0.01}`).replace(/rem/g, `* ${fontRem}`).replace(/px/g, ''));
        if (curImgWidthNum < 400) {
            barTopRight = `${barTopRight} + ${400 - curImgWidthNum}px`;
            curImgWidth = `100vw - ${barTopLeft} - ${barTopRight} - 10px`;
        }
        inputSidebar.style.width = `${barTopLeft}`;
        mainInputsAreaWrapper.classList[pageBarTop < 350 ? "add" : "remove"]("main_inputs_small");
        mainInputsAreaWrapper.style.width = `${barTopLeft}`;
        inputSidebar.style.display = leftShut ? 'none' : '';
        altRegion.style.width = `calc(100vw - ${barTopLeft} - ${barTopRight} - 10px)`;
        mainImageArea.style.width = `calc(100vw - ${barTopLeft})`;
        mainImageArea.scrollTop = 0;
        if (imageEditor.active) {
            let imageEditorSizePercent = imageEditorSizeBarVal < 0 ? 0.5 : (imageEditorSizeBarVal / 100.0);
            imageEditor.inputDiv.style.width = `calc((${curImgWidth}) * ${imageEditorSizePercent} - 3px)`;
            currentImage.style.width = `calc((${curImgWidth}) * ${(1.0 - imageEditorSizePercent)} - 3px)`;
        }
        else {
            currentImage.style.width = `calc(${curImgWidth})`;
        }
        currentImageBatch.style.width = `calc(${barTopRight} - 22px)`;
        if (currentImageBatchCore.offsetWidth < 425) {
            currentImageBatchCore.classList.add('current_image_batch_core_small');
        }
        else {
            currentImageBatchCore.classList.remove('current_image_batch_core_small');
        }
        topSplitButton.innerHTML = leftShut ? '&#x21DB;' : '&#x21DA;';
        midSplitButton.innerHTML = midForceToBottom ? '&#x290A;' : '&#x290B;';
        let altHeight = altRegion.style.display == 'none' ? '0px' : `(${altText.offsetHeight + altNegText.offsetHeight + altImageRegion.offsetHeight}px + 2rem)`;
        if (pageBarMid != -1 || midForceToBottom) {
            let fixed = midForceToBottom ? `6.5rem` : `${pageBarMid}px`;
            topSplit.style.height = `calc(100vh - ${fixed})`;
            topSplit2.style.height = `calc(100vh - ${fixed})`;
            inputSidebar.style.height = `calc(100vh - ${fixed})`;
            mainInputsAreaWrapper.style.height = `calc(100vh - ${fixed})`;
            mainImageArea.style.height = `calc(100vh - ${fixed})`;
            currentImage.style.height = `calc(100vh - ${fixed} - ${altHeight})`;
            imageEditor.inputDiv.style.height = `calc(100vh - ${fixed} - ${altHeight})`;
            editorSizebar.style.height = `calc(100vh - ${fixed} - ${altHeight})`;
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
            imageEditor.inputDiv.style.height = `calc(49vh - ${altHeight})`;
            editorSizebar.style.height = `calc(49vh - ${altHeight})`;
            currentImageBatch.style.height = '';
            topBar.style.height = '';
            bottomBarContent.style.height = '';
        }
        imageEditor.resize();
        alignImageDataFormat();
        imageHistoryBrowser.makeVisible(getRequiredElementById('t2i_bottom_bar'));
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
    let cookieD = getCookie('barspot_imageEditorSizeBar');
    if (cookieD) {
        imageEditorSizeBarVal = parseInt(cookieD);
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
    topSplit.addEventListener('touchstart', (e) => {
        topDrag = true;
        e.preventDefault();
    }, true);
    topSplit2.addEventListener('touchstart', (e) => {
        topDrag2 = true;
        e.preventDefault();
    }, true);
    editorSizebar.addEventListener('mousedown', (e) => {
        imageEditorSizeBarDrag = true;
        e.preventDefault();
    }, true);
    editorSizebar.addEventListener('touchstart', (e) => {
        imageEditorSizeBarDrag = true;
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
    midSplit.addEventListener('touchstart', (e) => {
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
        triggerChangeFor(altNegText);
    }, true);
    let moveEvt = (e, x, y) => {
        let offX = x;
        offX = Math.min(Math.max(offX, 100), window.innerWidth - 10);
        if (topDrag) {
            pageBarTop = Math.min(offX - 5, 51 * 16);
            setLeftShut(pageBarTop < 300);
            setPageBars();
        }
        if (topDrag2) {
            pageBarTop2 = window.innerWidth - offX + 15;
            if (pageBarTop2 < 100) {
                pageBarTop2 = 22;
            }
            setPageBars();
        }
        if (imageEditorSizeBarDrag) {
            let maxAreaWidth = imageEditor.inputDiv.offsetWidth + currentImage.offsetWidth + 10;
            let imageAreaLeft = imageEditor.inputDiv.getBoundingClientRect().left;
            let val = Math.min(Math.max(offX - imageAreaLeft + 3, 200), maxAreaWidth - 200);
            imageEditorSizeBarVal = Math.min(90, Math.max(10, val / maxAreaWidth * 100));
            setPageBars();
        }
        if (midDrag) {
            const MID_OFF = 85;
            let refY = Math.min(Math.max(e.pageY, MID_OFF), window.innerHeight - MID_OFF);
            setMidForce(refY >= window.innerHeight - MID_OFF);
            pageBarMid = window.innerHeight - refY + topBar.getBoundingClientRect().top + 3;
            setPageBars();
        }
    };
    document.addEventListener('mousemove', (e) => moveEvt(e, e.pageX, e.pageY));
    document.addEventListener('touchmove', (e) => moveEvt(e, e.touches.item(0).pageX, e.touches.item(0).pageY));
    document.addEventListener('mouseup', (e) => {
        topDrag = false;
        topDrag2 = false;
        midDrag = false;
        imageEditorSizeBarDrag = false;
    });
    document.addEventListener('touchend', (e) => {
        topDrag = false;
        topDrag2 = false;
        midDrag = false;
        imageEditorSizeBarDrag = false;
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
        textPromptDoCount(altText, getRequiredElementById('alt_text_tokencount'));
        monitorPromptChangeForEmbed(altText.value, 'positive');
    });
    altText.addEventListener('input', () => {
        setCookie(`lastparam_input_prompt`, altText.value, 0.25);
        setPageBars();
    });
    altNegText.addEventListener('input', (e) => {
        let inputNegPrompt = document.getElementById('input_negativeprompt');
        if (inputNegPrompt) {
            inputNegPrompt.value = altNegText.value;
        }
        setCookie(`lastparam_input_negativeprompt`, altNegText.value, 0.25);
        let negTokCount = getRequiredElementById('alt_negtext_tokencount');
        if (altNegText.value == '') {
            negTokCount.style.display = 'none';
        }
        else {
            negTokCount.style.display = '';
        }
        textPromptDoCount(altNegText, negTokCount, ', Neg: ');
        monitorPromptChangeForEmbed(altNegText.value, 'negative');
    });
    altNegText.addEventListener('input', () => {
        setCookie(`lastparam_input_negativeprompt`, altNegText.value, 0.25);
        setPageBars();
    });
    function altPromptSizeHandle() {
        altRegion.style.top = `calc(-${altText.offsetHeight + altNegText.offsetHeight + altImageRegion.offsetHeight}px - 2rem)`;
        setPageBars();
    }
    altPromptSizeHandle();
    new ResizeObserver(altPromptSizeHandle).observe(altText);
    new ResizeObserver(altPromptSizeHandle).observe(altNegText);
    altPromptSizeHandleFunc = altPromptSizeHandle;
    textPromptAddKeydownHandler(altText);
    textPromptAddKeydownHandler(altNegText);
    addEventListener("resize", setPageBars);
    textPromptAddKeydownHandler(getRequiredElementById('edit_wildcard_contents'));
}

/** Clears out and resets the image-batch view, only if the user wants that. */
function resetBatchIfNeeded() {
    if (autoClearBatchElem.checked) {
        getRequiredElementById('current_image_batch').innerHTML = '';
    }
}

function loadUserData(callback) {
    genericRequest('GetMyUserData', {}, data => {
        autoCompletionsList = {};
        if (data.autocompletions) {
            let allSet = [];
            autoCompletionsList['all'] = allSet;
            for (let val of data.autocompletions) {
                let split = val.split(',');
                let datalist = autoCompletionsList[val[0]];
                let entry = { low: split[0].toLowerCase(), raw: val };
                if (!datalist) {
                    datalist = [];
                    autoCompletionsList[val[0]] = datalist;
                }
                datalist.push(entry);
                allSet.push(entry);
            }
        }
        else {
            autoCompletionsList = null;
        }
        allPresets = data.presets;
        if (!language) {
            language = data.language;
        }
        sortPresets();
        presetBrowser.update();
        if (shouldApplyDefault) {
            shouldApplyDefault = false;
            let defaultPreset = getPresetByTitle('default');
            if (defaultPreset) {
                applyOnePreset(defaultPreset);
            }
        }
        if (callback) {
            callback();
        }
        loadAndApplyTranslations();
    });
}

function updateAllModels(models) {
    coreModelMap = models;
    allModels = models['Stable-Diffusion'];
    let selector = getRequiredElementById('current_model');
    let selectorVal = selector.value;
    selector.innerHTML = '';
    let emptyOption = document.createElement('option');
    emptyOption.value = '';
    emptyOption.innerText = '';
    selector.appendChild(emptyOption);
    for (let model of allModels) {
        let option = document.createElement('option');
        let clean = cleanModelName(model);
        option.value = clean;
        option.innerText = clean;
        selector.appendChild(option);
    }
    selector.value = selectorVal;
    pickle2safetensor_load();
}

let shutdownConfirmationText = translatable("Are you sure you want to shut StableSwarmUI down?");

function shutdown_server() {
    if (confirm(shutdownConfirmationText.get())) {
        genericRequest('ShutdownServer', {}, data => {
            close();
        });
    }
}

let restartConfirmationText = translatable("Are you sure you want to update and restart StableSwarmUI?");
let checkingForUpdatesText = translatable("Checking for updates...");

function update_and_restart_server() {
    let noticeArea = getRequiredElementById('shutdown_notice_area');
    if (confirm(restartConfirmationText.get())) {
        noticeArea.innerText = checkingForUpdatesText.get();
        genericRequest('UpdateAndRestart', {}, data => {
            noticeArea.innerText = data.result;
        });
    }
}

function server_clear_vram() {
    genericRequest('FreeBackendMemory', { 'system_ram': false }, data => {});
}

function server_clear_sysram() {
    genericRequest('FreeBackendMemory', { 'system_ram': true }, data => {});
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

function doFeatureInstaller(path, author, name, button_div_id, alt_confirm = null, callback = null) {
    if (!confirm(alt_confirm || `This will install ${path} which is a third-party extension maintained by community developer '${author}'.\nWe cannot make any guarantees about it.\nDo you wish to install?`)) {
        return;
    }
    let buttonDiv = getRequiredElementById(button_div_id);
    buttonDiv.querySelector('button').disabled = true;
    buttonDiv.appendChild(createDiv('', null, 'Installing...'));
    genericRequest('ComfyInstallFeatures', {'feature': name}, data => {
        buttonDiv.appendChild(createDiv('', null, "Installed! Please wait while backends restart. If it doesn't work, you may need to restart Swarm."));
        reviseStatusBar();
        setTimeout(() => {
            buttonDiv.remove();
            hasAppliedFirstRun = false;
            reviseStatusBar();
            if (callback) {
                callback();
            }
        }, 8000);
    }, 0, (e) => {
        showError(e);
        buttonDiv.appendChild(createDiv('', null, 'Failed to install!'));
        buttonDiv.querySelector('button').disabled = false;
    });
}

function revisionInstallIPAdapter() {
    doFeatureInstaller('https://github.com/cubiq/ComfyUI_IPAdapter_plus', 'cubiq', 'ipadapter', 'revision_install_ipadapter');
}

function installControlnetPreprocessors() {
    doFeatureInstaller('https://github.com/Fannovel16/comfyui_controlnet_aux', 'Fannovel16', 'controlnet_preprocessors', 'controlnet_install_preprocessors');
}

function installVideoRife() {
    doFeatureInstaller('https://github.com/Fannovel16/ComfyUI-Frame-Interpolation', 'Fannovel16', 'frame_interpolation', 'video_install_frameinterps');
}

function installTensorRT() {
    doFeatureInstaller('https://github.com/comfyanonymous/ComfyUI_TensorRT', 'comfyanonymous + NVIDIA', 'comfyui_tensorrt', 'install_trt_button', `This will install TensorRT support developed by Comfy and NVIDIA.\nDo you wish to install?`, () => {
        getRequiredElementById('tensorrt_mustinstall').style.display = 'none';
        getRequiredElementById('tensorrt_modal_ready').style.display = '';
    });
}

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

function showRevisionInputs(toggleOn = false) {
    let revisionGroup = document.getElementById('input_group_revision');
    let revisionToggler = document.getElementById('input_group_content_revision_toggle');
    if (revisionGroup) {
        toggleGroupOpen(revisionGroup, true);
        if (toggleOn) {
            revisionToggler.checked = true;
            triggerChangeFor(revisionToggler);
        }
        revisionGroup.style.display = '';
    }
}

function autoRevealRevision() {
    let promptImageArea = getRequiredElementById('alt_prompt_image_area');
    if (promptImageArea.children.length > 0) {
        showRevisionInputs();
    }
    else {
        hideRevisionInputs();
    }
}

function revisionAddImage(file) {
    let clearButton = getRequiredElementById('alt_prompt_image_clear_button');
    let promptImageArea = getRequiredElementById('alt_prompt_image_area');
    let reader = new FileReader();
    reader.onload = (e) => {
        let data = e.target.result;
        let imageObject = new Image();
        imageObject.src = data;
        imageObject.height = 128;
        imageObject.className = 'alt-prompt-image';
        imageObject.dataset.filedata = data;
        clearButton.style.display = '';
        showRevisionInputs(true);
        promptImageArea.appendChild(imageObject);
        altPromptSizeHandleFunc();
    };
    reader.readAsDataURL(file);
}

function revisionInputHandler() {
    let dragArea = getRequiredElementById('alt_prompt_region');
    dragArea.addEventListener('dragover', (e) => {
        e.preventDefault();
        e.stopPropagation();
    });
    let clearButton = getRequiredElementById('alt_prompt_image_clear_button');
    clearButton.addEventListener('click', () => {
        hideRevisionInputs();
    });
    dragArea.addEventListener('drop', (e) => {
        if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
            e.preventDefault();
            e.stopPropagation();
            for (let file of e.dataTransfer.files) {
                if (file.type.startsWith('image/')) {
                    revisionAddImage(file);
                }
            }
        }
    });
}
revisionInputHandler();

function revisionImagePaste(e) {
    let items = (e.clipboardData || e.originalEvent.clipboardData).items;
    for (let item of items) {
        if (item.kind === 'file') {
            let file = item.getAsFile();
            if (file.type.startsWith('image/')) {
                revisionAddImage(file);
            }
        }
    }
}

function openEmptyEditor() {
    let canvas = document.createElement('canvas');
    canvas.width = document.getElementById('input_width').value;
    canvas.height = document.getElementById('input_height').value;
    let ctx = canvas.getContext('2d');
    ctx.fillStyle = 'white';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    let image = new Image();
    image.onload = () => {
        imageEditor.clearVars();
        imageEditor.setBaseImage(image);
        imageEditor.activate();
    };
    image.src = canvas.toDataURL();
}

function upvertAutoWebuiMetadataToSwarm(metadata) {
    let realData = {};
    [realData['prompt'], remains] = metadata.split("\nNegative prompt: ");
    let lines = remains.split('\n');
    realData['negativeprompt'] = lines.slice(0, -1).join('\n');
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

const imageMetadataKeys = ['prompt', 'Prompt', 'parameters', 'Parameters', 'userComment', 'UserComment', 'model', 'Model'];

function imageInputHandler() {
    let imageArea = getRequiredElementById('current_image');
    imageArea.addEventListener('dragover', (e) => {
        e.preventDefault();
        e.stopPropagation();
    });
    imageArea.addEventListener('drop', (e) => {
        if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
            e.preventDefault();
            e.stopPropagation();
            let file = e.dataTransfer.files[0];
            if (file.type.startsWith('image/')) {
                let reader = new FileReader();
                parsemetadata = (e) => {
                    let data = e.target.result;
                    exifr.parse(data).then(parsed => {
                        if (parsed && imageMetadataKeys.some(key => key in parsed)) {
                            return parsed;
                        }
                        return exifr.parse(data, imageMetadataKeys);
                    }).then(parsed => {
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
                            else if (parsed.UserComment) {
                                metadata = parsed.UserComment;
                            }
                            else if (parsed.userComment) {
                                metadata = parsed.userComment;
                            }
                            else if (parsed.model) {
                                metadata = parsed.model;
                            }
                            else if (parsed.Model) {
                                metadata = parsed.Model;
                            }
                        }
                        if (metadata instanceof Uint8Array) {
                            let prefix = metadata.slice(0, 8);
                            let data = metadata.slice(8);
                            let encodeType = new TextDecoder().decode(prefix);
                            metadata = encodeType.startsWith('UNICODE') ? decodeUtf16(data) : new TextDecoder().decode(data);
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
                                    metadata = upvertAutoWebuiMetadataToSwarm(metadata);
                                }
                                else {
                                    // ???
                                    metadata = null;
                                }
                            }
                        }
                        setCurrentImage(data, metadata);
                    }).catch(err => {
                        setCurrentImage(e.target.result, null);
                    });
                };
                reader.onload = (e) => {
                    try {
                        parsemetadata(e);
                    }
                    catch (e) {
                        setCurrentImage(e.target.result, null);
                    }
                }
                reader.readAsDataURL(file);
            }
        }
    });
}
imageInputHandler();

function debugGenAPIDocs() {
    genericRequest('DebugGenDocs', { }, data => { });
}

let hashSubTabMapping = {
    'utilities_tab': 'utilitiestablist',
    'user_tab': 'usertablist',
    'server_tab': 'servertablist',
};

function updateHash() {
    let tabList = getRequiredElementById('toptablist');
    let bottomTabList = getRequiredElementById('bottombartabcollection');
    let activeTopTab = tabList.querySelector('.active');
    let activeBottomTab = bottomTabList.querySelector('.active');
    let activeTopTabHref = activeTopTab.href.split('#')[1];
    let hash = `#${activeBottomTab.href.split('#')[1]},${activeTopTabHref}`;
    let subMapping = hashSubTabMapping[activeTopTabHref];
    if (subMapping) {
        let subTabList = getRequiredElementById(subMapping);
        let activeSubTab = subTabList.querySelector('.active');
        hash += `,${activeSubTab.href.split('#')[1]}`;
    }
    else if (activeTopTabHref == 'Simple') {
        let target = simpleTab.browser.selected || simpleTab.browser.folder;
        if (target) {
            hash += `,${encodeURIComponent(target)}`;
        }
    }
    history.pushState(null, null, hash);
}

function loadHashHelper() {
    let tabList = getRequiredElementById('toptablist');
    let bottomTabList = getRequiredElementById('bottombartabcollection');
    let tabs = [... tabList.getElementsByTagName('a')];
    tabs = tabs.concat([... bottomTabList.getElementsByTagName('a')]);
    for (let subMapping of Object.values(hashSubTabMapping)) {
        tabs = tabs.concat([... getRequiredElementById(subMapping).getElementsByTagName('a')]);
    }
    if (location.hash) {
        let split = location.hash.substring(1).split(',');
        let bottomTarget = bottomTabList.querySelector(`a[href='#${split[0]}']`);
        if (bottomTarget) {
            bottomTarget.click();
        }
        let target = tabList.querySelector(`a[href='#${split[1]}']`);
        if (target) {
            target.click();
        }
        let subMapping = hashSubTabMapping[split[1]];
        if (subMapping && split.length > 2) {
            let subTabList = getRequiredElementById(subMapping);
            let subTarget = subTabList.querySelector(`a[href='#${split[2]}']`);
            if (subTarget) {
                subTarget.click();
            }
        }
        else if (split[1] == 'Simple' && split.length > 2) {
            let target = decodeURIComponent(split[2]);
            simpleTab.mustSelectTarget = target;
        }
    }
    for (let tab of tabs) {
        tab.addEventListener('click', (e) => {
            updateHash();
        });
    }
}

function storeImageToHistoryWithCurrentParams(img) {
    let data = getGenInput();
    data['image'] = img;
    delete data['initimage'];
    delete data['maskimage'];
    genericRequest('AddImageToHistory', data, res => {
        mainGenHandler.gotImageResult(res.images[0].image, res.images[0].metadata, '0');
    });
}

function genpageLoad() {
    console.log('Load page...');
    window.imageEditor = new ImageEditor(getRequiredElementById('image_editor_input'), true, true, () => setPageBarsFunc(), () => needsNewPreview());
    let editorSizebar = getRequiredElementById('image_editor_sizebar');
    window.imageEditor.onActivate = () => {
        editorSizebar.style.display = '';
    };
    window.imageEditor.onDeactivate = () => {
        editorSizebar.style.display = 'none';
    };
    window.imageEditor.tools['options'].optionButtons = [
        ... window.imageEditor.tools['options'].optionButtons,
        { key: 'Store Current Image To History', action: () => {
            let img = window.imageEditor.getFinalImageData();
            storeImageToHistoryWithCurrentParams(img);
        }},
        { key: 'Store Full Canvas To History', action: () => {
            let img = window.imageEditor.getMaximumImageData();
            storeImageToHistoryWithCurrentParams(img);
        }}
    ];
    pageSizer();
    reviseStatusBar();
    loadHashHelper();
    getSession(() => {
        console.log('First session loaded - prepping page.');
        imageHistoryBrowser.navigate('');
        initialModelListLoad();
        loadBackendTypesMenu();
        genericRequest('ListT2IParams', {}, data => {
            updateAllModels(data.models);
            allWildcards = data.wildcards;
            rawGenParamTypesFromServer = sortParameterList(data.list);
            gen_param_types = rawGenParamTypesFromServer;
            paramConfig.preInit();
            paramConfig.applyParamEdits(data.param_edits);
            paramConfig.loadUserParamConfigTab();
            genInputs();
            genToolsList();
            reviseStatusBar();
            getRequiredElementById('advanced_options_checkbox').checked = localStorage.getItem('display_advanced') == 'true';
            toggle_advanced();
            setCurrentModel();
            loadUserData();
            for (let callback of sessionReadyCallbacks) {
                callback();
            }
            automaticWelcomeMessage();
        });
        reviseStatusInterval = setInterval(reviseStatusBar, 2000);
        window.resLoopInterval = setInterval(serverResourceLoop, 1000);
    });
}

setTimeout(genpageLoad, 1);
