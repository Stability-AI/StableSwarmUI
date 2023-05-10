let core_inputs = ['prompt', 'negative_prompt', 'seed', 'steps', 'width', 'height', 'images', 'cfg_scale'];

let session_id = null;

let batches = 0;

let backend_types = {};

let backends_loaded = [];

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

function appendImage(spot, imageSrc, batchId, textPreview) {
    let div = createDiv(null, `image-block image-bitch-${batchId % 2}`);
    div.dataset.batch_id = batchId;
    let img = document.createElement('img');
    img.addEventListener('load', () => {
        div.style.width = `calc(${img.width}px + 2rem)`;
    });
    img.src = imageSrc;
    div.appendChild(img);
    let textBlock = createDiv(null, 'image-preview-text');
    textBlock.innerText = textPreview;
    div.appendChild(textBlock);
    document.getElementById(spot).appendChild(div);
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

function loadHistory(path) {
    genericRequest('ListImages', {'path': path}, data => {
        document.getElementById('image_history').innerHTML = '';
        let prefix;
        if (path == '') {
            prefix = '';
        }
        else {
            prefix = path + '/';
            let above = path.split('/').slice(0, -1).join('/');
            let div = appendImage('image_history', '/imgs/folder_up.png', 'folder', `../`);
            div.addEventListener('click', () => loadHistory(above));
        }
        for (let folder of data.folders) {
            let div = appendImage('image_history', '/imgs/folder.png', 'folder', `${folder}/`);
            div.addEventListener('click', () => loadHistory(`${prefix}${folder}`));
        }
        for (let img of data.images) {
            let div = appendImage('image_history', `Output/${prefix}${img.src}`, img.batch_id, img.src);
            div.addEventListener('click', () => selectImageInHistory(div));
        }
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

function genpageLoad() {
    document.getElementById('generate_button').addEventListener('click', doGenerate);
    getSession(() => {
        loadHistory('');
        loadBackendTypesMenu();
    });
}

genpageLoad();
