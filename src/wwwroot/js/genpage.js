let core_inputs = ['prompt', 'negative_prompt', 'seed', 'steps', 'width', 'height', 'images', 'cfg_scale'];

let session_id = null;

let batches = 0;

const time_started = Date.now();

function clickImageInBatch(div) {
    setCurrentImage(div.getElementsByTagName('img')[0].src);
}

function selectImageInHistory(div) {
    let batchId = div.dataset.batch_id;
    document.getElementById('current_image_batch').innerHTML = '';
    for (let img of document.getElementById('image_history').querySelectorAll(`[data-batch_id="${batchId}"]`)) {
        let batch_div = appendImage('current_image_batch', img.getElementsByTagName('img')[0].src, batchId, '(TODO)');
        batch_div.addEventListener('click', () => clickImageInBatch(batch_div));
    }
    setCurrentImage(div.getElementsByTagName('img')[0].src);
}

function setCurrentImage(src) {
    let curImg = document.getElementById('current_image');
    curImg.innerHTML = '';
    let img = document.createElement('img');
    img.src = src;
    curImg.appendChild(img);
}

function appendImage(spot, imageSrc, batchId, textPreview) {
    let div = document.createElement('div');
    div.classList.add('image-block');
    div.classList.add(`image-batch-${batchId % 2}`);
    div.dataset.batch_id = batchId;
    let img = document.createElement('img');
    img.addEventListener('load', () => {
        div.style.width = `calc(${img.width}px + 2rem)`;
    });
    img.src = imageSrc;
    div.appendChild(img);
    let textBlock = document.createElement('div');
    textBlock.classList.add('image-preview-text');
    textBlock.innerText = textPreview;
    div.appendChild(textBlock);
    document.getElementById(spot).appendChild(div);
    return div;
}

function gotImageResult(image) {
    let src = image;
    let batch_div = appendImage('current_image_batch', src, batches, '(TODO)');
    batch_div.addEventListener('click', () => clickImageInBatch(batch_div));
    let history_div = appendImage('image_history', src, batches, '(TODO)');
    history_div.addEventListener('click', () => selectImageInHistory(history_div));
    setCurrentImage(src);
}

function doGenerate() {
    if (session_id == null) {
        if (Date.now() - time_started > 1000 * 60) {
            showError("Cannot generate, session not started. Did the server crash?");
        }
        else {
            showError("Cannot generate, session not started. Please wait a moment for the page to load.");
        }
        return;
    }
    let input = {};
    for (let id of core_inputs) {
        input[id] = document.getElementById('input_' + id).value;
    }
    document.getElementById('current_image_batch').innerHTML = '';
    batches++;
    makeWSRequest('GenerateText2ImageWS', input, data => {
        gotImageResult(data.image);
    });
}

function loadHistory(path) {
    genericRequest('ListImages', {'path': path}, data => {
        document.getElementById('image_history').innerHTML = '';
        let prefix;
        if (path == '') {
            prefix = '';
        }
        else {
            prefix = path + '/';
            let above = path.split('/').slice(0, -1).join('/');
            let div = appendImage('image_history', '/imgs/folder.png', 'folder', `../`);
            div.addEventListener('click', () => loadHistory(above));
        }
        for (let folder of data.folders) {
            let div = appendImage('image_history', '/imgs/folder.png', 'folder', `${folder}/`);
            div.addEventListener('click', () => loadHistory(`${prefix}${folder}`));
        }
        for (let img of data.images) {
            let div = appendImage('image_history', `Output/${prefix}${img.src}`, img.batch_id, '(Image metadata TODO)');
            div.addEventListener('click', () => selectImageInHistory(div));
        }
    });
}

function genpageLoad() {
    document.getElementById('generate_button').addEventListener('click', doGenerate);
    getSession(() => {
        loadHistory('');
    });
}

genpageLoad();
