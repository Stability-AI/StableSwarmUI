let gridGen_mainDiv = null;
let gridGen_axisDiv = null;
let gridGen_settingsDiv = null;
let gridGrid_lastAxisId = 0;

let gridGen_modes = [];

function gridGen_addAxis() {
    let id = gridGrid_lastAxisId++;
    let wrapper = createDiv(null, 'grid-gen-axis-wrapper');
    let axisTypeSelector = document.createElement('select');
    axisTypeSelector.className = 'grid-gen-selector';
    axisTypeSelector.id = `grid-gen-axis-type-${id}`;
    axisTypeSelector.add(new Option('', '', true, true));
    for (let option of gridGen_modes) {
        axisTypeSelector.add(new Option(option.name, option.name));
    }
    let inputBox = document.createElement('textarea');
    inputBox.className = 'grid-gen-axis-input';
    inputBox.rows = 1;
    inputBox.id = `grid-gen-axis-input-${id}`;
    axisTypeSelector.addEventListener('change', () => {
        if (Array.from(gridGen_axisDiv.getElementsByClassName('grid-gen-selector')).filter(e => e.value == '').length == 0) {
            gridGen_addAxis();
        }
    });
    wrapper.appendChild(axisTypeSelector);
    wrapper.appendChild(inputBox);
    gridGen_axisDiv.appendChild(wrapper);
}

function gridGen_register() {
    gridGen_mainDiv = registerNewTool('grid_generator', 'Grid Generator');
    gridGen_axisDiv = createDiv('grid-gen-axis-area', 'grid-gen-axis-area');
    gridGen_settingsDiv = createDiv('grid-gen-settings-area', 'grid-gen-settings-area');
    gridGen_settingsDiv.innerHTML =
        '<button class="grid-gen-run-button" id="grid-gen-run-button">Create Grid</button><br>'
        + '<br><div id="grid-gen-info-box"></div>'
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
    outputFolder.addEventListener('input', () => {
        outInfoBox.innerHTML = `Output will be saved to <a href="Output/${outputFolder.value}/index.html" target="_blank">Output/<code>${outputFolder.value}</code></a>`;
    });
    let runButton = document.getElementById('grid-gen-run-button');
    runButton.addEventListener('click', () => {
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
            let input = axis.getElementsByClassName('grid-gen-axis-input')[0].value;
            axisData.push({ 'mode': type, 'vals': input });
        }
        data['gridAxes'] = axisData;
        genericRequest('GridGenRun', data, data => {
            console.log('Grid complete!');
        });
    });
    gridGen_addAxis();
}

if (registerNewTool) {
    sessionReadyCallbacks.push(() => {
        genericRequest('GridGenListModes', {}, data => {
            gridGen_modes = data.list;
            gridGen_register();
        });
    });
}
