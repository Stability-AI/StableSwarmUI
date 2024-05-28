
let postParamBuildSteps = [];

let refreshParamsExtra = [];

function getHtmlForParam(param, prefix) {
    try {
        let example = param.examples ? `<br><span class="translate">Examples</span>: <code>${param.examples.map(escapeHtmlNoBr).join(`</code>,&emsp;<code>`)}</code>` : '';
        let pop = param.no_popover ? '' : `<div class="sui-popover" id="popover_${prefix}${param.id}"><b class="translate">${escapeHtmlNoBr(param.name)}</b> (${param.type}):<br><span class="translate slight-left-margin-block">${safeHtmlOnly(param.description)}</span>${example}</div>`;
        switch (param.type) {
            case 'text':
                let runnable = param.view_type == 'prompt' ? () => textPromptAddKeydownHandler(getRequiredElementById(`${prefix}${param.id}`)) : null;
                return {html: makeTextInput(param.feature_flag, `${prefix}${param.id}`, param.id, param.name, param.description, param.default, param.view_type, param.description, param.toggleable, false, !param.no_popover) + pop, runnable: runnable};
            case 'decimal':
            case 'integer':
                let min = param.min, max = param.max, step = param.step || 1;
                if (!min && min != 0) {
                    min = -9999999;
                }
                if (!max && max != 0) {
                    max = 9999999;
                }
                switch (param.view_type) {
                    case 'small':
                        return {html: makeNumberInput(param.feature_flag, `${prefix}${param.id}`, param.id, param.name, param.description, param.default, min, max, step, 'small', param.toggleable, !param.no_popover) + pop,
                        runnable: () => autoNumberWidth(getRequiredElementById(`${prefix}${param.id}`))};
                    case 'normal':
                    case 'big':
                        return {html: makeNumberInput(param.feature_flag, `${prefix}${param.id}`, param.id, param.name, param.description, param.default, min, max, step, 'big', param.toggleable, !param.no_popover) + pop,
                        runnable: () => autoNumberWidth(getRequiredElementById(`${prefix}${param.id}`))};
                    case 'seed':
                        return {html: makeNumberInput(param.feature_flag, `${prefix}${param.id}`, param.id, param.name, param.description, param.default, min, max, step, 'seed', param.toggleable, !param.no_popover) + pop};
                    case 'slider':
                        return {html: makeSliderInput(param.feature_flag, `${prefix}${param.id}`, param.id, param.name, param.description, param.default, min, max, param.view_min || min, param.view_max || max, step, false, param.toggleable, !param.no_popover) + pop,
                            runnable: () => enableSliderForBox(findParentOfClass(getRequiredElementById(`${prefix}${param.id}`), 'auto-slider-box'))};
                    case 'pot_slider':
                        return {html: makeSliderInput(param.feature_flag, `${prefix}${param.id}`, param.id, param.name, param.description, param.default, min, max, param.view_min || min, param.view_max || max, step, true, param.toggleable, !param.no_popover) + pop,
                            runnable: () => enableSliderForBox(findParentOfClass(getRequiredElementById(`${prefix}${param.id}`), 'auto-slider-box'))};
                }
                break;
            case 'boolean':
                return {html: makeCheckboxInput(param.feature_flag, `${prefix}${param.id}`, param.id, param.name, param.description, param.default, param.toggleable, false, !param.no_popover) + pop};
            case 'dropdown':
                return {html: makeDropdownInput(param.feature_flag, `${prefix}${param.id}`, param.id, param.name, param.description, param.values, param.default, param.toggleable, !param.no_popover, param['value_names']) + pop,
                        runnable: () => autoSelectWidth(getRequiredElementById(`${prefix}${param.id}`))};
            case 'list':
                if (param.values) {
                    return {html: makeMultiselectInput(param.feature_flag, `${prefix}${param.id}`, param.id, param.name, param.description, param.values, param.default, "Select...", param.toggleable, !param.no_popover) + pop,
                        runnable: () => $(`#${prefix}${param.id}`).select2({ theme: "bootstrap-5", width: 'style', placeholder: $(this).data('placeholder'), closeOnSelect: false }) };
                }
                return {html: makeTextInput(param.feature_flag, `${prefix}${param.id}`, param.id, param.name, param.description, param.default, param.view_type, param.description, param.toggleable, false, !param.no_popover) + pop};
            case 'model':
                let modelList = param.values && param.values.length > 0 ? param.values : coreModelMap[param.subtype || 'Stable-Diffusion'];
                modelList = modelList.map(m => cleanModelName(m));
                return {html: makeDropdownInput(param.feature_flag, `${prefix}${param.id}`, param.id, param.name, param.description, modelList, param.default, param.toggleable, !param.no_popover) + pop,
                    runnable: () => autoSelectWidth(getRequiredElementById(`${prefix}${param.id}`))};
            case 'image':
                return {html: makeImageInput(param.feature_flag, `${prefix}${param.id}`, param.id, param.name, param.description, param.toggleable, !param.no_popover) + pop};
            case 'image_list':
                return {html: makeImageInput(param.feature_flag, `${prefix}${param.id}`, param.id, param.name, param.description, param.toggleable, !param.no_popover) + pop};
        }
        console.log(`Cannot generate input for param ${param.id} of type ${param.type} - unknown type`);
        return null;
    }
    catch (e) {
        console.log(e);
        throw new Error(`Error generating input for param '${param.id}' (${JSON.stringify(param)}): ${e}`);
    }
}

function toggleGroupOpen(elem, shouldOpen = null) {
    let parent = findParentOfClass(elem, 'input-group');
    let group = parent.querySelector('.input-group-content');
    let isClosed = group.style.display == 'none';
    if (shouldOpen == null) {
        shouldOpen = isClosed;
    }
    doGroupOpenUpdate(group, parent, shouldOpen);
}

function doGroupOpenUpdate(group, parent, isOpen) {
    let header = parent.querySelector('.input-group-header');
    parent.classList.remove('input-group-closed');
    parent.classList.remove('input-group-open');
    let symbol = parent.querySelector('.auto-symbol');
    if (isOpen || header.classList.contains('input-group-noshrink')) {
        group.style.display = 'flex';
        parent.classList.add('input-group-open');
        if (symbol) {
            symbol.innerHTML = '&#x2B9F;';
        }
        if (!group.dataset.do_not_save) {
            setCookie(`group_open_${parent.id}`, 'open', 365);
        }
    }
    else {
        group.style.display = 'none';
        parent.classList.add('input-group-closed');
        if (symbol) {
            symbol.innerHTML = '&#x2B9E;';
        }
        if (!group.dataset.do_not_save) {
            setCookie(`group_open_${parent.id}`, 'closed', 365);
        }
    }
}

