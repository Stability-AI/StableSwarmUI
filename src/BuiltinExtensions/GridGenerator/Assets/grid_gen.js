
class GridGenClass {

    mainDiv = null;
    axisDiv = null;
    settingsDiv = null;
    lastAxisId = 0;
    popover = null;
    excludedParams = ['images', 'batchsize', 'refinersavebeforerefine'];

    fillSelectorOptions(selector) {
        selector.add(new Option('', '', true, true));
        let opts = [...gen_param_types];
        opts.sort((a, b) => {
            if (a.id == 'model') {
                return -1;
            }
            if (b.id == 'model') {
                return 1;
            }
            if (a.id == 'prompt') {
                return -1;
            }
            if (b.id == 'prompt') {
                return 1;
            }
            if (a.id.startsWith('gridgen') && !b.id.startsWith('gridgen')) {
                return -1;
            }
            if (!a.id.startsWith('gridgen') && b.id.startsWith('gridgen')) {
                return 1;
            }
            if (!a.visible && b.visible) {
                return 1;
            }
            if (a.visible && !b.visible) {
                return -1;
            }
            if (isParamAdvanced(a) && !isParamAdvanced(b)) {
                return 1;
            }
            if (!isParamAdvanced(a) && isParamAdvanced(b)) {
                return -1;
            }
            if (a.group == b.group) {
                return a.priority - b.priority;
            }
            let aPrio = a.group ? a.group.priority : a.priority;
            let bPrio = b.group ? b.group.priority : b.priority;
            return aPrio - bPrio;

        });
        for (let option of opts) {
            if (!option.extra_hidden && !this.excludedParams.includes(option.id)) {
                selector.add(new Option(option.name, option.id));
            }
        }
    }

    rebuildAxes() {
        if (!this.axisDiv) {
            return;
        }
        for (let selector of this.axisDiv.getElementsByClassName('grid-gen-selector')) {
            let curVal = selector.value;
            selector.innerHTML = '';
            this.fillSelectorOptions(selector);
            selector.value = curVal;
        }
    }

