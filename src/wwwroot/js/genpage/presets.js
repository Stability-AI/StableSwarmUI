
let allPresets = [];
let currentPresets = [];

let preset_to_edit = null;

function fixPresetParamClickables() {
    for (let param of gen_param_types) {
        doToggleEnable(`preset_input_${param.id}`);
    }
}

function getPresetByTitle(title) {
    title = title.toLowerCase();
    return allPresets.find(p => p.title.toLowerCase() == title);
}

function getPresetTypes() {
    return gen_param_types.filter(type => !type.toggleable || getRequiredElementById(`preset_input_${type.id}_toggle`).checked);
}

function clearPresetView() {
    preset_to_edit = null;
    getRequiredElementById('preset_advanced_options_checkbox').checked = false;
    preset_toggle_advanced();
    getRequiredElementById('new_preset_name').value = '';
    getRequiredElementById('preset_description').value = '';
    getRequiredElementById('new_preset_modal_error').value = '';
    getRequiredElementById('new_preset_image').innerHTML = '';
    let enableImage = getRequiredElementById('new_preset_enable_image');
    enableImage.checked = false;
    enableImage.disabled = true;
    for (let type of getPresetTypes()) {
        let elem = getRequiredElementById('input_' + type.id);
        let presetElem = getRequiredElementById('preset_input_' + type.id);
        if (type.type == "boolean") {
            presetElem.checked = elem.checked;
        }
        else if (type.type == "text") {
            presetElem.value = "{value} " + elem.value;
        }
        else if (type.type == "list" && presetElem.tagName == "SELECT") {
            let selected = [...elem.selectedOptions].map(o => o.value);
            $(presetElem).val(selected);
            $(presetElem).trigger('change');
        }
        else {
            presetElem.value = elem.value;
        }
        triggerChangeFor(presetElem);
        getRequiredElementById(presetElem.id + '_toggle').checked = false;
        doToggleEnable(presetElem.id);
    }
}

function create_new_preset_button() {
    clearPresetView();
    $('#add_preset_modal').modal('show');
    let curImg = document.getElementById('current_image_img');
    if (curImg) {
        let newImg = curImg.cloneNode(true);
        newImg.id = 'new_preset_image_img';
        newImg.style.maxWidth = '100%';
        newImg.style.maxHeight = '';
        getRequiredElementById('new_preset_image').appendChild(newImg);
        let enableImage = getRequiredElementById('new_preset_enable_image');
        enableImage.checked = true;
        enableImage.disabled = false;
    }
    fixPresetParamClickables();
}

function close_create_new_preset() {
    $('#add_preset_modal').modal('hide');
}

function save_new_preset() {
    let errorOut = getRequiredElementById('new_preset_modal_error');
    let name = getRequiredElementById('new_preset_name').value;
    if (name == '') {
        errorOut.innerText = "Must set a Preset Name.";
        return;
    }
    let description = getRequiredElementById('preset_description').value;
    let data = {};
    for (let type of getPresetTypes()) {
        if (!getRequiredElementById(`preset_input_${type.id}_toggle`).checked) {
            continue;
        }
        let elem = getRequiredElementById(`preset_input_${type.id}`);
        if (type.type == "boolean") {
            data[type.id] = elem.checked ? "true" : "false";
        }
        else if (type.type == "list" && elem.tagName == "SELECT") {
            let selected = [...elem.selectedOptions].map(o => o.value);
            data[type.id] = selected.join(',');
        }
        else {
            data[type.id] = elem.value;
        }
    }
    if (Object.keys(data).length == 0) {
        errorOut.innerText = "Must enable at least one parameter.";
        return;
    }
    let toSend = { title: name, description: description, param_map: data };
    if (preset_to_edit) {
        toSend['preview_image'] = preset_to_edit.preview_image;
        toSend['is_edit'] = true;
        toSend['editing'] = preset_to_edit.title;
    }
    if (getRequiredElementById('new_preset_enable_image').checked) {
        toSend['preview_image'] = imageToSmallPreviewData(getRequiredElementById('new_preset_image').getElementsByTagName('img')[0]);
    }
    genericRequest('AddNewPreset', toSend, data => {
        if (Object.keys(data).includes("preset_fail")) {
            errorOut.innerText = data.preset_fail;
            return;
        }
        loadUserData();
        $('#add_preset_modal').modal('hide');
    });
}