function doToggleGroup(id) {
    let elem = getRequiredElementById(`${id}_toggle`);
    let parent = findParentOfClass(elem, 'input-group');
    let header = parent.querySelector('.input-group-header .header-label-wrap');
    let group = parent.querySelector('.input-group-content');
    if (elem.checked) {
        header.classList.add('input-group-header-activated');
        group.classList.add('input-group-content-activated');
    }
    else {
        header.classList.remove('input-group-header-activated');
        group.classList.remove('input-group-content-activated');
    }
    if (!group.dataset.do_not_save) {
        setCookie(`group_toggle_${parent.id}`, elem.checked ? 'yes' : 'no', 365);
    }
    doGroupOpenUpdate(group, parent, group.style.display != 'none');
}

function isParamAdvanced(p) {
    return p.group ? p.group.advanced : p.advanced;
}

document.addEventListener('click', e => {
    if (e.target.onclick) {
        return;
    }
    let header = findParentOfClass(e.target, 'input-group-header');
    if (header) {
        toggleGroupOpen(header);
    }
});

function genInputs(delay_final = false) {
    let runnables = [];
    let groupsClose = [];
    let groupsEnable = [];
    let isPrompt = (p) => p.id == 'prompt' || p.id == 'negativeprompt';
    let defaultPromptVisible = rawGenParamTypesFromServer.find(p => isPrompt(p)).visible;
    for (let areaData of [['main_inputs_area', 'new_preset_modal_inputs', (p) => (p.visible || isPrompt(p)) && !isParamAdvanced(p), true],
            ['main_inputs_area_advanced', 'new_preset_modal_advanced_inputs', (p) => p.visible && isParamAdvanced(p), false],
            ['main_inputs_area_hidden', 'new_preset_modal_hidden_inputs', (p) => (!p.visible || isPrompt(p)), false]]) {
        let area = getRequiredElementById(areaData[0]);
        area.innerHTML = '';
        let presetArea = areaData[1] ? getRequiredElementById(areaData[1]) : null;
        let html = '', presetHtml = '';
        let lastGroup = null;
        let isMain = areaData[3];
        if (isMain && defaultPromptVisible) {
            html += `<button class="generate-button" id="generate_button" onclick="getRequiredElementById('alt_generate_button').click()" oncontextmenu="return getRequiredElementById('alt_generate_button').oncontextmenu()">Generate</button>
            <button class="interrupt-button legacy-interrupt interrupt-button-none" id="interrupt_button" onclick="getRequiredElementById('alt_interrupt_button').click()" oncontextmenu="return getRequiredElementById('alt_interrupt_button').oncontextmenu()">&times;</button>`;
        }
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
                    let infoButton = '';
                    let groupId = param.group.id;
                    if (param.group.description) {
                        html += `<div class="sui-popover" id="popover_group_${groupId}"><b>${translateableHtml(escapeHtml(param.group.name))}</b>:<br>&emsp;${translateableHtml(safeHtmlOnly(param.group.description))}</div>`;
                        infoButton = `<span class="auto-input-qbutton info-popover-button" onclick="doPopover('group_${groupId}', arguments[0])">?</span>`;
                    }
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
                    let symbol = param.group.can_shrink ? '<span class="auto-symbol">&#x2B9F;</span>' : '';
                    let shrinkClass = param.group.can_shrink ? 'input-group-shrinkable' : 'input-group-noshrink';
                    let extraSpanInfo = param.group.id == 'revision' ? ' style="display: none;"' : '';
                    let openClass = shouldOpen == 'closed' ? 'input-group-closed' : 'input-group-open';
                    let extraContentInfo = shouldOpen == 'closed' ? ' style="display: none;"' : '';
                    let toggler = getToggleHtml(param.group.toggles, `input_group_content_${groupId}`, escapeHtml(param.group.name), ' group-toggler-switch', 'doToggleGroup');
                    html += `<div class="input-group ${openClass}" id="auto-group-${groupId}"><span${extraSpanInfo} id="input_group_${groupId}" class="input-group-header ${shrinkClass}"><span class="header-label-wrap">${symbol}<span class="header-label">${translateableHtml(escapeHtml(param.group.name))}</span>${toggler}${infoButton}</span></span><div${extraContentInfo} class="input-group-content" id="input_group_content_${groupId}">`;
                    if (presetArea) {
                        presetHtml += `<div class="input-group ${openClass}"><span id="input_group_preset_${groupId}" class="input-group-header ${shrinkClass}">${symbol}${translateableHtml(escapeHtml(param.group.name))}</span><div class="input-group-content">`;
                    }
                }
                lastGroup = groupName;
            }
            if (isPrompt(param) ? param.visible == isMain : true) {
                let newData = getHtmlForParam(param, "input_");
                html += newData.html;
                if (newData.runnable) {
                    runnables.push(newData.runnable);
                }
            }
            if (isPrompt(param) ? isMain : true) {
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
    hideUnsupportableParams();
    let final = () => {
        for (let runnable of runnables) {
            runnable();
        }
        for (let group of groupsClose) {
            let elem = getRequiredElementById(`input_group_${group}`);
            toggleGroupOpen(elem, false);
            let pelem = document.getElementById(`input_group_preset_${group}`);
            if (pelem) {
                toggleGroupOpen(pelem, false);
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
            if (param.toggleable) {
                doToggleEnable(`input_${param.id}`);
                doToggleEnable(`preset_input_${param.id}`);
            }
        }
        let inputAspectRatio = document.getElementById('input_aspectratio');
        let inputWidth = document.getElementById('input_width');
        let inputHeight = document.getElementById('input_height');
        if (inputAspectRatio && inputWidth && inputHeight) {
            let inputWidthParent = findParentOfClass(inputWidth, 'slider-auto-container');
            let inputWidthSlider = getRequiredElementById('input_width_rangeslider');
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
                resGroupLabel.innerText = `${translate('Resolution')}: ${aspect} (${inputWidth.value}x${inputHeight.value})`;
            };
            for (let target of [inputWidth, inputWidthSlider, inputHeight, inputHeightSlider]) {
                target.addEventListener('input', resTrick);
            }
            inputAspectRatio.addEventListener('change', () => {
                if (inputAspectRatio.value != "Custom") {
                    let aspectRatio = inputAspectRatio.value;
                    let width, height;
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
                    triggerChangeFor(inputWidth);
                    triggerChangeFor(inputHeight);
                }
                resTrick();
            });
            resTrick();
        }
        autoRevealRevision();
        let inputPrompt = document.getElementById('input_prompt');
        if (inputPrompt) {
            let altText = getRequiredElementById('alt_prompt_textbox');
            let update = () => {
                altText.value = inputPrompt.value;
                triggerChangeFor(altText);
            };
            inputPrompt.addEventListener('input', update);
            inputPrompt.addEventListener('change', update);
        }
        let altPromptArea = getRequiredElementById('alt_prompt_region');
        if (defaultPromptVisible) {
            altPromptArea.style.display = 'none';
        }
        else {
            altPromptArea.style.display = 'block';
        }
        let inputNegativePrompt = document.getElementById('input_negativeprompt');
        if (inputNegativePrompt) {
            let altNegText = getRequiredElementById('alt_negativeprompt_textbox');
            let update = () => {
                altNegText.value = inputNegativePrompt.value;
                triggerChangeFor(altNegText);
            };
            inputNegativePrompt.addEventListener('input', update);
            inputNegativePrompt.addEventListener('change', update);
        }
        let inputLoras = document.getElementById('input_loras');
        if (inputLoras) {
            inputLoras.addEventListener('change', () => {
                updateLoraList();
                sdLoraBrowser.browser.rerender();
            });
        }
        let inputLoraWeights = document.getElementById('input_loraweights');
        if (inputLoraWeights) {
            inputLoraWeights.addEventListener('change', reapplyLoraWeights);
        }
        let inputBatchSize = document.getElementById('input_batchsize');
        let shouldResetBatch = getUserSetting('resetbatchsizetoone', false);
        if (inputBatchSize && shouldResetBatch) {
            inputBatchSize.value = 1;
            triggerChangeFor(inputBatchSize);
        }
        shouldApplyDefault = true;
        for (let param of gen_param_types) {
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
        let modelCookie = getCookie('selected_model');
        if (modelCookie) {
            directSetModel(modelCookie);
        }
        let modelInput = getRequiredElementById('input_model');
        modelInput.addEventListener('change', () => {
            forceSetDropdownValue('current_model', modelInput.value);
        });
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
        let controlnetGroup = document.getElementById('input_group_content_controlnet');
        if (controlnetGroup) {
            controlnetGroup.append(createDiv(`controlnet_button_preview`, null, `<button class="basic-button" onclick="controlnetShowPreview()">Preview</button>`));
            if (!currentBackendFeatureSet.includes('controlnetpreprocessors')) {
                controlnetGroup.append(createDiv(`controlnet_install_preprocessors`, null, `<button class="basic-button" onclick="installControlnetPreprocessors()">Install Controlnet Preprocessors</button>`));
            }
        }
        let revisionGroup = document.getElementById('input_group_content_revision');
        if (revisionGroup && !currentBackendFeatureSet.includes('ipadapter')) {
            revisionGroup.append(createDiv(`revision_install_ipadapter`, null, `<button class="basic-button" onclick="revisionInstallIPAdapter()">Install IP Adapter</button>`));
        }
        let videoGroup = document.getElementById('input_group_content_video');
        if (videoGroup && !currentBackendFeatureSet.includes('frameinterps')) {
            videoGroup.append(createDiv(`video_install_frameinterps`, null, `<button class="basic-button" onclick="installVideoRife()">Install Frame Interpolation</button>`));
        }
        hideUnsupportableParams();
        for (let runnable of postParamBuildSteps) {
            runnable();
        }
        let loras = document.getElementById('input_loras');
        if (loras) {
            reapplyLoraWeights();
        }
        if (imageEditor.active) {
            imageEditor.doParamHides();
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
    localStorage.setItem('display_advanced', toggler.checked);
    for (let param of gen_param_types) {
        if (param.toggleable) {
            doToggleEnable(`input_${param.id}`);
        }
    }
    hideUnsupportableParams();
}

function toggle_advanced_checkbox_manual() {
    let toggler = getRequiredElementById('advanced_options_checkbox');
    toggler.checked = !toggler.checked;
    toggle_advanced();
}

let currentAutomaticVae = 'None';

function getGenInput(input_overrides = {}, input_preoverrides = {}) {
    let input = JSON.parse(JSON.stringify(input_preoverrides));
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
        let val = getInputVal(elem);
        if (val != null) {
            input[type.id] = val;
        }
        if (type.id == 'prompt') {
            let container = findParentOfClass(elem, 'auto-input');
            let addedImageArea = container.querySelector('.added-image-area');
            addedImageArea.style.display = '';
            let imgs = [...addedImageArea.children].filter(c => c.tagName == "IMG");
            if (imgs.length > 0) {
                input["promptimages"] = imgs.map(img => img.dataset.filedata).join('|');
            }
        }
    }
    if (!input['vae'] || input['vae'] == 'Automatic') {
        input['vae'] = currentAutomaticVae;
    }
    let revisionImageArea = getRequiredElementById('alt_prompt_image_area');
    let revisionImages = [...revisionImageArea.children].filter(c => c.tagName == "IMG");
    if (revisionImages.length > 0) {
        input["promptimages"] = revisionImages.map(img => img.dataset.filedata).join('|');
    }
    if (imageEditor.active) {
        input["initimage"] = imageEditor.getFinalImageData();
        input["maskimage"] = imageEditor.getFinalMaskData();
        input["width"] = imageEditor.realWidth;
        input["height"] = imageEditor.realHeight;
        if (!input["initimagecreativity"]) {
            let param = document.getElementById('input_initimagecreativity');
            if (param) {
                input["initimagecreativity"] = param.value;
            }
            else {
                input["initimagecreativity"] = 0.6;
            }
        }
    }
    input["presets"] = currentPresets.map(p => p.title);
    for (let key in input_overrides) {
        let val = input_overrides[key];
        if (val == null) {
            delete input[key];
        }
        else {
            input[key] = input_overrides[key];
        }
    }
    return input;
}

function refreshParameterValues(strong = true, callback = null) {
    genericRequest('TriggerRefresh', {strong: strong}, data => {
        loadUserData();
        for (let param of data.list) {
            let origParam = gen_param_types.find(p => p.id == param.id);
            if (origParam) {
                origParam.values = param.values;
            }
        }
        genericRequest('ListT2IParams', {}, data => {
            updateAllModels(data.models);
            allWildcards = data.wildcards;
        });
        let promises = [Promise.resolve(true)];
        for (let extra of refreshParamsExtra) {
            let promise = extra();
            promises.push(Promise.resolve(promise));
        }
        Promise.all(promises).then(() => {
            for (let param of gen_param_types) {
                let elem = document.getElementById(`input_${param.id}`);
                let presetElem = document.getElementById(`preset_input_${param.id}`);
                if (!elem) {
                    console.log(`Could not find element for param ${param.id}`);
                    continue;
                }
                if ((param.type == "dropdown" || param.type == "model") && param.values) {
                    let val = elem.value;
                    let html = '';
                    for (let value of param.values) {
                        let selected = value == val ? ' selected="true"' : '';
                        html += `<option value="${escapeHtmlNoBr(value)}"${selected}>${escapeHtml(value)}</option>`;
                    }
                    elem.innerHTML = html;
                    presetElem.innerHTML = html;
                }
                else if (param.type == "list" && param.values) {
                    let listOpts = [...elem.options].map(o => o.value);
                    let newVals = param.values.filter(v => !listOpts.includes(v));
                    for (let val of newVals) {
                        $(elem).append(new Option(val, val, false, false));
                        $(presetElem).append(new Option(val, val, false, false));
                    }
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
        $(paramElem).val(vals);
        $(paramElem).trigger('change');
    }
    else if (param.type == "image" || param.type == "image_list") {
        // do not edit images directly, this will just misbehave
    }
    else {
        paramElem.value = value;
    }
    triggerChangeFor(paramElem);
}

function resetParamsToDefault(exclude = []) {
    for (let cookie of listCookies('lastparam_')) {
        deleteCookie(cookie);
    }
    localStorage.removeItem('last_comfy_workflow_input');
    for (let box of ['alt_prompt_textbox', 'alt_negativeprompt_textbox']) {
        let elem = getRequiredElementById(box);
        elem.value = '';
        triggerChangeFor(elem);
    }
    for (let param of gen_param_types) {
        let id = `input_${param.id}`;
        if (param.visible && !exclude.includes(param.id) && document.getElementById(id) != null) {
            setDirectParamValue(param, param.default);
            if (param.id == 'prompt' || param.id == 'negativeprompt') {
                triggerChangeFor(getRequiredElementById(id));
            }
            if (param.toggleable) {
                let toggler = getRequiredElementById(`${id}_toggle`);
                toggler.checked = false;
                triggerChangeFor(toggler);
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
    let aspect = document.getElementById('input_aspectratio');
    if (aspect) { // Fix resolution trick incase the reset broke it
        triggerChangeFor(aspect);
    }
    let defaultPreset = getPresetByTitle('default');
    if (defaultPreset) {
        applyOnePreset(defaultPreset);
    }
    hideUnsupportableParams();
}

function hideUnalteredParameters() {
    let filterBox = getRequiredElementById('main_inputs_filter');
    let filter = filterBox.value.toLowerCase();
    if (filter.includes('<unaltered>')) {
        filter = filter.replaceAll('<unaltered>', '');
    }
    else {
        filter += '<unaltered>';
    }
    filterBox.value = filter;
    hideUnsupportableParams();
}

function hideUnsupportableParams() {
    if (!gen_param_types) {
        return;
    }
    let ipadapterInstallButton = document.getElementById('revision_install_ipadapter');
    if (ipadapterInstallButton && currentBackendFeatureSet.includes('ipadapter')) {
        ipadapterInstallButton.remove();
    }
    let controlnetInstallButton = document.getElementById('controlnet_install_preprocessors');
    if (controlnetInstallButton && currentBackendFeatureSet.includes('controlnetpreprocessors')) {
        controlnetInstallButton.remove();
    }
    let videoFrameInterpInstallButton = document.getElementById('video_install_frameinterps');
    if (videoFrameInterpInstallButton && currentBackendFeatureSet.includes('frameinterps')) {
        videoFrameInterpInstallButton.remove();
    }
    let filter = getRequiredElementById('main_inputs_filter').value.toLowerCase();
    let hideUnaltered = filter.includes('<unaltered>');
    if (hideUnaltered) {
        filter = filter.replaceAll('<unaltered>', '');
    }
    let groups = {};
    let advancedCount = 0;
    let toggler = getRequiredElementById('advanced_options_checkbox');
    for (let param of gen_param_types) {
        let elem = document.getElementById(`input_${param.id}`);
        if (elem) {
            let box = findParentOfClass(elem, 'auto-input');
            let supported = param.feature_flag == null || currentBackendFeatureSet.includes(param.feature_flag);
            let filterShow = true;
            if (filter && param.id != 'prompt') {
                let searchText = `${param.id} ${param.name} ${param.description} ${param.group ? param.group.name : ''}`.toLowerCase();
                filterShow = searchText.includes(filter);
            }
            param.feature_missing = !supported;
            let show = supported && param.visible;
            if (hideUnaltered) {
                let paramToggler = document.getElementById(`input_${param.id}_toggle`);
                let isAltered = paramToggler ? paramToggler.checked : `${getInputVal(elem)}` != param.default;
                if (param.group && param.group.toggles && !getRequiredElementById(`input_group_content_${param.group.id}_toggle`).checked) {
                    isAltered = false;
                }
                if (!isAltered) {
                    show = false;
                }
            }
            if (param.advanced && !toggler.checked) {
                show = false;
            }
            if (!filterShow) {
                show = false;
            }
            if (param.advanced && supported && filterShow) {
                advancedCount++;
            }
            if (!box.dataset.visible_controlled) {
                box.style.display = show ? '' : 'none';
            }
            box.dataset.disabled = supported ? 'false' : 'true';
            if (param.group) {
                let groupData = groups[param.group.id] || { visible: 0 };
                groups[param.group.id] = groupData;
                if (show) {
                    groupData.visible++;
                }
            }
        }
    }
    getRequiredElementById('advanced_hidden_count').innerText = `(${advancedCount})`;
    for (let group in groups) {
        let groupData = groups[group];
        let groupElem = getRequiredElementById(`auto-group-${group}`);
        if (groupData.visible == 0) {
            groupElem.style.display = 'none';
        }
        else {
            groupElem.style.display = 'block';
        }
    }
}

/**
 * Returns a sorted list of parameters, with the parameters in the order of top, then groupless, then otherParams, then all remaining grouped.
 * Within each section, parameters are sorted by group priority, then group id, then parameter priority, then parameter id.
 */
function sortParameterList(params, top = [], otherParams = []) {
    function sortFunc(a, b) {
        if (a.group != null && b.group != null) {
            if (a.group.priority != b.group.priority) {
                return a.group.priority - b.group.priority;
            }
            if (a.group.id != b.group.id) {
                return a.group.id.localeCompare(b.group.id);
            }
        }
        if (a.priority != b.priority) {
            return a.priority - b.priority;
        }
        return a.id.localeCompare(b.id);
    }
    let first = params.filter(p => p.always_first).sort(sortFunc);
    let prims = params.filter(p => p.group == null && !p.always_first).sort(sortFunc);
    let others = params.filter(p => p.group != null && !p.always_first).sort(sortFunc);
    return first.concat(top).concat(prims).concat(otherParams).concat(others);
}

/** Returns a copy of the parameter name, cleaned for ID format input. */
function cleanParamName(name) {
    return name.toLowerCase().replaceAll(/[^a-z]/g, '');
}

/** Sets the value of a parameter to the value used in the currently selected image, if any. */
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

/** Internal debug function to show the hidden params. */
function debugShowHiddenParams() {
    for (let id of ['main_inputs_area_hidden', 'simple_inputs_area_hidden']) {
        let hiddenArea = getRequiredElementById(id);
        hiddenArea.style.display = 'block';
        hiddenArea.style.visibility = 'visible';
    }
}

/** Loads and shows a preview of ControlNet preprocessing to the user. */
function controlnetShowPreview() {
    let toggler = getRequiredElementById('input_group_content_controlnet_toggle');
    if (!toggler.checked) {
        toggler.checked = true;
        doToggleGroup('input_group_content_controlnet');
    }
    setCurrentModel(() => {
        if (getRequiredElementById('current_model').value == '') {
            showError("Cannot generate, no model selected.");
            return;
        }
        let previewArea = getRequiredElementById('controlnet_button_preview');
        let clearPreview = () => {
            let lastResult = previewArea.querySelector('.controlnet-preview-result');
            if (lastResult) {
                lastResult.remove();
            }
        };
        clearPreview();
        let imgInput = getRequiredElementById('input_controlnetimageinput');
        if (!imgInput || !imgInput.dataset.filedata) {
            let secondaryImageOption = getRequiredElementById('input_initimage');
            if (!secondaryImageOption || !secondaryImageOption.dataset.filedata) {
                clearPreview();
                previewArea.append(createDiv(null, 'controlnet-preview-result', 'Must select an image.'));
                return;
            }
        }
        let genData = getGenInput();
        genData['images'] = 1;
        genData['prompt'] = '';
        delete genData['batchsize'];
        genData['donotsave'] = true;
        genData['controlnetpreviewonly'] = true;
        makeWSRequestT2I('GenerateText2ImageWS', genData, data => {
            if (!data.image) {
                return;
            }
            let imgElem = document.createElement('img');
            imgElem.src = data.image;
            let resultBox = createDiv(null, 'controlnet-preview-result');
            resultBox.append(imgElem);
            clearPreview();
            previewArea.append(resultBox);
        });
    });
}

/** Central handler for user-edited parameters. */
class ParamConfigurationClass {

    constructor() {
        this.edited_groups = {};
        this.edited_params = {};
        this.extra_count = 0;
        this.param_edits = {};
        this.saved_edits = {};
        this.container = getRequiredElementById('user_param_config_container');
        this.confirmer = getRequiredElementById('user_param_config_confirmer');
    }

    /** First init, mostly just to store the server's original param info. */
    preInit() {
        this.original_param_types = JSON.parse(JSON.stringify(rawGenParamTypesFromServer));
        let arr = filterDistinctBy(this.original_param_types.filter(p => p.group).map(p => p.group), g => g.id);
        this.original_groups = {};
        for (let group of arr) {
            this.original_groups[group.id] = group;
        }
    }

    /** Loads the user-editable parameter configuration tab, filling out the inputs and values. Called only once during init. */
    loadUserParamConfigTab() {
        this.container.innerHTML = ``;
        let lastGroup = '__none__';
        let groupDiv = null;
        for (let param of rawGenParamTypesFromServer) {
            let groupId = param.group ? param.group.id : null;
            if (groupId != lastGroup) {
                lastGroup = groupId;
                groupDiv = createDiv(null, 'param-edit-group-container');
                if (groupId) {
                    let groupPrefix = `user_param_config_group_${param.group.id}`;
                    let groupHtml = `
                        <div class="param-edit-header">Group: ${param.group.name}</div>
                        <div class="param-edit-part"><button id="${groupPrefix}_reset" class="basic-button">Reset</button></div>
                        <div class="param-edit-part">Open by default: <input type="checkbox" id="${groupPrefix}__open"${param.group.open ? ` checked="true"` : ''} autocomplete="off"></div>
                        <div class="param-edit-part">IsAdvanced: <input type="checkbox" id="${groupPrefix}__advanced"${param.group.advanced ? ` checked="true"` : ''} autocomplete="off"></div>
                        <div class="param-edit-part">Ordering Priority: <input type="number" class="param-edit-number" id="${groupPrefix}__priority" value="${param.group.priority}" autocomplete="off"></div>`;
                    groupDiv.appendChild(createDiv(null, 'param-edit-container-for-group', groupHtml));
                    this.container.appendChild(groupDiv);
                    getRequiredElementById(`${groupPrefix}_reset`).addEventListener('click', () => {
                        for (let opt of ['open', 'advanced', 'priority']) {
                            let elem = getRequiredElementById(`${groupPrefix}__${opt}`);
                            delete elem.dataset.orig_val;
                            setInputVal(elem, this.original_groups[groupId][opt]);
                            triggerChangeFor(elem);
                        }
                        delete this.edited_groups[groupId];
                        delete this.param_edits.groups[groupId];
                        this.extra_count++;
                        this.updateConfirmer();
                    });
                    for (let opt of ['open', 'advanced', 'priority']) {
                        let elem = getRequiredElementById(`${groupPrefix}__${opt}`);
                        elem.dataset.orig_val = getInputVal(elem);
                        elem.addEventListener('input', () => {
                            if (!this.edited_groups[param.group.id]) {
                                this.edited_groups[param.group.id] = { changed: {} };
                            }
                            let val = getInputVal(elem);
                            if (`${val}` == elem.dataset.orig_val) {
                                delete this.edited_groups[param.group.id].changed[opt];
                                if (Object.keys(this.edited_groups[param.group.id].changed).length == 0) {
                                    delete this.edited_groups[param.group.id];
                                }
                            }
                            else {
                                this.edited_groups[param.group.id].changed[opt] = val;
                            }
                            this.updateConfirmer();
                        });
                    }
                }
                else {
                    this.container.appendChild(groupDiv);
                }
            }
            let paramPrefix = `user_param_config_param_${param.id}`;
            let paramHtml = `
                <div class="param-edit-header">Param: ${param.name} (${param.type})</div>
                <div class="param-edit-part"><button id="${paramPrefix}_reset" class="basic-button">Reset</button></div>
                <div class="param-edit-part">Visible Normally: <input type="checkbox" id="${paramPrefix}__visible"${param.visible ? ` checked="true"` : ''} autocomplete="off"></div>
                    <div class="param-edit-part">Do Not Save: <input type="checkbox" id="${paramPrefix}__do_not_save"${param.do_not_save ? ` checked="true"` : ''} autocomplete="off"></div>
                    <div class="param-edit-part">IsAdvanced: <input type="checkbox" id="${paramPrefix}__advanced"${param.advanced ? ` checked="true"` : ''} autocomplete="off"></div>
                    <div class="param-edit-part">Ordering Priority: <input type="number" class="param-edit-number" id="${paramPrefix}__priority" value="${param.priority}" autocomplete="off"></div>`;
            if (param.type == "integer" || param.type == "decimal" || (param.type == "list" && param.max)) {
                paramHtml += `
                    <div class="param-edit-part">Min: <input class="param-edit-number" type="number" id="${paramPrefix}__min" value="${param.min}" autocomplete="off"></div>
                    <div class="param-edit-part">Max: <input class="param-edit-number" type="number" id="${paramPrefix}__max" value="${param.max}" autocomplete="off"></div>
                    <div class="param-edit-part"><span title="If using a slider, this is where the slider stops">View Max</span>: <input type="number" id="${paramPrefix}__view_max" value="${param.view_max}" autocomplete="off"></div>
                    <div class="param-edit-part">Step: <input class="param-edit-number" type="number" id="${paramPrefix}__step" value="${param.step}" autocomplete="off"></div>
                    <div class="param-edit-part">View Type: <select id="${paramPrefix}__view_type" autocomplete="off">`;
                for (let type of ['small', 'big', 'seed', 'slider', 'pot_slider']) {
                    paramHtml += `<option value="${type}"${param.view_type == type ? ` selected="true"` : ''}>${type}</option>`;
                }
                paramHtml += `</select></div>`;
            }
            else if (param.type == "text") {
                paramHtml += `<div class="param-edit-part">View Type: <select id="${paramPrefix}__view_type" autocomplete="off">`;
                for (let type of ['normal', 'prompt']) {
                    paramHtml += `<option value="${type}"${param.view_type == type ? ` selected="true"` : ''}>${type}</option>`;
                }
                paramHtml += `</select></div>`;
            }
            if (!param.values && param.type != "boolean") {
                paramHtml += `<div class="param-edit-part">Examples: <input class="param-edit-text" type="text" id="${paramPrefix}__examples" value="${param.examples ? param.examples.join(' || ') : ''}" autocomplete="off"></div>`;
            }
            groupDiv.appendChild(createDiv(null, 'param-edit-container', paramHtml));
            getRequiredElementById(`${paramPrefix}_reset`).addEventListener('click', () => {
                for (let opt of ['visible', 'do_not_save', 'advanced', 'priority', 'min', 'max', 'view_max', 'step', 'view_type', 'examples']) {
                    let elem = document.getElementById(`${paramPrefix}__${opt}`);
                    if (!elem) {
                        continue;
                    }
                    delete elem.dataset.orig_val;
                    let val = this.original_param_types.find(p => p.id == param.id)[opt];
                    if (opt == 'examples') {
                        val = val ? val.join(' || ') : '';
                    }
                    setInputVal(elem, val);
                    triggerChangeFor(elem);
                }
                delete this.edited_params[param.id];
                delete this.param_edits.params[param.id];
                this.extra_count++;
                this.updateConfirmer();
            });
            for (let opt of ['visible', 'do_not_save', 'advanced', 'priority', 'min', 'max', 'view_max', 'step', 'view_type', 'examples']) {
                let elem = document.getElementById(`${paramPrefix}__${opt}`);
                if (!elem) {
                    continue;
                }
                elem.dataset.orig_val = getInputVal(elem);
                elem.addEventListener('input', () => {
                    if (!this.edited_params[param.id]) {
                        this.edited_params[param.id] = { changed: {} };
                    }
                    let val = getInputVal(elem);
                    if (`${val}` == elem.dataset.orig_val) {
                        delete this.edited_params[param.id].changed[opt];
                        if (Object.keys(this.edited_params[param.id].changed).length == 0) {
                            delete this.edited_params[param.id];
                        }
                    }
                    else {
                        this.edited_params[param.id].changed[opt] = val;
                    }
                    this.updateConfirmer();
                });
            }
        }
    }

    /** Applies a map of parameter edits provided by the server. */
    applyParamEdits(edits) {
        let doReplace = rawGenParamTypesFromServer == gen_param_types;
        rawGenParamTypesFromServer = JSON.parse(JSON.stringify(this.original_param_types));
        if (doReplace) {
            gen_param_types = rawGenParamTypesFromServer;
        }
        this.param_edits = edits;
        this.saved_edits = JSON.parse(JSON.stringify(edits));
        if (!edits) {
            return;
        }
        for (let param of rawGenParamTypesFromServer) {
            if (param.group) {
                let groupEdits = edits.groups[param.group.id];
                if (groupEdits) {
                    for (let key in groupEdits) {
                        param.group[key] = groupEdits[key];
                    }
                }
            }
            let paramEdits = edits.params[param.id];
            if (paramEdits) {
                for (let key in paramEdits) {
                    if (key == 'examples') {
                        param[key] = paramEdits[key].split('||').map(s => s.trim()).filter(s => s != '');
                    }
                    else {
                        param[key] = paramEdits[key];
                    }
                }
            }
        }
    }

    /** Updates the save/cancel confirm menu. */
    updateConfirmer() {
        let data = Object.values(this.edited_groups).concat(Object.values(this.edited_params)).map(g => Object.keys(g.changed).length);
        let count = (data.length == 0 ? 0 : data.reduce((a, b) => a + b)) + this.extra_count;
        getRequiredElementById(`user_param_config_edit_count`).innerText = count;
        this.confirmer.style.display = count == 0 ? 'none' : 'block';
    }

    /** Saves any edits to parameter settings to the server, and applies them. */
    saveEdits() {
        if (!this.param_edits) {
            this.param_edits = { groups: {}, params: {} };
        }
        for (let groupId in this.edited_groups) {
            let edit = this.edited_groups[groupId];
            if (!this.param_edits.groups[groupId]) {
                this.param_edits.groups[groupId] = {};
            }
            for (let key in edit.changed) {
                this.param_edits.groups[groupId][key] = edit.changed[key];
                let elem = getRequiredElementById(`user_param_config_group_${groupId}__${key}`);
                elem.dataset.orig_val = edit.changed[key];
            }
        }
        for (let paramId in this.edited_params) {
            let edit = this.edited_params[paramId];
            if (!this.param_edits.params[paramId]) {
                this.param_edits.params[paramId] = {};
            }
            for (let key in edit.changed) {
                this.param_edits.params[paramId][key] = edit.changed[key];
                let elem = getRequiredElementById(`user_param_config_param_${paramId}__${key}`);
                elem.dataset.orig_val = edit.changed[key];
            }
        }
        this.edited_groups = [];
        this.edited_params = [];
        this.extra_count = 0;
        this.updateConfirmer();
        this.applyParamEdits(this.param_edits);
        genInputs();
        genericRequest('SetParamEdits', { edits: this.param_edits }, data => {});
    }

    /** Reverts any edits to parameter settings. */
    cancelEdits() {
        for (let groupId in this.edited_groups) {
            let edit = this.edited_groups[groupId];
            for (let key in edit.changed) {
                let input = getRequiredElementById(`user_param_config_group_${groupId}__${key}`);
                setInputVal(input, input.dataset.orig_val);
            }
        }
        for (let paramId in this.edited_params) {
            let edit = this.edited_params[paramId];
            for (let key in edit.changed) {
                let input = getRequiredElementById(`user_param_config_param_${paramId}__${key}`);
                setInputVal(input, input.dataset.orig_val);
            }
        }
        this.edited_groups = [];
        this.edited_params = [];
        this.param_edits = JSON.parse(JSON.stringify(this.saved_edits));
        this.extra_count = 0;
        this.updateConfirmer();
    }
}

/** Instance of ParamConfigurationClass, central handler for user-edited parameters. */
let paramConfig = new ParamConfigurationClass();

class PromptTabCompleteClass {
    constructor() {
        this.prefixes = {
        };
        this.registerPrefix('random', 'Select from a set of random words to include', (prefix) => {
            return ['\nSpecify a comma-separated list of words to choose from, like "<random:cat,dog,elephant>".', '\nYou can use "||" instead of "," if you need to include commas in your values.', '\nYou can use eg "1-5" to pick a random number in a range.'];
        });
        this.registerPrefix('random[2-4]', 'Selects multiple options from a set of random words to include', (prefix) => {
            return ['\nSpecify a comma-separated list of words to choose from, like "<random[2]:cat,dog,elephant>".', '\nYou can use "||" instead of "," if you need to include commas in your values. You can use eg "1-5" to pick a random number in a range.', '\nPut a comma in the input (eg "random[2,]:") to make the output have commas too.'];
        });
        this.registerPrefix('alternate', 'Cause multiple different words or phrases to be alternated between.', (prefix) => {
            return ['\nSpecify a comma-separated list of words to choose from, like "<alternate:cat,dog>".', '\nYou can use "||" instead of "," if you need to include commas in your values.'];
        });
        this.registerPrefix('fromto[0.5]', 'Have the prompt change after a given timestep.', (prefix) => {
            return ['\nSpecify in the brackets a timestep like 10 (for step 10) or 0.5 (for halfway through).', '\nIn the data area specify the before and the after separate by "," or "|".', '\nFor example, "<fromto[10]:cat,dog>" switches from "cat" to "dog" at step 10.'];
        });
        this.registerPrefix('wildcard', 'Select a random line from a wildcard file (presaved list of options)', (prefix) => {
            let prefixLow = prefix.toLowerCase();
            return allWildcards.filter(w => w.toLowerCase().includes(prefixLow));
        });
        this.registerPrefix('wildcard[2-4]', 'Select multiple random lines from a wildcard file (presaved list of options) (works same as "random" but for wildcards)', (prefix) => {
            let prefixLow = prefix.toLowerCase();
            return allWildcards.filter(w => w.toLowerCase().includes(prefixLow));
        });
        this.registerPrefix('repeat', 'Repeat a value several times', (prefix) => {
            return ['\nUse for example like "<repeat:3,very> big" to get "very very very big",', '\nor "<repeat:1-3,very>" to get randomly between 1 to 3 "very"s,', '\nor <repeat:3,<random:cat,dog>>" to get "cat" or "dog" 3 times in a row eg "cat dog cat".'];
        });
        this.registerPrefix('preset', 'Forcibly apply a preset onto the current generation (useful eg inside wildcards or other automatic inclusions - normally use the Presets UI tab)', (prefix) => {
            let prefixLow = prefix.toLowerCase();
            return allPresets.map(p => p.title).filter(p => p.toLowerCase().includes(prefixLow));
        });
        this.registerPrefix('embed', 'Use a pretrained CLIP TI Embedding', (prefix) => {
            let prefixLow = prefix.toLowerCase();
            return coreModelMap['Embedding'].map(cleanModelName).filter(e => e.toLowerCase().includes(prefixLow));
        });
        this.registerPrefix('lora', 'Forcibly apply a pretrained LoRA model (useful eg inside wildcards or other automatic inclusions - normally use the LoRAs UI tab)', (prefix) => {
            let prefixLow = prefix.toLowerCase();
            return coreModelMap['LoRA'].map(cleanModelName).filter(m => m.toLowerCase().includes(prefixLow));
        });
        this.registerPrefix('region', 'Apply a different prompt to a sub-region within the image', (prefix) => {
            return ['\nx,y,width,height eg "0.25,0.25,0.5,0.5"', '\nor x,y,width,height,strength eg "0,0,1,1,0.5"', '\nwhere strength is how strongly to apply the prompt to the region (vs global prompt). Can do "region:background" for background-only region.'];
        });
        this.registerPrefix('object', 'Select a sub-region inside the image and inpaint over it with a different prompt', (prefix) => {
            return ['\nx,y,width,height eg "0.25,0.25,0.5,0.5"', '\nor x,y,width,height,strength,strength2 eg "0,0,1,1,0.5,0.4"', '\nwhere strength is how strongly to apply the prompt to the region (vs global prompt) on the general pass, and strength2 is how strongly to inpaint (ie InitImageCreativity).'];
        });
        this.registerPrefix('segment', 'Automatically segment an area by CLIP matcher and inpaint it (optionally with a unique prompt)', (prefix) => {
            let prefixLow = prefix.toLowerCase();
            if (prefixLow.startsWith('yolo-')) {
                let yolomodels = rawGenParamTypesFromServer.filter(p => p.id == 'yolomodelinternal')[0].values;
                return yolomodels.map(m => `yolo-${m}`).filter(m => m.toLowerCase().includes(prefixLow));
            }
            return ['\nSpecify before the ">" some text to match against in the image, like "<segment:face>".', '\nCan also do "<segment:text,creativity,threshold>" eg "face,0.6,0.5" where creativity is InitImageCreativity, and threshold is mask matching threshold for CLIP-Seg.', '\nYou may use the "yolo-" prefix to use a YOLOv8 seg model,', '\nor format "yolo-<model>-1" to get specifically the first result from a YOLOv8 match list.'];
        });
        this.registerPrefix('clear', 'Automatically clear part of the image to transparent (by CLIP segmentation matching) (iffy quality, prefer the Remove Background parameter over this)', (prefix) => {
            return ['\nSpecify before the ">" some text to match against in the image, like "<segment:background>"'];
        });
        this.registerPrefix('break', 'Split this prompt across multiple lines of conditioning to the model (helps separate concepts for long prompts).', (prefix) => {
            return [];
        }, true);
        this.lastWord = null;
        this.lastResults = null;
        this.blockInput = false;
    }

    enableFor(box) {
        box.addEventListener('keydown', e => this.onKeyDown(e), true);
        box.addEventListener('input', () => this.onInput(box), true);
    }

    registerPrefix(name, description, completer, selfStanding = false) {
        this.prefixes[name] = { name, description, completer, selfStanding };
    }

    getPromptBeforeCursor(box) {
        return box.value.substring(0, box.selectionStart);
    }

    findLastWordIndex(text) {
        let index = -1;
        for (let cut of [' ', ',', '.', '\n']) {
            let i = text.lastIndexOf(cut);
            if (i > index) {
                index = i;
            }
        }
        return index + 1;
    }

    getPossibleList(box) {
        let prompt = this.getPromptBeforeCursor(box);
        let word = prompt.substring(this.findLastWordIndex(prompt));
        let baseList = [];
        if (word.length > 1 && autoCompletionsList) {
            let completionSet;
            if (this.lastWord && word.startsWith(this.lastWord)) {
                completionSet = this.lastResults;
            }
            else {
                completionSet = autoCompletionsOptimize ? autoCompletionsList[word[0]] : autoCompletionsList['all'];
            }
            let wordLow = word.toLowerCase();
            let rawMatchSet = [];
            if (completionSet) {
                let startWithList = [];
                let containList = [];
                for (let i = 0; i < completionSet.length; i++) {
                    let entry = completionSet[i];
                    if (entry.low.includes(wordLow)) {
                        if (entry.low.startsWith(wordLow)) {
                            startWithList.push(entry);
                        }
                        else {
                            containList.push(entry);
                        }
                        rawMatchSet.push(entry);
                    }
                }
                startWithList.sort((a, b) => a.low.length - b.low.length || a.low.localeCompare(b.low));
                containList.sort((a, b) => a.low.length - b.low.length || a.low.localeCompare(b.low));
                baseList = startWithList.concat(containList).map(w => `<raw>${w.raw}`);
                if (baseList.length > 50) {
                    baseList = baseList.slice(0, 50);
                }
            }
            this.lastWord = word;
            this.lastResults = rawMatchSet;
        }
        let lastBrace = prompt.lastIndexOf('<');
        if (lastBrace == -1) {
            return baseList;
        }
        let lastClose = prompt.lastIndexOf('>');
        if (lastClose > lastBrace) {
            return baseList;
        }
        let content = prompt.substring(lastBrace + 1);
        let colon = content.indexOf(':');
        if (colon == -1) {
            content = content.toLowerCase();
            return Object.keys(this.prefixes).filter(p => p.toLowerCase().startsWith(content)).map(p => [p, this.prefixes[p].description]);
        }
        let prefix = content.substring(0, colon);
        let suffix = content.substring(colon + 1);
        if (!(prefix in this.prefixes)) {
            return [];
        }
        return this.prefixes[prefix].completer(suffix).map(p => p.startsWith('\n') ? p : `<${prefix}:${p}>`);
    }

    onKeyDown(e) {
        // block pageup/down because chrome is silly
        if (e.keyCode == 33 || e.keyCode == 34) {
            e.preventDefault();
        }
        if (this.popover) {
            this.popover.onKeyDown(e);
            if (this.popover && (e.key == 'Tab' || e.key == 'Enter')) {
                this.popover.remove();
                this.popover = null;
                this.blockInput = true;
                setTimeout(() => this.blockInput = false, 10);
            }
        }
    }

    onInput(box) {
        if (this.blockInput) {
            return;
        }
        if (this.popover) {
            this.popover.remove();
            this.popover = null;
        }
        let possible = this.getPossibleList(box);
        if (possible.length == 0) {
            return;
        }
        let buttons = [];
        let prompt = this.getPromptBeforeCursor(box);
        let lastBrace = prompt.lastIndexOf('<');
        let wordIndex = this.findLastWordIndex(prompt);
        for (let val of possible) {
            let name = val;
            let desc = '';
            let apply = name;
            let isClickable = true;
            let index = lastBrace;
            let className = null;
            if (typeof val == 'object') {
                [name, desc] = val;
                if (this.prefixes[name].selfStanding) {
                    apply = `<${name}>`;
                }
                else {
                    apply = `<${name}:`;
                }
            }
            else if (val.startsWith(`<raw>`)) {
                name = val.substring(`<raw>`.length);
                desc = '';
                let split = name.split(',');
                name = split[0];
                if (split.length > 1) {
                    className = `tag-text tag-type-${split[1]}`;
                }
                apply = name;
                index = wordIndex;
            }
            else if (val.startsWith('\n')) {
                isClickable = false;
                name = '';
                desc = val.substring(1);
            }
            let button = { key: desc.length == 0 ? name: `${name} - ${desc}`, className: className };
            if (isClickable) {
                button.action = () => {
                    let areaPre = prompt.substring(0, index);
                    let areaPost = box.value.substring(box.selectionStart);
                    box.value = areaPre + apply + areaPost;
                    box.selectionStart = areaPre.length + apply.length;
                    box.selectionEnd = areaPre.length + apply.length;
                    box.focus();
                    box.dispatchEvent(new Event('input'));
                };
            }
            buttons.push(button);
        }
        let rect = box.getBoundingClientRect();
        this.popover = new AdvancedPopover('prompt_suggest', buttons, false, rect.x, rect.y + box.offsetHeight + 6, box.parentElement, null, box.offsetHeight + 6, 250);
    }
}

let promptTabComplete = new PromptTabCompleteClass();
