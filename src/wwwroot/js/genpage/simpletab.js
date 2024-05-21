
class SimpleTab {

    constructor() {
        this.hasLoaded = false;
        this.hasBuilt = false;
        this.containerDiv = getRequiredElementById('simpletabmainview');
        this.inputsSidebar = getRequiredElementById('simple_input_sidebar');
        this.inputsArea = getRequiredElementById('simple_inputs_area');
        this.inputsAreaAdvanced = getRequiredElementById('simple_inputs_area_advanced');
        this.inputsAreaHidden = getRequiredElementById('simple_inputs_area_hidden');
        this.tabButton = getRequiredElementById('simpletabbutton');
        this.wrapperDiv = getRequiredElementById('simpletabbrowserwrapper');
        this.imageContainer = getRequiredElementById('simple_image_container');
        this.imageElem = getRequiredElementById('simple_image_container_img');
        this.progressWrapper = getRequiredElementById('simpletab_progress_wrapper');
        this.loadingSpinner = getRequiredElementById('simple_loading_spinner');
        this.batchArea = getRequiredElementById('simple_current_image_batch');
        this.browser = new GenPageBrowserClass('simpletabbrowserwrapper', this.browserListEntries.bind(this), 'simpletabbrowser', 'Big Thumbnails', this.browserDescribeEntry.bind(this), this.browserSelectEntry.bind(this), '', 10);
        this.browser.depth = 10;
        this.browser.showDepth = false;
        this.browser.showRefresh = false;
        this.browser.showUpFolder = false;
        this.browser.folderTreeShowFiles = true;
        this.browser.folderSelectedEvent = this.onFolderSelected.bind(this);
        this.browser.builtEvent = this.onBrowserBuilt.bind(this);
        this.browser.sizeChangedEvent = this.onBrowserSizeChanged.bind(this);
        this.tabButton.addEventListener('click', this.onTabClicked.bind(this));
        this.genHandler = new SimpleTabGenerateHandler();
        this.genHandler.validateModel = false;
        this.genHandler.imageContainerDivId = 'simple_image_container';
        this.genHandler.imageId = 'simple_image_container_img';
        this.mustSelectTarget = null;
    }

    onFolderSelected() {
        this.browser.fullContentDiv.style.display = 'inline-block';
        this.containerDiv.style.display = 'none';
        setTimeout(() => updateHash(), 10);
    }

    onBrowserBuilt() {
        if (this.hasBuilt) {
            return;
        }
        this.wrapperDiv.appendChild(this.containerDiv);
        this.hasBuilt = true;
        if (this.mustSelectTarget) {
            let target = this.mustSelectTarget;
            setTimeout(() => this.browser.clickPath(target), 500);
            this.mustSelectTarget = null;
        }
    }

    onTabClicked() {
        this.browser.navigate('');
        if (this.hasLoaded) {
            this.onFolderSelected();
            return;
        }
        for (let key in sessionStorage) {
            if (key.startsWith('simpletablast_')) {
                sessionStorage.removeItem(key);
            }
        }
        this.hasLoaded = true;
    }

    onBrowserSizeChanged() {
        this.containerDiv.style.width = this.browser.fullContentDiv.style.width;
    }

    generate() {
        this.setNoImage();
        this.markLoading();
        let inputs = {};
        let elems = [...this.inputsSidebar.querySelectorAll('.auto-input')].map(i => i.querySelector('[data-param_id]'));
        for (let elem of elems) {
            let toggler = document.getElementById(`${elem.id}_toggle`);
            if (toggler && !toggler.checked) {
                continue;
            }
            let id = elem.dataset.param_id;
            let value = getInputVal(elem);
            inputs[id] = value;
        }
        this.genHandler.doGenerate(inputs);
    }

    interrupt() {
    }

    setImage(imgSrc) {
        this.imageElem.src = imgSrc;
        this.imageElem.style.opacity = 1;
    }

    markLoading() {
        this.loadingSpinner.style.display = '';
        this.imageElem.style.filter = 'blur(5px)';
    }

    markDoneLoading() {
        this.loadingSpinner.style.display = 'none';
        this.imageElem.style.filter = '';
        this.genHandler.gotProgress(-1, -1, '');
    }

    setNoImage() {
        this.imageElem.style.opacity = 0;
    }

    browserDescribeEntry(workflow) {
        let buttons = [];
        return { name: workflow.name, description: `<b>${escapeHtmlNoBr(workflow.name)}</b><br>${escapeHtmlNoBr(workflow.data.description ?? "")}`, image: workflow.data.image, buttons: buttons, className: '', searchable: `${workflow.name}\n${workflow.data.description}` };
    }

