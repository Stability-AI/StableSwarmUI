let serverSettingsContainer = getRequiredElementById('server_settings_container');
let userSettingsContainer = getRequiredElementById('user_settings_container');

let userSettingsData = {
    known: {},
    altered: {}
};

let serverSettingsData = {
    known: {},
    altered: {}
};

function buildSettingsMenu(container, data, prefix, tracker) {
    let content = '';
    let runnables = [];
    let keys = [];
    function addBlock(block, blockPrefix = '') {
        let groups = Object.keys(block).filter(x => block[x].type == 'group');
        let settings = Object.keys(block).filter(x => block[x].type != 'group');
        for (let setting of settings) {
            let data = block[setting];
            let settingFull = `${blockPrefix}${setting}`;
            let fakeParam = { feature_flag: null, type: data.type, id: settingFull, name: data.name, description: data.description, default: data.value, min: null, max: null, step: null, toggleable: false, view_type: 'normal', values: data.values };
            let result = getHtmlForParam(fakeParam, prefix);
            content += result.html;
            keys.push(settingFull);
            tracker.known[settingFull] = data;
            if (result.runnable) {
                runnables.push(result.runnable);
            }
        }
        for (let setting of groups) {
            let data = block[setting];
            let settingFull = `${blockPrefix}${setting}`;
            content += `<div class="input-group settings-group" id="auto-group-${prefix}${settingFull}"><span id="input_group_${prefix}${settingFull}" class="input-group-header group-label"><span onclick="toggleGroupOpen(this)"><span class="auto-symbol">&#x2B9F;</span><span class="header-label">${data.name}: ${escapeHtml(data.description)}</span></span></span><div class="input-group-content" id="input_group_content_${prefix}${settingFull}">`;
            addBlock(data.value, `${settingFull}.`);
            content += '</div></div>';
        }
    }
    addBlock(data);
    container.innerHTML = content;
    for (let runnable of runnables) {
        runnable();
    }
    let confirmer = getRequiredElementById(`${prefix}confirmer`);
    for (let key of keys) {
        let elem = getRequiredElementById(prefix + key);
        elem.addEventListener('change', () => {
            let value = null;
            if (elem.type == 'checkbox') {
                value = elem.checked;
            }
            else {
                value = elem.value;
            }
            if (value == tracker.known[key].value) {
                delete tracker.altered[key];
            }
            else {
                tracker.altered[key] = value;
            }
            let count = Object.keys(tracker.altered).length;
            getRequiredElementById(`${prefix}edit_count`).innerText = count;
            confirmer.style.display = count == 0 ? 'none' : 'block';
        });
    }
}

/** Returns the current value of the specified user setting (by ID). */
function getUserSetting(id, def = 'require') {
    let elem = document.getElementById(`usersettings_${id}`);
    if (!elem) {
        if (def == 'require') {
            throw new Error(`Unknown user setting: ${id}`);
        }
        return def;
    }
    if (elem.type == 'checkbox') {
        return elem.checked;
    }
    else {
        return elem.value;
    }
}

function applyThemeSetting(theme_info) {
    setTimeout(() => {
        let themeSelectorElement = getRequiredElementById('usersettings_theme');
        function setTheme() {
            let theme_id = themeSelectorElement.value;
            let theme = theme_info[theme_id];
            setCookie('sui_theme_id', theme_id, 365);
            getRequiredElementById('theme_sheet_header').href = theme.path;
            getRequiredElementById('bs_theme_header').href = theme.is_dark ? '/css/bootstrap.min.css' : '/css/bootstrap_light.min.css';
        }
        themeSelectorElement.addEventListener('change', setTheme);
        setTheme();
    }, 1);
}

function loadUserSettings(callback = null) {
    genericRequest('GetUserSettings', {}, data => {
        buildSettingsMenu(userSettingsContainer, data.settings, 'usersettings_', userSettingsData);
        applyThemeSetting(data.themes);
        // Build a second time to self-apply settings
        buildSettingsMenu(userSettingsContainer, data.settings, 'usersettings_', userSettingsData);
        if (callback) {
            callback();
        }
    });
}

function loadServerSettings() {
    genericRequest('ListServerSettings', {}, data => {
        buildSettingsMenu(serverSettingsContainer, data.settings, 'serversettings_', serverSettingsData);
    });
}

function loadSettingsEditor() {
    // TODO: Permission check
    loadServerSettings();
    loadUserSettings(() => {
        let inputBatchSize = document.getElementById('input_batchsize');
        let shouldResetBatch = getUserSetting('resetbatchsizetoone', false);
        if (inputBatchSize && shouldResetBatch) {
            inputBatchSize.value = 1;
            triggerChangeFor(inputBatchSize);
        }
        genInputs(true);
    });
}

document.getElementById('serverconfigtabbutton').addEventListener('click', loadServerSettings);
document.getElementById('usersettingstabbutton').addEventListener('click', () => loadUserSettings());

sessionReadyCallbacks.push(loadSettingsEditor);

function save_user_settings() {
    genericRequest('ChangeUserSettings', { settings: userSettingsData.altered }, data => {
        getRequiredElementById(`usersettings_confirmer`).style.display = 'none';
        loadUserSettings();
    });
}

function save_server_settings() {
    genericRequest('ChangeServerSettings', { settings: serverSettingsData.altered }, data => {
        getRequiredElementById(`serversettings_confirmer`).style.display = 'none';
        loadServerSettings();
    });
}

function doSettingsReset(prefix, tracker) {
    for (let setting of Object.keys(tracker.altered)) {
        let data = tracker.known[setting];
        let element = getRequiredElementById(prefix + setting);
        if (data.type == 'boolean') {
            element.checked = data.value;
        }
        else {
            element.value = data.value;
        }
    }
    tracker.altered = {};
    let confirmer = getRequiredElementById(`${prefix}confirmer`);
    confirmer.style.display = 'none';
}

function cancel_server_settings_edit() {
    doSettingsReset('serversettings_', serverSettingsData);
}

function cancel_user_settings_edit() {
    doSettingsReset('usersettings_', serverSettingsData);
}