   addAxis() {
        let id = this.lastAxisId++;
        let wrapper = createDiv(null, 'grid-gen-axis-wrapper');
        let axisTypeSelector = document.createElement('select');
        axisTypeSelector.className = 'grid-gen-selector';
        axisTypeSelector.id = `grid-gen-axis-type-${id}`;
        this.fillSelectorOptions(axisTypeSelector);
        let inputBox = document.createElement('div');
        inputBox.className = 'grid-gen-axis-input';
        inputBox.id = `grid-gen-axis-input-${id}`;
        let mode = null;
        function getFillable() {
            if (mode && (mode.values || mode.type == 'model')) {
                return mode.type == 'model' ? coreModelMap[mode.subtype || 'Stable-Diffusion'] : mode.values;
            }
            else if (mode && mode.type == 'boolean') {
                return ['true', 'false'];
            }
            else if (mode && mode.examples) {
                return mode.examples;
            }
            return null;
        }
        let updateInput = () => {
            let lastSelection = getCurrentCursorPosition(inputBox.id);
            let text = inputBox.innerText;
            let separator = text.includes('||') ? '||' : ',';
            let parts = text.split(separator);
            let html = '';
            for (let i in parts) {
                let title = '';
                let clazz = 'grid-gen-axis-input-value';
                let cleanPart = parts[i].trim();
                if (cleanPart.startsWith('SKIP:')) {
                    clazz += ' grid-gen-axis-input-value-skipped';
                    title += "(This value is skipped)";
                }
                else if (mode) {
                    cleanPart = cleanPart.toLowerCase();
                    if (mode.values || mode.type == 'model' || mode.type == 'boolean') {
                        let matched = getFillable().filter(f => f.toLowerCase().includes(cleanPart));
                        if (matched.length == 0) {
                            title += "This value does not appear in the available value set. Are you sure it's correct?";
                            clazz += ' grid-gen-axis-input-value-invalid';
                        }
                        else if (matched.length == 1) {
                            title += `Perfect match: ${matched[0]}`;
                        }
                        else {
                            let firstMatch = matched.filter(f => f.toLowerCase().startsWith(cleanPart));
                            if (firstMatch.length == 1) {
                                title += `StartsWith match: ${firstMatch[0]}`;
                            }
                            else {
                                let exactMatch = matched.filter(f => f.toLowerCase() == cleanPart);
                                if (exactMatch.length == 1) {
                                    title += `Exact match: ${exactMatch[0]}`;
                                }
                                else {
                                    title += `Multiple possible matches: ${matched.join(' || ')}`;
                                }
                            }
                        }
                    }
                    else if (['integer', 'decimal'].includes(mode.type) && ['..', '...', '....'].includes(cleanPart)) {
                    }
                    else if (mode.type == 'integer') {
                        let ival = parseInt(cleanPart);
                        if (isNaN(ival) || cleanPart.includes('.')) {
                            title += "This value is not a valid integer number.";
                            clazz += ' grid-gen-axis-input-value-invalid';
                        }
                        else if (ival < mode.min || ival > mode.max) {
                            title += `The value ${ival} is outside the range of ${mode.min} to ${mode.max}`;
                            clazz += ' grid-gen-axis-input-value-invalid';
                        }
                    }
                    else if (mode.type == 'decimal') {
                        let fval = parseFloat(cleanPart);
                        if (isNaN(fval)) {
                            title += "This value is not a valid decimal number.";
                            clazz += ' grid-gen-axis-input-value-invalid';
                        }
                        else if (fval < mode.min || fval > mode.max) {
                            title += `The value ${fval} is outside the range of ${mode.min} to ${mode.max}`;
                            clazz += ' grid-gen-axis-input-value-invalid';
                        }
                    }
                }
                html += `<span class="${clazz}" title="${escapeHtmlNoBr(title)}">${escapeHtmlNoBr(parts[i])}</span>` + (i == parts.length - 1 ? '' : `<span class="grid-gen-axis-input-separator">${separator}</span>`);
            }
            inputBox.innerHTML = html;
            if (lastSelection != -1) {
                let searchable = mode.type == 'model' ? coreModelMap[mode.subtype || 'Stable-Diffusion'] : mode.values;
                if (searchable) {
                    let searchPre = text.substring(0, lastSelection);
                    let searchPost = text.substring(lastSelection);
                    let areaPre = "", areaPost = "";
                    if (searchPre.includes(separator)) {
                        let index = searchPre.lastIndexOf(separator);
                        areaPre = searchPre.substring(0, index + separator.length);
                        searchPre = searchPre.substring(index + separator.length);
                    }
                    if (searchPost.includes(separator)) {
                        let index = searchPost.indexOf(separator);
                        areaPost = searchPost.substring(index);
                        searchPost = searchPost.substring(0, index);
                    }
                    searchPre = searchPre.trim().toLowerCase();
                    searchPost = searchPost.trim().toLowerCase();
                    let possible = searchable.filter(e => e.toLowerCase().includes(searchPre) && e.toLowerCase().includes(searchPost));
                    if (this.popover) {
                        hidePopover('grid_search');
                        this.popover.remove();
                        this.popover = null;
                    }
                    if (possible.length > 0 && possible.filter(e => e.toLowerCase() == searchPre).length == 0) {
                        this.popover = createDiv('popover_grid_search', 'sui-popover sui_popover_model sui_popover_scrollable');
                        let isFirst = true;
                        if (possible.length > 1) {
                            let button = createDiv(null, 'sui_popover_model_button_selected sui_popover_model_button_add_all sui_popover_model_button');
                            isFirst = false;
                            button.innerText = "(Add All)";
                            let combined = possible.join(', ');
                            if (combined.includes('||') || separator == '||') {
                                combined = possible.join(' || ');
                            }
                            button.addEventListener('click', () => {
                                hidePopover('grid_search');
                                this.popover.remove();
                                this.popover = null;
                                inputBox.innerText = areaPre + combined + areaPost;
                                setSelectionRange(inputBox, areaPre.length + combined.length, areaPre.length + combined.length);
                                updateInput();
                            });
                            this.popover.appendChild(button);
                        }
                        for (let val of possible) {
                            let button = createDiv(null, (isFirst ? 'sui_popover_model_button_selected ' : '') + 'sui_popover_model_button');
                            isFirst = false;
                            button.innerText = val;
                            button.addEventListener('click', () => {
                                hidePopover('grid_search');
                                this.popover.remove();
                                this.popover = null;
                                inputBox.innerText = areaPre + val + areaPost;
                                setSelectionRange(inputBox, areaPre.length + val.length, areaPre.length + val.length);
                                updateInput();
                            });
                            this.popover.appendChild(button);
                        }
                        this.mainDiv.appendChild(this.popover);
                        let rect = inputBox.getBoundingClientRect();
                        showPopover('grid_search', rect.x, rect.y + inputBox.offsetHeight + 6);
                    }
                }
                setSelectionRange(inputBox, lastSelection, lastSelection);
            }
            lastSelection = -1;
        };
        let popoverSelected = () => this.popover.getElementsByClassName('sui_popover_model_button_selected')[0];
        let popoverScrollFix = () => {
            let selected = popoverSelected();
            if (selected.offsetTop + selected.offsetHeight > this.popover.scrollTop + this.popover.offsetHeight) {
                this.popover.scrollTop = selected.offsetTop + selected.offsetHeight - this.popover.offsetHeight + 6;
            }
            else if (selected.offsetTop < this.popover.scrollTop) {
                this.popover.scrollTop = selected.offsetTop;
            }
        };
        inputBox.addEventListener('keydown', e => {
            if ((e.key == 'Tab' || (e.key == 'Enter' && !e.shiftKey)) && this.popover) {
                popoverSelected().click();
            }
            else if (e.key == 'ArrowDown' && this.popover) {
                let possible = [...this.popover.getElementsByClassName('sui_popover_model_button')];
                let selectedIndex = possible.findIndex(e => e.classList.contains('sui_popover_model_button_selected'));
                possible[selectedIndex].classList.remove('sui_popover_model_button_selected');
                possible[(selectedIndex + 1) % possible.length].classList.add('sui_popover_model_button_selected');
                popoverScrollFix();
            }
            else if (e.key == 'ArrowUp' && this.popover) {
                let possible = [...this.popover.getElementsByClassName('sui_popover_model_button')];
                let selectedIndex = possible.findIndex(e => e.classList.contains('sui_popover_model_button_selected'));
                possible[selectedIndex].classList.remove('sui_popover_model_button_selected');
                possible[(selectedIndex + possible.length - 1) % possible.length].classList.add('sui_popover_model_button_selected');
                popoverScrollFix();
            }
            else if (e.key == 'Home') {
                setSelectionRange(inputBox, 0, 0);
            }
            else if (e.key == 'End') {
                setSelectionRange(inputBox, inputBox.innerText.length, inputBox.innerText.length);
            }
            else {
                return;
            }
            e.preventDefault();
            e.stopPropagation();
            return false;
        });
        inputBox.addEventListener('input', updateInput);
        inputBox.contentEditable = true;
        let fillButton = document.createElement('button');
        fillButton.style.visibility = 'hidden';
        fillButton.innerText = 'Fill';
        fillButton.className = 'basic-button grid-gen-axis-fill-button';
        fillButton.title = 'Fill with available values';
        fillButton.addEventListener('click', () => {
            let toFill = getFillable();
            if (!toFill) {
                return;
            }
            toFill = toFill.join(' || ');
            let text = inputBox.innerText.trim();
            if (!toFill.includes(',') && !text.includes('||')) {
                toFill = toFill.replaceAll(' || ', ', ');
            }
            if (text == '') {
                inputBox.innerText = toFill;
            }
            else if (text.includes(',') && toFill.includes('||')) {
                inputBox.innerText = text.replaceAll(',', ' ||') + ' || ' + toFill;
            }
            else {
                inputBox.innerText = text + (toFill.includes('||') ? ' || ' : ', ') + toFill;
            }
            updateInput();
        });
        axisTypeSelector.addEventListener('change', () => {
            mode = gen_param_types.find(e => e.id == axisTypeSelector.value);
            if (mode && (mode.values || mode.type == 'model')) {
                fillButton.innerText = 'Fill';
                fillButton.style.visibility = 'visible';
                fillButton.title = 'Fill with available values';
            }
            else if (mode && mode.type == 'boolean') {
                fillButton.innerText = 'Fill';
                fillButton.style.visibility = 'visible';
                fillButton.title = 'Fill with "true" and "false"';
            }
            else if (mode && mode.examples) {
                fillButton.innerText = 'Examples';
                fillButton.style.visibility = 'visible';
                fillButton.title = 'Fill with example values';
            }
            else {
                fillButton.style.visibility = 'hidden';
            }
            if (Array.from(this.axisDiv.getElementsByClassName('grid-gen-selector')).filter(e => e.value == '').length == 0) {
                this.addAxis();
            }
        });
        wrapper.appendChild(axisTypeSelector);
        wrapper.appendChild(inputBox);
        wrapper.appendChild(fillButton);
        this.axisDiv.appendChild(wrapper);
    }

