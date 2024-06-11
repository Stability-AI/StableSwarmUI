
/**
 * If true, the workflow iframe is present. If false, the tab has never been opened, or loading failed.
 */
let hasComfyLoaded = false;

let comfyButtonsArea = getRequiredElementById('comfy_workflow_buttons');

let comfyObjectData = {};

let comfyIsOutputNodeMap = {};

let comfyHasTriedToLoad = false;

let comfyAltSaveNodes = ['ADE_AnimateDiffCombine', 'VHS_VideoCombine', 'SaveAnimatedWEBP', 'SaveAnimatedPNG', 'SwarmSaveAnimatedWebpWS', 'SwarmSaveAnimationWS'];

/**
 * Tries to load the ComfyUI workflow frame.
 */
function comfyTryToLoad() {
    if (hasComfyLoaded) {
        return;
    }
    hasComfyLoaded = true;
    comfyButtonsArea.style.display = 'block';
    let container = getRequiredElementById('comfy_workflow_frameholder');
    container.innerHTML = `<iframe class="comfy_workflow_frame" id="comfy_workflow_frame" src="/ComfyBackendDirect/" onload="comfyOnLoadCallback()"></iframe>`;
}

/**
 * Returns the ComfyUI workflow frame (or errors if not present).
 */
function comfyFrame() {
    return getRequiredElementById('comfy_workflow_frame');
}

let comfyEnableInterval = null;

let comfyFailedToLoad = translatable(`Failed to load ComfyUI Workflow backend. The server may still be loading.`);
let comfyTryAgain = translatable(`Try Again?`);

/**
 * Callback triggered when the ComfyUI workflow frame loads.
 */
function comfyOnLoadCallback() {
    if (comfyFrame().contentWindow.document.body.getElementsByClassName('comfy-failed-to-load').length == 1) {
        hasComfyLoaded = false;
        comfyButtonsArea.style.display = 'none';
        comfyFrame().remove();
        getRequiredElementById('comfy_workflow_frameholder').innerHTML = `<h2>${comfyFailedToLoad.get()} <button onclick="comfyTryToLoad()">${comfyTryAgain.get()}</button></h2>`;
    }
    else {
        getJsonDirect('/ComfyBackendDirect/object_info', (_, data) => {
            comfyObjectData = data;
            for (let key of Object.keys(data)) {
                if (data[key].output_node) {
                    comfyIsOutputNodeMap[key] = true;
                }
            }
        });
        comfyReconfigureQuickload();
        let comfyRefreshControlInterval = setInterval(() => {
            let app = comfyFrame().contentWindow.app;
            if (!app) {
                return;
            }
            if (!app.swarmHasReplacedRefresh) {
                let origRefreshFunc = app.refreshComboInNodes.bind(app);
                app.refreshComboInNodes = async function () {
                    await new Promise(r => {
                        genericRequest('ComfyEnsureRefreshable', {}, () => r(), 0, () => r());
                    });
                    return await origRefreshFunc();
                };
                app.swarmHasReplacedRefresh = true;
            }
            clearInterval(comfyRefreshControlInterval);
        }, 500);
        if (getCookie('comfy_domulti') == 'true') {
            comfyEnableInterval = setInterval(() => {
                let api = comfyFrame().contentWindow.swarmApiDirect;
                if (!api) {
                    return;
                }
                clearInterval(comfyEnableInterval);
                comfyEnableInterval = null;
                let origQueuePrompt = api.queuePrompt.bind(api);
                async function swarmQueuePrompt(number, { output, workflow }) {
                    let nodeColorMap = {};
                    for (let node of workflow.nodes) {
                        let color = node.color || 'none';
                        if (comfyIsOutputNodeMap[node.type]) {
                            let set = nodeColorMap[color] || [];
                            set.push(node.id);
                            nodeColorMap[color] = set;
                        }
                    }
                    if (Object.keys(nodeColorMap).length == 1) {
                        return await origQueuePrompt(number, { output, workflow });
                    }
                    let promises = [];
                    for (let color of Object.keys(nodeColorMap)) {
                        let ids = nodeColorMap[color];
                        let newPrompt = {};
                        function addAncestors(nodeId) {
                            if (newPrompt[nodeId]) {
                                return;
                            }
                            newPrompt[nodeId] = output[nodeId];
                            for (let input of Object.values(output[nodeId].inputs || {})) {
                                if (typeof input == 'object' && input.length == 2) {
                                    addAncestors(input[0]);
                                }
                            }
                        }
                        for (let nodeId of ids) {
                            addAncestors(nodeId);
                        }
                        newPrompt['swarm_prefer'] = promises.length;
                        let newPromise = origQueuePrompt(number, { output: newPrompt, workflow: workflow });
                        promises.push(newPromise);
                    }
                    let results = await Promise.all(promises);
                    let newOutput = results[0];
                    for (let result of results.slice(1)) {
                        if (result.node_errors && result.node_errors.length > 0) {
                            newOutput.node_errors = newOutput.node_errors.concat(result.node_errors);
                        }
                    }
                    return newOutput;
                }
                api.queuePrompt = swarmQueuePrompt;
            }, 500);
        }
    }
}

/**
 * Callback when params refresh, to re-assign object_info.
 */
function comfyReloadObjectInfo() {
    let resolve = undefined;
    let promise = new Promise(r => { resolve = r });
    getJsonDirect('/ComfyBackendDirect/object_info', (_, data) => {
        comfyObjectData = data;
        for (let param of gen_param_types) {
            if (param.revalueGetter) {
                param.values = param.revalueGetter();
            }
        }
        resolve();
    });
    return promise;
}

/**
 * Gets the current Comfy API prompt and UI Workflow file (async) then calls a callback with the (workflow, prompt).
 */
function comfyGetPromptAndWorkflow(callback) {
    comfyFrame().contentWindow.app.graphToPrompt().then(r => callback(r.workflow, r.output));
}

/**
 * Gets the current Comfy workflow prompt (async) then calls a callback with the prompt object.
 */
function comfyGetPrompt(callback) {
    comfyFrame().contentWindow.app.graphToPrompt().then(r => callback(r.output));
}

