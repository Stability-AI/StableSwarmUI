
let postParamBuildSteps = [];

let refreshParamsExtra = [];

function getHtmlForParam(param, prefix, textRows = 2) {
    try {
        // Actual HTML popovers are too new at time this code was written (experimental status, not supported on most browsers)
        let example = param.examples ? `<br><br>Examples: <code>${param.examples.map(escapeHtml).join("</code>,&emsp;<code>")}</code>` : '';
        let pop = `<div class="sui-popover" id="popover_${prefix}${param.id}"><b>${escapeHtml(param.name)}</b> (${param.type}):<br>&emsp;${escapeHtml(param.description)}${example}</div>`;
        switch (param.type) {
            case 'text':
                return {html: makeTextInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, textRows, param.description, param.toggleable) + pop};
            case 'decimal':
            case 'integer':
                let min = param.min;
                let max = param.max;
                if (min == 0 && max == 0) {
                    min = -9999999;
                    max = 9999999;
                }
                switch (param.number_view_type) {
                    case 'small':
                        return {html: makeNumberInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, param.min, param.max, param.step, true, param.toggleable) + pop};
                    case 'big':
                        return {html: makeNumberInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, param.min, param.max, param.step, false, param.toggleable) + pop};
                    case 'seed':
                        return {html: makeNumberInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, param.min, param.max, param.step, 'seed', param.toggleable) + pop};
                    case 'slider':
                        return {html: makeSliderInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, param.min, param.max, param.view_max || param.max, param.step, false, param.toggleable) + pop,
                            runnable: () => enableSliderForBox(findParentOfClass(getRequiredElementById(`${prefix}${param.id}`), 'auto-slider-box'))};
                    case 'pot_slider':
                        return {html: makeSliderInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, param.min, param.max, param.view_max || param.max, param.step, true, param.toggleable) + pop,
                            runnable: () => enableSliderForBox(findParentOfClass(getRequiredElementById(`${prefix}${param.id}`), 'auto-slider-box'))};
                }
                break;
            case 'boolean':
                return {html: makeCheckboxInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, param.toggleable) + pop};
            case 'dropdown':
                return {html: makeDropdownInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.values, param.default, param.toggleable) + pop};
            case 'list':
                if (param.values) {
                    return {html: makeMultiselectInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.values, param.default, "Select...", param.toggleable) + pop,
                        runnable: () => $(`#${prefix}${param.id}`).select2({ theme: "bootstrap-5", width: 'style', placeholder: $(this).data('placeholder'), closeOnSelect: false }) };
                }
                return {html: makeTextInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, textRows, param.description, param.toggleable) + pop};
            case 'model':
                let modelList = param.values && param.values.length > 0 ? param.values : coreModelMap[param.subtype || 'Stable-Diffusion'];
                return {html: makeDropdownInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, modelList, param.default, param.toggleable) + pop};
            case 'image':
                return {html: makeImageInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.toggleable) + pop};
            case 'image_list':
                return {html: makeImageInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.toggleable) + pop};

        }
        console.log(`Cannot generate input for param ${param.id} of type ${param.type} - unknown type`);
        return null;
    }
    catch (e) {
        console.log(e);
        throw new Error(`Error generating input for param '${param.id}' (${JSON.stringify(param)}): ${e}`);
    }
}

function toggleGroupOpen(elem) {
    let parent = findParentOfClass(elem, 'input-group');
    let group = parent.querySelector('.input-group-content');
    if (group.style.display == 'none') {
        group.style.display = 'block';
        parent.classList.remove('input-group-closed');
        parent.querySelector('.auto-symbol').innerHTML = '&#x2B9F;';
        if (!group.dataset.do_not_save) {
            setCookie(`group_open_${parent.id}`, 'open', 365);
        }
        let toggler = document.getElementById(`${group.id}_toggle`);
        if (toggler) {
            toggler.checked = true;
        }
    }
    else {
        group.style.display = 'none';
        parent.classList.add('input-group-closed');
        parent.querySelector('.auto-symbol').innerHTML = '&#x2B9E;';
        if (!group.dataset.do_not_save) {
            setCookie(`group_open_${parent.id}`, 'closed', 365);
        }
    }
}

