
let backend_types = {};

let backends_loaded = {};

let backendsRevisedCallbacks = [];

let hasLoadedBackends = false;

function addNewBackend(type_id) {
    if (confirm(`Are you sure you want to add a new backend of type ${backend_types[type_id].name}?`)) {
        genericRequest('AddNewBackend', {'type_id': type_id}, data => {
            backends_loaded[data.id] = data;
            addBackendToHtml(data, false);
        });
    }
}

function addBackendToHtml(backend, disable, spot = null) {
    if (spot == null) {
        spot = createDiv(`backend-wrapper-spot-${backend.id}`, 'backend-wrapper-spot');
        document.getElementById('backends_list').appendChild(spot);
    }
    spot.innerHTML = '';
    let type = backend_types[backend.type];
    let cardBase = createDiv(`backend-card-${backend.id}`, `card backend-${backend.status} backend-card`);
    let cardHeader = createDiv(null, 'card-header');
    let cardTitleSpan = document.createElement('span');
    cardTitleSpan.className = 'card-title-span';
    cardTitleSpan.innerText = `${backend.status} backend: (${backend.id}): ${type.name}`;
    cardHeader.appendChild(cardTitleSpan);
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
        let input = document.createElement('div');
        let pop = `<div class="sui-popover" id="popover_setting_${backend.id}_${setting.name}"><b>${escapeHtml(setting.name)}</b> (${setting.type}):<br>&emsp;${escapeHtml(setting.description)}</div>`;
        if (setting.type == 'text') {
            input.innerHTML = makeTextInput(null, `setting_${backend.id}_${setting.name}`, setting.name, setting.description, backend.settings[setting.name], 1, setting.placeholder) + pop;
        }
        else if (setting.type == 'integer') {
            input.innerHTML = makeNumberInput(null, `setting_${backend.id}_${setting.name}`, setting.name, setting.description, backend.settings[setting.name], 0, 1000, 1) + pop;
        }
        else if (setting.type == 'bool') {
            input.innerHTML = makeCheckboxInput(null, `setting_${backend.id}_${setting.name}`, setting.name, setting.description, backend.settings[setting.name]) + pop;
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
        }
        genericRequest('EditBackend', {'backend_id': backend.id, 'settings': backend.settings}, data => {
            addBackendToHtml(data, true, spot);
        });
    });
}

function loadBackendsList() {
    genericRequest('ListBackends', {}, data => {
        hasLoadedBackends = true;
        for (let oldBack of Object.values(backends_loaded)) {
            let spot = document.getElementById(`backend-wrapper-spot-${oldBack.id}`);
            let newBack = data[oldBack.id];
            if (!newBack) {
                delete backends_loaded[oldBack.id];
                spot.remove();
            }
            else {
                if (oldBack.status != newBack.status) {
                    let card = document.getElementById(`backend-card-${oldBack.id}`);
                    card.classList.remove(`backend-${oldBack.status}`);
                    card.classList.add(`backend-${newBack.status}`);
                    card.querySelector('.card-title-span').innerText = `${newBack.status} backend: (${newBack.id}): ${backend_types[newBack.type].name}`;
                }
                if (newBack.modcount > oldBack.modcount) {
                    addBackendToHtml(newBack, true, spot);
                }
            }
        }
        for (let newBack of Object.values(data)) {
            let oldBack = backends_loaded[newBack.id];
            if (!oldBack) {
                addBackendToHtml(newBack, true);
            }
        }
        backends_loaded = data;
        hideUnsupportableParams();
        for (let callback of backendsRevisedCallbacks) {
            callback();
        }
    });
}

function countBackendsByStatus(status) {
    return Object.values(backends_loaded).filter(x => x.status == status).length;
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

let backendsListView = document.getElementById('backends_list');
let backendsCheckRateCounter = 0;
let hasAppliedFirstRun = false;

function isVisible(element) {
    // DOM Element visibility isn't supported in all browsers
    // https://caniuse.com/mdn-api_element_checkvisibility
    if (typeof element.checkVisibility != "undefined") {
        return element.checkVisibility();
    } else {
        return !(element.offsetParent === null);
    }
}

function backendLoopUpdate() {
    let loading = countBackendsByStatus('loading') + countBackendsByStatus('waiting');

    if (loading > 0 || !isVisible(backendsListView)) {
        if (backendsCheckRateCounter++ % 5 == 0) {
            loadBackendsList(); // TODO: only if have permission
        }
    }
    else {
        if (!hasAppliedFirstRun) {
            hasAppliedFirstRun = true;
            refreshParameterValues();
        }
        backendsCheckRateCounter = 0;
    }
}