    typeChanged() {
        let type = this.outputType.value;
        getRequiredElementById('grid-gen-page-config').style.display = type == 'Web Page' ? 'block' : 'none';
        localStorage.setItem('gridgen_output_type', type);
        this.updateOutputInfo();
    }

    listAxes() {
        return this.axisDiv.getElementsByClassName('grid-gen-axis-wrapper');
    }

    doGenerate() {
        resetBatchIfNeeded();
        let startTime = Date.now();
        let generatedCount = 0;
        let getOpt = (o) => getRequiredElementById('grid-gen-opt-' + o).checked;
        let type = this.outputType.value;
        this.updateOutputInfo();
        let data = {
            'baseParams': getGenInput(),
            'outputFolderName': this.lastPath,
            'doOverwrite': getOpt('do-overwrite'),
            'fastSkip': getOpt('fast-skip'),
            'generatePage': getOpt('generate-page'),
            'publishGenMetadata': getOpt('publish-metadata'),
            'dryRun': getOpt('dry-run'),
            'weightOrder': getOpt('weight-order'),
            'outputType': type,
            'continueOnError': getOpt('continue-on-error'),
            'showOutputs': getOpt('show-outputs'),
            'saveConfig': this.getSaveConfig(),
        };
        let axisData = [];
        for (let axis of this.listAxes()) {
            let type = axis.getElementsByClassName('grid-gen-selector')[0].value;
            let input = axis.getElementsByClassName('grid-gen-axis-input')[0].innerText;
            if (type != '') {
                axisData.push({ 'mode': type, 'vals': input });
            }
        }
        if (axisData.length == 0) {
            showError('No axes defined.');
            return;
        }
        data['gridAxes'] = axisData;
        let timeLastGenHit = Date.now();
        let path = this.lastPath;
        makeWSRequestT2I('GridGenRun', data, data => {
            if (data.image) {
                let timeNow = Date.now();
                let timeDiff = timeNow - timeLastGenHit;
                timeLastGenHit = timeNow;
                mainGenHandler.appendGenTimeFrom(timeDiff / 1000);
                gotImageResult(data.image, data.metadata);
                generatedCount++;
                let timeProgress = Math.round((Date.now() - startTime) / 1000);
                let rate = Math.round(generatedCount / timeProgress * 100) / 100;
                let message = `${rate} images per second`;
                if (rate < 1) {
                    rate = 1 / rate;
                    rate = Math.round(rate * 100) / 100;
                    message = `${rate} seconds per image`;
                }
                if (type == 'Web Page') {
                    this.outInfoBox.innerHTML = `<b>Running at ${message}</b> Output saved to <a href="${getImageOutPrefix()}/Grids/${path}/index.html" target="_blank">${getImageOutPrefix()}/Grids/<code>${path}</code></a>`;
                }
                else {
                    this.outInfoBox.innerHTML = `<b>Running at ${message}</b>`;
                }
            }
            else if (data.success) {
                if (type == 'Web Page') {
                    this.outInfoBox.innerHTML = `<b>Completed!</b> Output saved to <a href="${getImageOutPrefix()}/Grids/${path}/index.html" target="_blank">${getImageOutPrefix()}/Grids/<code>${path}</code></a>`;
                }
                else {
                    this.outInfoBox.innerHTML = `<b>Completed!</b>`;
                }
            }
        });
    }

