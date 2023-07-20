
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
    getRequiredElementById('edit_model_author').value = model.author || '';
    getRequiredElementById('edit_model_type').value = model.class || '';
    getRequiredElementById('edit_model_resolution').value = `${model.standard_width}x${model.standard_height}`;
    getRequiredElementById('edit_model_description').value = model.description || '';
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
        'author': getRequiredElementById('edit_model_author').value,
        'type': getRequiredElementById('edit_model_type').value,
        'description': getRequiredElementById('edit_model_description').value,
        'standard_width': parseInt(resolution[0]),
        'standard_height': parseInt(resolution[1]),
        'preview_image': ''
    };
    if (getRequiredElementById('edit_model_enable_image').checked) {
        let img = getRequiredElementById('edit_model_image').getElementsByTagName('img')[0].src;
        let index = img.indexOf('/Output/');
        if (index != -1) {
            img = img.substring(index);
        }
        data['preview_image'] = img;
    }
    genericRequest('EditModelMetadata', data, data => {
        loadModelList(lastModelDir);
    });
    $('#edit_model_modal').modal('hide');
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
    let className = getRequiredElementById('current_model').innerText == model.data.name ? 'model-selected' : (model.data.loaded ? 'model-loaded' : '');
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
        getRequiredElementById('input_model').value = model.name;
        getRequiredElementById('current_model').innerText = model.name;
        setCookie('selected_model', `${model.name},${model.standard_width},${model.standard_height}`, 90);
        curModelWidth = model.standard_width;
        curModelHeight = model.standard_height;
    }
    else if (model.includes(',')) {
        let [name, width, height] = model.split(',');
        getRequiredElementById('input_model').value = name;
        getRequiredElementById('current_model').innerText = name;
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
    if (currentModel.innerText == '') {
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
