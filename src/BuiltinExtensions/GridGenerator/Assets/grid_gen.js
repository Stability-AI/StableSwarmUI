let gridGen_mainDiv = null;
let gridGen_axisDiv = null;
let gridGen_settingsDiv = null;
let gridGrid_lastAxisId = 0;

function gridGen_addAxis() {
    let id = gridGrid_lastAxisId++;
    let wrapper = createDiv(null, 'grid-gen-axis-wrapper');
    let axisTypeSelector = document.createElement('select');
    axisTypeSelector.className = 'grid-gen-selector';
    axisTypeSelector.id = `grid-gen-axis-type-${id}`;
    axisTypeSelector.add(new Option('', '', true, true));
    for (let option of gen_param_types) {
        axisTypeSelector.add(new Option(option.name, option.name));
    }
    let inputBox = document.createElement('div');
    inputBox.className = 'grid-gen-axis-input';
    inputBox.id = `grid-gen-axis-input-${id}`;
    //let lastSelection = 0;
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
            setSelectionRange(inputBox, lastSelection, lastSelection);
        }
        lastSelection = -1;
    };
    inputBox.addEventListener('input', updateInput);
    inputBox.contentEditable = true;
    let fillButton = document.createElement('button');
    fillButton.style.visibility = 'hidden';
    fillButton.innerText = 'Fill';
    fillButton.className = 'basic-button grid-gen-axis-fill-button';
    fillButton.title = 'Fill with available values';
    fillButton.addEventListener('click', () => {
        let toFill = fillButton.dataset.values;
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
        let mode = gen_param_types.find(e => e.name == axisTypeSelector.value);
        if (mode && (mode.values || mode.type == 'model')) {
            fillButton.innerText = 'Fill';
            fillButton.style.visibility = 'visible';
            fillButton.dataset.values = mode.type == 'model' ? allModels.join(' || ') : mode.values.join(' || ');
            fillButton.title = 'Fill with available values';
        }
        else if (mode && mode.type == 'boolean') {
            fillButton.innerText = 'Fill';
            fillButton.style.visibility = 'visible';
            fillButton.dataset.values = 'true || false';
            fillButton.title = 'Fill with "true" and "false"';
        }
        else if (mode && mode.examples) {
            fillButton.innerText = 'Examples';
            fillButton.style.visibility = 'visible';
            fillButton.dataset.values = mode.examples.join(' || ');
            fillButton.title = 'Fill with example values';
        }
        else {
            fillButton.style.visibility = 'hidden';
        }
        if (Array.from(gridGen_axisDiv.getElementsByClassName('grid-gen-selector')).filter(e => e.value == '').length == 0) {
            gridGen_addAxis();
        }
    });
    wrapper.appendChild(axisTypeSelector);
    wrapper.appendChild(inputBox);
    wrapper.appendChild(fillButton);
    gridGen_axisDiv.appendChild(wrapper);
}

function gridGen_register() {
    let doGenerate = () => {
        let getOpt = (o) => document.getElementById('grid-gen-opt-' + o).checked;
        let data = {
            'baseParams': getGenInput(),
            'outputFolderName': document.getElementById('grid-gen-output-folder-name').value,
            'doOverwrite': getOpt('do-overwrite'),
            'fastSkip': getOpt('fast-skip'),
            'generatePage': getOpt('generate-page'),
            'publishGenMetadata': getOpt('publish-metadata'),
            'dryRun': getOpt('dry-run')
        };
        let axisData = [];
        for (let axis of gridGen_axisDiv.getElementsByClassName('grid-gen-axis-wrapper')) {
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
                gotImageResult(data.image, data.metadata);
            }
            else if (data.success) {
                outInfoBox.innerHTML = `<b>Completed!</b> Output saved to <a href="Output/${outputFolder.value}/index.html" target="_blank">Output/<code>${outputFolder.value}</code></a>`;
            }
        });
    };
    let doGenWrapper = () => {
        setCurrentModel(() => {
            if (document.getElementById('current_model').innerText == '') {
                showError("Cannot generate, no model selected.");
                return;
            }
            doGenerate();
        });
    };
    gridGen_mainDiv = registerNewTool('grid_generator', 'Grid Generator', 'Generate Grid', doGenWrapper);
    gridGen_axisDiv = createDiv('grid-gen-axis-area', 'grid-gen-axis-area');
    gridGen_settingsDiv = createDiv('grid-gen-settings-area', 'grid-gen-settings-area');
    gridGen_settingsDiv.innerHTML =
        '<br><div id="grid-gen-info-box">...</div>'
        + makeTextInput(null, 'grid-gen-output-folder-name', 'Output Folder Name', '', '', 1, 'Output folder name...')
        + '<br>'
        + makeCheckboxInput(null, 'grid-gen-opt-do-overwrite', 'Overwrite Existing Files', 'If checked, will overwrite any already-generated images.', false)
        + makeCheckboxInput(null, 'grid-gen-opt-fast-skip', 'Fast Skip', 'If checked, uses faster skipping algorithm (prevents validation of skipped axes).', false)
        + makeCheckboxInput(null, 'grid-gen-opt-generate-page', 'Generate Page', 'If unchecked, will prevent regenerating the page for the grid.', true)
        + makeCheckboxInput(null, 'grid-gen-opt-publish-metadata', 'Publish Generation Metadata', 'If unchecked, will hide the image generation metadata.', true)
        + makeCheckboxInput(null, 'grid-gen-opt-dry-run', 'Dry Run', 'If checked, will not actually generate any images - useful to validate your grid.', false)
        ;
    gridGen_mainDiv.appendChild(gridGen_settingsDiv);
    gridGen_mainDiv.appendChild(gridGen_axisDiv);
    let outInfoBox = document.getElementById('grid-gen-info-box');
    let outputFolder = document.getElementById('grid-gen-output-folder-name');
    let updateOutputInfo = () => {
        outInfoBox.innerHTML = `Output will be saved to <a href="Output/${outputFolder.value}/index.html" target="_blank">Output/<code>${outputFolder.value}</code></a>`;
    };
    outputFolder.addEventListener('input', updateOutputInfo);
    let today = new Date();
    outputFolder.value = `grid-${today.getFullYear()}-${today.getMonth() + 1}-${today.getDate()}-${today.getHours()}-${today.getMinutes()}-${today.getSeconds()}`;
    updateOutputInfo();
    gridGen_addAxis();
}

if (registerNewTool) {
    sessionReadyCallbacks.push(() => {
        gridGen_register();
    });
}