function doToggleGroup(id) {
    let elem = getRequiredElementById(`${id}_toggle`);
    let parent = findParentOfClass(elem, 'input-group');
    let header = parent.querySelector('.input-group-header');
    let group = parent.querySelector('.input-group-content');
    if (!elem.checked) {
        if (group.style.display != 'none') {
            toggleGroupOpen(header);
        }
    }
    if (!group.dataset.do_not_save) {
        setCookie(`group_toggle_${parent.id}`, elem.checked ? 'yes' : 'no', 365);
    }
}

function isParamAdvanced(p) {
    return p.group ? p.group.advanced : p.advanced;
}

function genInputs(delay_final = false) {
    let runnables = [];
    let groupsClose = [];
    let groupsEnable = [];
    for (let areaData of [['main_inputs_area', 'new_preset_modal_inputs', (p) => p.visible && !isParamAdvanced(p)],
            ['main_inputs_area_advanced', 'new_preset_modal_advanced_inputs', (p) => p.visible && isParamAdvanced(p)],
            ['main_inputs_area_hidden', null, (p) => !p.visible]]) {
        let area = getRequiredElementById(areaData[0]);
        area.innerHTML = '';
        let presetArea = areaData[1] ? getRequiredElementById(areaData[1]) : null;
        let html = '', presetHtml = '';
        let lastGroup = null;
        for (let param of gen_param_types.filter(areaData[2])) {
            let groupName = param.group ? param.group.name : null;
            if (groupName != lastGroup) {
                if (lastGroup) {
                    html += '</div></div>';
                    if (presetArea) {
                        presetHtml += '</div></div>';
                    }
                }
                if (param.group) {
                    let groupId = param.group.id;
                    let shouldOpen = getCookie(`group_open_auto-group-${groupId}`) || (param.group.open ? 'open' : 'closed');
                    if (shouldOpen == 'closed') {
                        groupsClose.push(groupId);
                    }
                    if (param.group.toggles) {
                        let shouldToggle = getCookie(`group_toggle_auto-group-${groupId}`) || 'no';
                        if (shouldToggle == 'yes') {
                            groupsEnable.push(groupId);
                        }
                    }
                    let toggler = getToggleHtml(param.group.toggles, `input_group_content_${groupId}`, escapeHtml(param.group.name), ' group-toggler-switch', 'doToggleGroup');
                    html += `<div class="input-group" id="auto-group-${groupId}"><span id="input_group_${groupId}" class="input-group-header"><span onclick="toggleGroupOpen(this)"><span class="auto-symbol">&#x2B9F;</span><span class="header-label">${escapeHtml(param.group.name)}</span></span>${toggler}</span><div class="input-group-content" id="input_group_content_${groupId}">`;
                    if (presetArea) {
                        presetHtml += `<div class="input-group"><span id="input_group_preset_${groupId}" onclick="toggleGroupOpen(this)" class="input-group-header"><span class="auto-symbol">&#x2B9F;</span>${escapeHtml(param.group.name)}</span><div class="input-group-content">`;
                    }
                }
                lastGroup = groupName;
            }
            let newData = getHtmlForParam(param, "input_");
            html += newData.html;
            if (newData.runnable) {
                runnables.push(newData.runnable);
            }
            if (param.visible) { // Hidden excluded from presets.
                let presetParam = JSON.parse(JSON.stringify(param));
                presetParam.toggleable = true;
                let presetData = getHtmlForParam(presetParam, "preset_input_");
                presetHtml += presetData.html;
                if (presetData.runnable) {
                    runnables.push(presetData.runnable);
                }
            }
        }
        area.innerHTML = html;
        if (presetArea) {
            presetArea.innerHTML = presetHtml;
        }
    }
    let final = () => {
        for (let runnable of runnables) {
            runnable();
        }
        for (let group of groupsClose) {
            let elem = getRequiredElementById(`input_group_${group}`);
            toggleGroupOpen(elem);
            let pelem = document.getElementById(`input_group_preset_${group}`);
            if (pelem) {
                toggleGroupOpen(pelem);
            }
        }
        for (let group of groupsEnable) {
            let elem = document.getElementById(`input_group_content_${group}_toggle`);
            if (elem) {
                elem.checked = true;
                doToggleGroup(`input_group_content_${group}`);
            }
        }
        for (let param of gen_param_types) {
            if (param.visible) {
                if (param.toggleable) {
                    doToggleEnable(`input_${param.id}`);
                    doToggleEnable(`preset_input_${param.id}`);
                }
            }
        }
        let inputAspectRatio = document.getElementById('input_aspectratio');
        if (inputAspectRatio) {
            let inputWidth = getRequiredElementById('input_width');
            let inputWidthParent = findParentOfClass(inputWidth, 'slider-auto-container');
            let inputWidthSlider = getRequiredElementById('input_width_rangeslider');
            let inputHeight = getRequiredElementById('input_height');
            let inputHeightParent = findParentOfClass(inputHeight, 'slider-auto-container');
            let inputHeightSlider = getRequiredElementById('input_height_rangeslider');
            let resGroupLabel = findParentOfClass(inputWidth, 'input-group').querySelector('.header-label');
            let resTrick = () => {
                let aspect;
                if (inputAspectRatio.value == "Custom") {
                    inputWidthParent.style.display = 'block';
                    inputHeightParent.style.display = 'block';
                    aspect = describeAspectRatio(inputWidth.value, inputHeight.value);
                }
                else {
                    inputWidthParent.style.display = 'none';
                    inputHeightParent.style.display = 'none';
                    aspect = inputAspectRatio.value;
                }
                resGroupLabel.innerText = `Resolution: ${aspect} (${inputWidth.value}x${inputHeight.value})`;
            };
            for (let target of [inputWidth, inputWidthSlider, inputHeight, inputHeightSlider]) {
                target.addEventListener('input', resTrick);
            }
            inputAspectRatio.addEventListener('change', () => {
                if (inputAspectRatio.value != "Custom") {
                    let aspectRatio = inputAspectRatio.value;
                    let width, height;
                    // "1:1", "4:3", "3:2", "8:5", "16:9", "21:9", "3:4", "2:3", "5:8", "9:16", "9:21", "Custom"
                    if (aspectRatio == "1:1") { width = 512; height = 512; }
                    else if (aspectRatio == "4:3") { width = 576; height = 448; }
                    else if (aspectRatio == "3:2") { width = 608; height = 416; }
                    else if (aspectRatio == "8:5") { width = 608; height = 384; }
                    else if (aspectRatio == "16:9") { width = 672; height = 384; }
                    else if (aspectRatio == "21:9") { width = 768; height = 320; }
                    else if (aspectRatio == "3:4") { width = 448; height = 576; }
                    else if (aspectRatio == "2:3") { width = 416; height = 608; }
                    else if (aspectRatio == "5:8") { width = 384; height = 608; }
                    else if (aspectRatio == "9:16") { width = 384; height = 672; }
                    else if (aspectRatio == "9:21") { width = 320; height = 768; }
                    inputWidth.value = width * (curModelWidth == 0 ? 512 : curModelWidth) / 512;
                    inputHeight.value = height * (curModelHeight == 0 ? 512 : curModelHeight) / 512;
                    inputWidth.dispatchEvent(new Event('input'));
                    inputHeight.dispatchEvent(new Event('input'));
                }
                resTrick();
            });
            resTrick();
        }
        let inputRevisionStrength = document.getElementById('input_revisionstrength');
        if (inputRevisionStrength) {
            let parent = findParentOfClass(inputRevisionStrength, 'auto-input');
            parent.style.display = 'none';
            parent.dataset.visible_controlled = true;
        }
        let inputPrompt = document.getElementById('input_prompt');
        if (inputPrompt) {
            let promptParent = findParentOfClass(inputPrompt, 'auto-input');
            promptParent.dataset.visible_controlled = true;
            promptParent.style.display = 'none';
            let altText = getRequiredElementById('alt_prompt_textbox');
            inputPrompt.addEventListener('change', () => {
                altText.value = inputPrompt.value;
                altText.dispatchEvent(new Event('input'));
            });
        }
        let inputLoras = document.getElementById('input_loras');
        if (inputLoras) {
            inputLoras.addEventListener('change', () => {
                updateLoraList();
                sdLoraBrowser.browser.rerender();
            });
        }
        shouldApplyDefault = true;
        for (let param of gen_param_types) {
            if (param.visible) {
                let elem = getRequiredElementById(`input_${param.id}`);
                let cookie = getCookie(`lastparam_input_${param.id}`);
                if (cookie) {
                    shouldApplyDefault = false;
                    if (param.type != "image") {
                        setDirectParamValue(param, cookie);
                    }
                }
                if (!param.do_not_save) {
                    elem.addEventListener('change', () => {
                        if (param.type == "boolean") {
                            setCookie(`lastparam_input_${param.id}`, elem.checked, 0.25);
                        }
                        else if (param.type == "list" && elem.tagName == "SELECT") {
                            let valSet = [...elem.selectedOptions].map(option => option.value);
                            setCookie(`lastparam_input_${param.id}`, valSet.join(','), 0.25);
                        }
                        else if (param.type != "image") {
                            setCookie(`lastparam_input_${param.id}`, elem.value, 0.25);
                        }
                    });
                }
                if (param.toggleable) {
                    let toggler = getRequiredElementById(`input_${param.id}_toggle`);
                    let cookie = getCookie(`lastparam_input_${param.id}_toggle`);
                    if (cookie) {
                        toggler.checked = cookie == "true";
                    }
                    doToggleEnable(`input_${param.id}`);
                    if (!param.do_not_save) {
                        toggler.addEventListener('change', () => {
                            setCookie(`lastparam_input_${param.id}_toggle`, toggler.checked, 0.25);
                        });
                    }
                }
            }
        }
        let modelCookie = getCookie('selected_model');
        if (modelCookie) {
            directSetModel(modelCookie);
        }
        let vaeInput = document.getElementById('input_vae');
        if (vaeInput) {
            vaeInput.addEventListener('change', () => {
                sdVAEBrowser.browser.rerender();
            });
            getRequiredElementById('input_vae_toggle').addEventListener('change', () => {
                sdVAEBrowser.browser.rerender();
            });
            sdVAEBrowser.browser.rerender();
        }
        hideUnsupportableParams();
        for (let runnable of postParamBuildSteps) {
            runnable();
        }
    };
    if (delay_final) {
        setTimeout(() => {
            final();
        }, 1);
    }
    else {
        final();
    }
}

