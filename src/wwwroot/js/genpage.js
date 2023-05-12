let core_inputs = ['prompt', 'negative_prompt', 'seed', 'steps', 'width', 'height', 'images', 'cfg_scale'];

let session_id = null;

let batches = 0;

let backend_types = {};

let backends_loaded = [];

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

function addNewBackend(type_id) {
    genericRequest('AddNewBackend', {'type_id': type_id}, data => {
        addBackendToHtml(data, false);
    });
}

function addBackendToHtml(backend, disable, spot = null) {
    if (spot == null) {
        spot = createDiv(`backend-wrapper-spot-${backend.id}`, 'backend-wrapper-spot');
        document.getElementById('backends_list').appendChild(spot);
    }
    spot.innerHTML = '';
    let type = backend_types[backend.type];
    let cardBase = createDiv(null, `card backend-${(backend.valid ? 'active' : 'dead')} backend-card`);
    let cardHeader = createDiv(null, 'card-header');
    cardHeader.innerText = `${(backend.valid ? 'Loaded Backend' : 'Inactive Backend')} (${backend.id}): ${type.name}`;
    let deleteButton = document.createElement('button');
    deleteButton.className = 'backend-delete-button';
    deleteButton.innerText = '✕';
    deleteButton.title = 'Delete';
    let editButton = document.createElement('button');
    editButton.className = 'backend-edit-button';
    editButton.innerText = '✎';
    editButton.title = 'Edit';
    editButton.disabled = !disable;
    let saveButton = document.createElement('button');
    saveButton.className = 'backend-save-button';
    saveButton.innerText = 'Save';
    saveButton.title = 'Save changes';
    saveButton.style.display = disable ? 'none' : 'inline-block';
    cardHeader.appendChild(deleteButton);
    cardHeader.appendChild(editButton);
    cardHeader.appendChild(saveButton);
    deleteButton.addEventListener('click', () => {
        if (confirm(`Are you sure you want to delete backend ${backend.id} (${type.name})?`)) {
            genericRequest('DeleteBackend', {'backend_id': backend.id}, data => {
                cardBase.remove();
            });
        }
    });
    let cardBody = createDiv(null, 'card-body');
    for (let setting of type.settings) {
        let input;
        if (setting.type == 'text') {
            input = document.createElement('div');
            input.innerHTML = makeTextInput(`setting_${backend.id}_${setting.name}`, setting.name, setting.description, backend.settings[setting.name], 1, setting.placeholder);
        }
        else {
            console.log(`Cannot create input slot of type ${setting.type}`);
        }
        cardBody.appendChild(input);
    }
    cardBase.appendChild(cardHeader);
    cardBase.appendChild(cardBody);
    spot.appendChild(cardBase);
    for (let entry of cardBody.querySelectorAll('[data-name]')) {
        entry.disabled = disable;
    }
    editButton.addEventListener('click', () => {
        saveButton.style.display = 'inline-block';
        editButton.disabled = true;
        for (let entry of cardBody.querySelectorAll('[data-name]')) {
            entry.disabled = false;
        }
    });
    saveButton.addEventListener('click', () => {
        saveButton.style.display = 'none';
        for (let entry of cardBody.querySelectorAll('[data-name]')) {
            let name = entry.dataset.name;
            let value = entry.value;
            backend.settings[name] = value;
            entry.disabled = true;
            console.log(`Setting ${name} to ${value} from ${entry}`)
        }
        genericRequest('EditBackend', {'backend_id': backend.id, 'settings': backend.settings}, data => {
            addBackendToHtml(data, true, spot);
        });
    });
}

function loadBackendsList() {
    let listSection = document.getElementById('backends_list');
    genericRequest('ListBackends', {}, data => {
        backends_loaded = data.list;
        listSection.innerHTML = '';
        for (let backend of backends_loaded) {
            addBackendToHtml(backend, true);
        }
    });
}

function loadBackendTypesMenu() {
    let addButtonsSection = document.getElementById('backend_add_buttons');
    genericRequest('ListBackendTypes', {}, data => {
        backend_types = {};
        addButtonsSection.innerHTML = '';
        for (let type of data.list) {
            backend_types[type.id] = type;
            let button = document.createElement('button');
            button.title = type.description;
            button.innerText = type.name;
            let id = type.id;
            button.addEventListener('click', () => { addNewBackend(id); });
            addButtonsSection.appendChild(button);
        }
        loadBackendsList();
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

function genpageLoad() {
    document.getElementById('generate_button').addEventListener('click', doGenerate);
    document.getElementById('image_history_refresh_button').addEventListener('click', () => loadHistory(lastImageDir));
    document.getElementById('model_list_refresh_button').addEventListener('click', () => loadModelList(lastModelDir));
    getSession(() => {
        loadHistory('');
        loadModelList('');
        loadBackendTypesMenu();
    });
}

genpageLoad();