function preset_toggle_advanced() {
    let advancedArea = getRequiredElementById('new_preset_modal_advanced_inputs');
    let toggler = getRequiredElementById('preset_advanced_options_checkbox');
    advancedArea.style.display = toggler.checked ? 'block' : 'none';
    fixPresetParamClickables();
}

function preset_toggle_advanced_checkbox_manual() {
    let toggler = getRequiredElementById('preset_advanced_options_checkbox');
    toggler.checked = !toggler.checked;
    preset_toggle_advanced();
}

function preset_toggle_hidden() {
    let hiddenArea = getRequiredElementById('new_preset_modal_hidden_inputs');
    let toggler = getRequiredElementById('preset_hidden_options_checkbox');
    hiddenArea.style.display = toggler.checked ? 'block' : 'none';
    fixPresetParamClickables();
}

function preset_toggle_hidden_checkbox_manual() {
    let toggler = getRequiredElementById('preset_hidden_options_checkbox');
    toggler.checked = !toggler.checked;
    preset_toggle_hidden();
}

function updatePresetList() {
    let view = getRequiredElementById('current_preset_list_view');
    view.innerHTML = '';
    for (let param of gen_param_types) {
        getRequiredElementById(`input_${param.id}`).disabled = false;
        if (param.toggleable) {
            getRequiredElementById(`input_${param.id}_toggle`).disabled = false;
        }
    }
    let overrideCount = 0;
    for (let preset of currentPresets) {
        let div = createDiv(null, 'preset-in-list');
        div.innerText = preset.title;
        let removeButton = createDiv(null, 'preset-remove-button');
        removeButton.innerHTML = '&times;';
        removeButton.title = "Remove this preset";
        removeButton.addEventListener('click', () => {
            currentPresets.splice(currentPresets.indexOf(preset), 1);
            updatePresetList();
            presetBrowser.rerender();
        });
        div.appendChild(removeButton);
        view.appendChild(div);
        for (let key of Object.keys(preset.param_map)) {
            let param = gen_param_types.filter(p => p.id == key)[0];
            if (param) {
                if (param.type != "text" || !preset.param_map[key].includes("{value}")) {
                    let elem = getRequiredElementById(`input_${param.id}`);
                    overrideCount += 1;
                    elem.disabled = true;
                    if (param.toggleable) {
                        getRequiredElementById(`input_${param.id}_toggle`).disabled = true;
                    }
                }
            }
        }
    }
    getRequiredElementById('current_presets_wrapper').style.display = currentPresets.length > 0 ? 'inline-block' : 'none';
    getRequiredElementById('preset_info_slot').innerText = ` (${currentPresets.length}, overriding ${overrideCount} params)`;
}

function applyOnePreset(preset) {
    for (let key of Object.keys(preset.param_map)) {
        let param = gen_param_types.filter(p => p.id == key)[0];
        if (param) {
            let elem = getRequiredElementById(`input_${param.id}`);
            let val = preset.param_map[key];
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
            setDirectParamValue(param, val);
            if (param.group && param.group.toggles) {
                let toggler = document.getElementById(`input_group_content_${param.group.id}_toggle`);
                toggler.checked = true;
                doToggleGroup(`input_group_content_${param.group.id}`);
            }
        }
    }
}

function apply_presets() {
    for (let preset of currentPresets) {
        applyOnePreset(preset);
    }
    currentPresets = [];
    updatePresetList();
    presetBrowser.rerender();
}

function duplicatePreset(preset) {
    genericRequest('DuplicatePreset', { preset: preset.title }, data => {
        loadUserData();
    });
}

