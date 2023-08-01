
let models = {};
let cur_model = null;
let curModelWidth = 0, curModelHeight = 0;
let curModelMenuModel = null;

function editModel(model) {
    if (model == null) {
        return;
    }
    curModelMenuModel = model;
    let imageInput = getRequiredElementById('edit_model_image');
    imageInput.innerHTML = '';
    let enableImage = getRequiredElementById('edit_model_enable_image');
    enableImage.checked = false;
    enableImage.disabled = true;
    let curImg = document.getElementById('current_image_img');
    if (curImg) {
        let newImg = curImg.cloneNode(true);
        imageInput.appendChild(newImg);
        enableImage.checked = true;
        enableImage.disabled = false;
    }
    getRequiredElementById('edit_model_name').value = model.title || model.name;
    getRequiredElementById('edit_model_type').value = model.architecture || '';
    getRequiredElementById('edit_model_resolution').value = `${model.standard_width}x${model.standard_height}`;
    for (let val of ['description', 'author', 'usage_hint', 'date', 'license', 'trigger_phrase', 'tags']) {
        getRequiredElementById(`edit_model_${val}`).value = model[val] || '';
    }
    $('#edit_model_modal').modal('show');
}

function save_edit_model() {
    let model = curModelMenuModel;
    if (model == null) {
        console.log("Model do save: no model");
        return;
    }
    let resolution = getRequiredElementById('edit_model_resolution').value.split('x');
    let data = {
        'model': model.name,
        'title': getRequiredElementById('edit_model_name').value,
        'standard_width': parseInt(resolution[0]),
        'standard_height': parseInt(resolution[1]),
        'preview_image': ''
    };
    for (let val of ['author', 'type', 'description', 'usage_hint', 'date', 'license', 'trigger_phrase', 'tags']) {
        data[val] = getRequiredElementById(`edit_model_${val}`).value;
    }
    function complete() {
        genericRequest('EditModelMetadata', data, data => {
            modelBrowser.update();
        });
        $('#edit_model_modal').modal('hide');
    }
    if (getRequiredElementById('edit_model_enable_image').checked) {
        var image = new Image();
        image.crossOrigin = 'Anonymous';
        image.onload = () => {
            let canvas = document.createElement('canvas');
            let context = canvas.getContext('2d');
            canvas.height = 256;
            canvas.width = 256;
            context.drawImage(image, 0, 0, 256, 256);
            let dataURL = canvas.toDataURL('image/jpeg');
            data['preview_image'] = dataURL;
            complete();
        };
        image.src = getRequiredElementById('edit_model_image').getElementsByTagName('img')[0].src;
    }
    else {
        complete();
    }
}

function close_edit_model() {
    $('#edit_model_modal').modal('hide');
}

function cleanModelName(name) {
    let index = name.lastIndexOf('/');
    if (index != -1) {
        name = name.substring(index + 1);
    }
    index = name.lastIndexOf('.');
    if (index != -1) {
        name = name.substring(0, index);
    }
    return name;
}

function sortModelName(a, b) {
    let aName = a.name.toLowerCase();
    let bName = b.name.toLowerCase();
    if (aName.endsWith('.safetensors') && !bName.endsWith('.safetensors')) {
        return -1;
    }
    if (!aName.endsWith('.safetensors') && bName.endsWith('.safetensors')) {
        return 1;
    }
    return aName.localeCompare(bName);
}

function listModelFolderAndFiles(path, isRefresh, callback) {
    let prefix = path == '' ? '' : (path.endsWith('/') ? path : `${path}/`);
    genericRequest('ListModels', {'path': path}, data => {
        callback(data.folders.sort((a, b) => a.localeCompare(b)), data.files.sort(sortModelName).map(f => { return { 'name': `${prefix}${f.name}`, 'data': f }; }));
    });
}

function modelsSearch() {
    // TODO
}

function describeModel(model) {
    let description = '';
    let buttonLoad = () => {
        directSetModel(model.data);
        makeWSRequestT2I('SelectModelWS', {'model': model.data.name}, data => {
            loadModelList(lastModelDir);
        });
    }
    let buttons = [
        { label: 'Load Now', onclick: buttonLoad }
    ];
    let name = cleanModelName(model.data.name);
    if (model.data.is_safetensors) {
        let getLine = (label, val) => `<b>${label}:</b> ${val == null ? "(Unset)" : escapeHtml(val)}<br>`;
        description = `<span class="model_filename">${escapeHtml(name)}</span><br>${getLine("Title", model.data.title)}${getLine("Author", model.data.author)}${getLine("Type", model.data.class)}${getLine("Resolution", `${model.data.standard_width}x${model.data.standard_height}`)}${getLine("Description", model.data.description)}`;
        buttons.push({ label: 'Edit Metadata', onclick: () => editModel(model.data) });
    }
    else {
        description = `${escapeHtml(name)}.ckpt<br>(Metadata only available for 'safetensors' models.)<br><b>WARNING:</b> 'ckpt' pickle files can contain malicious code! Use with caution.<br>`;
    }
    let className = getRequiredElementById('current_model').value == model.data.name ? 'model-selected' : (model.data.loaded ? 'model-loaded' : '');
    return { name, description, buttons, 'image': model.data.preview_image, className };
}

function selectModel(model) {
    directSetModel(model.data);
    modelBrowser.update();
}

let modelBrowser = new GenPageBrowserClass('model_list', listModelFolderAndFiles, modelsSearch, 'modelbrowser', 'Cards', describeModel, selectModel);

function loadModelList(path) {
    modelBrowser.navigate(path);
}

function directSetModel(model) {
    if (!model) {
        return;
    }
    if (model.name) {
        forceSetDropdownValue('input_model', model.name);
        forceSetDropdownValue('current_model', model.name);
        setCookie('selected_model', `${model.name},${model.standard_width},${model.standard_height}`, 90);
        curModelWidth = model.standard_width;
        curModelHeight = model.standard_height;
    }
    else if (model.includes(',')) {
        let [name, width, height] = model.split(',');
        forceSetDropdownValue('input_model', name);
        forceSetDropdownValue('current_model', name);
        setCookie('selected_model', `${name},${width},${height}`, 90);
        curModelWidth = parseInt(width);
        curModelHeight = parseInt(height);
    }
    let aspect = document.getElementById('input_aspectratio');
    if (aspect) {
        aspect.dispatchEvent(new Event('change'));
    }
}

function setCurrentModel(callback) {
    let currentModel = getRequiredElementById('current_model');
    if (currentModel.value == '') {
        genericRequest('ListLoadedModels', {}, data => {
            if (data.models.length > 0) {
                directSetModel(data.models[0]);
            }
            if (callback) {
                callback();
            }
        });
    }
    else {
        if (callback) {
            callback();
        }
    }
}

let noDup = false;

getRequiredElementById('current_model').addEventListener('change', () => {
    if (noDup) {
        return;
    }
    let name = getRequiredElementById('current_model').value;
    if (name == '') {
        return;
    }
    genericRequest('DescribeModel', {'modelName': name}, data => {
        noDup = true;
        directSetModel(data.model);
        noDup = false;
    });
});
