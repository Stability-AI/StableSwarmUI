
class SimpleTab {

    constructor() {
        this.hasLoaded = false;
        this.hasBuilt = false;
        this.containerDiv = getRequiredElementById('simpletabmainview');
        this.inputsArea = getRequiredElementById('simple_inputs_area');
        this.inputsAreaAdvanced = getRequiredElementById('simple_inputs_area_advanced');
        this.inputsAreaHidden = getRequiredElementById('simple_inputs_area_hidden');
        this.tabButton = getRequiredElementById('simpletabbutton');
        this.wrapperDiv = getRequiredElementById('simpletabbrowserwrapper');
        this.imageContainer = getRequiredElementById('simple_image_container');
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
    }

    onFolderSelected() {
        this.browser.fullContentDiv.style.display = 'inline-block';
        this.containerDiv.style.display = 'none';
    }

    onBrowserBuilt() {
        if (this.hasBuilt) {
            return;
        }
        this.wrapperDiv.appendChild(this.containerDiv);
        this.hasBuilt = true;
    }

    onTabClicked() {
        if (this.hasLoaded) {
            return;
        }
        this.browser.navigate('');
        this.hasLoaded = true;
    }

    onBrowserSizeChanged() {
        this.containerDiv.style.width = this.browser.fullContentDiv.style.width;
    }

    generate() {
    }

    interrupt() {
    }

    browserDescribeEntry(workflow) {
        let buttons = [];
        return { name: workflow.name, description: `<b>${escapeHtmlNoBr(workflow.name)}</b><br>${escapeHtmlNoBr(workflow.data.description ?? "")}`, image: workflow.data.image, buttons: buttons, className: '', searchable: `${workflow.name}\n${workflow.description}` };
    }

    browserSelectEntry(workflow) {
        genericRequest('ComfyReadWorkflow', { name: workflow.name }, (data) => {
            let params = Object.values(JSON.parse(data.result.custom_params));
            let groupsEnable = [], groupsClose = [], runnables = [];
            let lastGroup = null;
            for (let areaData of [[this.inputsArea, (p) => p.visible && !isParamAdvanced(p), true],
                    [this.inputsAreaAdvanced, (p) => p.visible && isParamAdvanced(p), false],
                    [this.inputsAreaHidden, (p) => !p.visible, false]]) {
                let html = '';
                if (areaData[2]) {
                    html += `<button class="generate-button" id="simple_generate_button" onclick="simpleTab.generate()">Generate</button>
                    <button class="interrupt-button legacy-interrupt interrupt-button-none" id="simple_interrupt_button" onclick="simpleTab.interrupt()">&times;</button>`;
                }
                for (let param of params.filter(areaData[1])) {
                    let groupName = param.group ? param.group.name : null;
                    if (groupName != lastGroup) {
                        if (lastGroup) {
                            html += '</div></div>';
                        }
                        if (param.group) {
                            let infoButton = '';
                            let groupId = param.group.id;
                            if (param.group.description) {
                                html += `<div class="sui-popover" id="popover_group_${groupId}"><b>${translateableHtml(escapeHtml(param.group.name))}</b>:<br>&emsp;${translateableHtml(escapeHtml(param.group.description))}</div>`;
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
                            let toggler = getToggleHtml(param.group.toggles, `simpleinput_group_content_${groupId}`, escapeHtml(param.group.name), ' group-toggler-switch', 'doToggleGroup');
                            html += `<div class="input-group input-group-open" id="auto-group-${groupId}"><span id="simpleinput_group_${groupId}" class="input-group-header"><span class="header-label-wrap"><span class="auto-symbol">&#x2B9F;</span><span class="header-label">${translateableHtml(escapeHtml(param.group.name))}</span>${toggler}${infoButton}</span></span><div class="input-group-content" id="simpleinput_group_content_${groupId}">`;
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
            this.imageContainer.innerHTML = `<img src="${data.result.image}" />`;
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

let simpleTab = new SimpleTab();