/**
 * Workaround hack, Chrome does not pass mouseup events to iframes when the mouse is outside the iframe.
 * Comfy has multiple different ways of listening to mouseups so aggressively trigger all of them. And manually force the LiteGraph handler to be safe.
 */
function comfyAggressiveMouseUp() {
    if (!hasComfyLoaded) {
        return;
    }
    function sendUp(elem) {
        if (elem) {
            elem.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true }));
            elem.dispatchEvent(new PointerEvent('pointerup', { isPrimary: true, bubbles: true, cancelable: true }));
            if (elem.onmouseup) {
                elem.onmouseup();
            }
            if (elem.onpointerup) {
                elem.onpointerup();
            }
        }
    }
    sendUp(comfyFrame().contentWindow);
    sendUp(comfyFrame().contentWindow.document);
    sendUp(comfyFrame().contentWindow.document.body);
    if (comfyFrame().contentWindow.app.canvas) {
        comfyFrame().contentWindow.app.canvas.processMouseUp(new PointerEvent('pointerup', { isPrimary: true, bubbles: true, cancelable: true }));
    }
}

document.addEventListener('mouseup', function (e) {
    if (hasComfyLoaded && e.button == 0) {
        comfyAggressiveMouseUp();
    }
});

/**
 * Builds a set of pseudo-parameters for the current Comfy workflow (async) then calls a callback with the parameter set object, the API workflow, and a list of retained default parameters, as callback(params, workflow, retained).
 */