function editPreset(preset) {
    clearPresetView();
    preset_to_edit = preset;
    getRequiredElementById('new_preset_name').value = preset.title;
    getRequiredElementById('preset_description').value = preset.description;
    let curImg = document.getElementById('current_image_img');
    if (curImg) {
        let newImg = curImg.cloneNode(true);
        newImg.id = 'new_preset_image_img';
        newImg.style.maxWidth = '100%';
        newImg.style.maxHeight = '';
        newImg.removeAttribute('width');
        newImg.removeAttribute('height');
        getRequiredElementById('new_preset_image').appendChild(newImg);
        let enableImage = getRequiredElementById('new_preset_enable_image');
        enableImage.checked = false;
        enableImage.disabled = false;
    }
    $('#add_preset_modal').modal('show');
    for (let key of Object.keys(preset.param_map)) {
        let type = gen_param_types.filter(p => p.id == key)[0];
        if (type) {
            let presetElem = getRequiredElementById(`preset_input_${type.id}`);
            setDirectParamValue(type, preset.param_map[key], presetElem);
            getRequiredElementById(`preset_input_${type.id}_toggle`).checked = true;
            doToggleEnable(presetElem.id);
        }
    }
    fixPresetParamClickables();
}

function sortPresets() {
    let preList = allPresets.filter(p => p.title.toLowerCase() == "default" || p.title.toLowerCase() == "preview");
    allPresets = preList.concat(allPresets.filter(p => p.title.toLowerCase() != "default" && p.title.toLowerCase() != "preview"));
}

function listPresetFolderAndFiles(path, isRefresh, callback, depth) {
    let proc = () => {
        let prefix = path == '' ? '' : (path.endsWith('/') ? path : `${path}/`);
        let folders = [];
        let files = [];
        for (let preset of allPresets) {
            if (preset.title.startsWith(prefix)) {
                let subPart = preset.title.substring(prefix.length);
                let slashes = subPart.split('/').length - 1;
                if (slashes > 0) {
                    let folderPart = subPart.substring(0, subPart.lastIndexOf('/'));
                    let subfolders = folderPart.split('/');
                    for (let i = 1; i <= subfolders.length && i <= depth; i++) {
                        let folder = subfolders.slice(0, i).join('/');
                        if (!folders.includes(folder)) {
                            folders.push(folder);
                        }
                    }
                }
                if (slashes < depth) {
                    files.push({ name: preset.title, data: preset });
                }
            }
        }
        callback(folders, files);
    };
    if (isRefresh) {
        genericRequest('GetMyUserData', {}, data => {
            allPresets = data.presets;
            sortPresets();
            proc();
        });
    }
    else {
        proc();
    }
}

function describePreset(preset) {
    let buttons = [
        { label: 'Toggle', onclick: () => selectPreset(preset) },
        { label: 'Direct Apply', onclick: () => applyOnePreset(preset.data) },
        { label: 'Edit Preset', onclick: () => editPreset(preset.data) },
        { label: 'Duplicate Preset', onclick: () => duplicatePreset(preset.data) },
        { label: 'Delete Preset', onclick: () => {
            if (confirm("Are you sure want to delete that preset?")) {
                genericRequest('DeletePreset', { preset: preset.data.title }, data => {
                    loadUserData();
                });
            }
        } }
    ];
    let description = `${preset.data.title}:\n${preset.data.description}\n\n${Object.keys(preset.data.param_map).map(key => `${key}: ${preset.data.param_map[key]}`).join('\n')}`;
    let className = currentPresets.some(p => p.title == preset.data.title) ? 'preset-block-selected preset-block' : 'preset-block';
    let name = preset.data.title;
    let index = name.lastIndexOf('/');
    if (index != -1) {
        name = name.substring(index + 1);
    }
    let searchable = description;
    return { name, description: escapeHtml(description), buttons, 'image': preset.data.preview_image, className, searchable };
}

function selectPreset(preset) {
    if (!currentPresets.some(p => p.title == preset.data.title)) {
        currentPresets.push(preset.data);
    }
    else {
        currentPresets.splice(currentPresets.indexOf(preset.data), 1);
    }
    updatePresetList();
    presetBrowser.rerender();
}

let presetBrowser = new GenPageBrowserClass('preset_list', listPresetFolderAndFiles, 'presetbrowser', 'Cards', describePreset, selectPreset,
    `<button id="preset_list_create_new_button translate" class="refresh-button" onclick="create_new_preset_button()">Create New Preset</button>
    <button id="preset_list_import_button translate" class="refresh-button" onclick="importPresetsButton()">Import Presets</button>
    <button id="preset_list_export_button translate" class="refresh-button" onclick="exportPresetsButton()">Export All Presets</button>
    <button id="preset_list_apply_button translate" class="refresh-button" onclick="apply_presets()" title="Apply all current presets directly to your parameter list.">Apply Presets</button>`);

