
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
    let togglerSpan = document.createElement('span');
    togglerSpan.className = 'form-check form-switch display-inline-block';
    let toggleSwitch = document.createElement('input');
    toggleSwitch.type = 'checkbox';
    toggleSwitch.className = 'form-check-input backend-toggle-switch';
    toggleSwitch.title = 'Enable/Disable backend';
    toggleSwitch.checked = backend.enabled;
    toggleSwitch.addEventListener('change', () => {
        backend.enabled = toggleSwitch.checked;
        genericRequest('ToggleBackend', {'backend_id': backend.id, 'enabled': toggleSwitch.checked}, data => {
            loadBackendsList();
        });
    });
    togglerSpan.appendChild(toggleSwitch);
    cardHeader.appendChild(togglerSpan);
    let cardTitleSpan = document.createElement('span');
    let cardTitleStatus = document.createElement('span');
    cardTitleStatus.className = 'card-title-status';
    cardTitleStatus.innerText = backend.status;
    cardTitleSpan.appendChild(cardTitleStatus);
    let cardTitleCenter = document.createElement('span');
    cardTitleCenter.innerText = ` backend: (${backend.id}): `;
    cardTitleSpan.appendChild(cardTitleCenter);
    let actualCardTitle = document.createElement('span');
    actualCardTitle.innerText = backend.title || type.name;
    cardTitleSpan.appendChild(actualCardTitle);
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
    let buttons = document.createElement('div');
    let isLogAvailable = serverLogs.matchIdentifier(`backend-${backend.id}`) != null;
    buttons.innerHTML = `<button class="basic-button backend-restart-button" disabled onclick="restart_backend('${backend.id}')">Restart</button> <button class="basic-button backend-log-view-button"${isLogAvailable ? '' : ' disabled'} onclick="serverLogs.showLogsForIdentifier('backend-${backend.id}')">View Logs</button> <span class="backend-last-used-time">Last used: <code>${backend.time_since_used}</code></span>`;
    cardBody.appendChild(buttons);
    for (let setting of type.settings) {
        let input = document.createElement('div');
        let pop = `<div class="sui-popover" id="popover_setting_${backend.id}_${setting.name}"><b>${escapeHtml(setting.name)}</b> (${setting.type}):<br>&emsp;${escapeHtml(setting.description)}</div>`;
        if (setting.type == 'text') {
            input.innerHTML = makeTextInput(null, `setting_${backend.id}_${setting.name}`, '', setting.name, setting.description, backend.settings[setting.name], 'normal', setting.placeholder) + pop;
        }
        else if (setting.type == 'integer') {
            input.innerHTML = makeNumberInput(null, `setting_${backend.id}_${setting.name}`, '', setting.name, setting.description, backend.settings[setting.name], 0, 1000, 1) + pop;
        }
        else if (setting.type == 'bool') {
            input.innerHTML = makeCheckboxInput(null, `setting_${backend.id}_${setting.name}`, '', setting.name, setting.description, backend.settings[setting.name]) + pop;
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
    if (!disable) {
        actualCardTitle.contentEditable = true;
        actualCardTitle.classList.add('backend-title-editable');
    }
    actualCardTitle.addEventListener('keydown', e => {
        if (e.key == 'Enter') {
            e.preventDefault();
        }
    });
    editButton.addEventListener('click', () => {
        saveButton.style.display = 'inline-block';
        editButton.disabled = true;
        actualCardTitle.contentEditable = true;
        actualCardTitle.classList.add('backend-title-editable');
        for (let entry of cardBody.querySelectorAll('[data-name]')) {
            entry.disabled = false;
        }
    });
    saveButton.addEventListener('click', () => {
        saveButton.style.display = 'none';
        actualCardTitle.contentEditable = false;
        actualCardTitle.classList.remove('backend-title-editable');
        for (let entry of cardBody.querySelectorAll('[data-name]')) {
            let name = entry.dataset.name;
            let value = entry.type == 'checkbox' ? entry.checked : entry.value;
            backend.settings[name] = value;
            entry.disabled = true;
        }
        genericRequest('EditBackend', {'backend_id': backend.id, 'title': actualCardTitle.textContent, 'settings': backend.settings}, data => {
            addBackendToHtml(data, true, spot);
        });
    });
}

function loadBackendsList() {
    reviseStatusBar();
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
                let card = document.getElementById(`backend-card-${oldBack.id}`);
                if (oldBack.status != newBack.status) {
                    card.classList.remove(`backend-${oldBack.status}`);
                    card.classList.add(`backend-${newBack.status}`);
                    card.querySelector('.card-title-status').innerText = newBack.status;
                }
                card.querySelector('.backend-restart-button').disabled = newBack.status != 'errored' && newBack.status != 'running';
                card.querySelector('.backend-log-view-button').disabled = serverLogs.matchIdentifier(`backend-${newBack.id}`) == null;
                card.querySelector('.backend-last-used-time').innerHTML = `Last used: <code>${newBack.time_since_used}</code>`;
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
        reviseStatusBar();
    });
}

function countBackendsByStatus(status) {
    return Object.values(backends_loaded).filter(x => x.enabled && x.status == status).length;
}

function toggleShowAdvancedBackends() {
    let showAdvanced = document.getElementById('backends_show_advanced').checked;
    for (let button of document.querySelectorAll('#backend_add_buttons button')) {
        if (button.dataset.isStandard == 'false') {
            button.style.display = showAdvanced ? 'inline-block' : 'none';
        }
    }
}

function loadBackendTypesMenu() {
    let addButtonsSection = document.getElementById('backend_add_buttons');
    genericRequest('ListBackendTypes', {}, data => {
        backend_types = {};
        addButtonsSection.innerHTML = '';
        for (let type of data.list) {
            backend_types[type.id] = type;
            let button = document.createElement('button');
            button.dataset.isStandard = type.is_standard;
            button.title = type.description;
            button.innerText = type.name;
            if (!type.is_standard) {
                button.style.display = 'none';
            }
            let id = type.id;
            button.addEventListener('click', () => { addNewBackend(id); });
            addButtonsSection.appendChild(button);
        }
        loadBackendsList();
    });
}

let backendsListView = document.getElementById('backends_list');
let backendsCheckRateCounter = 0;

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
    if (isVisible(backendsListView)) {
        serverLogs.onTabButtonClick();
        if (backendsCheckRateCounter++ % 3 == 0) {
            loadBackendsList(); // TODO: only if have permission
        }
    }
    else {
        backendsCheckRateCounter = 0;
    }
}

function restart_backend(id) {
    if (confirm(`Are you sure you want to restart backend ${id}?`)) {
        genericRequest('RestartBackends', {backend: `${id}`}, data => {
            loadBackendsList();
        });
    }
}

function restart_all_backends() {
    if (confirm('Are you sure you want to restart all backends?')) {
        genericRequest('RestartBackends', {}, data => {
            loadBackendsList();
        });
    }
}