function comfyBuildParams(callback) {
    comfyGetPromptAndWorkflow((workflow, prompt) => {
        let params = {};
        let inputPrefix = 'comfyrawworkflowinput';
        let idsUsed = [];
        function addSimpleParam(name, defVal, type, groupName, values, view_type, min, max, step, inputIdDirect, groupId, priority, visible = true, toggles = true, revalueGetter = null) {
            let inputId = inputIdDirect;
            let counter = 0;
            while (inputId in params) {
                inputId = `${inputIdDirect}${numberToLetters(counter++)}`;
            }
            let groupObj = groupId == 'primitives' ? null : {
                name: groupName,
                id: groupId,
                open: false,
                priority: priority,
                advanced: groupId != 'primitives',
                can_shrink: true,
                toggles: false
            };
            params[inputId] = {
                name: name,
                default: defVal,
                id: inputId,
                type: type,
                description: `The ${name} input for ${groupName} (${type})`,
                values: values,
                view_type: view_type,
                min: min,
                max: max,
                step: step,
                visible: visible,
                toggleable: toggles,
                priority: priority,
                advanced: false,
                feature_flag: null,
                do_not_save: false,
                revalueGetter: revalueGetter,
                no_popover: true,
                group: groupObj
            };
        }
        let labelAlterations = {};
        let nodeStatics = {};
        let nodeIdToClean = {};
        let nodeStaticUnique = [];
        let nodeLabelPaths = {};
        let nodeIsRandomize = {};
        let claimedByPrimitives = [];
        let doAutoClaim = true;
        for (let node of workflow.nodes) {
            if (node.title) {
                labelAlterations[`${node.id}`] = node.title;
            }
            // This is weird edge case hacking. There's a lot of weird values this key can hold for some reason.
            let isRandom = node.widgets_values && "includes" in node.widgets_values && node.widgets_values.includes('randomize');
            if (isRandom) {
                nodeIsRandomize[`${node.id}`] = true;
            }
            if (node.type.startsWith("SwarmInput")) {
                doAutoClaim = false;
            }
            if (node.type == 'PrimitiveNode' && node.title) {
                let colon = node.title.indexOf(':');
                if (colon > 0) {
                    let before = node.title.substring(0, colon).trim().toLowerCase();
                    if (before == "swarmui") {
                        claimedByPrimitives.push(cleanParamName(node.title.substring(colon + 1)));
                    }
                }
                let cleanTitle = cleanParamName(node.title);
                let cleaned = inputPrefix + cleanTitle;
                let id = cleaned;
                let x = 0;
                while (nodeStaticUnique.includes(node.title)) {
                    id = `${cleaned}${numberToLetters(x++)}`;
                }
                nodeStaticUnique.push(id);
                for (let links of workflow.links) {
                    if (links[1] == node.id) {
                        nodeStatics[`${links[3]}.${links[4]}`] = id;
                        nodeIdToClean[id] = node.title;
                    }
                }
            }
        }
        for (let node of workflow.nodes) {
            if (node.inputs) {
                let x = 0;
                for (let input of node.inputs) {
                    nodeLabelPaths[`${node.id}.${input.name}`] = `${node.id}.${x++}`;
                    let link = `${node.id}`;
                    if (link in labelAlterations) {
                        labelAlterations[`${node.id}.${input.name}`] = labelAlterations[link];
                    }
                }
            }
        }
        let hasSaves = false;
        let previewNodes = [];
        for (let nodeId of Object.keys(prompt)) {
            let node = prompt[nodeId];
            if (node.class_type == 'PreviewImage') {
                previewNodes.push(nodeId);
                continue;
            }
            if (node.class_type == 'SaveImage') {
                if ('SwarmSaveImageWS' in comfyObjectData) {
                    node.class_type = 'SwarmSaveImageWS';
                    delete node.inputs['filename_prefix'];
                }
                hasSaves = true;
            }
            else if (node.class_type == 'SwarmSaveImageWS') {
                hasSaves = true;
            }
            else if (comfyAltSaveNodes.includes(node.class_type)) {
                hasSaves = true;
            }
            if (node.inputs) {
                for (let inputId of Object.keys(node.inputs)) {
                    let val = node.inputs[inputId];
                    if (val == null) {
                        console.log(`Null input ${inputId} on node ${nodeId} (${JSON.stringify(node)})`);
                    }
                    else if (typeof val == 'object' && val.length == 2) {
                        if (inputId == 'negative' && val[1] == 0) {
                            labelAlterations[val[0]] = 'Negative Prompt';
                        }
                        else if (inputId == 'positive' && val[1] == 0) {
                            labelAlterations[val[0]] = 'Positive Prompt';
                        }
                    }
                }
            }
        }
        if (!hasSaves && previewNodes.length > 0) {
            prompt[previewNodes[0]].class_type = 'SwarmSaveImageWS';
            hasSaves = true;
            previewNodes = previewNodes.slice(1);
        }
        for (let preview of previewNodes) {
            delete prompt[preview];
        }
        // Special case: propagate label alterations to conditioning nodes, for ReVision workflows
        let hasFixes = true;
        while (hasFixes) {
            hasFixes = false;
            for (let nodeId of Object.keys(prompt)) {
                let node = prompt[nodeId];
                if (node.class_type == 'unCLIPConditioning' && labelAlterations[nodeId]) {
                    let inputCond = node.inputs['conditioning'];
                    if (typeof inputCond == 'object' && inputCond.length == 2 && !labelAlterations[inputCond[0]]) {
                        labelAlterations[inputCond[0]] = labelAlterations[nodeId];
                        hasFixes = true;
                    }
                }
            }
        }
        if (!hasSaves) {
            showError('ComfyUI Workflow must have at least one SaveImage node!');
            document.getElementById('maintab_comfyworkflow').click();
            return;
        }
        let initialRetainSet = ['images', 'model', 'comfyuicustomworkflow'];
        let defaultParamsRetain = [...initialRetainSet];
        let defaultParamValue = {};
        let groups = [];
        let findConnection = (target, pos) => {
            for (let nodeId of Object.keys(prompt)) {
                let node = prompt[nodeId];
                for (let inputId of Object.keys(node.inputs)) {
                    let val = node.inputs[inputId];
                    if (typeof val == 'object' && val.length == 2 && val[0] == target && val[1] == pos) {
                        return [nodeId, inputId];
                    }
                }
            }
            return [null, null];
        };
        for (let nodeId of Object.keys(prompt)) {
            let node = prompt[nodeId];
            let groupLabel = `${labelAlterations[nodeId] || node.class_type} (Node ${nodeId})`;
            let groupId = cleanParamName(labelAlterations[nodeId] || node.class_type);
            if (groups.includes(groupId)) {
                groupId = `${groupId}${numberToLetters(parseInt(nodeId))}`;
            }
            groups.push(groupId);
            let priority = 0;
            if (groupLabel.includes('Prompt')) {
                priority = -10;
            }
            else if (groupLabel.includes('EmptyLatent')) {
                priority = -7;
            }
            else if (groupLabel.includes('KSampler')) {
                priority = -5;
            }
            if (node.class_type.startsWith('SwarmInput') && node.class_type != 'SwarmInputGroup') {
                let type = '';
                let subtype = null;
                let defaultVal = node.inputs['value'];
                let values = null;
                switch (node.class_type) {
                    case 'SwarmInputInteger': type = 'integer'; break;
                    case 'SwarmInputFloat': type = 'decimal'; break;
                    case 'SwarmInputText': type = 'text'; break;
                    case 'SwarmInputModelName':
                        type = 'model';
                        subtype = node.inputs['subtype'];
                        defaultVal = defaultVal.replaceAll('\\', '/').replaceAll('.safetensors', '');
                        break;
                    case 'SwarmInputCheckpoint':
                        type = 'model';
                        subtype = 'Stable-Diffusion';
                        defaultVal = defaultVal.replaceAll('\\', '/').replaceAll('.safetensors', '');
                        break;
                    case 'SwarmInputDropdown':
                        type = 'dropdown';
                        values = node.inputs['values'].split(',').map(s => s.trim());
                        if (values.length <= 1) {
                            let [remoteNodeId, remoteInput] = findConnection(nodeId, 1);
                            if (remoteNodeId && remoteInput) {
                                let remoteNode = prompt[remoteNodeId];
                                let data = comfyObjectData[remoteNode.class_type];
                                if (data) {
                                    if (remoteInput in data.input.required) {
                                        values = data.input.required[remoteInput][0];
                                    }
                                    else if (remoteInput in data.input.optional) {
                                        values = data.input.optional[remoteInput][0];
                                    }
                                }
                            }
                        }
                    break;
                    case 'SwarmInputBoolean': type = 'boolean'; break;
                    case 'SwarmInputImage': type = 'image'; break;
                    default: throw new Error(`Unknown SwarmInput type ${node.class_type}`);
                }
                let inputIdDirect = node.inputs['raw_id'] || cleanParamName(node.inputs['title']);
                let inputId = inputIdDirect;
                let counter = 0;
                while (inputId in params) {
                    inputId = `${inputIdDirect}${numberToLetters(counter++)}`;
                }
                let groupObj = { name: 'Ungrouped', id: 'ungrouped', open: true, priority: 0, advanced: false, toggles: false, can_shrink: false };
                if (node.inputs.group) {
                    let groupData = prompt[node.inputs.group[0]];
                    groupObj = {
                        name: groupData.inputs.title,
                        id: cleanParamName(groupData.inputs.title),
                        open: groupData.inputs.open_by_default,
                        priority: groupData.inputs.order_priority,
                        advanced: groupData.inputs.is_advanced,
                        can_shrink: groupData.inputs.can_shrink,
                        toggles: false
                    };
                }
                params[inputId] = {
                    name: node.inputs['title'],
                    id: inputId,
                    type: type,
                    subtype: subtype,
                    description: node.inputs['description'],
                    default: defaultVal,
                    values: values,
                    view_type: node.inputs['view_type'],
                    min: node.inputs['min'] || 0,
                    max: node.inputs['max'] || 0,
                    view_max: node.inputs['view_max'] || 0,
                    step: node.inputs['step'] || 0,
                    visible: true,
                    toggleable: false,
                    priority: node.inputs['order_priority'],
                    advanced: node.inputs['is_advanced'],
                    feature_flag: null,
                    do_not_save: false,
                    revalueGetter: null,
                    no_popover: node.inputs['description'].length == 0,
                    group: groupObj
                };
                if (node.class_type == 'SwarmInputImage') {
                    params[inputId].image_should_resize = node.inputs['auto_resize'];
                    params[inputId].image_always_b64 = true;
                }
                node.inputs['value'] = "${" + inputId + ":" + `${node.inputs['value']}`.replaceAll('${', '(').replaceAll('}', ')') + "}";
            }
            function injectType(id, type) {
                if (id.startsWith(inputPrefix)) {
                    id = inputPrefix + type + id.substring(inputPrefix.length);
                }
                return id;
            }
            function addParam(inputId, inputIdDirect, inputLabel, val, groupId, groupLabel, forceUniqueId) {
                let paramDataRaw;
                if (node.class_type in comfyObjectData) {
                    let possible = comfyObjectData[node.class_type].input;
                    if ('required' in possible && inputId in possible.required) {
                        paramDataRaw = possible.required[inputId];
                    }
                }
                let revalueGetter = null;
                let type, values = null, min = -9999999999, max = 9999999999, view_type = 'normal', step = 1;
                if (typeof val == 'number') {
                    let asSeed = false;
                    if (['seed', 'noise_seed'].includes(inputId)) {
                        type = 'integer';
                        view_type = 'seed';
                        asSeed = true;
                        if (nodeId in nodeIsRandomize) {
                            val = -1;
                        }
                    }
                    else if (['width', 'height'].includes(inputId)) {
                        type = 'integer';
                        view_type = 'pot_slider';
                        min = 128;
                        max = 8192;
                        step = 64;
                    }
                    else if (inputId == 'denoise') {
                        type = 'decimal';
                        min = 0;
                        max = 1;
                        step = 0.05;
                        view_type = 'slider';
                    }
                    else if (inputId == 'cfg') {
                        type = 'decimal';
                        min = 1;
                        max = 50;
                        step = 0.5;
                    }
                    else if (['steps', 'start_at_step', 'end_at_step'].includes(inputId)) {
                        type = 'integer';
                        min = 1;
                        max = 50;
                        step = 1;
                    }
                    else {
                        if (paramDataRaw && paramDataRaw[0] == 'INT' && paramDataRaw.length == 2) {
                            type = 'integer';
                            view_type = 'big';
                            min = paramDataRaw[1].min;
                            max = paramDataRaw[1].max;
                            step = 1;
                            if (inputId == 'batch_size' && getUserSetting('resetbatchsizetoone') && !claimedByPrimitives.includes('batchsize')) {
                                val = 1;
                            }
                        }
                        else if (paramDataRaw && paramDataRaw[0] == 'FLOAT' && paramDataRaw.length == 2) {
                            type = 'decimal';
                            view_type = 'slider';
                            min = paramDataRaw[1].min;
                            max = paramDataRaw[1].max;
                            step = (max - min) * 0.01;
                        }
                        else {
                            type = 'decimal';
                        }
                    }
                    inputIdDirect = injectType(inputIdDirect, asSeed ? 'seed' : type);
                    if (forceUniqueId) {
                        let count = 0;
                        while (idsUsed.includes(inputIdDirect)) {
                            count++;
                            inputIdDirect = `${inputIdDirect}${numberToLetters(count)}`;
                        }
                    }
                    node.inputs[inputId] = "%%_COMFYFIXME_${" + inputIdDirect + (asSeed ? "+seed" : "") + ":" + val + "}_ENDFIXME_%%";
                }
                else if (typeof val == 'string') {
                    if (doAutoClaim && node.class_type == 'SaveImage' && inputId == 'filename_prefix') {
                        node.inputs[inputId] = "${prefix:}";
                        return inputIdDirect;
                    }
                    else if (doAutoClaim && node.class_type == 'CheckpointLoaderSimple' && inputId == 'ckpt_name') {
                        if (!('model' in defaultParamValue) && !claimedByPrimitives.includes('model')) {
                            let model = node.inputs[inputId];
                            node.inputs[inputId] = "${model:" + model.replaceAll('${', '(').replaceAll('}', ')') + "}";
                            if (model.endsWith('.safetensors')) {
                                model = model.substring(0, model.length - '.safetensors'.length);
                            }
                            defaultParamValue['model'] = model.replaceAll('\\', '/');
                            return inputIdDirect;
                        }
                        type = 'model';
                        values = allModels;
                    }
                    else if (node.class_type == 'KSamplerAdvanced' && inputId in ['add_noise', 'return_with_leftover_noise']) {
                        // TODO: Should be a checkbox.
                        type = 'dropdown';
                        values = ['enable', 'disable'];
                    }
                    else if (node.class_type == 'SwarmLoadImageB64') {
                        type = 'image';
                    }
                    else {
                        if (paramDataRaw && paramDataRaw.length == 1 && paramDataRaw[0].length > 1) {
                            type = 'dropdown';
                            function fixArr(arr) {
                                arr = JSON.parse(JSON.stringify(arr));
                                for (let i = 0; i < arr.length; i++) {
                                    if (Array.isArray(arr[i])) {
                                        arr[i] = arr[i][0];
                                    }
                                }
                                return arr;
                            }
                            values = fixArr(paramDataRaw[0]);
                            revalueGetter = () => {
                                return fixArr(comfyObjectData[node.class_type].input.required[inputId][0]);
                            };
                        }
                        else {
                            view_type = 'prompt';
                            type = 'text';
                        }
                    }
                    inputIdDirect = injectType(inputIdDirect, type);
                    if (forceUniqueId) {
                        let count = 0;
                        while (idsUsed.includes(inputIdDirect)) {
                            count++;
                            inputIdDirect = `${inputIdDirect}${numberToLetters(count)}`;
                        }
                    }
                    node.inputs[inputId] = "${" + inputIdDirect + ":" + val.replaceAll('${', '(').replaceAll('}', ')') + "}";
                }
                else {
                    return inputIdDirect;
                }
                if (!idsUsed.includes(inputIdDirect)) {
                    idsUsed.push(inputIdDirect);
                    addSimpleParam(inputLabel, val, type, groupLabel, values, view_type, min, max, step, inputIdDirect, groupId, groupId == 'primitives' ? -200 : priority, true, true, revalueGetter);
                }
                return inputIdDirect;
            }
            function claimOnce(classType, paramName, fieldName, numeric) {
                if (claimedByPrimitives.includes(cleanParamName(paramName))) {
                    return false;
                }
                if (node.class_type != classType) {
                    return false;
                }
                let val = node.inputs[fieldName];
                if (typeof val != (numeric ? 'number' : 'string')) {
                    return false;
                }
                if (paramName == 'seed' && nodeId in nodeIsRandomize) {
                    val = -1;
                }
                let redirId = nodeStatics[nodeLabelPaths[`${nodeId}.${fieldName}`]];
                let useParamName = paramName;
                let paramNameClean = cleanParamName(paramName);
                let actualId = useParamName;
                let result = false;
                if (redirId) {
                    useParamName = redirId;
                    actualId = redirId;
                    let title = nodeIdToClean[redirId] || redirId.substring(inputPrefix.length);
                    let colon = title.indexOf(':');
                    if (colon > 0 && cleanParamName(title.substring(0, colon)) == 'swarmui') {
                        let reuseParam = cleanParamName(title.substring(colon + 1));
                        if (rawGenParamTypesFromServer.filter(x => x.id == reuseParam).length > 0) {
                            if (!defaultParamsRetain.includes(reuseParam)) {
                                defaultParamsRetain.push(reuseParam);
                                defaultParamValue[reuseParam] = val;
                            }
                            node.inputs[fieldName] = numeric ? "%%_COMFYFIXME_${" + reuseParam + ":" + val + "}_ENDFIXME_%%" : "${" + reuseParam + ":" + val.replaceAll('${', '(').replaceAll('}', ')') + "}";
                            return true;
                        }
                    }
                    actualId = addParam(fieldName, actualId, title, val, 'primitives', 'Primitives', false);
                }
                else if (defaultParamsRetain.includes(paramNameClean)) {
                    return false;
                }
                else {
                    defaultParamsRetain.push(paramNameClean);
                    defaultParamValue[paramNameClean] = val;
                    result = true;
                }
                node.inputs[fieldName] = numeric ? "%%_COMFYFIXME_${" + actualId + ":" + val + "}_ENDFIXME_%%" : "${" + actualId + ":" + val.replaceAll('${', '(').replaceAll('}', ')') + "}";
                return result;
            }
            if (doAutoClaim) {
                if (claimOnce('EmptyLatentImage', 'width', 'width', true) && claimOnce('EmptyLatentImage', 'height', 'height', true) && claimOnce('EmptyLatentImage', 'batchsize', 'batch_size', true)) {
                    defaultParamsRetain.push('aspectratio');
                    defaultParamValue['aspectratio'] = 'Custom';
                    continue;
                }
                claimOnce('KSampler', 'seed', 'seed', true);
                claimOnce('KSampler', 'steps', 'steps', true);
                claimOnce('KSampler', 'sampler', 'sampler_name', false);
                claimOnce('KSampler', 'scheduler', 'scheduler', false);
                claimOnce('KSampler', 'cfg_scale', 'cfg', true);
                claimOnce('KSamplerAdvanced', 'seed', 'noise_seed', true);
                claimOnce('KSamplerAdvanced', 'steps', 'steps', true);
                claimOnce('KSamplerAdvanced', 'sampler', 'sampler_name', false);
                claimOnce('KSamplerAdvanced', 'scheduler', 'scheduler', false);
                claimOnce('KSamplerAdvanced', 'cfg_scale', 'cfg', true);
                claimOnce('SwarmLoadImageB64', 'init_image', 'image_base64', false);
                claimOnce('LoadImage', 'initimage', 'image', false);
                claimOnce('SwarmLoraLoader', 'loras', 'lora_names', false);
                claimOnce('SwarmLoraLoader', 'loraweights', 'lora_weights', false);
                if (node.class_type == 'CLIPTextEncode' && groupLabel.startsWith("Positive Prompt") && !defaultParamsRetain.includes('prompt') && typeof node.inputs.text == 'string') {
                    defaultParamsRetain.push('prompt');
                    defaultParamValue['prompt'] = node.inputs.text;
                    node.inputs.text = "${prompt}";
                    continue;
                }
                else if (node.class_type == 'CLIPTextEncode' && groupLabel.startsWith("Negative Prompt") && !defaultParamsRetain.includes('negativeprompt') && typeof node.inputs.text == 'string') {
                    defaultParamsRetain.push('negativeprompt');
                    defaultParamValue['negativeprompt'] = node.inputs.text;
                    node.inputs.text = "${negativeprompt}";
                    continue;
                }
            }
            for (let inputId of Object.keys(node.inputs)) {
                if (inputId == 'choose file to upload' || inputId == 'image_upload') {
                    continue;
                }
                let val = node.inputs[inputId];
                if (`${val}`.startsWith('${') || `${val}`.startsWith('%%_COMFYFIXME_${')) {
                    continue;
                }
                if (['KSampler', 'KSamplerAdvanced'].includes(node.class_type) && inputId == 'control_after_generate') {
                    continue;
                }
                if (node.class_type.startsWith('SwarmInput')) {
                    continue;
                }
                let redirId = nodeStatics[nodeLabelPaths[`${nodeId}.${inputId}`]];
                if (redirId) {
                    let title = nodeIdToClean[redirId] || redirId.substring(inputPrefix.length);
                    let colon = title.indexOf(':');
                    if (colon > 0 && cleanParamName(title.substring(0, colon)) == 'swarmui') {
                        let reuseParam = cleanParamName(title.substring(colon + 1));
                        if (rawGenParamTypesFromServer.filter(x => x.id == reuseParam).length > 0) {
                            if (!defaultParamsRetain.includes(reuseParam)) {
                                defaultParamsRetain.push(reuseParam);
                                defaultParamValue[reuseParam] = val;
                            }
                            node.inputs[inputId] = typeof val != 'string' ? "%%_COMFYFIXME_${" + reuseParam + ":" + val + "}_ENDFIXME_%%" : "${" + reuseParam + ":" + val.replaceAll('${', '(').replaceAll('}', ')') + "}";
                            continue;
                        }
                    }
                    addParam(inputId, redirId, title, val, 'primitives', 'Primitives', false);
                }
                else {
                    let inputLabel = labelAlterations[`${nodeId}.${inputId}`] || inputId;
                    let inputIdDirect = cleanParamName(`${inputPrefix}${groupLabel}${inputId}${numberToLetters(parseInt(nodeId))}`);
                    addParam(inputId, inputIdDirect, inputLabel, val, groupId, groupLabel, true);
                }
            }
        }
        addSimpleParam('comfyworkflowparammetadata', JSON.stringify(params), 'text', 'Comfy Workflow', null, 'big', 0, 1, 1, 'comfyworkflowparammetadata', 'comfyworkflow', -999999, false, false, null);
        addSimpleParam('comfyworkflowraw', JSON.stringify(prompt), 'text', 'Comfy Workflow', null, 'big', 0, 1, 1, 'comfyworkflowraw', 'comfyworkflow', -999999, false, false, null);
        let coreRetain = [];
        if (defaultParamValue['model']) {
            coreRetain.push('model');
        }
        if (defaultParamsRetain.includes('width') && defaultParamsRetain.includes('height') && !defaultParamsRetain.includes('aspectratio')) {
            defaultParamsRetain.push('aspectratio');
            defaultParamValue['aspectratio'] = 'Custom';
        }
        for (let param of defaultParamsRetain) {
            if (!initialRetainSet.includes(param) && param in defaultParamValue) {
                coreRetain.push(param);
            }
        }
        let sorted = sortAndFixComfyParameters(params, coreRetain, false, defaultParamValue, false);
        let newParams = {};
        for (let param of sorted) {
            newParams[param.id] = param;
        }
        callback(newParams, prompt, defaultParamsRetain, defaultParamValue, workflow);
    });
}