function toggle_advanced() {
    let advancedArea = getRequiredElementById('main_inputs_area_advanced');
    let toggler = getRequiredElementById('advanced_options_checkbox');
    advancedArea.style.display = toggler.checked ? 'block' : 'none';
    for (let param of gen_param_types) {
        if (param.toggleable && param.visible) {
            doToggleEnable(`input_${param.id}`);
        }
    }
}

function toggle_advanced_checkbox_manual() {
    let toggler = getRequiredElementById('advanced_options_checkbox');
    toggler.checked = !toggler.checked;
    toggle_advanced();
}

function getGenInput(input_overrides = {}) {
    let input = {};
    for (let type of gen_param_types) {
        if (type.toggleable && !getRequiredElementById(`input_${type.id}_toggle`).checked) {
            continue;
        }
        if (type.feature_missing) {
            continue;
        }
        if (type.group && type.group.toggles && !getRequiredElementById(`input_group_content_${type.group.id}_toggle`).checked) {
            continue;
        }
        let elem = getRequiredElementById(`input_${type.id}`);
        let parent = findParentOfClass(elem, 'auto-input');
        if (parent && parent.dataset.disabled == 'true') {
            continue;
        }
        if (type.type == "boolean") {
            input[type.id] = elem.checked;
        }
        else if (type.type == "image") {
            if (elem.dataset.filedata) {
                input[type.id] = elem.dataset.filedata;
            }
        }
        else if (type.id == "loraweights") {
            // Special-cased
        }
        else if (type.type == "list" && elem.tagName == "SELECT") {
            let valSet = [...elem.selectedOptions].map(option => option.value);
            if (valSet.length > 0) {
                input[type.id] = valSet.join(',');
                if (type.id == 'loras') {
                    input['loraweights'] = valSet.map(lora => loraWeightPref[lora] || 1).join(',');
                }
            }
        }
        else {
            input[type.id] = elem.value;
        }
    }
    let revisionImageArea = getRequiredElementById('alt_prompt_image_area');
    let imgs = [...revisionImageArea.children].filter(c => c.tagName == "IMG");
    if (imgs.length > 0) {
        input["promptimages"] = imgs.map(img => img.dataset.filedata).join('|');
    }
    input["presets"] = currentPresets.map(p => p.title);
    for (let key in input_overrides) {
        input[key] = input_overrides[key];
    }
    return input;
}