    doGenWrapper() {
        setCurrentModel(() => {
            if (document.getElementById('current_model').value == '') {
                showError("Cannot generate, no model selected.");
                return;
            }
            this.doGenerate();
        });
    }

    register() {
        this.mainDiv = registerNewTool('grid_generator', 'Grid Generator', 'Generate Grid', () => this.doGenWrapper());
        this.axisDiv = createDiv('grid-gen-axis-area', 'grid-gen-axis-area');
        this.settingsDiv = createDiv('grid-gen-settings-area', 'grid-gen-settings-area');
        this.settingsDiv.innerHTML =
            `<br><label for="grid_generate_type">Output Type: </label><select id="grid_generate_type" onchange="extensionGridGen.typeChanged()"><option>Just Images</option><option>Grid Image</option><option selected>Web Page</option></select>&emsp;<button class="basic-button" onclick="extensionGridGen.saveGridConfig()">Save Grid Config</button>&emsp;<button class="basic-button" onclick="extensionGridGen.loadGridConfig()">Load Grid Config</button>
            ${modalHeader('gridgen_load_modal', 'Grid Generator: Load Config')}
                <div class="modal-body">
                    <p>Load grid config...</p>
                    <select id="grid_gen_load_config_selector">
                    </select>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-danger" onclick="extensionGridGen.loadModalDelete()">Delete Grid Config</button>
                    <button type="button" class="btn btn-primary" onclick="extensionGridGen.loadModalLoadNow()">Load Grid Config</button>
                    <button type="button" class="btn btn-secondary" onclick="extensionGridGen.hideLoadModal()">Cancel</button>
                </div>
            ${modalFooter()}
            ${modalHeader('gridgen_save_modal', 'Grid Generator: Save Config')}
                <div class="modal-body">
                    <p>Save grid config...</p>
                    <input type="text" id="grid_gen_save_config_name" placeholder="Config Name..." class="auto-input-text">
                    <br><label for="grid_gen_save_is_public">Share Grid With All Users</label> <input type="checkbox" id="grid_gen_save_is_public">
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-primary" onclick="extensionGridGen.saveModalSaveNow()">Save Grid Config</button>
                    <button type="button" class="btn btn-secondary" onclick="extensionGridGen.hideSaveModal()">Cancel</button>
                </div>
            ${modalFooter()}
            <div id="grid-gen-info-box">...</div>
            <div id="grid-gen-page-config">
                ${makeTextInput(null, 'grid-gen-output-folder-name', '', 'Output Folder Name', 'Name of the folder to save this grid under in your Image History.\nYou can use auto-fills [date], [time], [year], [month], [day], [hour], [minute], [second]', '', 'normal', 'Output folder name...', false, true)}
            </div>
            <br>
            <div class="grid-gen-checkboxes">
                ${makeCheckboxInput(null, 'grid-gen-opt-do-overwrite', '', 'Overwrite Existing Files', 'If checked, will overwrite any already-generated images.', false, false, true)}&emsp;
                ${makeCheckboxInput(null, 'grid-gen-opt-fast-skip', '', 'Fast Skip', 'If checked, uses faster skipping algorithm (prevents validation of skipped axes).', false, false, true)}&emsp;
                ${makeCheckboxInput(null, 'grid-gen-opt-generate-page', '', 'Generate Page', 'If unchecked, will prevent regenerating the page for the grid.', true, false, true)}&emsp;
                ${makeCheckboxInput(null, 'grid-gen-opt-publish-metadata', '', 'Publish Generation Metadata', 'If unchecked, will hide the image generation metadata.', true, false, true)}&emsp;
                ${makeCheckboxInput(null, 'grid-gen-opt-dry-run', '', 'Dry Run', 'If checked, will not actually generate any images - useful to validate your grid.', false, false, true)}&emsp;
                ${makeCheckboxInput(null, 'grid-gen-opt-weight-order', '', 'Allow Reordering', 'If checked, the grid generator will reorder processing order of axes to maximize generation speed.', true, false, true)}&emsp;
                ${makeCheckboxInput(null, 'grid-gen-opt-continue-on-error', '', 'Continue On Error', 'If checked, if any generations failure, those will be skipped and the grid will continue.', false, false, true)}&emsp;
                ${makeCheckboxInput(null, 'grid-gen-opt-show-outputs', '', 'Show Outputs', 'If checked, shows the images on-page as they come in. If not checked, only the final result is shown.', true, false, true)}&emsp;
            </div>
            <div class="hoverable-minor-hint-text">
                When using numbered parameters, you can type for example "<code>1, 2, .., 10</code>" to automatically have the "<code>..</code>" part filled in.
                &emsp;You can prefix any value with "<code>SKIP:</code>" (in all caps) to automatically skip that value (but keep it listed in the grid).&emsp;<a href="https://github.com/Stability-AI/StableSwarmUI/blob/master/src/BuiltinExtensions/GridGenerator/README.md" target="_blank">Full README/docs here</a>
            </div>`;
        this.mainDiv.appendChild(this.settingsDiv);
        this.mainDiv.appendChild(this.axisDiv);
        this.outInfoBox = document.getElementById('grid-gen-info-box');
        this.outputFolder = document.getElementById('grid-gen-output-folder-name');
        this.outputType = document.getElementById('grid_generate_type');
        this.outputType.value = localStorage.getItem('gridgen_output_type') || 'Web Page';
        this.outputFolder.addEventListener('input', () => { this.typeChanged(); this.updateOutputInfo() });
        this.outputFolder.value = `grid-[date]-[time]`;
        this.updateOutputInfo();
        this.typeChanged();
        this.addAxis();
    }

