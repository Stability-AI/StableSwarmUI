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
    gridGen_mainDiv.appendChild(gridGen_axisDiv);
    gridGen_settingsDiv = createDiv('grid-gen-settings-area', 'grid-gen-settings-area');
    let runButton = document.createElement('button');
    runButton.innerText = 'Run';
    runButton.addEventListener('click', () => {
        let data = {
            'baseParams': getGenInput(),
            'outputFolderName': document.getElementById('grid-gen-output-folder-name').value
        };
        for (let optElem of gridGen_settingsDiv.querySelectorAll('[data-opt]')) {
            data[optElem.dataset.opt] = optElem.checked;
        }
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
    gridGen_settingsDiv.appendChild(runButton);
    let outputFolderName = createDiv(null, 'grid-gen-setting-wrapper');
    outputFolderName.innerHTML = makeTextInput(null, 'grid-gen-output-folder-name', 'Output Folder Name', '', '', 1, 'Output folder name...');
    gridGen_settingsDiv.appendChild(outputFolderName);
    for (let opt of ['doOverwrite', 'fastSkip', 'generatePage', 'publishGenMetadata', 'dryRun']) {
        let wrapper = createDiv(null, 'grid-gen-setting-wrapper');
        let label = document.createElement('label');
        label.className = 'grid-gen-setting-label';
        label.for = `grid-gen-setting-${opt}`;
        label.innerText = opt;
        let input = document.createElement('input');
        input.type = 'checkbox';
        input.id = `grid-gen-setting-${opt}`;
        input.dataset.opt = opt;
        wrapper.appendChild(label);
        wrapper.appendChild(input);
        gridGen_settingsDiv.appendChild(wrapper);
    }
    gridGen_mainDiv.appendChild(gridGen_settingsDiv);
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