/**
 * Updates the parameter list to match the currently ComfyUI workflow.
 */
function replaceParamsToComfy() {
    comfyBuildParams((params, prompt, retained, paramVal, workflow) => {
        setComfyWorkflowInput(params, retained, paramVal, true);
    });
}

let comfyInfoSpanNotice = '<b>(Custom Comfy Workflow <button class="basic-button interrupt-button" onclick="comfyParamsDisable()">Disable</button>)</b>';

function sortAndFixComfyParameters(params, retained, applyValues = false, paramVal = null, includeAlwaysRetain = true) {
    let actualParams = [];
    for (let pid of ['comfyworkflowraw', 'comfyworkflowparammetadata']) {
        params[pid].extra_hidden = true;
        params[pid].always_first = true;
    }
    actualParams.push(params['comfyworkflowraw']); // must be first
    delete params['comfyworkflowraw'];
    let handled = {};
    for (let param of rawGenParamTypesFromServer.filter(p => retained.includes(p.id) || (p.always_retain && includeAlwaysRetain))) {
        if (paramVal && param.id in paramVal) {
            param = JSON.parse(JSON.stringify(param));
            param.default = paramVal[param.id];
        }
        actualParams.push(param);
        handled[param.id] = true;
        if (applyValues) {
            let val = paramVal[param.id];
            if (val) {
                // Comfy can do full 2^64 but that causes backend issues (can't have 2^64 *and* -1 as options, so...) so cap to 2^63
                if (param.type == 'integer' && param.view_type == 'seed' && val > 2**63) {
                    val = -1;
                }
                if (val !== null && val !== undefined) {
                    if (param.id == 'model') {
                        val = val.replaceAll('\\', '/');
                        setCookie('selected_model', val, 90);
                        forceSetDropdownValue('input_model', val);
                        forceSetDropdownValue('current_model', val);
                        let setModelVal = val;
                        setTimeout(() => {
                            setCookie('selected_model', setModelVal, 90);
                            forceSetDropdownValue('input_model', setModelVal);
                            forceSetDropdownValue('current_model', setModelVal);
                        }, 100);
                    }
                    else {
                        setCookie(`lastparam_input_${param.id}`, `${val}`, 0.5);
                    }
                }
            }
        }
    }
    let isSortTop = p => p.id == 'prompt' || p.id == 'negativeprompt' || p.id == 'comfyworkflowraw' || p.id == 'comfyworkflowparammetadata';
    let prompt = Object.values(actualParams).filter(isSortTop);
    let otherParams = Object.values(actualParams).filter(p => !isSortTop(p));
    return sortParameterList(Object.values(params).filter(p => !handled[p.id]), prompt, otherParams);
}