function importPresetsButton() {
    getRequiredElementById('import_presets_textarea').value = '';
    getRequiredElementById('import_presets_activate_button').disabled = true;
    $('#import_presets_modal').modal('show');
}

function importPresetsToData(text) {
    function addValueToPrompt(text) {
        if (text.includes('{value}')) {
            return text;
        }
        else if (text.includes('{prompt}')) {
            return text.replace('{prompt}', '{value}');
        }
        return '{value} ' + text;
    }
    if (text.trim() == '') {
        return null;
    }
    if (text.startsWith('{')) {
        return JSON.parse(text);
    }
    if (text.startsWith('[')) {
        let parsed = JSON.parse(`{ "list": ${text} }`);
        let data = {};
        for (let item of parsed.list) {
            if (item.name) {
                data[item.name] = {
                    title: item.name,
                    description: `Imported prompt preset '${item.name}'`,
                    preview_image: '',
                    param_map: {
                        prompt: addValueToPrompt(item.prompt || ''),
                        negativeprompt: addValueToPrompt(item.negative_prompt || item.negativeprompt || '')
                    }
                };
            }
        }
        return data;
    }
    if (text.startsWith('name,prompt,negative_prompt,')) {
        data = {};
        let lines = text.split('\n');
        for (let line of lines.slice(1)) {
            if (line.trim() == '') {
                continue;
            }
            let parts = parseCsvLine(line);
            if (parts.length < 3 || parts.length > 5) {
                console.log(`Invalid CSV line: ${line}, splits=${parts.length}`);
                return null;
            }
            let name = parts[0];
            let prompt = parts[1];
            let negativeprompt = parts[2];
            if (!prompt && !negativeprompt) {
                continue;
            }
            prompt = addValueToPrompt(prompt || '');
            negativeprompt = addValueToPrompt(negativeprompt || '');
            data[parts[0].toLowerCase()] = {
                title: name,
                description: `Imported prompt preset '${name}'`,
                preview_image: '',
                param_map: {
                    prompt: prompt,
                    negativeprompt: negativeprompt
                }
            };
        }
        return data;
    }
    if (text.includes(': ')) {
        let data = microYamlParse(text);
        console.log(JSON.stringify(data));
        if (!data) {
            return "Data doesn't look valid";
        }
        let result = {};
        for (let key of Object.keys(data)) {
            let val = data[key];
            let prompt = val.prompt;
            let negativeprompt = val.negativeprompt;
            if (!prompt && val.prompt_prefix) {
                prompt = val.prompt_prefix + ' {value} ' + val.prompt_suffix;
            }
            if (!negativeprompt && val.uc_prompt) {
                negativeprompt = val.uc_prompt;
            }
            if (!prompt && !negativeprompt) {
                continue;
            }
            prompt = addValueToPrompt(prompt || '');
            negativeprompt = addValueToPrompt(negativeprompt || '');
            result[key] = {
                title: key,
                description: `Imported prompt preset '${key}'`,
                preview_image: '',
                param_map: {
                    prompt: prompt,
                    negativeprompt: negativeprompt
                }
            };
        }
        return result;
    }
    return "data had no recognizable format";
}

function importPresetUpload() {
    let file = getRequiredElementById('import_preset_uploader').files[0];
    readFileText(file, text => {
        getRequiredElementById('import_presets_textarea').value = text;
        importPresetsCheck();
    });
}

let importPresetUploadContainer = getRequiredElementById('import_preset_upload_container');

importPresetUploadContainer.addEventListener('dragover', e => {
    e.preventDefault();
    e.stopPropagation();
}, false);
importPresetUploadContainer.addEventListener('dragenter', e => {
    e.preventDefault();
    e.stopPropagation();
}, false);
importPresetUploadContainer.addEventListener('dragleave', e => {
    e.preventDefault();
    e.stopPropagation();
}, false);
importPresetUploadContainer.addEventListener('drop', e => {
    e.preventDefault();
    e.stopPropagation();
    readFileText(e.dataTransfer.files[0], text => {
        getRequiredElementById('import_presets_textarea').value = text;
        importPresetsCheck();
    });
}, false);

