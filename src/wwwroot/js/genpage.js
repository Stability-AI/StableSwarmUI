let core_inputs = ['prompt', 'negative_prompt', 'seed', 'steps', 'width', 'height', 'images', 'cfg_scale'];

let session_id = null;

let batches = 0;

let lastImageDir = '';

let lastModelDir = '';

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

function setCurrentImage(src) {
    let curImg = document.getElementById('current_image');
    curImg.innerHTML = '';
    let img = document.createElement('img');
    img.src = src;
    curImg.appendChild(img);
}

function appendImage(container, imageSrc, batchId, textPreview) {
    if (typeof container == 'string') {
        container = document.getElementById(container);
    }
    let div = createDiv(null, `image-block image-batch-${batchId % 2}`);
    div.dataset.batch_id = batchId;
    let img = document.createElement('img');
    img.addEventListener('load', () => {
        let ratio = img.width / img.height;
        div.style.width = `${(ratio * 8) + 2}rem`;
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
    let src = image;
    let batch_div = appendImage('current_image_batch', src, batches, '(TODO)');
    batch_div.addEventListener('click', () => clickImageInBatch(batch_div));
    let history_div = appendImage('image_history', src, batches, '(TODO)');
    history_div.addEventListener('click', () => selectImageInHistory(history_div));
    setCurrentImage(src);
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
    let input = {};
    for (let id of core_inputs) {
        input[id] = document.getElementById('input_' + id).value;
    }
    document.getElementById('current_image_batch').innerHTML = '';
    batches++;
    makeWSRequest('GenerateText2ImageWS', input, data => {
        gotImageResult(data.image);
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

function loadFileList(api, path, container, loadCaller, fileCallback) {
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
        for (let folder of data.folders) {
            let div = appendImage(container, '/imgs/folder.png', 'folder', `${folder}/`);
            let helper = new FileListCallHelper(`${prefix}${folder}`, loadCaller);
            div.addEventListener('click', helper.call.bind(helper));
        }
        container.appendChild(document.createElement('br'));
        for (let file of data.files) {
            fileCallback(prefix, file);
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
    });
}

let models = {};
let cur_model = null;

function appendModel(container, prefix, model) {
    models[`${prefix}${model.name}`] = model;
    let batch = model.loaded ? 'model-loaded' : `image-batch-${Object.keys(models).length % 2}`;
    let div = createDiv(null, `model-block model-block-hoverable ${batch}`);
    let img = document.createElement('img');
    img.src = model.preview_image;
    div.appendChild(img);
    let textBlock = createDiv(null, 'model-descblock');
    textBlock.innerText = `${model.name}\n${model.description}`;
    div.appendChild(textBlock);
    container.appendChild(div);
    div.addEventListener('click', () => {
        for (let possible of container.getElementsByTagName('div')) {
            possible.classList.remove('model-block-hoverable');
            possible.parentElement.replaceChild(possible.cloneNode(true), possible);
        }
        genericRequest('SelectModel', {'model': model.name}, data => {
            loadModelList(lastModelDir);
        });
    });
}

function loadModelList(path) {
    let container = document.getElementById('model_list');
    lastModelDir = path;
    container.innerHTML = '';
    let id = 0;
    models = {};
    loadFileList('ListModels', path, container, loadModelList, (prefix, model) => {
        appendModel(container, prefix, model);
    });
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

function genInputs() {
    let area = document.getElementById('main_inputs_area');
    let html = '';
    html += makeNumberInput(null, 'input_images', 'Images', 'How many images to generate at once.', 1, 1, 100, 1, true);
    html += makeNumberInput('seed', 'input_seed', 'Seed', 'Image seed. -1 = random.', -1, -1, 1000000000, 1, true);
    html += makeNumberInput('steps', 'input_steps', 'Steps', 'How many times to run the model. More steps = better quality, but more time.', 20, 1, 100, 1, true);
    html += makeNumberInput('cfg_scale', 'input_cfg_scale', 'CFG Scale', 'How strongly to scale prompt input. Too-high values can cause corrupted/burnt images, too-low can cause nonsensical images.', 7, 0, 30, 0.25, 1, true);
    html += '<br>' + makeSliderInput('width', 'input_width', 'Width', 'Image width, in pixels.', 1024, 128, 4096, 64, true);
    html += '<br>' + makeSliderInput('height', 'input_height', 'Height', 'Image height, in pixels.', 1024, 128, 4096, 64, true);
    area.innerHTML = html;
    enableSlidersIn(area);
}

function genpageLoad() {
    genInputs();
    reviseStatusBar();
    document.getElementById('generate_button').addEventListener('click', doGenerate);
    document.getElementById('image_history_refresh_button').addEventListener('click', () => loadHistory(lastImageDir));
    document.getElementById('model_list_refresh_button').addEventListener('click', () => loadModelList(lastModelDir));
    getSession(() => {
        loadHistory('');
        loadModelList('');
        loadBackendTypesMenu();
    });
    setInterval(genpageLoop, 1000);
}

genpageLoad();