function setComfyWorkflowInput(params, retained, paramVal, applyValues) {
    localStorage.setItem('last_comfy_workflow_input', JSON.stringify({params, retained, paramVal}));
    gen_param_types = sortAndFixComfyParameters(params, retained, applyValues, paramVal, true);
    genInputs(true);
    let buttonHolder = getRequiredElementById('comfy_workflow_disable_button');
    buttonHolder.style.display = 'block';
    if (!otherInfoSpanContent.includes(comfyInfoSpanNotice)) {
        otherInfoSpanContent.push(comfyInfoSpanNotice);
        updateOtherInfoSpan();
    }
}

/**
 * Called to forced the parameter list back to default (instead of comfy-workflow-specific).
 */
function comfyParamsDisable() {
    localStorage.removeItem('last_comfy_workflow_input');
    gen_param_types = rawGenParamTypesFromServer;
    genInputs(true);
    let buttonHolder = getRequiredElementById('comfy_workflow_disable_button');
    buttonHolder.style.display = 'none';
    if (otherInfoSpanContent.includes(comfyInfoSpanNotice)) {
        otherInfoSpanContent.splice(otherInfoSpanContent.indexOf(comfyInfoSpanNotice), 1);
        updateOtherInfoSpan();
    }
}

/**
 * Called when the user wants to use the workflow (via button press) to load the workflow and tab over.
 */