function refreshParameterValues(callback = null) {
    genericRequest('TriggerRefresh', {}, data => {
        for (let param of data.list) {
            let origParam = gen_param_types.find(p => p.id == param.id);
            if (origParam) {
                origParam.values = param.values;
            }
        }
        let promises = [Promise.resolve(true)];
        for (let extra of refreshParamsExtra) {
            let promise = extra();
            promises.push(Promise.resolve(promise));
        }
        Promise.all(promises).then(() => {
            for (let param of gen_param_types) {
                if (param.type == "dropdown") {
                    let dropdown = getRequiredElementById(`input_${param.id}`);
                    let val = dropdown.value;
                    let html = '';
                    for (let value of param.values) {
                        let selected = value == val ? ' selected="true"' : '';
                        html += `<option value="${escapeHtml(value)}"${selected}>${escapeHtml(value)}</option>`;
                    }
                    dropdown.innerHTML = html;
                }
            }
            if (callback) {
                callback();
            }
            hideUnsupportableParams();
        });
    });
}

function setDirectParamValue(param, value, paramElem = null) {
    if (!paramElem) {
        paramElem = getRequiredElementById(`input_${param.id}`);
    }
    if (param.type == "boolean") {
        paramElem.checked = `${value}` == "true";
    }
    else if (param.type == "list" && paramElem.tagName == "SELECT") {
        let vals = typeof value == 'string' ? value.split(',').map(v => v.trim()) : value;
        for (let option of paramElem.options) {
            option.selected = vals.includes(option.value);
        }
    }
    else {
        paramElem.value = value;
    }
    paramElem.dispatchEvent(new Event('input'));
    paramElem.dispatchEvent(new Event('change'));
}

