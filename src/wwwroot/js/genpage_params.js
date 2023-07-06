
function getHtmlForParam(param, prefix) {
    // Actual HTML popovers are too new at time this code was written (experimental status, not supported on most browsers)
    let example = param.examples ? `<br><br>Examples: <code>${param.examples.map(escapeHtml).join("</code>,&emsp;<code>")}</code>` : '';
    let pop = `<div class="sui-popover" id="popover_${prefix}${param.id}"><b>${escapeHtml(param.name)}</b> (${param.type}):<br>&emsp;${escapeHtml(param.description)}${example}</div>`;
    switch (param.type) {
        case 'text':
            return makeTextInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, 2, param.description, param.toggleable) + pop;
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
                    return makeNumberInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, param.min, param.max, param.step, true, param.toggleable) + pop;
                case 'big':
                    return makeNumberInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, param.min, param.max, param.step, false, param.toggleable) + pop;
                case 'slider':
                    let val = makeSliderInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, param.min, param.max, param.step, false, param.toggleable) + pop;
                    return val;
                case 'pot_slider':
                    return makeSliderInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, param.min, param.max, param.step, true, param.toggleable) + pop;
            }
        case 'boolean':
            return makeCheckboxInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.default, param.toggleable) + pop;
        case 'dropdown':
            return makeDropdownInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.values, param.default, param.toggleable) + pop;
        case 'image':
            return makeImageInput(param.feature_flag, `${prefix}${param.id}`, param.name, param.description, param.toggleable) + pop;
    }
    console.log(`Cannot generate input for param ${param.id} of type ${param.type} - unknown type`);
    return null;
}

function toggleGroupOpen(elem) {
    // ⮟⮞
    let group = elem.parentElement.getElementsByClassName('input-group-content')[0];
    if (group.style.display == 'none') {
        group.style.display = 'block';
        elem.innerText = '⮟' + elem.innerText.replaceAll('⮞', '');
    }
    else {
        group.style.display = 'none';
        elem.innerText = '⮞' + elem.innerText.replaceAll('⮟', '');
    }
}

function genInputs() {
    for (let areaData of [['main_inputs_area', 'new_preset_modal_inputs', (p) => p.visible && !p.advanced],
            ['main_inputs_area_advanced', 'new_preset_modal_advanced_inputs', (p) => p.visible && p.advanced],
            ['main_inputs_area_hidden', null, (p) => !p.visible]]) {
        let area = document.getElementById(areaData[0]);
        let presetArea = areaData[1] ? document.getElementById(areaData[1]) : null;
        let html = '', presetHtml = '';
        let lastGroup = null;
        let groupsClose = [];
        let groupId = 0;
        for (let param of gen_param_types.filter(areaData[2])) {
            if (param.group != lastGroup) {
                if (lastGroup) {
                    html += '</div></div>';
                    if (presetArea) {
                        presetHtml += '</div></div>';
                    }
                }
                if (param.group) {
                    groupId++;
                    if (!param.group_open) {
                        groupsClose.push(groupId);
                    }
                    html += `<div class="input-group"><span id="input_group_${groupId}" onclick="toggleGroupOpen(this)" class="input-group-header">⮟${escapeHtml(param.group)}</span><div class="input-group-content">`;
                    if (presetArea) {
                        presetHtml += `<div class="input-group"><span id="input_group_preset_${groupId}" onclick="toggleGroupOpen(this)" class="input-group-header">⮟${escapeHtml(param.group)}</span><div class="input-group-content">`;
                    }
                }
                lastGroup = param.group;
            }
            html += getHtmlForParam(param, "input_");
            if (param.visible) { // Hidden excluded from presets.
                let presetParam = JSON.parse(JSON.stringify(param));
                presetParam.toggleable = true;
                presetHtml += getHtmlForParam(presetParam, "preset_input_");
            }
        }
        area.innerHTML = html;
        enableSlidersIn(area);
        if (presetArea) {
            presetArea.innerHTML = presetHtml;
            enableSlidersIn(presetArea);
        }
        for (let group of groupsClose) {
            let elem = document.getElementById(`input_group_${group}`);
            toggleGroupOpen(elem);
            let pelem = document.getElementById(`input_group_preset_${group}`);
            if (pelem) {
                toggleGroupOpen(pelem);
            }
        }
    }
    for (let param of gen_param_types) {
        if (param.toggleable) {
            doToggleEnable(`input_${param.id}`);
        }
    }
    let inputAspectRatio = document.getElementById('input_aspectratio');
    let inputWidth = document.getElementById('input_width');
    let inputWidthParent = findParentOfClass(inputWidth, 'auto-input');
    let inputWidthSlider = document.getElementById('input_width_rangeslider');
    let inputHeight = document.getElementById('input_height');
    let inputHeightParent = findParentOfClass(inputHeight, 'auto-input');
    let inputHeightSlider = document.getElementById('input_height_rangeslider');
    let resGroupLabel = findParentOfClass(inputWidth, 'input-group').getElementsByClassName('input-group-header')[0];
    let resTrick = () => {
        if (inputAspectRatio.value == "Custom") {
            inputWidthParent.style.display = 'block';
            inputHeightParent.style.display = 'block';
        }
        else {
            inputWidthParent.style.display = 'none';
            inputHeightParent.style.display = 'none';
        }
        resGroupLabel.innerText = resGroupLabel.innerText[0] + `Resolution: ${describeAspectRatio(inputWidth.value, inputHeight.value)} (${inputWidth.value}x${inputHeight.value})`;
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
        }
        resTrick();
    });
    resTrick();
    shouldApplyDefault = true;
    for (let param of gen_param_types) {
        if (!param.hidden) {
            let elem = document.getElementById(`input_${param.id}`);
            let cookie = getCookie(`lastparam_input_${param.id}`);
            if (cookie) {
                shouldApplyDefault = false;
                if (param.type == "boolean") {
                    elem.checked = cookie == "true";
                }
                else {
                    elem.value = cookie;
                }
                elem.dispatchEvent(new Event('input'));
                elem.dispatchEvent(new Event('change'));
            }
            elem.addEventListener('change', () => {
                if (param.type == "boolean") {
                    setCookie(`lastparam_input_${param.id}`, elem.checked, 0.25);
                }
                else {
                    setCookie(`lastparam_input_${param.id}`, elem.value, 0.25);
                }
            });
            if (param.toggleable) {
                let toggler = document.getElementById(`input_${param.id}_toggle`);
                let cookie = getCookie(`lastparam_input_${param.id}_toggle`);
                if (cookie) {
                    toggler.checked = cookie == "true";
                }
                doToggleEnable(`input_${param.id}`);
                toggler.addEventListener('change', () => {
                    setCookie(`lastparam_input_${param.id}_toggle`, toggler.checked, 0.25);
                });
            }
        }
    }
    let modelCookie = getCookie('selected_model');
    if (modelCookie) {
        directSetModel(modelCookie);
    }
}