    parseOutputPath() {
        let today = new Date();
        let path = this.outputFolder.value;
        function pad(n) {
            return n < 10 ? '0' + n : n;
        }
        path = path.replaceAll('[date]', `${today.getFullYear()}-${pad(today.getMonth() + 1)}-${pad(today.getDate())}`)
            .replaceAll('[time]', `${pad(today.getHours())}-${pad(today.getMinutes())}-${pad(today.getSeconds())}`)
            .replaceAll('[year]', `${today.getFullYear()}`)
            .replaceAll('[month]', `${pad(today.getMonth() + 1)}`)
            .replaceAll('[day]', `${pad(today.getDate())}`)
            .replaceAll('[hour]', `${pad(today.getHours())}`)
            .replaceAll('[minute]', `${pad(today.getMinutes())}`)
            .replaceAll('[second]', `${pad(today.getSeconds())}`);
        this.lastPath = path;
        return path;
    }

    updateOutputInfo() {
        this.lastPath = this.parseOutputPath();
        if (this.outputType.value == 'Web Page') {
            genericRequest('GridGenDoesExist', { 'folderName': this.lastPath }, data => {
                let prefix = data.exists ? '<span class="gridgen_warn">Output WILL OVERRIDE existing folder</span>' : 'Output will be saved to';
                this.outInfoBox.innerHTML = `${prefix} <a href="${getImageOutPrefix()}/Grids/${this.lastPath}/index.html" target="_blank">${getImageOutPrefix()}/Grids/<code>${this.lastPath}</code></a>`;
            });
        }
        else {
            this.outInfoBox.innerHTML = '...';
        }
    };

