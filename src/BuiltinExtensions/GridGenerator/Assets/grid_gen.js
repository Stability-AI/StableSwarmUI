let gridGen_mainDiv = null;
let gridGrid_lastAxisId = 0;

function gridGen_addAxis() {
    let id = gridGrid_lastAxisId++;
    let wrapper = createDiv(null, 'grid-gen-axis-wrapper');
    let axisTypeSelector = document.createElement('select');
    axisTypeSelector.className = 'grid-gen-selector';
    axisTypeSelector.id = `grid-gen-axis-type-${id}`;
    axisTypeSelector.add(new Option('', '', true, true));
    for (let option of core_inputs) {
        axisTypeSelector.add(new Option(option, option));
    }
    let inputBox = document.createElement('textarea');
    inputBox.id = `grid-gen-axis-input-${id}`;
    axisTypeSelector.addEventListener('change', () => {
        if (Array.from(gridGen_mainDiv.getElementsByClassName('grid-gen-selector')).filter(e => e.value == '').length == 0) {
            gridGen_addAxis();
        }
    });
    wrapper.appendChild(axisTypeSelector);
    wrapper.appendChild(inputBox);
    gridGen_mainDiv.appendChild(wrapper);
}

function gridGen_register() {
    gridGen_mainDiv = registerNewTool('grid_generator', 'Grid Generator');
    gridGen_addAxis();
}

if (registerNewTool) {
    gridGen_register();
}