function comfyUseWorkflowNow() {
    replaceParamsToComfy();
    getRequiredElementById('text2imagetabbutton').click();
}

let lastComfyMessageId = 0;

function comfyNoticeMessage(message) {
    let messageId = ++lastComfyMessageId;
    let infoSlot = getRequiredElementById('comfy_notice_slot');
    infoSlot.innerText = message;
    setTimeout(() => {
        if (lastComfyMessageId == messageId) {
            infoSlot.innerText = "";
        }
    }, 2000);
}

/**
 * Called when the user wants to save the workflow (via button press).
 */
function comfySaveWorkflowNow() {
    comfyReconfigureQuickload();
    let curImg = document.getElementById('current_image_img');
    let enableImage = getRequiredElementById('comfy_save_use_image');
    let saveImageSection = getRequiredElementById('comfy_save_image');
    saveImageSection.innerHTML = '';
    if (curImg) {
        let newImg = curImg.cloneNode(true);
        newImg.id = 'comfy_save_image_img';
        newImg.style.maxWidth = '100%';
        newImg.removeAttribute('width');
        newImg.removeAttribute('height');
        saveImageSection.appendChild(newImg);
        enableImage.checked = true;
        enableImage.disabled = false;
    }
    else {
        enableImage.checked = false;
        enableImage.disabled = true;
    }
    $('#comfy_workflow_save_modal').modal('show');
}