    saveGridConfig() {
        $('#gridgen_save_modal').modal('show');
    }

    loadGridConfig() {
        genericRequest('GridGenListData', {}, data => {
            let selector = document.getElementById('grid_gen_load_config_selector');
            selector.innerHTML = '';
            for (let config of data.data) {
                selector.add(new Option(config, config));
            }
            for (let config of data.history) {
                selector.add(new Option(`History - ${config}`, `history/${config}`));
            }
            $('#gridgen_load_modal').modal('show');
        });
    }

    loadModalDelete() {
        let gridName = document.getElementById('grid_gen_load_config_selector').value;
        genericRequest('GridGenDeleteData', { 'gridName': gridName }, data => {
            this.hideLoadModal();
            this.outInfoBox.innerHTML = `<b>Config deleted!</b>`;
            setTimeout(() => this.updateOutputInfo(), 5000);
        });
    }

    loadModalLoadNow() {
        let gridName = document.getElementById('grid_gen_load_config_selector').value;
        let applyData = (data) => {
            this.hideLoadModal();
            this.outputFolder.value = data.output_folder_name;
            this.outputType.value = data.output_type;
            this.typeChanged();
            for (let opt of this.settingsDiv.getElementsByClassName('grid-gen-checkboxes')[0].getElementsByTagName('input')) {
                if (data.checkboxes[opt.id] != null) {
                    opt.checked = data.checkboxes[opt.id];
                }
            }
            this.axisDiv.innerHTML = '';
            this.lastAxisId = 0;
            this.addAxis();
            for (let axis of data.axes) {
                let wrapper = this.listAxes()[this.listAxes().length - 1];
                let selector = wrapper.getElementsByClassName('grid-gen-selector')[0];
                selector.value = axis.mode;
                triggerChangeFor(selector);
                let input = wrapper.getElementsByClassName('grid-gen-axis-input')[0];
                input.innerText = axis.values;
                triggerChangeFor(input);
            }
            this.outInfoBox.innerHTML = `<b>Config loaded!</b>`;
            setTimeout(() => this.updateOutputInfo(), 5000);
        }
        if (gridName.startsWith('history/')) {
            getJsonDirect(`${getImageOutPrefix()}/Grids/${gridName.substring('history/'.length)}/swarm_save_config.json`, (_, resp) => {
                applyData(resp);
            });
        }
        else {
            genericRequest('GridGenGetData', { 'gridName': gridName }, data => applyData(data.data));
        }
    }