function importPresetsCheck() {
    let text = getRequiredElementById('import_presets_textarea').value;
    let errorBox = getRequiredElementById('import_preset_modal_error');
    let activateButton = getRequiredElementById('import_presets_activate_button');
    activateButton.disabled = true;
    errorBox.innerText = '';
    errorBox.className = 'modal_error_bottom';
    if (text.trim() == '') {
        return;
    }
    let data;
    try {
        data = importPresetsToData(text);
    }
    catch (e) {
        console.log(e);
        errorBox.innerText = 'Error parsing data: ' + e;
        return;
    }
    if (typeof data == 'string') {
        errorBox.innerText = data;
        return;
    }
    if (!data) {
        errorBox.innerText = 'Data input looks invalid.';
        return;
    }
    let willBreak = [];
    for (let key of Object.keys(data)) {
        if (allPresets.some(p => p.title == key)) {
            willBreak.push(key);
        }
    }
    if (willBreak.length > 0) {
        let canOverwrite = getRequiredElementById('import_presets_overwrite').checked;
        if (!canOverwrite) {
            errorBox.innerText = `Would overwrite ${willBreak.length} preset(s): ${willBreak.join(', ')}.`;
            activateButton.disabled = true;
            return;
        }
        errorBox.className = 'modal_success_bottom';
        errorBox.innerText = `Will import ${Object.keys(data).length}, overwriting ${willBreak} presets.`;
    }
    errorBox.className = 'modal_success_bottom';
    errorBox.innerText = `Will import ${Object.keys(data).length} presets.`;
    activateButton.disabled = false;
}

function importPresetsActivate() {
    let data = importPresetsToData(getRequiredElementById('import_presets_textarea').value);
    let expectedCount = Object.keys(data).length;
    let overwrite = getRequiredElementById('import_presets_overwrite').checked;
    let ranCount = 0;
    let failedCount = 0;
    let errorBox = getRequiredElementById('import_preset_modal_error');
    getRequiredElementById('import_presets_activate_button').disabled = true;
    errorBox.innerText = '';
    errorBox.className = 'modal_success_bottom';
    console.log(JSON.stringify(data));
    for (let key of Object.keys(data)) {
        let preset = data[key];
        let toSend = { title: key, description: preset.description, preview_image: preset.preview_image, param_map: preset.param_map, is_edit: overwrite, editing: key };
        genericRequest('AddNewPreset', toSend, data => {
            ranCount++;
            if (Object.keys(data).includes("preset_fail")) {
                failedCount++;
            }
            if (ranCount == expectedCount) {
                loadUserData();
            }
            if (failedCount > 0) {
                errorBox.className = 'modal_error_bottom';
                errorBox.innerText = `Imported ${ranCount} presets, ${failedCount} failed.`;
            }
            else {
                errorBox.innerText = `Imported ${ranCount} presets.`;
            }
        });
    }
}

function exportPresetsButton() {
    let text = '';
    if (getRequiredElementById('export_preset_format_json').checked) {
        let data = {};
        for (let preset of allPresets) {
            data[preset.title] = preset;
        }
        text = JSON.stringify(data, null, 4);
    }
    else { // CSV
        text = 'name,prompt,negative_prompt,\n';
        for (let preset of allPresets) {
            if (preset.param_map.prompt || preset.param_map.negativeprompt) {
                text += `"${preset.title.replace('"', '""')}","${(preset.param_map.prompt || '').replaceAll('"', '""')}","${(preset.param_map.negativeprompt || '').replaceAll('"', '""')}",\n`;
            }
        }
    }
    getRequiredElementById('export_presets_textarea').value = text;
    $('#export_presets_modal').modal('show');
}

function exportPresetsDownload() {
    let fname;
    if (getRequiredElementById('export_preset_format_json').checked) {
        fname = 'presets.json';
    }
    else {
        fname = 'presets.csv';
    }
    downloadPlainText(fname, getRequiredElementById('export_presets_textarea').value);
}

function closeExportPresetViewer() {
    $('#export_presets_modal').modal('hide');
}

function closeImportPresetViewer() {
    $('#import_presets_modal').modal('hide');
}