    browserSelectEntry(workflow) {
        this.browser.selected = workflow.name;
        updateHash();
        this.browser.rerender();
        genericRequest('ComfyReadWorkflow', { name: workflow.name }, (data) => {
            let params = Object.values(JSON.parse(data.result.custom_params));
            let fakeInternalGroup = { name: 'SimpleTabInternalGroup', id: 'simpletabinternalgroup', open: false, priority: -99999, advanced: true, can_shrink: true, toggles: false };
            function getFakeParam(name, val) {
                return { name: name, default: val, id: cleanParamName(name), type: 'text', description: '', values: null, view_type: 'normal', min: 0, max: 0, step: 0, visible: false, toggleable: false, priority: -99999, advanced: true, feature_flag: null, do_not_save: true, no_popover: true, group: fakeInternalGroup };
            }
            params.push(getFakeParam('comfyworkflowraw', data.result.prompt));
            params.push(getFakeParam('comfyworkflowparammetadata', data.result.custom_params));
            if (data.result.param_values) {
                let paramVals = JSON.parse(data.result.param_values);
                for (let key in paramVals) {
                    let param = params.find(p => p.id == key);
                    if (param) {
                        param.default = paramVals[key];
                    }
                }
            }
            let groupsEnable = [], groupsClose = [], runnables = [];
            let lastGroup = null;
            for (let areaData of [[this.inputsArea, (p) => p.visible && !isParamAdvanced(p), 0],
                    [this.inputsAreaAdvanced, (p) => p.visible && isParamAdvanced(p), 1],
                    [this.inputsAreaHidden, (p) => !p.visible, 2]]) {
                let html = '';
                if (areaData[2] == 0) {
                    html += `<div class="simpletab-workflow-header">${escapeHtml(workflow.name)}</div><div class="simpletab-workflow-description">${safeHtmlOnly(workflow.data.description)}</div>`;
                }
                for (let param of sortParameterList(params.filter(areaData[1]))) {
                    let groupName = param.group ? param.group.name : null;
                    if (groupName != lastGroup) {
                        if (lastGroup) {
                            html += '</div></div>';
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
                            let toggler = getToggleHtml(param.group.toggles, `simpleinput_group_content_${groupId}`, escapeHtml(param.group.name), ' group-toggler-switch', 'doToggleGroup');
                            html += `<div class="input-group input-group-open" id="auto-group-${groupId}"><span id="simpleinput_group_${groupId}" class="input-group-header ${shrinkClass}"><span class="header-label-wrap">${symbol}<span class="header-label">${translateableHtml(escapeHtml(param.group.name))}</span>${toggler}${infoButton}</span></span><div class="input-group-content" id="simpleinput_group_content_${groupId}">`;
                        }
                        lastGroup = groupName;
                    }
                    let newData = getHtmlForParam(param, "simpleinput_");
                    html += newData.html;
                    if (newData.runnable) {
                        runnables.push(newData.runnable);
                    }
                }
                areaData[0].innerHTML = html;
            }
            this.setNoImage();
            this.browser.fullContentDiv.style.display = 'none';
            this.containerDiv.style.display = 'inline-block';
            for (let group of groupsClose) {
                let elem = getRequiredElementById(`simpleinput_group_${group}`);
                toggleGroupOpen(elem);
            }
            for (let group of groupsEnable) {
                let elem = document.getElementById(`simpleinput_group_content_${group}_toggle`);
                if (elem) {
                    elem.checked = true;
                    doToggleGroup(`simpleinput_group_content_${group}`);
                }
            }
            for (let param of params) {
                if (param.toggleable) {
                    doToggleEnable(`simpleinput_${param.id}`);
                }
                let elem = getRequiredElementById(`simpleinput_${param.id}`);
                let lastVal = sessionStorage.getItem(`simpletablast_${workflow.name}_simpleinput_${param.id}`);
                if (lastVal) {
                    setInputVal(elem, lastVal);
                }
                elem.addEventListener('change', () => {
                    sessionStorage.setItem(`simpletablast_${workflow.name}_simpleinput_${param.id}`, getInputVal(elem));
                });
            }
            for (let runnable of runnables) {
                runnable();
            }
        });
    }

    browserListEntries(path, isRefresh, callback, depth) {
        genericRequest('ComfyListWorkflows', {}, (data) => {
            let relevant = data.workflows.filter(w => w.enable_in_simple && w.name.startsWith(path));
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
}

class SimpleTabGenerateHandler extends GenerateHandler {

    constructor() {
        super();
    }

    resetBatchIfNeeded() {
        // No batch.
    }

    beforeGenRun() {
        // Nothing to do.
    }

    getGenInput(input_overrides = {}, input_preoverrides = {}) {
        let data = JSON.parse(JSON.stringify(input_overrides));
        if (!data['images']) {
            data['images'] = 1;
        }
        if (!data['model']) {
            data['model'] = '(none)';
        }
        delete data['stability_api_key'];
        return data;
    }

    setCurrentImage(src, metadata = '', batchId = '', previewGrow = false, smoothAdd = false) {
        simpleTab.markDoneLoading();
        simpleTab.setImage(src);
    }

    gotImageResult(image, metadata, batchId) {
        simpleTab.markDoneLoading();
        simpleTab.setImage(image);
        let fname = image && image.includes('/') ? image.substring(image.lastIndexOf('/') + 1) : image;
        let batch_div = appendImage(simpleTab.batchArea, image, batchId, fname, metadata, 'batch');
        batch_div.addEventListener('click', () => this.setCurrentImage(image));
    }

    gotImagePreview(image, metadata, batchId) {
        simpleTab.markLoading();
        if (image == 'imgs/model_placeholder.jpg') {
            return;
        }
        simpleTab.setImage(image);
    }

    gotProgress(current, overall, batchId) {
        if (current < 0) {
            simpleTab.progressWrapper.style.display = 'none';
            return;
        }
        simpleTab.markLoading();
        simpleTab.progressWrapper.style.display = '';
        simpleTab.progressWrapper.querySelector('.image-preview-progress-current').style.width = `${current * 100}%`;
        simpleTab.progressWrapper.querySelector('.image-preview-progress-overall').style.width = `${overall * 100}%`;
    }

    hadError(msg) {
        simpleTab.markDoneLoading();
        simpleTab.setNoImage();
        super.hadError(msg);
    }
}

let simpleTab = new SimpleTab();