    getSaveConfig() {
        let axes = [];
        for (let axis of this.listAxes()) {
            let type = axis.getElementsByClassName('grid-gen-selector')[0].value;
            let input = axis.getElementsByClassName('grid-gen-axis-input')[0].innerText;
            if (type != '' && input.trim() != '') {
                axes.push({ 'mode': type, 'values': input });
            }
        }
        let checkboxes = {};
        for (let opt of this.settingsDiv.getElementsByClassName('grid-gen-checkboxes')[0].getElementsByTagName('input')) {
            checkboxes[opt.id] = opt.checked;
        }
        return {
            'axes': axes,
            'checkboxes': checkboxes,
            'output_folder_name': this.outputFolder.value,
            'output_type': this.outputType.value,
        };
    }

    saveModalSaveNow() {
        let gridName = getRequiredElementById('grid_gen_save_config_name').value;
        if (!gridName) {
            return;
        }
        let isPublic = getRequiredElementById('grid_gen_save_is_public').checked;
        let data = this.getSaveConfig();
        genericRequest('GridGenSaveData', { 'gridName': gridName, 'data': data, 'isPublic': isPublic }, data => {
            this.outInfoBox.innerHTML = `<b>Config saved!</b>`;
            setTimeout(() => this.updateOutputInfo(), 5000);
        });
        this.hideSaveModal();
    }

    hideLoadModal() {
        $('#gridgen_load_modal').modal('hide');
    }

    hideSaveModal() {
        $('#gridgen_save_modal').modal('hide');
    }
}

let extensionGridGen = new GridGenClass();

postParamBuildSteps.push(() => extensionGridGen.rebuildAxes());
sessionReadyCallbacks.push(() => {
    extensionGridGen.register();
});