function resetParamsToDefault() {
    for (let param of gen_param_types) {
        let id = `input_${param.id}`;
        if (param.visible) {
            setDirectParamValue(param, param.default);
            deleteCookie(`lastparam_${id}`);
            if (param.toggleable) {
                getRequiredElementById(`${id}_toggle`).checked = false;
                deleteCookie(`lastparam_${id}_toggle`);
                doToggleEnable(id);
            }
            if (param.group && param.group.toggles) {
                let toggler = document.getElementById(`input_group_content_${param.group.id}_toggle`);
                if (toggler && toggler.checked) {
                    toggler.checked = false;
                    doToggleGroup(`input_group_content_${param.group.id}`);
                }
            }
        }
    }
    let defaultPreset = getPresetByTitle('default');
    if (defaultPreset) {
        applyOnePreset(defaultPreset);
    }
    hideUnsupportableParams();
}

function hideUnsupportableParams() {
    if (!gen_param_types) {
        return;
    }
    let groups = {};
    for (let param of gen_param_types) {
        let elem = document.getElementById(`input_${param.id}`);
        if (elem) {
            let box = findParentOfClass(elem, 'auto-input');
            let show = param.feature_flag == null || Object.values(backends_loaded).filter(b => b.features.includes(param.feature_flag)).length > 0;
            param.feature_missing = !show;
            if (box.dataset.visible_controlled) {
            }
            else if (show) {
                box.style.display = '';
                box.dataset.disabled = 'false';
            }
            else {
                box.style.display = 'none';
                box.dataset.disabled = 'true';
            }
            let group = findParentOfClass(elem, 'input-group');
            if (group) {
                let groupData = groups[group.id] || { visible: 0 };
                groups[group.id] = groupData;
                if (show) {
                    groupData.visible++;
                }
            }
        }
    }
    for (let group in groups) {
        let groupData = groups[group];
        let groupElem = getRequiredElementById(group);
        if (groupData.visible == 0) {
            groupElem.style.display = 'none';
        }
        else {
            groupElem.style.display = 'block';
        }
    }
}

function paramSorter(a, b) {
    if (a.group == b.group) {
        return a.priority - b.priority;
    }
    else if (a.group && !b.group) {
        return a.group.priority - b.priority;
    }
    else if (!a.group && b.group) {
        return a.priority - b.group.priority;
    }
    else {
        return a.group.priority - b.group.priority;
    }
}

/**
 * Returns a copy of the parameter name, cleaned for ID format input.
 */
function cleanParamName(name) {
    return name.toLowerCase().replaceAll(/[^a-z]/g, '');
}

/**
 * Sets the value of a parameter to the value used in the currently selected image, if any.
 */
function reuseLastParamVal(paramId) {
    if (!currentMetadataVal) {
        return;
    }
    let pid;
    if (paramId.startsWith("input_")) {
        pid = paramId.substring("input_".length);
    }
    else if (paramId.startsWith("preset_input_")) {
        pid = paramId.substring("preset_input_".length);
    }
    else {
        return;
    }
    let params = JSON.parse(currentMetadataVal).sui_image_params;
    if (pid in params) {
        getRequiredElementById(paramId).value = params[pid];
    }
}