function comfyLoadByName(name) {
    comfyNoticeMessage("Loading...");
    genericRequest('ComfyReadWorkflow', { 'name': name }, (data) => {
        let workflow = data.result.workflow;
        // Note: litegraph does some dumb prototype hacks so this clone forces it to work properly
        comfyFrame().contentWindow.app.loadGraphData(comfyFrame().contentWindow.LiteGraph.cloneObject(JSON.parse(workflow)));
        comfyNoticeMessage("Loaded.");
    });
}

let comfySaveSearchPopover = null;
let comfySaveModalName = getRequiredElementById('comfy_save_modal_name');

comfySaveModalName.addEventListener('input', e => {
    let popId = `uiimprover_comfy_save_modal_name`;
    let rect = e.target.getBoundingClientRect();
    let selector = getRequiredElementById('comfy_quickload_select');
    let search = e.target.value.toLowerCase();
    let buttons = [...selector.options].filter(o => o.value && !o.value.startsWith("--") && o.value.toLowerCase().includes(search));
    buttons = buttons.map(o => { return { key: o.innerText, action: () => { e.target.value = o.value; } }; });
    if (comfySaveSearchPopover) {
        comfySaveSearchPopover.remove();
        comfySaveSearchPopover = null;
    }
    if (buttons.length > 0) {
        comfySaveSearchPopover = new AdvancedPopover(popId, buttons, false, rect.x, rect.y + e.target.offsetHeight + 6, e.target.parentElement, null, e.target.offsetHeight + 6);
    }
});
comfySaveModalName.addEventListener('keydown', e => {
    if (comfySaveSearchPopover) {
        comfySaveSearchPopover.onKeyDown(e);
    }
}, true);

/** Save button in the Save modal. */
function comfySaveModalSaveNow() {
    let saveName = comfySaveModalName.value;
    if (!saveName || !saveName.trim()) {
        alert("No name given, can't save");
        return;
    }
    let selector = getRequiredElementById('comfy_quickload_select');
    let search = saveName.toLowerCase();
    let match = [...selector.options].find(o => o.value.toLowerCase() == search);
    if (match) {
        if (!confirm(`Are you sure you want to overwrite the workflow "${match.value}"?`)) {
            return;
        }
        saveName = match.value;
    }
    let image = null;
    if (getRequiredElementById('comfy_save_use_image').checked) {
        image = imageToSmallPreviewData(getRequiredElementById('comfy_save_image').getElementsByTagName('img')[0]);
    }
    $('#comfy_workflow_save_modal').modal('hide');
    comfyNoticeMessage("Saving...");
    comfyBuildParams((params, prompt_text, retained, paramVal, workflow) => {
        params = JSON.parse(JSON.stringify(params));
        delete params.comfyworkflowparammetadata;
        delete params.comfyworkflowraw;
        let inputs = {
            'name': saveName,
            'description': getRequiredElementById('comfy_save_description').value,
            'enable_in_simple': getRequiredElementById('comfy_save_enable_simple').checked,
            'workflow': JSON.stringify(workflow),
            'prompt': prompt_text,
            'custom_params': params,
            'param_values': paramVal,
            'image': image
        };
        genericRequest('ComfySaveWorkflow', inputs, (data) => {
            comfyNoticeMessage("Saved!");
            comfyReconfigureQuickload();
            if (comfyWorkflowBrowser.everLoaded) {
                comfyWorkflowBrowser.refresh();
            }
        });
    });
}

/** Cancel button in the Save modal. */
function comfyHideSaveModal() {
    $('#comfy_workflow_save_modal').modal('hide');
}

/** Fills the quick-load selector with the provided values. */
function comfyFillQuickLoad(vals) {
    let selector = getRequiredElementById('comfy_quickload_select');
    selector.innerHTML = '<option value="" selected>-- Quick Load --</option>';
    for (let workflow of vals) {
        let option = document.createElement('option');
        option.innerText = workflow.name;
        selector.appendChild(option);
    }
}

/** Ensures the quick-load list is up-to-date. */
function comfyReconfigureQuickload() {
    genericRequest('ComfyListWorkflows', {}, (data) => {
        comfyFillQuickLoad(data.workflows);
    });
}

/** Triggered when the quick-load selector changes, to cause a load if needed. */
function comfyQuickloadSelectChanged() {
    let selector = getRequiredElementById('comfy_quickload_select');
    let opt = selector.options[selector.selectedIndex].value;
    if (!opt) {
        return;
    }
    comfyLoadByName(opt);
    selector.selectedIndex = 0;
}

/** Triggered when the multi-GPU selector changes, to change the setting. */
function comfyMultiGPUSelectChanged() {
    let multiGpuSelector = getRequiredElementById('comfy_multigpu_select');
    if (multiGpuSelector.value == 'all') {
        setCookie('comfy_domulti', 'true', 365);
    }
    else if (multiGpuSelector.value == 'reserve') {
        setCookie('comfy_domulti', 'reserve', 365);
    }
    else if (multiGpuSelector.value == 'none') {
        deleteCookie('comfy_domulti');
    }
    multiGpuSelector.selectedIndex = 0;
    let container = getRequiredElementById('comfy_workflow_frameholder');
    container.innerHTML = '';
    hasComfyLoaded = false;
    if (comfyEnableInterval != null) {
        clearInterval(comfyEnableInterval);
        comfyEnableInterval = null;
    }
    comfyTryToLoad();
}

