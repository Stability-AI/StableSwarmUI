
class GridGenClass {

    mainDiv = null;
    axisDiv = null;
    settingsDiv = null;
    lastAxisId = 0;
    popover = null;

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
            if (!option.extra_hidden && option.id != 'images') {
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
        let updateInput = () => {
            let lastSelection = getCurrentCursorPosition(inputBox.id);
            let text = inputBox.innerText;
            let separator = text.includes('||') ? '||' : ',';
            let parts = text.split(separator);
            let html = '';
            for (let i in parts) {
                html += `<span class="grid-gen-axis-input-value">${parts[i]}</span>` + (i == parts.length - 1 ? '' : `<span class="grid-gen-axis-input-separator">${separator}</span>`);
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
            let toFill;
            if (mode && (mode.values || mode.type == 'model')) {
                toFill = mode.type == 'model' ? coreModelMap[mode.subtype || 'Stable-Diffusion'].join(' || ') : mode.values.join(' || ');
            }
            else if (mode && mode.type == 'boolean') {
                toFill = 'true || false';
            }
            else if (mode && mode.examples) {
                toFill = mode.examples.join(' || ');
            }
            else {
                return;
            }
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

    register() {
        let doGenerate = () => {
            resetBatchIfNeeded();
            let startTime = Date.now();
            let generatedCount = 0;
            let getOpt = (o) => document.getElementById('grid-gen-opt-' + o).checked;
            let data = {
                'baseParams': getGenInput(),
                'outputFolderName': document.getElementById('grid-gen-output-folder-name').value,
                'doOverwrite': getOpt('do-overwrite'),
                'fastSkip': getOpt('fast-skip'),
                'generatePage': getOpt('generate-page'),
                'publishGenMetadata': getOpt('publish-metadata'),
                'dryRun': getOpt('dry-run'),
                'weightOrder': getOpt('weight-order')
            };
            let axisData = [];
            for (let axis of this.axisDiv.getElementsByClassName('grid-gen-axis-wrapper')) {
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
            makeWSRequestT2I('GridGenRun', data, data => {
                if (data.image) {
                    appendGenTimeFrom(data.metadata);
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
                    outInfoBox.innerHTML = `<b>Running at ${message}</b> Output saved to <a href="Output/Grids/${outputFolder.value}/index.html" target="_blank">Output/Grids/<code>${outputFolder.value}</code></a>`;
                }
                else if (data.success) {
                    outInfoBox.innerHTML = `<b>Completed!</b> Output saved to <a href="Output/Grids/${outputFolder.value}/index.html" target="_blank">Output/Grids/<code>${outputFolder.value}</code></a>`;
                }
            });
        };
        let doGenWrapper = () => {
            setCurrentModel(() => {
                if (document.getElementById('current_model').value == '') {
                    showError("Cannot generate, no model selected.");
                    return;
                }
                doGenerate();
            });
        };
        this.mainDiv = registerNewTool('grid_generator', 'Grid Generator', 'Generate Grid', doGenWrapper);
        this.axisDiv = createDiv('grid-gen-axis-area', 'grid-gen-axis-area');
        this.settingsDiv = createDiv('grid-gen-settings-area', 'grid-gen-settings-area');
        this.settingsDiv.innerHTML =
            '<br><div id="grid-gen-info-box">...</div>'
            + makeTextInput(null, 'grid-gen-output-folder-name', 'Output Folder Name', 'Name of the folder to save this grid under in your Image History.', '', 'normal', 'Output folder name...', false, true)
            + '<br><div class="grid-gen-checkboxes">'
            + makeCheckboxInput(null, 'grid-gen-opt-do-overwrite', 'Overwrite Existing Files', 'If checked, will overwrite any already-generated images.', false, false, true)
            + makeCheckboxInput(null, 'grid-gen-opt-fast-skip', 'Fast Skip', 'If checked, uses faster skipping algorithm (prevents validation of skipped axes).', false, false, true)
            + makeCheckboxInput(null, 'grid-gen-opt-generate-page', 'Generate Page', 'If unchecked, will prevent regenerating the page for the grid.', true, false, true)
            + makeCheckboxInput(null, 'grid-gen-opt-publish-metadata', 'Publish Generation Metadata', 'If unchecked, will hide the image generation metadata.', true, false, true)
            + makeCheckboxInput(null, 'grid-gen-opt-dry-run', 'Dry Run', 'If checked, will not actually generate any images - useful to validate your grid.', false, false, true)
            + makeCheckboxInput(null, 'grid-gen-opt-weight-order', 'Allow Reordering', 'If checked, the grid generator will reorder processing order of axes to maximize generation speed.', true, false, true)
            + '</div>';
        this.mainDiv.appendChild(this.settingsDiv);
        this.mainDiv.appendChild(this.axisDiv);
        let outInfoBox = document.getElementById('grid-gen-info-box');
        let outputFolder = document.getElementById('grid-gen-output-folder-name');
        let updateOutputInfo = () => {
            genericRequest('GridGenDoesExist', { 'folderName': outputFolder.value }, data => {
                let prefix = data.exists ? '<span class="gridgen_warn">Output WILL OVERRIDE existing folder</span>' : 'Output will be saved to';
                outInfoBox.innerHTML = `${prefix} <a href="Output/Grids/${outputFolder.value}/index.html" target="_blank">Output/Grids/<code>${outputFolder.value}</code></a>`;
            });
        };
        outputFolder.addEventListener('input', updateOutputInfo);
        let today = new Date();
        function pad(n) {
            return n < 10 ? '0' + n : n;
        }
        outputFolder.value = `grid-${today.getFullYear()}-${pad(today.getMonth() + 1)}-${pad(today.getDate())}-${pad(today.getHours())}-${pad(today.getMinutes())}-${pad(today.getSeconds())}`;
        updateOutputInfo();
        this.addAxis();
    }
}

let extensionGridGen = new GridGenClass();

postParamBuildSteps.push(() => extensionGridGen.rebuildAxes());
sessionReadyCallbacks.push(() => {
    extensionGridGen.register();
});
