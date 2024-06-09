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
            let visible = setting != 'language';
            let fakeParam = { feature_flag: null, type: data.type, id: settingFull, name: data.name, description: data.description, default: data.value, min: null, max: null, step: null, toggleable: false, view_type: 'normal', values: data.values, visible: visible, value_names: data.value_names };
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
            content += `<div class="input-group input-group-open settings-group" id="auto-group-${prefix}${settingFull}"><span id="input_group_${prefix}${settingFull}" class="input-group-header input-group-shrinkable group-label"><span class="header-label-wrap"><span class="auto-symbol">&#x2B9F;</span><span class="header-label">${translateableHtml(data.name)}: ${translateableHtml(escapeHtmlNoBr(data.description))}</span></span></span><div class="input-group-content" id="input_group_content_${prefix}${settingFull}">`;
            for (let i = 0; i < data.description.split('\n').length - 1; i++) {
                content += '<br>';
            }
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
    id = id.toLowerCase();
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

function aggressivelySetTheme(them_id) {
    let themeSelectorElement = getRequiredElementById('usersettings_theme');
    themeSelectorElement.value = them_id;
    triggerChangeFor(themeSelectorElement);
    save_user_settings();
}

function applyThemeSetting(theme_info) {
    setTimeout(() => {
        let themeSelectorElement = getRequiredElementById('usersettings_theme');
        function setTheme() {
            let theme_id = themeSelectorElement.value;
            let theme = theme_info[theme_id];
            setCookie('sui_theme_id', theme_id, 365);
            let themeCss = document.head.querySelectorAll('.theme_sheet_header');
            let oldPaths = Array.from(themeCss).map(x => x.href.split('?')[0]);
            if (theme.css_paths.every(x => oldPaths.some(o => o.endsWith(x)))) {
                return;
            }
            let siteHeader = getRequiredElementById('sitecssheader');
            getRequiredElementById('bs_theme_header').href = theme.is_dark ? '/css/bootstrap.min.css' : '/css/bootstrap_light.min.css';
            themeCss.forEach(x => x.remove());
            let newTheme = theme.css_paths.map(path => `<link class="theme_sheet_header" rel="stylesheet" href="${path}?${siteHeader.href.split('?')[1]}" />`).join('\n');
            document.head.insertAdjacentHTML('beforeend', newTheme);
        }
        themeSelectorElement.addEventListener('change', setTheme);
        setTheme();
    }, 1);
}

function loadUserSettings(callback = null) {
    genericRequest('GetUserSettings', {}, data => {
        data.settings.vaes.value.defaultsdxlvae.values = ['None'].concat(coreModelMap['VAE']);
        data.settings.vaes.value.defaultsdv1vae.values = ['None'].concat(coreModelMap['VAE']);
        buildSettingsMenu(userSettingsContainer, data.settings, 'usersettings_', userSettingsData);
        applyThemeSetting(data.themes);
        // Build a second time to self-apply settings
        buildSettingsMenu(userSettingsContainer, data.settings, 'usersettings_', userSettingsData);
        findParentOfClass(getRequiredElementById('usersettings_language'), 'auto-input').style.display = 'none';
        if (callback) {
            callback();
        }
    });
}

function loadServerSettings() {
    genericRequest('ListServerSettings', {}, data => {
        buildSettingsMenu(serverSettingsContainer, data.settings, 'serversettings_', serverSettingsData);
        toggleGroupOpen(getRequiredElementById('input_group_serversettings_defaultuser'), false);
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
        loadUserData();
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