/** Button to get the buttons out of the way. */
function comfyToggleButtonsVisible() {
    let button = getRequiredElementById('comfy_buttons_closer');
    let area = getRequiredElementById('comfy_buttons_closeable_area');
    if (area.style.display == 'none') {
        area.style.display = '';
        button.innerHTML = '&#x2B9D;';
    }
    else {
        area.style.display = 'none';
        button.innerHTML = '&#x2B9F;';
    }
}

/** Triggered when the 'import from generate tab' button is clicked. */
function comfyImportWorkflow() {
    genericRequest('ComfyGetGeneratedWorkflow', getGenInput(), (data) => {
        if (!data.workflow) {
            showError('No workflow found.');
            return;
        }
        comfyFrame().contentWindow.app.loadApiJson(comfyFrame().contentWindow.LiteGraph.cloneObject(JSON.parse(data.workflow)));
    });
}

getRequiredElementById('maintab_comfyworkflow').addEventListener('click', comfyTryToLoad);

backendsRevisedCallbacks.push(() => {
    let hasAny = Object.values(backends_loaded).filter(x => x.type.startsWith('comfyui_')
        || x.type == 'swarmswarmbackend' // TODO: Actually check if the backend has a comfy instance rather than just assuming swarmback==comfy
        ).length > 0;
    getRequiredElementById('maintab_comfyworkflow').style.display = hasAny ? 'block' : 'none';
    if (hasAny && !comfyHasTriedToLoad) {
        comfyHasTriedToLoad = true;
        comfyReloadObjectInfo();
    }
});

function comfyListWorkflowsForBrowser(path, isRefresh, callback, depth) {
    genericRequest('ComfyListWorkflows', {}, (data) => {
        let relevant = data.workflows.filter(w => w.name.startsWith(path));
        let workflowsWithSlashes = relevant.map(w => w.name.substring(path.length)).map(w => w.startsWith('/') ? w.substring(1) : w).filter(w => w.includes('/'));
        let preSlashes = workflowsWithSlashes.map(w => w.substring(0, w.lastIndexOf('/')));
        let fixedFolders = preSlashes.map(w => w.split('/').map((_, i, a) => a.slice(0, i + 1).join('/'))).flat();
        let deduped = [...new Set(fixedFolders)];
        let folders = deduped.sort((a, b) => b.toLowerCase().localeCompare(a.toLowerCase()));
        let mapped = relevant.map(f => {
            return { 'name': f.name, 'data': f };
        });
        callback(folders, mapped);
    });
}

function comfyDescribeWorkflowForBrowser(workflow) {
    let buttons = [
        {
            label: 'Replace',
            onclick: (e) => {
                getRequiredElementById('comfy_save_modal_name').value = workflow.name;
                getRequiredElementById('comfy_save_description').value = workflow.data.description;
                getRequiredElementById('comfy_save_enable_simple').checked = workflow.data.enable_in_simple;
                comfySaveWorkflowNow();
            }
        },
        {
            label: 'Delete',
            onclick: (e) => {
                comfyNoticeMessage("Deleting...");
                genericRequest('ComfyDeleteWorkflow', {'name': workflow.name}, data => {
                    comfyNoticeMessage("Deleted.");
                    e.remove();
                });
            }
        }
    ];
    return { name: workflow.name, description: `<b>${escapeHtmlNoBr(workflow.name)}</b><br>${escapeHtmlNoBr(workflow.data.description ?? "")}`, image: workflow.data.image, buttons: buttons, className: '', searchable: `${workflow.name}\n${workflow.description}` };
}

function comfySelectWorkflowForBrowser(workflow) {
    comfyLoadByName(workflow.name);
}

let comfyWorkflowBrowser = new GenPageBrowserClass('comfy_workflow_browser_container', comfyListWorkflowsForBrowser, 'comfyworkflowbrowser', 'Small Thumbnails', comfyDescribeWorkflowForBrowser, comfySelectWorkflowForBrowser);
comfyWorkflowBrowser.folderTreeVerticalSpacing = '8rem';
comfyWorkflowBrowser.splitterMinWidth = 16 * 20;

/**
 * Called when the user wants to browse their workflows (via button press).
 */
function comfyBrowseWorkflowsNow() {
    let holder = getRequiredElementById('comfy_workflow_topbar_holder');
    let button = getRequiredElementById('comfy_workflow_browse_button');
    let frameholder = getRequiredElementById('comfy_workflow_frameholder');
    if (holder.style.display == 'none') {
        holder.style.display = 'block';
        if (comfyWorkflowBrowser.everLoaded) {
            comfyWorkflowBrowser.update(false);
        }
        else {
            comfyWorkflowBrowser.navigate('');
        }
        button.innerText = translate('Hide Workflows');
        frameholder.style.height = 'calc(100% - 20rem)';
    }
    else {
        holder.style.display = 'none';
        button.innerText = translate('Browse Workflows');
        frameholder.style.height = '100%';
    }
}

/**
 * Prep-callback that can restore the last comfy workflow input you had.
 */
function comfyCheckPrep() {
    let lastComfyWorkflowInput = localStorage.getItem('last_comfy_workflow_input');
    if (lastComfyWorkflowInput) {
        let {params, retained, paramVal} = JSON.parse(lastComfyWorkflowInput);
        setComfyWorkflowInput(params, retained, paramVal, false);
    }
    metadataKeyFormatCleaners.push(key => {
        if (key.startsWith('comfyrawworkflowinput')) {
            key = key.substring('comfyrawworkflowinput'.length);
            for (let type of ['decimal', 'seed', 'integer', 'string']) {
                if (key.startsWith(type)) {
                    key = key.substring(type.length);
                    break;
                }
            }
        }
        return key;
    });
}

sessionReadyCallbacks.push(comfyCheckPrep);