function toggle_advanced() {
    let advancedArea = document.getElementById('main_inputs_area_advanced');
    let toggler = document.getElementById('advanced_options_checkbox');
    advancedArea.style.display = toggler.checked ? 'block' : 'none';
}

function toggle_advanced_checkbox_manual() {
    let toggler = document.getElementById('advanced_options_checkbox');
    toggler.checked = !toggler.checked;
    toggle_advanced();
}

function getGenInput() {
    let input = {};
    for (let type of gen_param_types) {
        if (type.toggleable && !document.getElementById(`input_${type.id}_toggle`).checked) {
            continue;
        }
        let elem = document.getElementById('input_' + type.id);
        if (type.type == "boolean") {
            input[type.id] = elem.checked;
        }
        else if (type.type == "image") {
            if (elem.dataset.filedata) {
                input[type.id] = elem.dataset.filedata;
            }
        }
        else {
            input[type.id] = elem.value;
        }
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
                if (origParam.type == "dropdown") {
                    let dropdown = document.getElementById(`input_${param.id}`);
                    let val = dropdown.value;
                    let html = '';
                    for (let value of param.values) {
                        let selected = value == val ? ' selected="true"' : '';
                        html += `<option value="${escapeHtml(value)}"${selected}>${escapeHtml(value)}</option>`;
                    }
                    dropdown.innerHTML = html;
                }
            }
        }
        if (callback) {
            callback();
        }
    });
}

function resetParamsToDefault() {
    for (let param of gen_param_types) {
        let id = `input_${param.id}`;
        let paramElem = document.getElementById(id);
        if (!param.hidden) {
            if (param.type == "boolean") {
                paramElem.checked = param.default;
            }
            else {
                paramElem.value = param.default;
            }
            deleteCookie(`lastparam_input_${param.id}`);
            if (param.toggleable) {
                document.getElementById(`${id}_toggle`).checked = false;
                deleteCookie(`lastparam_input_${param.id}_toggle`);
                doToggleEnable(id);
            }
        }
    }
    let defaultPreset = getPresetByTitle('default');
    console.log(`Found default = ${defaultPreset}`)
    if (defaultPreset) {
        applyOnePreset(defaultPreset);
    }
}
