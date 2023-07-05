
let models = {};
let cur_model = null;
let curModelWidth = 0, curModelHeight = 0;
let curModelMenuModel = null;

function modelMenuDoLoadNow() {
    if (curModelMenuModel == null) {
        return;
    }
    directSetModel(curModelMenuModel);
    makeWSRequestT2I('SelectModelWS', {'model': curModelMenuModel.name}, data => {
        loadModelList(lastModelDir);
    });
}

function modelMenuDoEdit() {
    let model = curModelMenuModel;
    if (model == null) {
        console.log("Model do edit: no model");
        return;
    }
    let imageInput = document.getElementById('edit_model_image');
    imageInput.innerHTML = '';
    let enableImage = document.getElementById('edit_model_enable_image');
    enableImage.checked = false;
    enableImage.disabled = true;
    let curImg = document.getElementById('current_image').getElementsByTagName('img')[0];
    if (curImg) {
        let newImg = curImg.cloneNode(true);
        imageInput.appendChild(newImg);
        enableImage.checked = true;
        enableImage.disabled = false;
    }
    document.getElementById('edit_model_name').value = model.title || model.name;
    document.getElementById('edit_model_author').value = model.author || '';
    document.getElementById('edit_model_type').value = model.class || '';
    document.getElementById('edit_model_resolution').value = `${model.standard_width}x${model.standard_height}`;
    document.getElementById('edit_model_description').value = model.description || '';
    $('#edit_model_modal').modal('show');
}

function save_edit_model() {
    let model = curModelMenuModel;
    if (model == null) {
        console.log("Model do save: no model");
        return;
    }
    let resolution = document.getElementById('edit_model_resolution').value.split('x');
    let data = {
        'model': model.name,
        'title': document.getElementById('edit_model_name').value,
        'author': document.getElementById('edit_model_author').value,
        'type': document.getElementById('edit_model_type').value,
        'description': document.getElementById('edit_model_description').value,
        'standard_width': parseInt(resolution[0]),
        'standard_height': parseInt(resolution[1]),
        'preview_image': ''
    };
    if (document.getElementById('edit_model_enable_image').checked) {
        let img = document.getElementById('edit_model_image').getElementsByTagName('img')[0].src;
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
    let index = name.indexOf('/');
    if (index != -1) {
        name = name.substring(index + 1);
    }
    index = name.indexOf('.');
    if (index != -1) {
        name = name.substring(0, index);
    }
    return name;
}

function appendModel(container, prefix, model) {
    models[`${prefix}${model.name}`] = model;
    let batch = document.getElementById('current_model').innerText == model.name ? 'model-selected' : (model.loaded ? 'model-loaded' : `image-batch-${Object.keys(models).length % 2}`);
    let div = createDiv(null, `model-block model-block-hoverable ${batch}`);
    let img = document.createElement('img');
    img.src = model.preview_image;
    div.appendChild(img);
    let textBlock = createDiv(null, 'model-descblock');
    if (model.is_safetensors) {
        let getLine = (label, val) => `<b>${label}:</b> ${val == null ? "(Unset)" : escapeHtml(val)}<br>`;
        textBlock.innerHTML = `<span class="model_filename">${escapeHtml(cleanModelName(model.name))}</span><br>${getLine("Title", model.title)}${getLine("Author", model.author)}${getLine("Type", model.class)}${getLine("Resolution", `${model.standard_width}x${model.standard_height}`)}${getLine("Description", model.description)}`;
    }
    else {
        textBlock.innerHTML = `${escapeHtml(cleanModelName(model.name))}.ckpt<br>(Metadata only available for 'safetensors' models.)<br><b>WARNING:</b> 'ckpt' pickle files can contain malicious code! Use with caution.<br>`;
    }
    div.appendChild(textBlock);
    container.appendChild(div);
    let menu = createDiv(null, 'model-block-menu-button');
    menu.innerText = '⬤⬤⬤';
    menu.addEventListener('click', () => {
        curModelMenuModel = model;
        doPopover('modelmenu');
    });
    div.appendChild(menu);
    img.addEventListener('click', () => {
        directSetModel(model);
        loadModelList(lastModelDir);
    });
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

function loadModelList(path, isRefresh = false) {
    let container = document.getElementById('model_list');
    lastModelDir = path;
    container.innerHTML = '';
    models = {};
    call = () => loadFileList('ListModels', path, container, loadModelList, (prefix, model) => {
        appendModel(container, prefix, model);
    }, () => {
        let current_model = document.getElementById('current_model');
        if (current_model.innerText == '') {
            let model = Object.values(models).find(m => m.loaded);
            if (model) {
                directSetModel(model);
            }
        }
    }, (list) => list.sort(sortModelName));
    if (isRefresh) {
        refreshParameterValues(call);
    }
    else {
        call();
    }
}

function directSetModel(model) {
    if (!model) {
        return;
    }
    if (model.name) {
        document.getElementById('input_model').value = model.name;
        document.getElementById('current_model').innerText = model.name;
        setCookie('selected_model', `${model.name},${model.standard_width},${model.standard_height}`, 90);
        curModelWidth = model.standard_width;
        curModelHeight = model.standard_height;
    }
    else if (model.includes(',')) {
        let [name, width, height] = model.split(',');
        document.getElementById('input_model').value = name;
        document.getElementById('current_model').innerText = name;
        setCookie('selected_model', `${name},${width},${height}`, 90);
        curModelWidth = parseInt(width);
        curModelHeight = parseInt(height);
    }
    document.getElementById('input_aspectratio').dispatchEvent(new Event('change'));
}

function setCurrentModel(callback) {
    let currentModel = document.getElementById('current_model');
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
